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
                if (sharedText == null && intent.getClipData() != null && intent.getClipData().getItemCount() > 0) {
                    CharSequence cs = intent.getClipData().getItemAt(0).getText();
                    if (cs != null) sharedText = cs.toString();
                }
                if (sharedText != null) {
                    sharedText = sharedText.replaceAll("(?i)https?://\\S+", "").trim();
                }
                final String cleanText = (sharedText != null) ? sharedText : "";
                captureAndSolve(cleanText);
                return;
            }
        }

        // Default: ACTION_PROCESS_TEXT ("Cavabı Tap" context menu)
        CharSequence text = intent != null ? intent.getCharSequenceExtra(Intent.EXTRA_PROCESS_TEXT) : null;
        final String selectedText = text != null ? text.toString().replaceAll("(?i)https?://\\S+", "").trim() : "";
        captureAndSolve(selectedText);
    }

    private void captureAndSolve(final String textHint) {
        final ScreenReaderService reader = ScreenReaderService.instance;
        if (reader != null && android.os.Build.VERSION.SDK_INT >= android.os.Build.VERSION_CODES.R) {
            // Delay 150ms so Chrome context menu / text selection toolbar dismisses cleanly
            new Handler(Looper.getMainLooper()).postDelayed(() -> {
                reader.captureScreenshot(new ScreenReaderService.ScreenshotCallback() {
                    @Override
                    public void onScreenshot(byte[] jpegBytes) {
                        if (jpegBytes != null && jpegBytes.length > 500) {
                            runOnUiThread(() -> showAnswerBanner("◌ Həll edilir...", 1200));
                            AiSolver.solveImageAsync(jpegBytes, textHint, new AiSolver.Callback() {
                                @Override
                                public void onResult(final String formattedAnswer) {
                                    runOnUiThread(() -> deliverAnswer(formattedAnswer));
                                }
                            });
                        } else if (!textHint.isEmpty()) {
                            processTextQuestion(textHint);
                        } else {
                            runOnUiThread(() -> deliverAnswer("✕ Ekran şəkli alına bilmədi"));
                        }
                    }
                });
            }, 150);
            return;
        }

        if (!textHint.isEmpty()) {
            processTextQuestion(textHint);
            return;
        }

        Toast.makeText(this, "Ekran Oxuyucu (Accessibility) icazəsini aktiv edin", Toast.LENGTH_LONG).show();
        finish();
    }

    private void handleSendImage(Intent intent) {
        Uri imageUri = intent.getParcelableExtra(Intent.EXTRA_STREAM);
        if (imageUri == null && intent.getClipData() != null && intent.getClipData().getItemCount() > 0) {
            imageUri = intent.getClipData().getItemAt(0).getUri();
        }
        if (imageUri == null) {
            imageUri = intent.getData();
        }
        if (imageUri == null) {
            Toast.makeText(this, "Şəkil tapılmadı", Toast.LENGTH_SHORT).show();
            finish();
            return;
        }

        final Uri targetUri = imageUri;
        showAnswerBanner("◌ Şəkil oxunur...", 1500);

        new Thread(() -> {
            try {
                android.graphics.BitmapFactory.Options boundsOpts = new android.graphics.BitmapFactory.Options();
                boundsOpts.inJustDecodeBounds = true;
                InputStream isBounds = getContentResolver().openInputStream(targetUri);
                if (isBounds != null) {
                    android.graphics.BitmapFactory.decodeStream(isBounds, null, boundsOpts);
                    isBounds.close();
                }

                int maxDim = Math.max(boundsOpts.outWidth, boundsOpts.outHeight);
                int sampleSize = 1;
                while (maxDim / sampleSize > 1800) {
                    sampleSize *= 2;
                }

                android.graphics.BitmapFactory.Options decodeOpts = new android.graphics.BitmapFactory.Options();
                decodeOpts.inSampleSize = sampleSize;
                InputStream isDecode = getContentResolver().openInputStream(targetUri);
                android.graphics.Bitmap bmp = android.graphics.BitmapFactory.decodeStream(isDecode, null, decodeOpts);
                if (isDecode != null) isDecode.close();

                if (bmp == null) {
                    runOnUiThread(() -> {
                        Toast.makeText(this, "Şəkil açıla bilmədi", Toast.LENGTH_SHORT).show();
                        finish();
                    });
                    return;
                }

                ByteArrayOutputStream baos = new ByteArrayOutputStream();
                bmp.compress(android.graphics.Bitmap.CompressFormat.JPEG, 85, baos);
                bmp.recycle();
                byte[] jpegBytes = baos.toByteArray();

                AiSolver.solveImageAsync(jpegBytes, "", new AiSolver.Callback() {
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
