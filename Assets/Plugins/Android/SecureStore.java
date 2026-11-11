package com.kyalio.secure;

import android.security.keystore.KeyGenParameterSpec;
import android.security.keystore.KeyPermanentlyInvalidatedException;
import android.security.keystore.KeyProperties;
import android.util.Base64;
import android.util.Log;

import java.nio.charset.StandardCharsets;
import java.security.KeyStore;
import java.security.UnrecoverableKeyException;

import javax.crypto.AEADBadTagException;
import javax.crypto.Cipher;
import javax.crypto.KeyGenerator;
import javax.crypto.SecretKey;
import javax.crypto.spec.GCMParameterSpec;

/**
 * Encrypts small secrets with an AES-256-GCM key that lives in the Android Keystore.
 * The key is non-exportable (hardware-backed where the device has a TEE), so a copy of
 * the app's data directory is useless off-device.
 *
 * Called from C# (Kyalio.Utils.SecureStore) via JNI. Payload format: Base64(iv || ciphertext+tag).
 * Methods never throw across JNI; see each method for its failure contract.
 */
public final class SecureStore {
    private static final String TAG = "KyalioSecureStore";
    private static final String KEYSTORE = "AndroidKeyStore";
    private static final String ALIAS = "kyalio_remember_me_v1";
    private static final String TRANSFORMATION = "AES/GCM/NoPadding";
    private static final int IV_LENGTH = 12;
    private static final int TAG_BITS = 128;

    /** decrypt() result prefix: the rest of the string is the plaintext. */
    private static final String RESULT_OK = "1";
    /** decrypt() result: the payload can never be decrypted (key gone, data corrupt). */
    private static final String RESULT_INVALID = "0";

    /** Keystore lookups go through a binder call to the keystore service; cache the handle. */
    private static SecretKey cachedKey;

    private SecureStore() { }

    /** Returns the payload, or null on failure. */
    public static synchronized String encrypt(String plaintext) {
        if (plaintext == null) return null;
        try {
            Cipher cipher = Cipher.getInstance(TRANSFORMATION);
            cipher.init(Cipher.ENCRYPT_MODE, getOrCreateKey());
            byte[] iv = cipher.getIV();
            byte[] ct = cipher.doFinal(plaintext.getBytes(StandardCharsets.UTF_8));

            byte[] out = new byte[iv.length + ct.length];
            System.arraycopy(iv, 0, out, 0, iv.length);
            System.arraycopy(ct, 0, out, iv.length, ct.length);
            return Base64.encodeToString(out, Base64.NO_WRAP);
        } catch (Exception e) {
            cachedKey = null;
            Log.w(TAG, "encrypt failed: " + e.getClass().getSimpleName());
            return null;
        }
    }

    /**
     * Returns "1" + plaintext on success, "0" when the payload is permanently undecryptable
     * (caller should discard it), or null on a possibly transient failure (caller should
     * keep the payload and retry later).
     */
    public static synchronized String decrypt(String payload) {
        if (payload == null || payload.isEmpty()) return RESULT_INVALID;

        byte[] in;
        try {
            in = Base64.decode(payload, Base64.NO_WRAP);
        } catch (IllegalArgumentException e) {
            return RESULT_INVALID;
        }
        if (in.length <= IV_LENGTH) return RESULT_INVALID;

        try {
            SecretKey key = getExistingKey();
            if (key == null) return RESULT_INVALID; // keystore reachable, key gone

            Cipher cipher = Cipher.getInstance(TRANSFORMATION);
            cipher.init(Cipher.DECRYPT_MODE, key, new GCMParameterSpec(TAG_BITS, in, 0, IV_LENGTH));
            byte[] pt = cipher.doFinal(in, IV_LENGTH, in.length - IV_LENGTH);
            return RESULT_OK + new String(pt, StandardCharsets.UTF_8);
        } catch (AEADBadTagException | KeyPermanentlyInvalidatedException | UnrecoverableKeyException e) {
            cachedKey = null;
            Log.w(TAG, "decrypt invalid: " + e.getClass().getSimpleName());
            return RESULT_INVALID;
        } catch (Exception e) {
            cachedKey = null;
            Log.w(TAG, "decrypt failed: " + e.getClass().getSimpleName());
            return null;
        }
    }

    private static SecretKey getExistingKey() throws Exception {
        if (cachedKey != null) return cachedKey;
        KeyStore ks = KeyStore.getInstance(KEYSTORE);
        ks.load(null);
        cachedKey = (SecretKey) ks.getKey(ALIAS, null);
        return cachedKey;
    }

    private static SecretKey getOrCreateKey() throws Exception {
        SecretKey existing = getExistingKey();
        if (existing != null) return existing;

        KeyGenerator gen = KeyGenerator.getInstance(KeyProperties.KEY_ALGORITHM_AES, KEYSTORE);
        gen.init(new KeyGenParameterSpec.Builder(ALIAS,
                KeyProperties.PURPOSE_ENCRYPT | KeyProperties.PURPOSE_DECRYPT)
                .setBlockModes(KeyProperties.BLOCK_MODE_GCM)
                .setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE)
                .setKeySize(256)
                .setRandomizedEncryptionRequired(true)
                .build());
        cachedKey = gen.generateKey();
        return cachedKey;
    }
}
