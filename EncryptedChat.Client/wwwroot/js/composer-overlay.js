// Despite the historical filename, there is no overlay anymore. This file
// now just runs autogrow on the composer textarea and exposes caret helpers
// for inserting emojis / GIFs / mentions at the current cursor position.
const stripLeadingBlankLines = (value) =>
    value.replace(/^(?:[^\S\r\n]*(?:\r\n|\r|\n))+/, '');

window.composerOverlay = {
    sync(inputId, _overlayId) {
        const input = document.getElementById(inputId);
        if (!input) return;
        if (input.dataset.composerBound === '1') return;
        input.dataset.composerBound = '1';

        const autogrow = () => {
            input.style.height = 'auto';
            input.style.height = input.scrollHeight + 'px';
        };

        const handleKeyDown = (event) => {
            if (event.key !== 'Enter') return;

            // Blazor still receives the event and handles send / mention
            // selection. Only suppress the textarea's native line break.
            if (!event.shiftKey) {
                event.preventDefault();
                return;
            }

            const caret = input.selectionStart ?? 0;
            const textBeforeCaret = input.value.slice(0, caret);
            if (!textBeforeCaret.trim()) event.preventDefault();
        };

        const handleInput = () => {
            const originalValue = input.value;
            const normalizedValue = stripLeadingBlankLines(originalValue);

            if (normalizedValue !== originalValue) {
                const removedLength = originalValue.length - normalizedValue.length;
                const start = Math.max(0, (input.selectionStart ?? originalValue.length) - removedLength);
                const end = Math.max(0, (input.selectionEnd ?? originalValue.length) - removedLength);

                input.value = normalizedValue;
                input.setSelectionRange(start, end);
            }

            autogrow();
        };

        input.addEventListener('keydown', handleKeyDown);
        input.addEventListener('input', handleInput);
        // Initial sizing in case the textarea mounted with content (e.g.
        // restored draft).
        autogrow();
    },

    // Called from C# after the textarea value is reset programmatically
    // (clearing after send) or set programmatically (inserting a GIF URL).
    // The input event listener only runs on user input, so this re-runs the
    // autogrow to resize the box to its real content height.
    resize(inputId) {
        const apply = () => {
            const input = document.getElementById(inputId);
            if (!input) return;
            input.style.height = 'auto';
            input.style.height = input.scrollHeight + 'px';
        };
        // Defer the scrollHeight read so Blazor's pending DOM mutation (e.g. the
        // cleared value="" after a send) is applied first. Reading synchronously
        // can measure the just-sent content — a long GIF URL wraps to two rows —
        // and leaves the textarea stuck at that taller height. Same rAF pattern
        // as focusAndSetCursor below; setTimeout covers late renders on mobile WebKit.
        requestAnimationFrame(apply);
        setTimeout(apply, 30);
    },

    getCursorPos(inputId) {
        const input = document.getElementById(inputId);
        if (!input) return 0;
        return input.selectionStart ?? (input.value ? input.value.length : 0);
    },

    setCursorPos(inputId, pos) {
        const input = document.getElementById(inputId);
        if (!input) return;
        try { input.setSelectionRange(pos, pos); } catch { /* unfocused or unsupported */ }
    },

    // Atomic focus + caret placement. The standalone focusComposerInput
    // helper uses rAF + setTimeout chains, which race against any
    // separately-scheduled setSelectionRange call: the focus runs LAST and
    // dumps the caret at position 0 (textarea's default on fresh focus).
    // This method runs focus and setSelectionRange in the same task so the
    // caret lands at the requested position and stays there. Called from
    // C# right after inserting emoji / GIFs / mention text.
    focusAndSetCursor(inputId, pos) {
        const input = document.getElementById(inputId);
        if (!input) return;
        const place = () => {
            input.focus();
            try { input.setSelectionRange(pos, pos); } catch { /* unsupported */ }
            input.scrollTop = input.scrollHeight; // keep caret visible
        };
        // Schedule on the next animation frame so Blazor's pending DOM
        // mutation (the new textarea value with the inserted emoji) has
        // been applied before we read scrollHeight and place the caret.
        requestAnimationFrame(place);
        // Belt-and-suspenders against late renders on mobile WebKit.
        setTimeout(place, 30);
    }
};
