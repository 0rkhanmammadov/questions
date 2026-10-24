package com.quiz.helper;

import android.app.Activity;
import android.content.ClipData;
import android.content.ClipboardManager;
import android.content.Context;
import android.content.Intent;
import android.graphics.Color;
import android.graphics.PixelFormat;
import android.graphics.drawable.GradientDrawable;
import android.os.Build;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.util.TypedValue;
import android.view.Gravity;
import android.view.View;
import android.view.WindowManager;
import android.widget.TextView;
import android.widget.Toast;

public class ProcessTextActivity extends Activity {

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);

        CharSequence text = getIntent().getCharSequenceExtra(Intent.EXTRA_PROCESS_TEXT);
        if (text == null || text.toString().trim().length() < 3) {
            Toast.makeText(this, "Mətn tapılmadı", Toast.LENGTH_SHORT).show();
            finish();
            return;
        }

        final String question = text.toString().trim();
        showAnswerBanner("◌ ...", 1000);

        AiSolver.solveAsync(question, new AiSolver.Callback() {
            @Override
            public void onResult(final String formattedAnswer) {
                new Handler(Looper.getMainLooper()).post(new Runnable() {
                    @Override
                    public void run() {
                        showAnswerBanner(formattedAnswer, 3200);
                        // Also copy answer to clipboard for convenience
                        try {
                            ClipboardManager cm = (ClipboardManager) getSystemService(Context.CLIPBOARD_SERVICE);
                            if (cm != null) {
                                cm.setPrimaryClip(ClipData.newPlainText("answer", formattedAnswer));
                            }
                        } catch (Exception ignored) {}

                        new Handler(Looper.getMainLooper()).postDelayed(new Runnable() {
                            @Override
                            public void run() {
                                finish();
                            }
                        }, 250);
                    }
                });
            }
        });
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
