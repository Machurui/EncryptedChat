const assert = require('node:assert/strict');
const path = require('node:path');
const test = require('node:test');

const composerScript = path.resolve(__dirname, '../wwwroot/js/composer-overlay.js');

function createInput(value, caret) {
    const listeners = new Map();

    return {
        value,
        selectionStart: caret,
        selectionEnd: caret,
        dataset: {},
        style: {},
        scrollHeight: 24,

        addEventListener(type, listener) {
            listeners.set(type, listener);
        },

        emit(type, event = {}) {
            const listener = listeners.get(type);
            assert.ok(listener, `Expected a ${type} listener to be registered.`);
            listener(event);
        },

        setSelectionRange(start, end) {
            this.selectionStart = start;
            this.selectionEnd = end;
        }
    };
}

function setupComposer(value, caret = value.length) {
    const input = createInput(value, caret);

    global.window = {};
    global.document = {
        getElementById(id) {
            return id === 'composer-input' ? input : null;
        }
    };

    delete require.cache[require.resolve(composerScript)];
    require(composerScript);
    global.window.composerOverlay.sync('composer-input', 'composer-highlight');

    return input;
}

function keydown(input, values) {
    const event = {
        defaultPrevented: false,
        ...values,
        preventDefault() {
            this.defaultPrevented = true;
        }
    };

    input.emit('keydown', event);
    return event;
}

test.afterEach(() => {
    delete global.window;
    delete global.document;
});

test('Shift+Enter is blocked until non-whitespace text precedes the caret', () => {
    const blankInput = setupComposer('   ', 3);
    const blankEvent = keydown(blankInput, { key: 'Enter', shiftKey: true });
    assert.equal(blankEvent.defaultPrevented, true);

    const beforeTextInput = setupComposer('hello', 0);
    const beforeTextEvent = keydown(beforeTextInput, { key: 'Enter', shiftKey: true });
    assert.equal(beforeTextEvent.defaultPrevented, true);

    const afterTextInput = setupComposer('hello', 5);
    const afterTextEvent = keydown(afterTextInput, { key: 'Enter', shiftKey: true });
    assert.equal(afterTextEvent.defaultPrevented, false);
});

test('plain Enter prevents a native line break while Blazor handles sending', () => {
    const input = setupComposer('hello');
    const event = keydown(input, { key: 'Enter', shiftKey: false });

    assert.equal(event.defaultPrevented, true);
});

test('input removes only leading blank lines and preserves internal line breaks', () => {
    const input = setupComposer('  \n\nhello\n\nworld');

    input.emit('input');

    assert.equal(input.value, 'hello\n\nworld');
    assert.equal(input.selectionStart, input.value.length);

    const alreadyValidInput = setupComposer('hello\n\nworld');
    alreadyValidInput.emit('input');

    assert.equal(alreadyValidInput.value, 'hello\n\nworld');
});
