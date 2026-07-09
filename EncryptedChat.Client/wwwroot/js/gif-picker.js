window.gifPicker = (function () {
    const observers = new Map();

    function observeLoadMore(sentinelEl, dotnetRef) {
        if (!sentinelEl || observers.has(sentinelEl)) return;

        const observer = new IntersectionObserver(function (entries) {
            if (entries[0].isIntersecting) {
                dotnetRef.invokeMethodAsync('OnGifLoadMore').catch(function (err) {
                    window.encryptedChatConsoleCapture?.capture('warn', ['OnGifLoadMore invoke failed:', err], 'gif-picker');
                });
            }
        }, { rootMargin: '400px 0px' });

        observer.observe(sentinelEl);
        observers.set(sentinelEl, observer);
    }

    function stopObserving(sentinelEl) {
        const obs = observers.get(sentinelEl);
        if (obs) {
            obs.disconnect();
            observers.delete(sentinelEl);
        }
    }

    function prefetchImages(urls) {
        if (!Array.isArray(urls)) return;
        urls.forEach(function (url) {
            if (typeof url === 'string' && url.length > 0) {
                const img = new Image();
                img.src = url;
            }
        });
    }

    return {
        observeLoadMore: observeLoadMore,
        stopObserving: stopObserving,
        prefetchImages: prefetchImages
    };
})();
