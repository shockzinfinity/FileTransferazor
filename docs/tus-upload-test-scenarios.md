# tus Resumable Upload + Download Streaming 테스트 시나리오

## 사전 준비

```bash
# 1. DB 마이그레이션
cd Server
dotnet ef database update

# 2. 서버 실행
dotnet run --launch-profile FileTransferazor.Server
```

---

## 테스트 결과 요약

| # | 테스트 | 핵심 확인 | 결과 |
|---|--------|----------|------|
| 1 | 소형 파일 (< 5MB) | POST 1회 완료, 오버헤드 없음 | PASS |
| 2 | 대형 파일 chunked | 5MB chunk 분할, PATCH 다수, 진행률 바 | PASS |
| 3 | 다중 파일 그룹핑 | 동일 GroupId, FileSendData 1건 + FileStorageData N건 | PASS |
| 4 | 네트워크 끊김 이어받기 | HEAD offset 확인 → PATCH 재개, 성공 파일 건너뛰기 | PASS |
| 5 | 업로드 취소 | DELETE 요청, tus 임시 파일 삭제, DB 영향 없음 | PASS |
| 6 | 다운로드 스트리밍 | 파일 정상 수신, DB 레코드 삭제 확인 | PASS |
| 7 | 메타데이터 검증 | 필수 필드 누락 시 400 Bad Request | PASS |
| 8 | 레거시 API | curl POST → 200 OK, DB 저장 확인 | PASS |

**테스트 일자:** 2026-03-23

---

## 테스트 1: 소형 파일 단일 업로드 (< 5MB)

**목적:** `creation-with-upload` 확장이 동작하여 POST 1회로 완료되는지 확인

**절차:**
1. `https://localhost:5001` 접속
2. Sender Email, Receiver Email 입력
3. [Upload] 버튼 → 1MB 이하 텍스트 파일 1개 선택
4. [Send] 클릭

**확인 사항:**
- [x] 진행률 바가 거의 즉시 100%로 완료
- [x] 녹색 `MudProgressLinear` 표시
- [x] "All files have been uploaded successfully." 다이얼로그 표시
- [x] Network 탭: `POST /api/tus` → 201, PATCH 1~2회

**DB 확인:**
```sql
SELECT * FROM "FileSendDatas" WHERE "GroupId" IS NOT NULL ORDER BY "Id" DESC;
SELECT * FROM "FileStorageDatas" ORDER BY "Id" DESC;
```

---

## 테스트 2: 대형 파일 chunked 업로드 (> 5MB)

**목적:** 5MB chunk 분할 + 진행률 바 동작 확인

**절차:**
1. 50MB~100MB 파일 1개 선택
2. [Send] 클릭

**확인 사항:**
- [x] 진행률 바가 0%부터 점진적으로 증가
- [x] 개별 파일 카드에 `X.X MB / Y.Y MB` 바이트 카운터 표시
- [x] 파란색 striped 바 → 완료 시 녹색 solid 바로 전환
- [x] Network 탭: PATCH 요청 다수, 각 PATCH의 Upload-Offset 증가 확인

**서버 측 확인:**
```bash
# 업로드 완료 후 tus 임시 파일 삭제 확인
ls -la /tmp/tusfiles/
# 최종 저장소에 파일 존재 확인
ls -la /tmp/filetransferazor-storage/
```

---

## 테스트 3: 다중 파일 그룹핑

**목적:** 여러 파일이 하나의 `groupId`로 묶이는지 확인

**절차:**
1. 파일 3개 선택 (크기 다양하게)
2. [Send] 클릭

**확인 사항:**
- [x] 파일 3개의 개별 진행률 바가 각각 독립적으로 동작
- [x] 모든 파일 완료 후 성공 다이얼로그 1회
- [x] DB: 동일 GroupId로 FileSendData 1건 + FileStorageData 3건

**DB 확인:**
```sql
SELECT fsd."Id", fsd."GroupId", fsd."SenderEmail",
       fst."OriginalFileName", fst."FileUri", fst."ContentType"
FROM "FileSendDatas" fsd
JOIN "FileStorageDatas" fst ON fsd."Id" = fst."FileSendDataId"
ORDER BY fsd."Id" DESC LIMIT 10;
```

---

## 테스트 4: 네트워크 끊김 → 이어받기 (Resumable)

**목적:** tus의 핵심 기능인 resumable upload 동작 확인

**절차:**
1. 100MB 이상 파일 + 다른 파일 1개 선택
2. [Send] 클릭
3. 첫 번째 파일 성공, 두 번째 파일 진행 중 DevTools > Network > Offline
4. Offline 해제
5. 같은 파일을 다시 선택하고 같은 이메일로 [Send]

**확인 사항:**
- [x] 오프라인 전환 시 빨간색 에러 바 표시
- [x] 재시도 시 브라우저 Console: `Resuming previous session: {groupId}` + `Skipping already completed file: {파일명}`
- [x] Network 탭: 성공한 파일의 PATCH 없음, 실패한 파일만 HEAD → PATCH (0이 아닌 offset)
- [x] localStorage 확인: 성공 파일 `"complete"`, 미완료 파일 `"pending"`

**localStorage 확인:**
```javascript
JSON.parse(localStorage.getItem('tusInterop_session'))
```

**서버 로그 확인:**
```
tus WRITE: FileId={id}, Offset={0이 아닌 값}  ← resume 동작
Skipping duplicate file: {파일명}              ← 중복 방어 동작 (서버 안전망)
```

---

## 테스트 5: 업로드 취소

**목적:** 개별 파일 취소 기능 확인

**절차:**
1. 대형 파일 1~2개 선택
2. [Send] 클릭
3. 업로드 진행 중 특정 파일의 X (취소) 버튼 클릭

**확인 사항:**
- [x] 취소된 파일: 빨간색 바 + "Cancelled by user" 메시지
- [x] 다른 파일: 영향 없이 계속 업로드
- [x] Network 탭: DELETE 요청 → 423 Locked (재시도) → 204 No Content
- [x] 취소된 파일은 DB에 저장되지 않음 (OnFileCompleteAsync 미호출)

---

## 테스트 6: 다운로드 스트리밍

**목적:** 파일 다운로드 동작 확인

**절차:**
1. DB에서 FileUri 확인:
   ```sql
   SELECT "FileUri", "OriginalFileName", "ContentType" FROM "FileStorageDatas" ORDER BY "Id" DESC LIMIT 5;
   ```
2. 브라우저에서 `https://localhost:5001/api/Files/{FileUri값}` 접속

**확인 사항:**
- [x] 파일 다운로드 정상 시작
- [x] 다운로드 후 DB 레코드 삭제 확인
- [x] 30분 후 Hangfire에 의해 스토리지 파일 삭제 스케줄 확인

---

## 테스트 7: 메타데이터 검증 (에러 케이스)

**목적:** senderEmail/receiverEmail 누락 시 서버가 거부하는지 확인

**절차:**
```javascript
var file = new File(["test"], "test.txt");
var upload = new tus.Upload(file, {
    endpoint: "/api/tus",
    metadata: { filename: "test.txt" }
});
upload.start();
```

**확인 사항:**
- [x] 서버 400 응답 ("senderEmail and receiverEmail metadata are required")
- [x] 파일이 서버에 저장되지 않음
- [x] tus-js-client가 retryDelays에 따라 5회 재시도 후 최종 에러 (400은 영구적 오류이므로 재시도해도 동일)

---

## 테스트 8: 레거시 API 하위 호환

**목적:** 기존 `/api/FileWithData` 엔드포인트가 여전히 동작하는지

**절차:**
```bash
curl -k -v -X POST https://localhost:5001/api/FileWithData \
  -F 'FileToUploads=@testfile.txt' \
  -F 'Data={"SenderEmail":"a@b.com","ReceiverEmail":"c@d.com"}'
```

**확인 사항:**
- [x] 200 OK 응답
- [x] DB에 FileSendData (GroupId = null) + FileStorageData 생성
- [x] 스토리지에 파일 저장 확인

---

## 테스트 중 발견된 이슈 및 수정 사항

| 발견 시점 | 이슈 | 해결 |
|----------|------|------|
| 테스트 1 | MudBlazor `DisableBackdropClick` 제거됨 | `BackdropClick="false"`로 변경 |
| 테스트 1 | tus-js-client CDN 스토리지 차단 | 로컬 번들로 전환 |
| 테스트 1 | `OnFileCompleteAsync` 에러 삼켜짐 | try-catch + LogError 추가 |
| 테스트 3 | FileSendData GroupId 중복 INSERT | select-first upsert로 변경 |
| 테스트 4 | 성공한 파일 재전송 | localStorage 세션 관리 (A+B) |
| 테스트 4 | `saveSession`이 complete 상태 덮어씀 | 이전 세션 존재 시 saveSession 건너뛰기 |
| 테스트 4 | UI에 중복 프로그레스 바 | `_uploadStates.Clear()` 위치 수정 |
| 공통 | 파일명 normalize 미처리 | NFC 유니코드 정규화 적용 |
| 공통 | Path Traversal 취약점 | `GetSafePath()` 검증 추가 |
| 공통 | 확장자 특수문자 | `SanitizeExtension()` 추가 |
| 공통 | content-type 저장 불일치 | DB `ContentType` 컬럼으로 통합 |
| 테스트 8 | 레거시 API 확장자가 랜덤 (.mpk) | `Path.GetRandomFileName()` → 원본 파일명 |
