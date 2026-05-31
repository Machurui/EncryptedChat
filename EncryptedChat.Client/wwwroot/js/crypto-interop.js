// WebCrypto bridge for E2E. All methods return Promises; the C# wrapper awaits
// them via IJSRuntime.InvokeAsync<T>(). Binary data crosses the boundary as
// base64 strings (Uint8Array round-trips through JSInterop are flakey under
// some Blazor versions).

(function () {
    const enc = new TextEncoder();
    const dec = new TextDecoder();

    function b64Encode(bytes) {
        let s = '';
        for (let i = 0; i < bytes.length; i++) s += String.fromCharCode(bytes[i]);
        return btoa(s);
    }
    function b64Decode(b64) {
        const s = atob(b64);
        const out = new Uint8Array(s.length);
        for (let i = 0; i < s.length; i++) out[i] = s.charCodeAt(i);
        return out;
    }

    async function importEcdsaPriv(pkcs8Bytes) {
        return crypto.subtle.importKey('pkcs8', pkcs8Bytes,
            { name: 'ECDSA', namedCurve: 'P-256' }, false, ['sign']);
    }
    async function importEcdsaPub(spkiBytes) {
        return crypto.subtle.importKey('spki', spkiBytes,
            { name: 'ECDSA', namedCurve: 'P-256' }, false, ['verify']);
    }
    async function importEcdhPriv(pkcs8Bytes) {
        return crypto.subtle.importKey('pkcs8', pkcs8Bytes,
            { name: 'ECDH', namedCurve: 'P-256' }, false, ['deriveBits']);
    }
    async function importEcdhPub(spkiBytes) {
        return crypto.subtle.importKey('spki', spkiBytes,
            { name: 'ECDH', namedCurve: 'P-256' }, false, []);
    }
    async function importAesKey(rawBytes) {
        return crypto.subtle.importKey('raw', rawBytes,
            { name: 'AES-GCM' }, false, ['encrypt', 'decrypt']);
    }

    // ---------- Identity keypair generation ----------

    async function generateIdentityKeyPair() {
        const signing = await crypto.subtle.generateKey(
            { name: 'ECDSA', namedCurve: 'P-256' }, true, ['sign', 'verify']);
        const encryption = await crypto.subtle.generateKey(
            { name: 'ECDH', namedCurve: 'P-256' }, true, ['deriveBits']);

        const [signPriv, signPub, encPriv, encPub] = await Promise.all([
            crypto.subtle.exportKey('pkcs8', signing.privateKey),
            crypto.subtle.exportKey('spki', signing.publicKey),
            crypto.subtle.exportKey('pkcs8', encryption.privateKey),
            crypto.subtle.exportKey('spki', encryption.publicKey)
        ]);

        return {
            signingPrivateKey: b64Encode(new Uint8Array(signPriv)),
            signingPublicKey: b64Encode(new Uint8Array(signPub)),
            encryptionPrivateKey: b64Encode(new Uint8Array(encPriv)),
            encryptionPublicKey: b64Encode(new Uint8Array(encPub))
        };
    }

    // ---------- AES-256-GCM ----------

    async function encryptAesGcm(plaintextB64, keyB64) {
        const key = await importAesKey(b64Decode(keyB64));
        const iv = crypto.getRandomValues(new Uint8Array(12));
        const ciphertextWithTag = new Uint8Array(await crypto.subtle.encrypt(
            { name: 'AES-GCM', iv: iv, tagLength: 128 }, key, b64Decode(plaintextB64)));
        return { iv: b64Encode(iv), ciphertextWithTag: b64Encode(ciphertextWithTag) };
    }

    async function decryptAesGcm(ivB64, ciphertextB64, keyB64) {
        const key = await importAesKey(b64Decode(keyB64));
        const plaintext = await crypto.subtle.decrypt(
            { name: 'AES-GCM', iv: b64Decode(ivB64), tagLength: 128 },
            key, b64Decode(ciphertextB64));
        return b64Encode(new Uint8Array(plaintext));
    }

    // ---------- ECDSA sign / verify ----------

    async function sign(dataB64, signingPrivPkcs8B64) {
        const key = await importEcdsaPriv(b64Decode(signingPrivPkcs8B64));
        const sig = await crypto.subtle.sign(
            { name: 'ECDSA', hash: { name: 'SHA-256' } },
            key, b64Decode(dataB64));
        return b64Encode(new Uint8Array(sig));
    }

    async function verify(dataB64, signatureB64, signingPubSpkiB64) {
        const key = await importEcdsaPub(b64Decode(signingPubSpkiB64));
        return crypto.subtle.verify(
            { name: 'ECDSA', hash: { name: 'SHA-256' } },
            key, b64Decode(signatureB64), b64Decode(dataB64));
    }

    // ---------- ECIES wrap / unwrap (ECDH-P256 + HKDF-SHA256 + AES-GCM) ----------

    const WRAP_INFO = enc.encode('EncryptedChat:TeamKeyWrap:v1');

    async function hkdfTo32(ikm) {
        const baseKey = await crypto.subtle.importKey('raw', ikm, 'HKDF', false, ['deriveBits']);
        const bits = await crypto.subtle.deriveBits(
            { name: 'HKDF', hash: 'SHA-256', salt: new Uint8Array(0), info: WRAP_INFO },
            baseKey, 256);
        return new Uint8Array(bits);
    }

    // Output blob layout: 2-byte BE length || ephemeralPubSpki || 12-byte iv || ciphertext+tag
    async function wrapKey(keyToWrapB64, recipientEncPubSpkiB64) {
        const recipientPub = await importEcdhPub(b64Decode(recipientEncPubSpkiB64));

        const ephemeral = await crypto.subtle.generateKey(
            { name: 'ECDH', namedCurve: 'P-256' }, true, ['deriveBits']);
        const ephemeralPub = new Uint8Array(await crypto.subtle.exportKey('spki', ephemeral.publicKey));

        const ephemeralPrivForDerive = await crypto.subtle.importKey(
            'pkcs8', await crypto.subtle.exportKey('pkcs8', ephemeral.privateKey),
            { name: 'ECDH', namedCurve: 'P-256' }, false, ['deriveBits']);

        const shared = new Uint8Array(await crypto.subtle.deriveBits(
            { name: 'ECDH', public: recipientPub }, ephemeralPrivForDerive, 256));

        const wrapKeyBytes = await hkdfTo32(shared);
        const aesKey = await importAesKey(wrapKeyBytes);
        const iv = crypto.getRandomValues(new Uint8Array(12));
        const ciphertextWithTag = new Uint8Array(await crypto.subtle.encrypt(
            { name: 'AES-GCM', iv: iv, tagLength: 128 }, aesKey, b64Decode(keyToWrapB64)));

        const totalLen = 2 + ephemeralPub.length + iv.length + ciphertextWithTag.length;
        const out = new Uint8Array(totalLen);
        out[0] = (ephemeralPub.length >> 8) & 0xFF;
        out[1] = ephemeralPub.length & 0xFF;
        out.set(ephemeralPub, 2);
        out.set(iv, 2 + ephemeralPub.length);
        out.set(ciphertextWithTag, 2 + ephemeralPub.length + iv.length);
        return b64Encode(out);
    }

    async function unwrapKey(wrappedB64, recipientEncPrivPkcs8B64) {
        const wrapped = b64Decode(wrappedB64);
        const ephemeralLen = (wrapped[0] << 8) | wrapped[1];
        const ephemeralSpki = wrapped.slice(2, 2 + ephemeralLen);
        const ivOffset = 2 + ephemeralLen;
        const iv = wrapped.slice(ivOffset, ivOffset + 12);
        const ciphertextWithTag = wrapped.slice(ivOffset + 12);

        const recipientPriv = await importEcdhPriv(b64Decode(recipientEncPrivPkcs8B64));
        const ephemeralPub = await importEcdhPub(ephemeralSpki);

        const shared = new Uint8Array(await crypto.subtle.deriveBits(
            { name: 'ECDH', public: ephemeralPub }, recipientPriv, 256));

        const wrapKeyBytes = await hkdfTo32(shared);
        const aesKey = await importAesKey(wrapKeyBytes);
        const plaintext = await crypto.subtle.decrypt(
            { name: 'AES-GCM', iv: iv, tagLength: 128 }, aesKey, ciphertextWithTag);
        return b64Encode(new Uint8Array(plaintext));
    }

    // ---------- PBKDF2-SHA256 / random salt / random bytes ----------

    async function deriveWrapKey(phrase, saltB64) {
        const baseKey = await crypto.subtle.importKey('raw', enc.encode(phrase),
            'PBKDF2', false, ['deriveBits']);
        const bits = await crypto.subtle.deriveBits(
            { name: 'PBKDF2', hash: 'SHA-256', salt: b64Decode(saltB64), iterations: 600000 },
            baseKey, 256);
        return b64Encode(new Uint8Array(bits));
    }

    function generateSalt() {
        return b64Encode(crypto.getRandomValues(new Uint8Array(16)));
    }

    function generateTeamSecret() {
        return b64Encode(crypto.getRandomValues(new Uint8Array(32)));
    }

    async function sha256(dataB64) {
        const h = await crypto.subtle.digest('SHA-256', b64Decode(dataB64));
        return b64Encode(new Uint8Array(h));
    }

    window.encryptedChatCrypto = {
        generateIdentityKeyPair,
        encryptAesGcm, decryptAesGcm,
        sign, verify,
        wrapKey, unwrapKey,
        deriveWrapKey, generateSalt, generateTeamSecret,
        sha256
    };

    // Tiny blob URL helper used by EncryptedImage.razor to render decrypted
    // attachment bytes directly in <img src="blob:...">. The component calls
    // revoke() on dispose so URLs don't leak memory.
    window.encryptedChatBlob = {
        fromBytes(b64, mimeType) {
            const bytes = b64Decode(b64);
            const blob = new Blob([bytes], { type: mimeType || 'application/octet-stream' });
            return URL.createObjectURL(blob);
        },
        revoke(url) {
            URL.revokeObjectURL(url);
        }
    };
})();
