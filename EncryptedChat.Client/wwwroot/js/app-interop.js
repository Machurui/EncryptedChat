window.applyTheme = function(theme) {
    const root = document.documentElement;

    // Remove existing theme classes
    root.classList.remove('theme-dark', 'theme-light', 'theme-auto');

    if (theme === 'auto') {
        // Use system preference
        const prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
        root.classList.add(prefersDark ? 'theme-dark' : 'theme-light');
        root.classList.add('theme-auto');
    } else {
        root.classList.add(`theme-${theme}`);
    }

    // Store in localStorage for persistence
    localStorage.setItem('theme', theme);
};

window.getStoredTheme = function() {
    return localStorage.getItem('theme') || 'dark';
};

// Listen for system theme changes when in auto mode
window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', (e) => {
    if (document.documentElement.classList.contains('theme-auto')) {
        document.documentElement.classList.remove('theme-dark', 'theme-light');
        document.documentElement.classList.add(e.matches ? 'theme-dark' : 'theme-light');
    }
});

// Apply stored theme on load
(function() {
    const storedTheme = localStorage.getItem('theme') || 'dark';
    window.applyTheme(storedTheme);
})();

window.setupMobileComposerScroll = (inputId, messagesId) => {
    const input = document.getElementById(inputId);
    if (!input) return;

    const scrollToBottom = () => {
        const container = document.getElementById(messagesId);
        if (container) container.scrollTop = container.scrollHeight;
    };

    input.addEventListener('focus', () => {
        setTimeout(scrollToBottom, 300);
    });
};

window.popupRegistry = {
    handlers: new Map(),
    documentListenerAttached: false,
    _listener: null,

    register(popupId, dotNetRef, methodName, containerSelector) {
        this.handlers.set(popupId, { dotNetRef, methodName, containerSelector });
        this.attachListenerIfNeeded();
    },

    unregister(popupId) {
        this.handlers.delete(popupId);
        if (this.handlers.size === 0 && this.documentListenerAttached) {
            document.removeEventListener('mousedown', this._listener, true);
            this.documentListenerAttached = false;
            this._listener = null;
        }
    },

    attachListenerIfNeeded() {
        if (this.documentListenerAttached) return;
        this._listener = (e) => this.onDocumentClick(e);
        document.addEventListener('mousedown', this._listener, true);
        this.documentListenerAttached = true;
    },

    async onDocumentClick(e) {
        for (const [, { dotNetRef, methodName, containerSelector }] of this.handlers) {
            const containers = document.querySelectorAll(containerSelector);
            const insideAny = Array.from(containers).some(c => c.contains(e.target));
            if (!insideAny) {
                try {
                    await dotNetRef.invokeMethodAsync(methodName);
                } catch (err) {
                    window.encryptedChatConsoleCapture?.capture('warn', ['Popup callback failed:', err], 'app-interop.popup');
                }
            }
        }
    }
};

window.getScrollHeight = (elementId) => {
    const el = document.getElementById(elementId);
    return el ? el.scrollHeight : 0;
};

window.preserveScrollAfterPrepend = (elementId, oldScrollHeight) => {
    const el = document.getElementById(elementId);
    if (!el) return;
    const newScrollHeight = el.scrollHeight;
    const delta = newScrollHeight - oldScrollHeight;
    el.scrollTop = el.scrollTop + delta;
};

window.setupMessagesScrollListener = (elementId, dotNetRef, loadOlderMethod, jumpVisibleMethod) => {
    // Track which elements already have a listener attached to avoid duplicates
    if (!window._messagesScrollListeners) window._messagesScrollListeners = new WeakSet();

    const attach = (container) => {
        if (window._messagesScrollListeners.has(container)) return;
        window._messagesScrollListeners.add(container);

        let paginationThrottled = false;
        let lastJumpVisible = null;

        container.addEventListener('scroll', async () => {
            if (!paginationThrottled && container.scrollTop <= 100) {
                paginationThrottled = true;
                try {
                    await dotNetRef.invokeMethodAsync(loadOlderMethod);
                } catch (err) {
                    window.encryptedChatConsoleCapture?.capture('warn', ['LoadOlderMessages call failed:', err], 'app-interop.scroll');
                } finally {
                    setTimeout(() => { paginationThrottled = false; }, 300);
                }
            }

            const distanceFromBottom = container.scrollHeight - container.scrollTop - container.clientHeight;
            const farFromBottom = distanceFromBottom >= 200;
            if (farFromBottom !== lastJumpVisible) {
                lastJumpVisible = farFromBottom;
                try {
                    await dotNetRef.invokeMethodAsync(jumpVisibleMethod, farFromBottom);
                } catch (err) {
                    window.encryptedChatConsoleCapture?.capture('warn', ['SetJumpToBottomVisible call failed:', err], 'app-interop.scroll');
                }
            }
        });
    };

    // Try immediately
    const existing = document.getElementById(elementId);
    if (existing) {
        attach(existing);
        return;
    }

    // Otherwise watch the DOM for the element to appear
    const observer = new MutationObserver(() => {
        const el = document.getElementById(elementId);
        if (el) {
            observer.disconnect();
            attach(el);
        }
    });
    observer.observe(document.body, { childList: true, subtree: true });

    // Safety timeout (10s) — give up if container never appears
    setTimeout(() => observer.disconnect(), 10000);
};

// Live preview of the bubble color during a native <input type="color"> drag.
// We bypass Blazor entirely and write the CSS variable straight onto the
// messages-container element so the bubbles re-paint at native browser speed
// without triggering a Blazor re-render of the (very large) Chat.razor.
window.setBubbleColorPreview = (color) => {
    const el = document.getElementById('messages-container');
    if (el) el.style.setProperty('--own-bubble', color);
};

// Used by Chat.razor's OnInitializedAsync to decide whether to auto-select
// the first team. On mobile (≤ 767px) we want the user to land on the
// sidebar (team/DM list) and pick a conversation themselves — same UX as
// WhatsApp / Telegram / Discord. Above that, the 3-column layout fits and
// auto-opening the first team is a faster start.
window.getViewportWidth = () => window.innerWidth || document.documentElement.clientWidth || 0;
