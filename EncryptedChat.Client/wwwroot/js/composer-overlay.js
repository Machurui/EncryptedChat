window.composerOverlay = {
    sync(inputId, overlayId) {
        const input = document.getElementById(inputId);
        const overlay = document.getElementById(overlayId);
        if (!input || !overlay) return;
        if (input.dataset.overlayBound === '1') return;
        input.dataset.overlayBound = '1';
        const apply = () => {
            overlay.style.transform = `translateX(${-input.scrollLeft}px)`;
        };
        input.addEventListener('input', apply);
        input.addEventListener('scroll', apply);
        input.addEventListener('keyup', apply);
        input.addEventListener('click', apply);
        apply();
    }
};
