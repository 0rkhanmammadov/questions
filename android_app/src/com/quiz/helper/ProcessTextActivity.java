package com.quiz.helper;

import android.app.Activity;
import android.content.ClipData;
import android.content.ClipboardManager;
import android.content.Context;
import android.content.Intent;
import android.net.Uri;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.view.Gravity;
import android.widget.Toast;

import java.io.ByteArrayOutputStream;
import java.io.InputStream;

public class ProcessTextActivity extends Activity {

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);

        Intent intent = getIntent();
        String action = intent != null ? intent.getAction() : null;

        if (Intent.ACTION_SEND.equals(action)) {
            String type = intent.getType();
            if (type != null && type.startsWith("image/")) {
                handleSendImage(intent);
                return;
            } else if ("text/plain".equals(type)) {
                String sharedText = intent.getStringExtra(Intent.EXTRA_TEXT);
                if (sharedText != null && sharedText.trim().length() >= 3) {
                    processTextQuestion(sharedText.trim());
                    return;
                }
            }
        }

        // Default: ACTION_PROCESS_TEXT
        CharSequence text = intent != null ? intent.getCharSequenceExtra(Intent.EXTRA_PROCESS_TEXT) : null;
        if (text != null && text.toString().trim().length() >= 3) {
            processTextQuestion(text.toString().trim());
            return;
        }

        Toast.makeText(this, "Mətn və ya şəkil tapılmadı", Toast.LENGTH_SHORT).show();
        finish();
    }

    private void handleSendImage(Intent intent) {
        Uri imageUri = intent.getParcelableExtra(Intent.EXTRA_STREAM);
        if (imageUri == null) {
            Toast.makeText(this, "Şəkil tapılmadı", Toast.LENGTH_SHORT).show();
            finish();
            return;
        }

        showAnswerBanner("◌ Şəkil oxunur...", 1500);

        new Thread(() -> {
            try {
                InputStream is = getContentResolver().openInputStream(imageUri);
                if (is == null) {
                    runOnUiThread(() -> {
                        Toast.makeText(this, "Şəkil açıla bilmədi", Toast.LENGTH_SHORT).show();
                        finish();
                    });
                    return;
                }
                ByteArrayOutputStream baos = new ByteArrayOutputStream();
                byte[] buffer = new byte[16384];
                int len;
                while ((len = is.read(buffer)) != -1) {
                    baos.write(buffer, 0, len);
                }
                is.close();
                byte[] bytes = baos.toByteArray();

                AiSolver.solveImageAsync(bytes, new AiSolver.Callback() {
                    @Override
                    public void onResult(final String formattedAnswer) {
                        runOnUiThread(() -> deliverAnswer(formattedAnswer));
                    }
                });
            } catch (Exception e) {
                runOnUiThread(() -> {
                    Toast.makeText(this, "✕ Şəkil xətası", Toast.LENGTH_SHORT).show();
                    finish();
                });
            }
        }).start();
    }

    private void processTextQuestion(String question) {
        showAnswerBanner("◌ ...", 1000);
        AiSolver.solveAsync(question, new AiSolver.Callback() {
            @Override
            public void onResult(final String formattedAnswer) {
                new Handler(Looper.getMainLooper()).post(() -> deliverAnswer(formattedAnswer));
            }
        });
    }

    private void deliverAnswer(String formattedAnswer) {
        showAnswerBanner(formattedAnswer, 3200);
        try {
            ClipboardManager cm = (ClipboardManager) getSystemService(Context.CLIPBOARD_SERVICE);
            if (cm != null) {
                cm.setPrimaryClip(ClipData.newPlainText("answer", formattedAnswer));
            }
        } catch (Exception ignored) {}

        new Handler(Looper.getMainLooper()).postDelayed(this::finish, 350);
    }

    private void showAnswerBanner(String text, int durationMs) {
        try {
            Toast toast = Toast.makeText(getApplicationContext(), text, Toast.LENGTH_SHORT);
            toast.setGravity(Gravity.CENTER, 0, 0);
            toast.show();
        } catch (Exception ignored) {
        }
    }
}
