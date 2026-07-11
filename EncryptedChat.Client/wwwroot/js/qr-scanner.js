window.encryptedChatQrScanner = (function () {
    let activeSession = null;

    function stopVideo(videoElement) {
        if (!videoElement) return;

        const stream = videoElement.srcObject;
        if (stream && typeof stream.getTracks === 'function') {
            stream.getTracks().forEach(function (track) {
                track.stop();
            });
        }

        videoElement.srcObject = null;
    }

    function stop() {
        const session = activeSession;
        activeSession = null;
        if (!session) return;

        session.completed = true;
        try {
            session.controls?.stop();
        } catch {
            // The stream may already have been stopped after a successful scan.
        }
        stopVideo(session.videoElement);
    }

    function cameraErrorCode(error) {
        switch (error?.name) {
            case 'NotAllowedError':
            case 'PermissionDeniedError':
                return 'permission-denied';
            case 'NotFoundError':
            case 'DevicesNotFoundError':
                return 'camera-not-found';
            case 'NotReadableError':
            case 'TrackStartError':
                return 'camera-busy';
            case 'OverconstrainedError':
            case 'ConstraintNotSatisfiedError':
                return 'camera-constraints';
            case 'SecurityError':
                return 'insecure-context';
            default:
                return 'camera-unavailable';
        }
    }

    async function start(videoElement, dotNetReference, facingMode, sessionId) {
        stop();

        if (!videoElement || !dotNetReference) {
            return { success: false, errorCode: 'scanner-unavailable' };
        }
        if (!window.isSecureContext) {
            return { success: false, errorCode: 'insecure-context' };
        }
        if (!navigator.mediaDevices?.getUserMedia) {
            return { success: false, errorCode: 'camera-unsupported' };
        }
        if (!window.ZXingBrowser?.BrowserQRCodeReader) {
            return { success: false, errorCode: 'scanner-unavailable' };
        }

        const reader = new window.ZXingBrowser.BrowserQRCodeReader(undefined, {
            delayBetweenScanAttempts: 150,
            delayBetweenScanSuccess: 500
        });
        const session = {
            reader: reader,
            controls: null,
            videoElement: videoElement,
            completed: false
        };
        activeSession = session;

        try {
            const controls = await reader.decodeFromConstraints({
                audio: false,
                video: {
                    facingMode: { ideal: facingMode === 'user' ? 'user' : 'environment' },
                    width: { ideal: 1280 },
                    height: { ideal: 720 }
                }
            }, videoElement, function (result, _error, callbackControls) {
                if (!result || session.completed || activeSession !== session) return;

                session.completed = true;
                activeSession = null;
                callbackControls.stop();
                stopVideo(videoElement);

                dotNetReference.invokeMethodAsync('OnQrCodeDetected', result.getText(), sessionId)
                    .catch(function (error) {
                        window.encryptedChatConsoleCapture?.capture(
                            'warn',
                            ['QR result delivery failed:', error],
                            'qr-scanner');
                    });
            });

            session.controls = controls;
            if (activeSession !== session || session.completed) {
                controls.stop();
            }

            return { success: true, errorCode: null };
        } catch (error) {
            if (activeSession === session) activeSession = null;
            stopVideo(videoElement);
            return { success: false, errorCode: cameraErrorCode(error) };
        }
    }

    async function decodeImage(fileInput) {
        stop();

        const file = fileInput?.files?.[0];
        if (!file) return { success: false, text: null, errorCode: 'image-missing' };
        if (!file.type?.startsWith('image/')) {
            fileInput.value = '';
            return { success: false, text: null, errorCode: 'image-invalid' };
        }
        if (!window.ZXingBrowser?.BrowserQRCodeReader) {
            fileInput.value = '';
            return { success: false, text: null, errorCode: 'scanner-unavailable' };
        }

        const objectUrl = URL.createObjectURL(file);
        try {
            const reader = new window.ZXingBrowser.BrowserQRCodeReader();
            const result = await reader.decodeFromImageUrl(objectUrl);
            return { success: true, text: result.getText(), errorCode: null };
        } catch {
            return { success: false, text: null, errorCode: 'qr-not-found' };
        } finally {
            URL.revokeObjectURL(objectUrl);
            fileInput.value = '';
        }
    }

    return {
        start: start,
        stop: stop,
        decodeImage: decodeImage
    };
})();
