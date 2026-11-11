package com.kyalio.secure;

import android.security.keystore.KeyGenParameterSpec;
import android.security.keystore.KeyProperties;
import android.util.Base64;
import android.util.Log;

import java.nio.charset.StandardCharsets;
import java.security.KeyStore;

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
 * Every method returns null on failure instead of throwing, so the caller can fall back to
 * "nothing remembered".
 */
public final class SecureStore {
    private static final String TAG = "KyalioSecureStore";
    private static final String KEYSTORE = "AndroidKeyStore";
    private static final String ALIAS = "kyalio_remember_me_v1";
    private static final String TRANSFORMATION = "AES/GCM/NoPadding";
    private static final int IV_LENGTH = 12;
    private static final int TAG_BITS = 128;

    private SecureStore() { }

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
            Log.w(TAG, "encrypt failed: " + e.getClass().getSimpleName());
            return null;
        }
    }

    public static synchronized String decrypt(String payload) {
        if (payload == null || payload.isEmpty()) return null;
        try {
            byte[] in = Base64.decode(payload, Base64.NO_WRAP);
            if (in.length <= IV_LENGTH) return null;

            SecretKey key = getExistingKey();
            if (key == null) return null;

            Cipher cipher = Cipher.getInstance(TRANSFORMATION);
            cipher.init(Cipher.DECRYPT_MODE, key, new GCMParameterSpec(TAG_BITS, in, 0, IV_LENGTH));
            byte[] pt = cipher.doFinal(in, IV_LENGTH, in.length - IV_LENGTH);
            return new String(pt, StandardCharsets.UTF_8);
        } catch (Exception e) {
            Log.w(TAG, "decrypt failed: " + e.getClass().getSimpleName());
            return null;
        }
    }

    private static SecretKey getExistingKey() throws Exception {
        KeyStore ks = KeyStore.getInstance(KEYSTORE);
        ks.load(null);
        return (SecretKey) ks.getKey(ALIAS, null);
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
        return gen.generateKey();
    }
}
