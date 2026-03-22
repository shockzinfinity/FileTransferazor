# tus Resumable Upload + Download Streaming 테스트 시나리오

## 사전 준비

```bash
# 1. DB 마이그레이션 (GroupId 컬럼 추가)
cd Server
dotnet ef migrations add AddGroupIdToFileSendData
dotnet ef database update

# 2. tus 임시 저장소 디렉토리 확인 (기본값: OS temp)
#    macOS: /var/folders/.../tusfiles
#    또는 appsettings.json에 Tus:StoragePath 설정

# 3. 서버 실행
dotnet run
```

---

## 테스트 1: 소형 파일 단일 업로드 (< 5MB)

**목적:** `creation-with-upload` 확장이 동작하여 POST 1회로 완료되는지 확인

**절차:**
1. `https://localhost:5001` 접속
2. Sender Email, Receiver Email 입력
3. [Upload] 버튼 → 1MB 이하 텍스트 파일 1개 선택
4. 파일 목록에 `파일명 (1.0 MB)` 형태로 표시되는지 확인
5. [Send] 클릭

**확인 사항:**
- [ ] 진행률 바가 거의 즉시 100%로 완료
- [ ] 녹색 `MudProgressLinear` 표시
- [ ] "All files have been uploaded successfully." 다이얼로그 표시
- [ ] 브라우저 DevTools > Network 탭에서 `/api/tus` 요청 확인:
  - `POST /api/tus` → 201 Created (body에 데이터 포함)
  - PATCH 요청이 없거나 1회만 있으면 정상 (소형 파일은 POST에 포함)

**DB 확인:**
```sql
SELECT * FROM FileSendDatas WHERE GroupId IS NOT NULL ORDER BY Id DESC;
SELECT * FROM FileStorageDatas ORDER BY Id DESC;
-- GroupId가 있는 FileSendData 1건 + FileStorageData 1건
```

---

## 테스트 2: 대형 파일 chunked 업로드 (> 5MB)

**목적:** 5MB chunk 분할 + 진행률 바 동작 확인

**절차:**
1. 50MB~100MB 파일 1개 선택
2. [Send] 클릭

**확인 사항:**
- [ ] 진행률 바가 0%부터 점진적으로 증가
- [ ] 개별 파일 카드에 `X.X MB / Y.Y MB` 바이트 카운터 표시
- [ ] 전체 진행률 바 (상단)와 개별 진행률 바 (파일 카드) 모두 동작
- [ ] 파란색 striped 바 → 완료 시 녹색 solid 바로 전환
- [ ] Network 탭에서:
  - `POST /api/tus` → 201
  - `PATCH /api/tus/{fileId}` 여러 건 (5MB씩)
  - 각 PATCH 응답에 `Upload-Offset` 헤더 증가 확인

**서버 측 확인:**
```bash
# tus 임시 파일이 업로드 완료 후 삭제되었는지 확인
ls -la /tmp/tusfiles/  # 또는 설정한 StoragePath
# 업로드 완료 후 해당 fileId 파일이 없어야 정상
```

---

## 테스트 3: 다중 파일 그룹핑

**목적:** 여러 파일이 하나의 `groupId`로 묶이는지 확인

**절차:**
1. 파일 3개 선택 (크기 다양하게: 1MB, 10MB, 30MB 등)
2. [Send] 클릭

**확인 사항:**
- [ ] 파일 3개의 개별 진행률 바가 각각 독립적으로 동작
- [ ] 전체 진행률은 3개 파일의 합산 기준
- [ ] 모든 파일 완료 후 성공 다이얼로그 1회만 표시
- [ ] [Send] 버튼이 업로드 중 "Uploading..."으로 비활성화
- [ ] [Upload] 버튼도 업로드 중 비활성화

**DB 확인:**
```sql
SELECT fsd.Id, fsd.GroupId, fsd.SenderEmail,
       fst.OriginalFileName, fst.FileUri
FROM FileSendDatas fsd
JOIN FileStorageDatas fst ON fsd.Id = fst.FileSendDataId
WHERE fsd.GroupId IS NOT NULL
ORDER BY fsd.Id DESC;
-- 동일한 GroupId로 FileSendData 1건 + FileStorageData 3건
```

---

## 테스트 4: 네트워크 끊김 → 이어받기 (Resumable)

**목적:** tus의 핵심 기능인 resumable upload 동작 확인

**절차:**
1. 100MB 이상 파일 선택
2. [Send] 클릭
3. 진행률이 30~50% 정도 되었을 때:
   - 브라우저 DevTools > Network 탭 > "Offline" 체크 (또는 throttling)
4. 업로드 에러 표시 확인
5. "Offline" 체크 해제
6. **같은 파일을 다시 선택하고 같은 이메일로 [Send] 클릭**

**확인 사항:**
- [ ] 오프라인 전환 시 빨간색 에러 바 + 에러 메시지 표시
- [ ] 재업로드 시 Network 탭에서:
  - `POST /api/tus` → 201 (새 리소스)
  - tus-js-client가 내부적으로 `findPreviousUploads()`를 호출
  - fingerprint가 같은 파일이면 이전 URL로 `HEAD` 요청 → offset 확인
  - `PATCH`가 0이 아닌 offset부터 시작하면 이어받기 성공
- [ ] 진행률이 0%가 아닌 이전 진행 지점부터 시작

> **참고:** tus-js-client는 `fingerprint` (파일명+크기+타입+endpoint 조합)로 이전 업로드를 localStorage에서 찾습니다. 같은 파일을 같은 endpoint로 올리면 이어받기가 동작합니다.

---

## 테스트 5: 업로드 취소

**목적:** 개별 파일 취소 기능 확인

**절차:**
1. 대형 파일 2~3개 선택
2. [Send] 클릭
3. 업로드 진행 중 특정 파일의 X (취소) 버튼 클릭

**확인 사항:**
- [ ] 취소된 파일: 빨간색 바 + "Cancelled by user" 메시지
- [ ] 다른 파일: 영향 없이 계속 업로드
- [ ] 취소 후 Network 탭에서 해당 파일의 PATCH 요청 중단 확인

---

## 테스트 6: 다운로드 스트리밍

**목적:** S3 직접 스트리밍으로 서버 메모리 사용량 절감 확인

**절차:**
1. 테스트 2에서 업로드한 파일의 S3 key를 DB에서 확인:
   ```sql
   SELECT FileUri FROM FileStorageDatas ORDER BY Id DESC LIMIT 1;
   ```
2. 브라우저에서 `https://localhost:5001/api/Files/{FileUri값}` 접속

**확인 사항:**
- [ ] 파일 다운로드가 정상적으로 시작
- [ ] 다운로드된 파일이 원본과 동일 (크기, 내용)
- [ ] 다운로드 후 DB 확인:
  ```sql
  -- 해당 FileStorageData, FileSendData 레코드가 삭제되었는지
  SELECT * FROM FileStorageDatas WHERE FileUri = '{FileUri값}';
  ```

**서버 메모리 모니터링 (선택):**
```bash
# 다운로드 중 서버 프로세스 메모리 확인
dotnet-counters monitor --process-id $(pgrep -f FileTransferazor) \
  --counters System.Runtime[gc-heap-size]
# 대형 파일 다운로드 시에도 heap 크기가 급증하지 않으면 정상
```

---

## 테스트 7: 메타데이터 검증 (에러 케이스)

**목적:** senderEmail/receiverEmail 누락 시 서버가 거부하는지 확인

**절차:**
1. 브라우저 DevTools > Console에서 직접 tus 요청:
   ```javascript
   var file = new File(["test"], "test.txt");
   var upload = new tus.Upload(file, {
       endpoint: "/api/tus",
       metadata: { filename: "test.txt" }  // senderEmail, receiverEmail 누락
   });
   upload.start();
   ```

**확인 사항:**
- [ ] 서버가 400 에러 응답 ("senderEmail and receiverEmail metadata are required")
- [ ] 파일이 서버에 저장되지 않음

---

## 테스트 8: 레거시 API 하위 호환

**목적:** 기존 `/api/FileWithData` 엔드포인트가 여전히 동작하는지

**절차:**
1. curl 또는 Postman으로:
   ```bash
   curl -X POST https://localhost:5001/api/FileWithData \
     -F 'FileToUploads=@testfile.txt' \
     -F 'Data={"SenderEmail":"a@b.com","ReceiverEmail":"c@d.com"}'
   ```

**확인 사항:**
- [ ] 200 OK 응답
- [ ] DB에 FileSendData (GroupId = null) + FileStorageData 생성
- [ ] S3에 파일 업로드 확인

---

## 테스트 체크리스트 요약

| # | 테스트 | 핵심 확인 | 결과 |
|---|--------|----------|------|
| 1 | 소형 파일 (< 5MB) | POST 1회 완료, 오버헤드 없음 | |
| 2 | 대형 파일 chunked | 5MB chunk, 진행률 바 | |
| 3 | 다중 파일 그룹핑 | 동일 GroupId, FileSendData 1건 | |
| 4 | 네트워크 끊김 이어받기 | HEAD offset 확인 → PATCH 재개 | |
| 5 | 업로드 취소 | 개별 파일 취소, 다른 파일 영향 없음 | |
| 6 | 다운로드 스트리밍 | 메모리 급증 없음, 파일 정합성 | |
| 7 | 메타데이터 검증 | 필수 필드 누락 시 400 | |
| 8 | 레거시 API | 기존 POST 방식 여전히 동작 | |
