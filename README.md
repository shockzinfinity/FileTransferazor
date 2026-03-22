# FileTransferazor

AWS S3 기반의 파일 전송 웹 애플리케이션. 발신자가 파일을 업로드하면 S3에 저장하고, 수신자에게 다운로드 링크를 제공합니다. 업로드된 파일은 24시간 후 자동 삭제됩니다.

## 기술 스택

| 구분 | 기술 | 버전 |
|------|------|------|
| Framework | .NET | 10.0 |
| Frontend | Blazor WebAssembly | 10.0.5 |
| UI Library | MudBlazor | 9.2.0 |
| Backend | ASP.NET Core (Minimal Hosting) | 10.0 |
| ORM | Entity Framework Core | 10.0.5 |
| Database | SQL Server | - |
| File Storage | AWS S3 | SDK 4.x |
| Secret Management | AWS Systems Manager Parameter Store | SDK 4.x |
| Email | Gmail API / SMTP | Google.Apis 1.73 |
| Resumable Upload | tusdotnet | 2.11.1 |
| Upload Client | tus-js-client | 4.3.1 (CDN) |
| Background Jobs | Hangfire | 1.8.23 |

## 프로젝트 구조

```
FileTransferazor.slnx
├── Client/                          # Blazor WebAssembly (Frontend)
│   ├── Pages/
│   │   └── SendFileForm.razor(.cs)  # 파일 업로드 폼 (tus 기반)
│   ├── Services/
│   │   └── TusInteropService.cs     # tus-js-client JS Interop wrapper
│   ├── Shared/
│   │   ├── MainLayout.razor
│   │   ├── NavMenu.razor
│   │   └── DialogNotification.razor # 알림 다이얼로그
│   ├── wwwroot/js/
│   │   └── tus-interop.js           # tus-js-client JavaScript wrapper
│   └── Program.cs                   # WASM 엔트리포인트
│
├── Server/                          # ASP.NET Core (Backend)
│   ├── Controllers/
│   │   ├── FilesController.cs       # GET  /api/Files/{fileName} - 다운로드 (S3 스트리밍)
│   │   └── FileWithDataController.cs# POST /api/FileWithData    - 레거시 업로드
│   ├── Services/
│   │   ├── AwsS3FileManager.cs      # S3 업로드/다운로드/삭제
│   │   ├── TusUploadService.cs      # tus 업로드 완료 핸들러
│   │   ├── AwsParameterStoreClient.cs # AWS Parameter Store 조회
│   │   ├── GmailEmailSender.cs      # 이메일 발송 (SMTP/API/ServiceAccount)
│   │   └── EmailConstructorHelpers.cs # 이메일 본문 생성
│   ├── Repositories/
│   │   └── FileRepository.cs        # 파일 업로드/다운로드 비즈니스 로직
│   ├── Data/
│   │   └── FileTransferazorDbContext.cs
│   ├── Options/
│   │   ├── AwsS3Options.cs          # S3 설정 (IOptionsSnapshot)
│   │   ├── GmailOptions.cs          # Gmail 설정 (IOptionsSnapshot)
│   │   └── TusOptions.cs            # tus 저장 경로/크기 제한 설정
│   ├── Migrations/
│   └── Program.cs                   # Minimal Hosting 엔트리포인트
│
└── Shared/                          # 공유 모델
    ├── FileSendData.cs              # 발신자/수신자 이메일 + GroupId
    ├── FileStorageData.cs           # S3 파일 메타데이터
    └── TransferFile.cs              # 파일 스트림 전송 객체 (IDisposable)
```

## 핵심 플로우

### 업로드 (tus Resumable Upload)

[tus 프로토콜](https://tus.io/protocols/resumable-upload) 기반의 resumable chunked upload를 사용합니다.
네트워크가 끊겨도 이어받기가 가능하며, 파일별 실시간 진행률을 표시합니다.

```
Client (SendFileForm + tus-js-client)
  → groupId 생성 (클라이언트 측 Guid)
  → 파일별로 tus 세션 시작:
    POST /api/tus (Upload-Length, Upload-Metadata 헤더)
      → tusdotnet 미들웨어: TusDiskStore에 리소스 생성
    PATCH /api/tus/{fileId} (chunk 단위, 5MB 기본)
      → TusDiskStore에 chunk append
      → [네트워크 끊김 시]
        HEAD /api/tus/{fileId} → 현재 offset 확인
        PATCH /api/tus/{fileId} (이전 offset부터 이어서)
    → 업로드 완료 시 OnFileCompleteAsync 이벤트:
      → TusUploadService.OnFileCompleteAsync()
        → groupId로 FileSendData upsert (다중 파일 그룹핑)
        → AwsS3FileManager.UploadFileAsync() → S3에 최종 저장
        → FileStorageData DB 저장
        → Hangfire: 24시간 후 S3 파일 자동 삭제
        → TusDiskStore 임시 파일 삭제
```

**레거시 업로드** (`POST /api/FileWithData`)도 하위 호환을 위해 유지됩니다.

### 다운로드 (S3 Direct Streaming)

S3에서 파일을 서버 메모리에 버퍼링하지 않고, ResponseStream을 HTTP 응답으로 직접 스트리밍합니다.
10GB 파일도 서버 메모리 ~80KB만 사용합니다.

```
수신자가 링크 클릭
  → GET /api/Files/{fileName}
    → FilesController.GetBlobDownload()
      → FileRepository.DownloadFileAsync()
        → DB에서 메타데이터 조회
        → AwsS3FileManager.DownloadFileAsync()
          → S3 ResponseStream 직접 반환 (MemoryStream 복사 없음)
        → DB 레코드 삭제
        → Hangfire: 30분 후 S3 파일 삭제 스케줄링
      → FileStreamResult로 스트리밍 응답
      → Response 완료 시 TransferFile 자동 Dispose
```

## 시작하기

### 사전 요구사항

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- SQL Server (Docker 권장: `mcr.microsoft.com/mssql/server`)
- AWS 계정 (S3, Systems Manager 권한)

### 1. 데이터베이스 설정

```bash
# SQL Server Docker 컨테이너 실행
docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=P@ssw0rd" \
  -p 11434:1433 --name filetransferazor-db \
  -d mcr.microsoft.com/mssql/server:2022-latest

# EF Core 마이그레이션 적용
cd Server
dotnet ef database update
```

### 2. User Secrets 설정

민감한 설정값은 `dotnet user-secrets`로 관리합니다. Git에 포함되지 않습니다.

```bash
cd Server

# Gmail 설정
dotnet user-secrets set "Gmail:GmailUser" "your-email@gmail.com"
dotnet user-secrets set "Gmail:FromAddress" "your-email@gmail.com"
dotnet user-secrets set "Gmail:CredentialFilePath" "path/to/credential.json"
dotnet user-secrets set "Gmail:ServiceAccountCredentialFilePath" "path/to/service-account.json"
dotnet user-secrets set "Gmail:ServiceAccountEmail" "your-service-account@project.iam.gserviceaccount.com"
```

### 3. appsettings 설정

`appsettings.json`은 `.gitignore`에 포함되어 있습니다. `appsettings.sample`을 참고하여 생성하세요.

필수 설정:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost,11434;Database=fileTransferazorDb;User id=sa;Password=P@ssw0rd;MultipleActiveResultSets=true"
  },
  "AwsS3": {
    "BucketName": "your-bucket-name"
  },
  "Gmail": {
    "GmailUser": "",
    "FromAddress": "",
    "CredentialFilePath": "",
    "ServiceAccountCredentialFilePath": "",
    "ServiceAccountEmail": ""
  },
  "Tus": {
    "StoragePath": "/tmp/tusfiles",
    "MaxFileSizeInBytes": 10737418240,
    "ExpirationHours": 24
  }
}
```

### 4. 실행

```bash
cd Server
dotnet run
```

`https://localhost:5001`에서 접속 가능합니다.

## 설정 관리

이 프로젝트는 .NET의 **IOptionsSnapshot 패턴**을 사용하여 3계층 설정 관리를 적용합니다.

```
우선순위 (높음 → 낮음)
─────────────────────────────────────────
1. 환경변수          (Production)    예: Gmail__GmailUser=...
2. dotnet user-secrets (Development)  Git 미추적, 로컬 전용
3. appsettings.json    (구조 정의)    키 구조만 정의 (빈 값)
```

- **IOptionsSnapshot\<T\>**: Scoped 수명으로, HTTP 요청마다 최신 설정 값을 읽습니다. `appsettings.json` 변경 시 앱 재시작 없이 다음 요청부터 반영됩니다.
- **User Secrets**: `~/.microsoft/usersecrets/`에 저장되어 소스 코드에 포함되지 않습니다.
- **Production**: 환경변수 또는 AWS Parameter Store를 통해 런타임에 주입합니다.

### Options 클래스

| 클래스 | 설정 섹션 | 용도 |
|--------|----------|------|
| `AwsS3Options` | `AwsS3` | S3 버킷 이름 |
| `GmailOptions` | `Gmail` | Gmail 사용자, 발신 주소, credential 경로 |
| `TusOptions` | `Tus` | tus 임시 저장 경로, 최대 파일 크기, 만료 시간 |

## API

| Method | Endpoint | 설명 | 비고 |
|--------|----------|------|------|
| `POST` | `/api/tus` | tus 업로드 리소스 생성 | `Upload-Length`, `Upload-Metadata` 헤더 필수 |
| `PATCH` | `/api/tus/{fileId}` | tus chunk 데이터 전송 | `Upload-Offset`, `Content-Type: application/offset+octet-stream` |
| `HEAD` | `/api/tus/{fileId}` | 현재 업로드 offset 조회 | Resumable upload 시 사용 |
| `OPTIONS` | `/api/tus` | 서버 tus 확장 기능 조회 | `Tus-Extension`, `Tus-Max-Size` 응답 |
| `GET` | `/api/Files/{fileName}` | 파일 다운로드 (S3 스트리밍) | 응답 완료 후 DB 삭제, 30분 후 S3 삭제 |
| `POST` | `/api/FileWithData` | 레거시 업로드 (최대 10 GB) | 하위 호환용, `multipart/form-data` |

### tus Upload-Metadata 필수 키

| 키 | 설명 | 예시 |
|----|------|------|
| `groupId` | 다중 파일 그룹 ID (클라이언트 생성 GUID) | `a1b2c3d4-...` |
| `senderEmail` | 발신자 이메일 | `sender@example.com` |
| `receiverEmail` | 수신자 이메일 | `receiver@example.com` |
| `filename` | 원본 파일명 | `document.pdf` |
| `contentType` | MIME 타입 | `application/pdf` |

## 기술적 결정 사항

### .NET 5 → .NET 10 마이그레이션 (2026-03)

| 영역 | Before | After |
|------|--------|-------|
| Target Framework | net5.0 (EOL) | net10.0 |
| Hosting 모델 | Startup.cs 패턴 | Minimal Hosting (Program.cs) |
| MudBlazor | 5.1.4 | 9.2.0 |
| AWS SDK | 3.x | 4.x |
| EF Core | 5.0.10 | 10.0.5 |
| Hangfire | 1.7.25 | 1.8.23 |

**주요 Breaking Changes 대응:**
- `MudDialogInstance` → `IMudDialogInstance`
- `IDialogService.Show()` → `ShowAsync()`
- `DialogResult.Cancelled` → `Canceled`
- `Icons.Filled` → `Icons.Material.Filled`
- `Router.PreferExactMatches` 제거 (obsolete)
- `Startup.cs` 제거, `Program.cs`로 Minimal Hosting 통합

### 버그 수정

| 버그 | 위치 | 원인 | 수정 |
|------|------|------|------|
| 다운로드 시 ObjectDisposedException | `AwsS3FileManager.DownloadFileAsync` | `using` 블록 내에서 S3 ResponseStream을 직접 반환 → 메서드 종료 시 Stream이 dispose됨 | S3 ResponseStream 직접 스트리밍 + `RegisterForDispose`로 수명 관리 |
| async void로 인한 예외 누락 | `IEmailSender`, `GmailEmailSender` | 인터페이스가 `void` 반환을 강제하여 구현체가 `async void` 사용 | 인터페이스를 `Task` 반환으로 변경, 메서드명에 `Async` 접미사 추가 |
| sync-over-async deadlock 위험 | `AwsParameterStoreClient.GetValue` | `.Result`로 async 메서드를 동기 블로킹 | 미사용 sync 메서드 제거, `GmailEmailSender`에서 `await` 사용 |
| ContentType 하드코딩 | `AwsS3FileManager.UploadFileAsync` | 모든 파일을 `application/zip`으로 업로드 | `IFormFile.ContentType`을 파라미터로 전달 |
| DateTime.Now 사용 | `AwsS3FileManager.UploadFileAsync` | 로컬 타임존 의존 S3 키 생성 | `DateTime.UtcNow`로 변경 |

### tus Resumable Upload 도입 (2026-03)

대용량 파일의 안정적 업로드를 위해 [tus 프로토콜 v1.0.0](https://tus.io/protocols/resumable-upload)을 도입했습니다.

**왜 tus인가:**
- HTTP 표준에는 resumable upload 메커니즘이 없음 (다운로드는 `Range` 헤더로 지원)
- 네트워크 끊김 시 `HEAD` 요청으로 서버의 현재 offset을 확인하고, 해당 지점부터 이어받기 가능
- 소형 파일은 `creation-with-upload` 확장으로 POST 1회에 완료 (오버헤드 없음)

**저장소 전략: TusDiskStore + S3 이동**

```
선택한 전략 (전략 1):
  Client → chunk → 서버 디스크 (TusDiskStore) → 완료 시 S3 이동

  장점: 기존 AwsS3FileManager 코드 100% 재사용, 구현 단순
  단점: 서버 디스크 공간 필요 (업로드 중 파일 크기만큼)

검토했으나 채택하지 않은 전략:
  전략 2 (TusS3Store): 커뮤니티 패키지 EUPL 라이선스, .NET 10 호환 미확인
  전략 3 (커스텀 ITusStore): 구현 복잡도 대비 현재 규모에서 이점 없음
```

**다중 파일 그룹핑:**
- 클라이언트가 `groupId`를 생성하여 모든 파일의 tus metadata에 포함
- 서버에서 `groupId`로 `FileSendData`를 upsert → 기존 1:N 관계 유지
- 추가 REST API 호출 없이 tus metadata만으로 해결

**JS Interop 전략:**
- Blazor WASM에서 `tus-js-client`를 직접 사용할 수 없어 JS Interop 사용
- `tus-interop.js` → `TusInteropService.cs` → `SendFileForm.razor.cs` 3계층
- `DotNetObjectReference`를 통해 JS → C# 콜백 (progress, success, error)

### 다운로드 S3 스트리밍 전환 (2026-03)

| 항목 | Before | After |
|------|--------|-------|
| 메모리 사용 | 파일 전체를 `MemoryStream`에 적재 (10GB = 10GB RAM) | S3 `ResponseStream` 직접 스트리밍 (~80KB 버퍼) |
| 리소스 정리 | `using` 블록에서 자동 dispose | `TransferFile`이 `IDisposable` 구현, `RegisterForDispose`로 Response 완료 시 정리 |
| 동시 다운로드 | 3건 × 10GB = 30GB RAM 필요 | 3건 × 80KB ≈ 240KB |

검토했으나 채택하지 않은 방식:
- **S3 Pre-signed URL**: 서버 부하 0이지만, URL 유출 시 만료까지 무제한 다운로드 가능 → 현재 "1회 다운로드 후 삭제" 정책과 충돌
- **스트리밍 + Range 지원**: 이어받기 가능하지만, 24시간 만료 파일에 Range 시나리오가 드물어 과도한 복잡도

### 보안 개선

| 항목 | Before | After |
|------|--------|-------|
| Hangfire Dashboard | 모든 환경에서 무인증 노출 | Development 환경에서만 접근 허용 |
| 이메일/credential 정보 | 소스코드에 하드코딩 (5곳) | IOptionsSnapshot + User Secrets로 외부화 |
| S3 버킷 이름 | 소스코드에 하드코딩 | IOptionsSnapshot으로 외부화 |
| 무의미한 try-catch | catch 후 그대로 throw | 불필요한 try-catch 제거 |
| 제네릭 Exception | `throw new Exception(...)` | `FileNotFoundException` 등 구체적 예외 사용 |

### 코드 정리

- Dead code 제거: 미사용 클래스 (`DisableFormValueModelBindingAttribute`), 주석 처리된 코드 블록 5개
- 미사용 using 지시문 정리 (`Amazon.Runtime.Internal.Util` 등 내부 네임스페이스 참조 제거)
- `EmailConstructorHelpers.from` 파라미터가 실제로 이메일 본문에 반영되도록 수정

## 제한 사항

- 최대 파일 크기: 10 GB (`TusOptions.MaxFileSizeInBytes`로 설정)
- 최대 동시 업로드 파일 수: 10개 (클라이언트 측 제한)
- tus chunk 크기: 5 MB (클라이언트 기본값, `tus-interop.js`에서 변경 가능)
- 업로드 중 서버 디스크 사용: 파일 크기만큼 (`TusOptions.StoragePath`)
- 업로드된 파일 보존 기간: 24시간 (Hangfire 스케줄)
- 다운로드 후 S3 파일 삭제: 30분 후 (Hangfire 스케줄)
- 이메일 알림: 구현되어 있으나 현재 비활성화 상태

## 향후 개선 가능 사항

- [ ] 테스트 프로젝트 추가 (xUnit + WebApplicationFactory)
- [ ] 파일 업로드 바이러스/악성코드 스캔
- [ ] 서버 측 파일 유효성 검증
- [ ] tus 미완료 업로드 자동 정리 (Hangfire recurring job)
- [ ] TusDiskStore → 커스텀 S3 Store 전환 (서버 디스크 의존 제거)
- [ ] tus Concatenation 확장으로 병렬 chunk 업로드
- [ ] 이메일 알림 기능 활성화
- [ ] Production Hangfire Dashboard 인증 (IDashboardAuthorizationFilter)
- [ ] Structured Logging (Serilog)
- [ ] Docker 컨테이너화
