window.tusInterop = {
    _uploads: {},
    _sessionKey: 'tusInterop_session',

    startUpload: function (dotNetRef, uploadId, endpoint, fileInputId, fileIndex, metadata, chunkSize) {
        var fileInput = document.getElementById(fileInputId);
        if (!fileInput || !fileInput.files || !fileInput.files[fileIndex]) {
            dotNetRef.invokeMethodAsync('OnTusError', uploadId, 'File not found at index ' + fileIndex);
            return;
        }

        var file = fileInput.files[fileIndex];

        var upload = new tus.Upload(file, {
            endpoint: endpoint,
            retryDelays: [0, 1000, 3000, 5000, 10000, 20000],
            chunkSize: chunkSize || 5 * 1024 * 1024,
            metadata: metadata,
            removeFingerprintOnSuccess: true,
            onProgress: function (bytesUploaded, bytesTotal) {
                dotNetRef.invokeMethodAsync('OnTusProgress', uploadId, bytesUploaded, bytesTotal);
            },
            onSuccess: function () {
                window.tusInterop._markFileComplete(metadata.groupId, metadata.filename);
                dotNetRef.invokeMethodAsync('OnTusSuccess', uploadId);
                delete window.tusInterop._uploads[uploadId];
            },
            onError: function (error) {
                dotNetRef.invokeMethodAsync('OnTusError', uploadId, error.toString());
                delete window.tusInterop._uploads[uploadId];
            }
        });

        window.tusInterop._uploads[uploadId] = upload;

        upload.findPreviousUploads().then(function (previousUploads) {
            if (previousUploads.length) {
                upload.resumeFromPreviousUpload(previousUploads[0]);
            }
            upload.start();
        });
    },

    abortUpload: function (uploadId) {
        var upload = window.tusInterop._uploads[uploadId];
        if (upload) {
            upload.abort(true);
            delete window.tusInterop._uploads[uploadId];
        }
    },

    pauseUpload: function (uploadId) {
        var upload = window.tusInterop._uploads[uploadId];
        if (upload) {
            upload.abort();
        }
    },

    resumeUpload: function (uploadId) {
        var upload = window.tusInterop._uploads[uploadId];
        if (upload) {
            upload.start();
        }
    },

    // --- Session 관리 (B: localStorage 기반) ---

    saveSession: function (groupId, senderEmail, receiverEmail, fileNames) {
        var session = {
            groupId: groupId,
            senderEmail: senderEmail,
            receiverEmail: receiverEmail,
            files: {},
            createdAt: new Date().toISOString()
        };
        fileNames.forEach(function (name) {
            session.files[name] = 'pending';
        });
        localStorage.setItem(window.tusInterop._sessionKey, JSON.stringify(session));
    },

    loadSession: function (senderEmail, receiverEmail, fileNames) {
        try {
            var raw = localStorage.getItem(window.tusInterop._sessionKey);
            if (!raw) return null;

            var session = JSON.parse(raw);

            // 24시간 초과 세션은 무효
            var created = new Date(session.createdAt);
            if (new Date() - created > 24 * 60 * 60 * 1000) {
                localStorage.removeItem(window.tusInterop._sessionKey);
                return null;
            }

            // 같은 이메일 + 같은 파일 구성인지 확인
            if (session.senderEmail !== senderEmail || session.receiverEmail !== receiverEmail) {
                return null;
            }

            var sessionFileNames = Object.keys(session.files).sort();
            var currentFileNames = fileNames.slice().sort();
            if (JSON.stringify(sessionFileNames) !== JSON.stringify(currentFileNames)) {
                return null;
            }

            return session;
        } catch (e) {
            return null;
        }
    },

    clearSession: function () {
        localStorage.removeItem(window.tusInterop._sessionKey);
    },

    _markFileComplete: function (groupId, filename) {
        try {
            var raw = localStorage.getItem(window.tusInterop._sessionKey);
            if (!raw) return;
            var session = JSON.parse(raw);
            if (session.groupId === groupId && session.files[filename] !== undefined) {
                session.files[filename] = 'complete';
                localStorage.setItem(window.tusInterop._sessionKey, JSON.stringify(session));
            }
        } catch (e) { }
    },

    isFileComplete: function (groupId, filename) {
        try {
            var raw = localStorage.getItem(window.tusInterop._sessionKey);
            if (!raw) return false;
            var session = JSON.parse(raw);
            return session.groupId === groupId && session.files[filename] === 'complete';
        } catch (e) {
            return false;
        }
    }
};
