(function () {
    'use strict';

    const assemblyName = 'EncryptedChat.Client';
    const maxQueueSize = 200;
    const interceptedMethods = ['log', 'debug', 'info', 'warn', 'error', 'trace', 'table', 'group', 'groupCollapsed', 'groupEnd'];
    const queue = [];
    let flushScheduled = false;
    let flushing = false;

    function truncate(text, maxLength) {
        if (typeof text !== 'string') return '';
        return text.length <= maxLength ? text : text.slice(0, maxLength) + '...';
    }

    function serializeValue(value, seen) {
        if (value === null) return 'null';
        if (value === undefined) return 'undefined';
        if (typeof value === 'string') return value;
        if (typeof value === 'number' || typeof value === 'boolean' || typeof value === 'bigint') return String(value);
        if (value instanceof Error) return truncate(`${value.name}: ${value.message}\n${value.stack || ''}`, 2000);
        if (value instanceof Element) {
            const id = value.id ? `#${value.id}` : '';
            const classes = value.className && typeof value.className === 'string'
                ? '.' + value.className.trim().split(/\s+/).join('.')
                : '';
            return `<${value.tagName.toLowerCase()}${id}${classes}>`;
        }
        if (seen.has(value)) return '[Circular]';

        try {
            seen.add(value);
            return truncate(JSON.stringify(value, function (_key, nestedValue) {
                if (typeof nestedValue === 'bigint') return String(nestedValue);
                if (typeof nestedValue === 'function') return `[Function ${nestedValue.name || 'anonymous'}]`;
                if (nestedValue instanceof Element) return serializeValue(nestedValue, seen);
                return nestedValue;
            }), 2000);
        } catch {
            return Object.prototype.toString.call(value);
        }
    }

    function serializeArguments(args) {
        return Array.prototype.slice.call(args || []).map(function (arg) {
            return serializeValue(arg, new WeakSet());
        });
    }

    function findStack(args) {
        const items = Array.prototype.slice.call(args || []);
        const error = items.find(function (item) { return item instanceof Error && item.stack; });
        return error ? error.stack : '';
    }

    function enqueue(level, args, source, stack) {
        const serializedArgs = serializeArguments(args);
        const payload = {
            Level: level,
            Message: truncate(serializedArgs.join(' '), 4000),
            Arguments: serializedArgs,
            Stack: truncate(stack || findStack(args), 4000),
            Url: window.location.href,
            UserAgent: navigator.userAgent,
            Source: source || `console.${level}`,
            Timestamp: new Date().toISOString()
        };

        if (queue.length >= maxQueueSize) queue.shift();
        queue.push(payload);
        scheduleFlush(0);
    }

    function flushQueue() {
        flushScheduled = false;
        if (flushing || queue.length === 0) return;
        if (!window.DotNet || typeof window.DotNet.invokeMethodAsync !== 'function') {
            scheduleFlush(500);
            return;
        }

        const payload = queue.shift();
        flushing = true;

        window.DotNet.invokeMethodAsync(assemblyName, 'CaptureConsoleMessage', payload)
            .catch(function () {
                if (queue.length < maxQueueSize) queue.unshift(payload);
                scheduleFlush(500);
            })
            .finally(function () {
                flushing = false;
                if (queue.length > 0) scheduleFlush(0);
            });
    }

    function scheduleFlush(delay) {
        if (flushScheduled) return;
        flushScheduled = true;
        window.setTimeout(flushQueue, delay);
    }

    function capture(level, args, source) {
        enqueue(level, args, source || `console.${level}`, '');
    }

    interceptedMethods.forEach(function (method) {
        if (typeof window.console[method] !== 'function') return;
        window.console[method] = function () {
            capture(method, arguments, `console.${method}`);
        };
    });

    window.addEventListener('error', function (event) {
        enqueue('error', [event.message || 'Unhandled browser error'], 'window.error', event.error && event.error.stack);
        event.preventDefault();
        return true;
    }, true);

    window.addEventListener('unhandledrejection', function (event) {
        enqueue('error', ['Unhandled promise rejection', event.reason], 'window.unhandledrejection', findStack([event.reason]));
        event.preventDefault();
        return true;
    }, true);

    window.encryptedChatConsoleCapture = {
        capture: function (level, args, source) {
            enqueue(level || 'log', Array.isArray(args) ? args : [args], source || `console.${level || 'log'}`, '');
        },
        flush: flushQueue
    };

    window.setInterval(flushQueue, 500);
})();
