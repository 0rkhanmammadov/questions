package com.quiz.helper;

import android.app.Notification;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.app.Service;
import android.content.ClipData;
import android.content.ClipboardManager;
import android.content.Context;
import android.content.Intent;
import android.graphics.Color;
import android.graphics.PixelFormat;
import android.graphics.drawable.GradientDrawable;
import android.os.Build;
import android.os.Handler;
import android.os.IBinder;
import android.os.Looper;
import android.provider.Settings;
import android.util.TypedValue;
import android.view.Gravity;
import android.view.MotionEvent;
import android.view.View;
import android.view.WindowManager;
import android.widget.LinearLayout;
import android.widget.TextView;

public class FloatingOverlayService extends Service {

    private WindowManager windowManager;
    private LinearLayout rootLayout;
    private TextView answerBox;
    private TextView dotButton;
    private final Handler mainHandler = new Handler(Looper.getMainLooper());
    private String lastAnswerText = "● Hazır";
    private boolean isHolding = false;

    private final Runnable hideRunnable = new Runnable() {
        @Override
        public void run() {
            if (!isHolding && answerBox != null) {
                answerBox.setVisibility(View.GONE);
            }
        }
    };

    @Override
    public IBinder onBind(Intent intent) {
        return null;
    }

    @Override
    public void onCreate() {
        super.onCreate();
        startAsForeground();
        createOverlay();
    }

    private void startAsForeground() {
        try {
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
                String channelId = "quiz_overlay_channel";
                NotificationChannel channel = new NotificationChannel(
                        channelId, "Köməkçi Xidməti", NotificationManager.IMPORTANCE_LOW);
                channel.setDescription("Üzən düymə xidməti");
                NotificationManager nm = getSystemService(NotificationManager.class);
                if (nm != null) {
                    nm.createNotificationChannel(channel);
                }

                Intent openIntent = new Intent(this, MainActivity.class);
                PendingIntent pi = PendingIntent.getActivity(
                        this, 0, openIntent,
                        Build.VERSION.SDK_INT >= Build.VERSION_CODES.M ? PendingIntent.FLAG_IMMUTABLE : 0);

                Notification notif = new Notification.Builder(this, channelId)
                        .setContentTitle("Sistem Köməkçisi Aktivdir")
                        .setContentText("Üzən düymə aktivdir (bağlamaq üçün toxunun)")
                        .setSmallIcon(android.R.drawable.ic_dialog_info)
                        .setContentIntent(pi)
                        .setOngoing(true)
                        .build();

                startForeground(101, notif);
            }
        } catch (Exception ignored) {
        }
    }

    private int dp(int v) {
        return (int) TypedValue.applyDimension(
                TypedValue.COMPLEX_UNIT_DIP, v, getResources().getDisplayMetrics());
    }

    private void createOverlay() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.M && !Settings.canDrawOverlays(this)) {
            return;
        }
        windowManager = (WindowManager) getSystemService(WINDOW_SERVICE);
        if (rootLayout != null) {
            try { windowManager.removeView(rootLayout); } catch (Exception ignored) {}
        }

        rootLayout = new LinearLayout(this);
        rootLayout.setOrientation(LinearLayout.HORIZONTAL);
        rootLayout.setGravity(Gravity.CENTER_VERTICAL);

        answerBox = new TextView(this);
        answerBox.setTextColor(Color.parseColor("#4ADE80"));
        answerBox.setTextSize(TypedValue.COMPLEX_UNIT_SP, 13);
        answerBox.setPadding(dp(10), dp(6), dp(10), dp(6));
        GradientDrawable boxBg = new GradientDrawable();
        boxBg.setColor(Color.parseColor("#EE141417"));
        boxBg.setCornerRadius(dp(8));
        boxBg.setStroke(dp(1), Color.parseColor("#993F3F46"));
        answerBox.setBackground(boxBg);
        answerBox.setVisibility(View.GONE);

        LinearLayout.LayoutParams boxLp = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.WRAP_CONTENT,
                LinearLayout.LayoutParams.WRAP_CONTENT
        );
        boxLp.setMarginEnd(dp(6));
        rootLayout.addView(answerBox, boxLp);

        dotButton = new TextView(this);
        dotButton.setText("•");
        dotButton.setTextColor(Color.WHITE);
        dotButton.setTextSize(TypedValue.COMPLEX_UNIT_SP, 22);
        dotButton.setGravity(Gravity.CENTER);
        GradientDrawable dotBg = new GradientDrawable();
        dotBg.setShape(GradientDrawable.OVAL);
        dotBg.setColor(Color.parseColor("#CC2563EB"));
        dotBg.setStroke(dp(1), Color.WHITE);
        dotButton.setBackground(dotBg);

        LinearLayout.LayoutParams dotLp = new LinearLayout.LayoutParams(dp(44), dp(44));
        rootLayout.addView(dotButton, dotLp);

        int layoutType = Build.VERSION.SDK_INT >= Build.VERSION_CODES.O
                ? WindowManager.LayoutParams.TYPE_APPLICATION_OVERLAY
                : WindowManager.LayoutParams.TYPE_PHONE;

        final WindowManager.LayoutParams params = new WindowManager.LayoutParams(
                WindowManager.LayoutParams.WRAP_CONTENT,
                WindowManager.LayoutParams.WRAP_CONTENT,
                layoutType,
                WindowManager.LayoutParams.FLAG_NOT_FOCUSABLE
                        | WindowManager.LayoutParams.FLAG_NOT_TOUCH_MODAL
                        | WindowManager.LayoutParams.FLAG_LAYOUT_IN_SCREEN,
                PixelFormat.TRANSLUCENT
        );
        params.gravity = Gravity.CENTER_VERTICAL | Gravity.END;
        params.x = dp(16);
        params.y = 0;

        setupTouchListeners(params);

        try {
            windowManager.addView(rootLayout, params);
            showTempMessage("● Aktivdir (Toxun = Həll et)", 2400);
        } catch (Exception ignored) {
        }
    }

    private void setupTouchListeners(final WindowManager.LayoutParams params) {
        dotButton.setOnTouchListener(new View.OnTouchListener() {
            private int initialX, initialY;
            private float initialTouchX, initialTouchY;
            private boolean moved = false;
            private boolean longPressFired = false;

            private final Runnable holdCheck = new Runnable() {
                @Override
                public void run() {
                    if (!moved) {
                        longPressFired = true;
                        isHolding = true;
                        answerBox.setText(lastAnswerText);
                        answerBox.setVisibility(View.VISIBLE);
                    }
                }
            };

            @Override
            public boolean onTouch(View v, MotionEvent event) {
                switch (event.getActionMasked()) {
                    case MotionEvent.ACTION_DOWN:
                        initialX = params.x;
                        initialY = params.y;
                        initialTouchX = event.getRawX();
                        initialTouchY = event.getRawY();
                        moved = false;
                        longPressFired = false;
                        mainHandler.postDelayed(holdCheck, 260);
                        return true;

                    case MotionEvent.ACTION_MOVE:
                        int dx = (int) (initialTouchX - event.getRawX());
                        int dy = (int) (initialTouchY - event.getRawY());
                        if (Math.abs(dx) > dp(8) || Math.abs(dy) > dp(8)) {
                            moved = true;
                            mainHandler.removeCallbacks(holdCheck);
                            params.x = initialX + dx;
                            params.y = initialY + dy;
                            try {
                                windowManager.updateViewLayout(rootLayout, params);
                            } catch (Exception ignored) {}
                        }
                        return true;

                    case MotionEvent.ACTION_UP:
                    case MotionEvent.ACTION_CANCEL:
                        mainHandler.removeCallbacks(holdCheck);
                        if (isHolding) {
                            isHolding = false;
                            answerBox.setVisibility(View.GONE);
                        } else if (!moved && !longPressFired) {
                            triggerScreenOrClipboardSolve();
                        }
                        return true;
                }
                return false;
            }
        });
    }

    private void triggerScreenOrClipboardSolve() {
        String questionText = "";
        ScreenReaderService reader = ScreenReaderService.instance;
        if (reader != null) {
            questionText = reader.readCurrentScreenText();
        }
        if (questionText == null || questionText.trim().length() < 6) {
            try {
                ClipboardManager cm = (ClipboardManager) getSystemService(Context.CLIPBOARD_SERVICE);
                if (cm != null && cm.hasPrimaryClip()) {
                    ClipData cd = cm.getPrimaryClip();
                    if (cd != null && cd.getItemCount() > 0 && cd.getItemAt(0).getText() != null) {
                        questionText = cd.getItemAt(0).getText().toString().trim();
                    }
                }
            } catch (Exception ignored) {}
        }

        if (questionText == null || questionText.trim().length() < 4) {
            showTempMessage("✕ Ekran oxuma icazəsini aç və ya mətni kopyala", 2400);
            return;
        }

        dotButton.setTextColor(Color.parseColor("#F59E0B"));
        showTempMessage("◌ ...", 1200);

        AiSolver.solveAsync(questionText, new AiSolver.Callback() {
            @Override
            public void onResult(final String formattedAnswer) {
                mainHandler.post(new Runnable() {
                    @Override
                    public void run() {
                        dotButton.setTextColor(Color.parseColor("#4ADE80"));
                        showTempMessage(formattedAnswer, 2600);
                    }
                });
            }
        });
    }

    private void showTempMessage(String msg, long durationMs) {
        lastAnswerText = msg;
        answerBox.setText(msg);
        answerBox.setVisibility(View.VISIBLE);
        mainHandler.removeCallbacks(hideRunnable);
        if (durationMs > 0 && !isHolding) {
            mainHandler.postDelayed(hideRunnable, durationMs);
        }
    }

    @Override
    public void onDestroy() {
        super.onDestroy();
        if (rootLayout != null && windowManager != null) {
            try {
                windowManager.removeView(rootLayout);
            } catch (Exception ignored) {}
        }
    }
}
