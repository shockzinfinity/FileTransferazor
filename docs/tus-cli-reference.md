# tus CLI 업로드 참고

## curl로 tus 프로토콜 직접 호출

tus-js-client가 자동으로 처리하는 과정을 curl로 수동 수행할 수 있다.
디버깅이나 서버 동작 확인 시 유용하다.

### 1. 리소스 생성 (POST)

```bash
curl -k -v -X POST https://localhost:5001/api/tus \
  -H "Tus-Resumable: 1.0.0" \
  -H "Upload-Length: $(wc -c < test.txt | tr -d ' ')" \
  -H "Upload-Metadata: filename $(echo -n 'test.txt' | base64),senderEmail $(echo -n 'a@b.com' | base64),receiverEmail $(echo -n 'c@d.com' | base64),groupId $(echo -n 'test-group-1' | base64),contentType $(echo -n 'text/plain' | base64)"
```

응답:
```
HTTP/1.1 201 Created
Location: /api/tus/{fileId}
```

### 2. 데이터 전송 (PATCH)

```bash
curl -k -v -X PATCH https://localhost:5001/api/tus/{fileId} \
  -H "Tus-Resumable: 1.0.0" \
  -H "Upload-Offset: 0" \
  -H "Content-Type: application/offset+octet-stream" \
  --data-binary @test.txt
```

응답:
```
HTTP/1.1 204 No Content
Upload-Offset: {파일크기}
```

### 3. offset 확인 (HEAD) — 이어받기 시

```bash
curl -k -v -X HEAD https://localhost:5001/api/tus/{fileId} \
  -H "Tus-Resumable: 1.0.0"
```

응답:
```
HTTP/1.1 200 OK
Upload-Offset: {현재까지 받은 바이트}
Upload-Length: {전체 크기}
```

### 4. 서버 기능 확인 (OPTIONS)

```bash
curl -k -v -X OPTIONS https://localhost:5001/api/tus \
  -H "Tus-Resumable: 1.0.0"
```

응답:
```
Tus-Version: 1.0.0
Tus-Extension: creation,creation-with-upload,termination,checksum,...
Tus-Max-Size: 10737418240
```

---

## tuspy (Python tus 클라이언트)

CLI 도구는 아니지만, Python 스크립트로 tus 업로드를 간편하게 할 수 있다.

### 설치

```bash
pip install tuspy
```

### 기본 업로드

```python
from tusclient import client

tus = client.TusClient('https://localhost:5001/api/tus/')

uploader = tus.uploader(
    'path/to/largefile.zip',
    chunk_size=5 * 1024 * 1024,  # 5MB
    metadata={
        'filename': 'largefile.zip',
        'senderEmail': 'sender@example.com',
        'receiverEmail': 'receiver@example.com',
        'groupId': 'my-group-id',
        'contentType': 'application/zip'
    }
)

uploader.upload()
```

### Resume (자동)

```python
# FileStorage에 업로드 상태가 자동 저장됨
# 같은 파일로 다시 실행하면 이어받기
uploader = tus.uploader('path/to/largefile.zip', chunk_size=5 * 1024 * 1024)
uploader.upload()  # 이전 offset부터 자동 재개
```

### Chunk 단위 제어

```python
uploader = tus.uploader('path/to/largefile.zip', chunk_size=5 * 1024 * 1024)

# 한 chunk만 전송
uploader.upload_chunk()

# 특정 바이트까지만 전송
uploader.upload(stop_at=10 * 1024 * 1024)  # 10MB까지
```

### 인증 헤더 추가

```python
tus = client.TusClient(
    'https://localhost:5001/api/tus/',
    headers={'Authorization': 'Bearer your-token'}
)
```

---

## 레거시 API (curl)

tus를 사용하지 않는 단일 POST 업로드. 소형 파일 또는 외부 시스템 연동 시 사용.

```bash
curl -k -v -X POST https://localhost:5001/api/FileWithData \
  -F 'FileToUploads=@path/to/file.txt' \
  -F 'Data={"SenderEmail":"a@b.com","ReceiverEmail":"c@d.com"}'
```

다중 파일:
```bash
curl -k -v -X POST https://localhost:5001/api/FileWithData \
  -F 'FileToUploads=@file1.txt' \
  -F 'FileToUploads=@file2.zip' \
  -F 'Data={"SenderEmail":"a@b.com","ReceiverEmail":"c@d.com"}'
```
