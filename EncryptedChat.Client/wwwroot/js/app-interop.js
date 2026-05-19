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
