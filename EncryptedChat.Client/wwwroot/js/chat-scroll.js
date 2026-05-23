window.chatScroll = (function () {
    const ATBOTTOM_THRESHOLD = 80;
    const containers = new Map();

    function attach(elementId) {
        const el = document.getElementById(elementId);
        if (!el || containers.has(elementId)) return;

        const state = { following: true, lastScrollTop: el.scrollTop };

        // ResizeObserver: any layout change (image load, font swap, message resize) → re-scroll if following
        const ro = new ResizeObserver(function () {
            if (state.following) el.scrollTop = el.scrollHeight;
        });
        ro.observe(el);

        // MutationObserver: new DOM nodes appended → re-scroll if following (covers cases ResizeObserver misses)
        const mo = new MutationObserver(function () {
            if (state.following) el.scrollTop = el.scrollHeight;
        });
        mo.observe(el, { childList: true, subtree: true });

        // Late-arriving image loads (capture phase to catch nested <img>)
        const onLoad = function (e) {
            if (e.target && e.target.tagName === 'IMG' && state.following) {
                el.scrollTop = el.scrollHeight;
            }
        };
        el.addEventListener('load', onLoad, true);

        // Scroll handler: only disable follow when user scrolls UP away from bottom.
        // Scrolling DOWN (including programmatic smooth scrolls in progress) preserves
        // the following state so MutationObserver / ResizeObserver keep us glued to
        // the bottom even while new content arrives mid-animation.
        const onScroll = function () {
            const atBottom = el.scrollHeight - el.scrollTop - el.clientHeight < ATBOTTOM_THRESHOLD;
            const scrolledUp = el.scrollTop < state.lastScrollTop - 5;
            if (atBottom) {
                state.following = true;
            } else if (scrolledUp) {
                state.following = false;
            }
            // Scrolling down but not yet at bottom: keep current state (programmatic
            // smooth scrolls pass through here; user manual down-scroll without
            // reaching bottom is rare and harmless).
            state.lastScrollTop = el.scrollTop;
        };
        el.addEventListener('scroll', onScroll, { passive: true });

        containers.set(elementId, { ro, mo, state, onLoad, onScroll });
    }

    function follow(elementId, smooth) {
        const c = containers.get(elementId);
        const el = document.getElementById(elementId);
        if (!c || !el) return;
        c.state.following = true;
        el.scrollTo({ top: el.scrollHeight, behavior: smooth ? 'smooth' : 'instant' });
    }

    function unfollow(elementId) {
        const c = containers.get(elementId);
        if (c) c.state.following = false;
    }

    function isFollowing(elementId) {
        const c = containers.get(elementId);
        return c ? c.state.following : false;
    }

    function detach(elementId) {
        const c = containers.get(elementId);
        const el = document.getElementById(elementId);
        if (!c) return;
        c.ro.disconnect();
        c.mo.disconnect();
        if (el) {
            el.removeEventListener('load', c.onLoad, true);
            el.removeEventListener('scroll', c.onScroll);
        }
        containers.delete(elementId);
    }

    return {
        attach: attach,
        follow: follow,
        unfollow: unfollow,
        isFollowing: isFollowing,
        detach: detach
    };
})();
