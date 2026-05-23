window.gifPicker = (function () {
    const observers = new Map();

    function observeLoadMore(sentinelEl, dotnetRef) {
        if (!sentinelEl || observers.has(sentinelEl)) return;

        const observer = new IntersectionObserver(function (entries) {
            if (entries[0].isIntersecting) {
                dotnetRef.invokeMethodAsync('OnGifLoadMore').catch(function (err) {
                    console.warn('OnGifLoadMore invoke failed:', err);
                });
            }
        }, { threshold: 0.1 });

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

    return { observeLoadMore: observeLoadMore, stopObserving: stopObserving };
})();
