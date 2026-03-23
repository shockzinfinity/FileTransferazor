# Session Handoff — 2026-03-23

## 완료된 작업

### tus Resumable Upload 도입
- tusdotnet 2.11.1 미들웨어 + tus-js-client 4.3.1 (로컬 번들) 통합
- TusDiskStore 전략: tus chunk → 디스크 임시 → 완료 시 스토리지 이동
- 클라이언트 groupId 기반 다중 파일 그룹핑 (FileSendData 1:N 유지)
- 파일별 진행률 바 + 전체 진행률 (MudProgressLinear)
- 업로드 취소 (DELETE), 네트워크 끊김 이어받기 (resume) 구현
- localStorage 세션 관리로 성공 파일 건너뛰기 (A+B 방안)
- 서버 측 중복 체크 안전망 (같은 groupId + 파일명)

### Download Streaming 개선
- MemoryStream 버퍼링 제거 → S3/Local ResponseStream 직접 스트리밍
- TransferFile IDisposable + RegisterForDispose 수명 관리

### IFileStorageProvider 추상화
- IAwsS3FileManager → IFileStorageProvider 리네이밍
- AwsS3FileManager (S3), LocalFileStorageProvider (로컬) 두 구현체
- appsettings.json `FileStorage:Provider` 값으로 전환 ("Local" | "S3")
- content-type DB 통합 (FileStorageData.ContentType 컬럼), .meta 파일 제거

### PostgreSQL 마이그레이션
- SQL Server → PostgreSQL (Npgsql + Hangfire.PostgreSql)
- EF Core 마이그레이션 재생성 (InitialPostgreSql + AddContentTypeToFileStorageData)
- GroupId unique filtered index PostgreSQL 문법 수정

### 보안/안정성 개선
- 파일명 NFC 유니코드 정규화 (FileNameNormalizer)
- Path Traversal 방지 (GetSafePath)
- 확장자 sanitize (SanitizeExtension) — S3/Local 양쪽
- OnFileCompleteAsync try-catch + LogError
- 서버 시작 시 만료 tus 임시 파일 정리
- nullable enable (Client, Shared 프로젝트)

### 설정 단일화
- TusOptions.ChunkSizeInBytes → TusConfigController API → 클라이언트 자동 적용
- appsettings.json 기반 일원 관리

### 문서화
- README.md 전면 업데이트 (tus, PostgreSQL, IFileStorageProvider, API 스펙)
- docs/tus-upload-test-scenarios.md (8개 시나리오, 전체 PASS)
- docs/known-issues.md (UI/UX 5건, 기능 1건, 향후 개선 8건)
- docs/tus-cli-reference.md (curl, tuspy, 레거시 API 사용법)

### 테스트 결과
| # | 테스트 | 결과 |
|---|--------|------|
| 1 | 소형 파일 (< 5MB) | PASS |
| 2 | 대형 파일 chunked | PASS |
| 3 | 다중 파일 그룹핑 | PASS |
| 4 | 네트워크 끊김 이어받기 | PASS |
| 5 | 업로드 취소 | PASS |
| 6 | 다운로드 스트리밍 | PASS |
| 7 | 메타데이터 검증 | PASS |
| 8 | 레거시 API | PASS |

---

## 미해결 이슈 (docs/known-issues.md 참조)

### 우선순위 높음 (NAS 배포 전 필수)
- **인증/인가 미구현** — 모든 API가 인증 없이 열려있음

### UI/UX (추후)
- Upload 버튼 재클릭 시 파일 리스트 소실
- 개별 파일 제거 기능 없음
- 에러 메시지가 기술적
- 에러 후 재시도 버튼 없음
- 부분 성공 상태 불명확

### 기능 (추후)
- 다중 파일 다운로드 (ZIP 묶음 등)
- 다운로드 Range 요청 지원
- 다운로드 중 취소 시 DB 레코드 이미 삭제 문제
- tus 미완료 업로드 주기적 정리 (Hangfire recurring)
- TusDiskStore → 커스텀 S3 Store 전환

---

## 현재 설정 상태

```
FileStorage.Provider: "Local" (로컬 파일시스템)
DB: PostgreSQL (postgres.shockz.io:15433)
tus 임시: /tmp/tusfiles/
최종 저장: /tmp/filetransferazor-storage/
ChunkSize: 5MB
```

---

## 다음 세션 시작 시

1. `docs/known-issues.md` 확인 — 미해결 이슈 현황
2. 인증 구현이 최우선 (NAS 배포 전)
3. UI/UX 이슈는 인증 이후
