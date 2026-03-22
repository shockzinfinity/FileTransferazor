window.tusInterop = {
    _uploads: {},

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
            onProgress: function (bytesUploaded, bytesTotal) {
                dotNetRef.invokeMethodAsync('OnTusProgress', uploadId, bytesUploaded, bytesTotal);
            },
            onSuccess: function () {
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
    }
};
