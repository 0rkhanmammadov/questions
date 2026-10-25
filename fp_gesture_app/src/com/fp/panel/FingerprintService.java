package com.fp.panel;

import android.accessibilityservice.AccessibilityService;
import android.accessibilityservice.AccessibilityServiceInfo;
import android.accessibilityservice.FingerprintGestureController;
import android.accessibilityservice.FingerprintGestureController.FingerprintGestureCallback;
import android.content.SharedPreferences;
import android.graphics.Color;
import android.graphics.PixelFormat;
import android.graphics.drawable.GradientDrawable;
import android.os.Build;
import android.os.Handler;
import android.os.Looper;
import android.os.VibrationEffect;
import android.os.Vibrator;
import android.util.TypedValue;
import android.view.Gravity;
import android.view.KeyEvent;
import android.view.MotionEvent;
import android.view.View;
import android.view.WindowManager;
import android.view.accessibility.AccessibilityEvent;
import android.widget.Toast;

public class FingerprintService extends AccessibilityService {

    public static volatile FingerprintService instance;
    public static volatile String lastEventInfo = "Hələ hadisə qeydə alınmayıb";

    private WindowManager windowManager;
    private View edgeSwipeView;
    private FingerprintGestureController gestureController;
    private FingerprintGestureCallback gestureCallback;
    private Vibrator vibrator;

    private float downY = 0;
    private boolean hasTriggered = false;
    private long lastTapTime = 0;

    private int dp(int v) {
        return (int) TypedValue.applyDimension(
                TypedValue.COMPLEX_UNIT_DIP, v, getResources().getDisplayMetrics());
    }

    @Override
    protected void onServiceConnected() {
        super.onServiceConnected();
        instance = this;
        vibrator = (Vibrator) getSystemService(VIBRATOR_SERVICE);

        try {
            AccessibilityServiceInfo info = getServiceInfo();
            if (info != null) {
                info.flags |= AccessibilityServiceInfo.FLAG_REQUEST_FINGERPRINT_GESTURES;
                info.flags |= AccessibilityServiceInfo.FLAG_REQUEST_FILTER_KEY_EVENTS;
                setServiceInfo(info);
            }
        } catch (Exception ignored) {}

        // Try standard AOSP Fingerprint Gesture if supported
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            try {
                gestureController = getFingerprintGestureController();
                if (gestureController != null) {
                    gestureCallback = new FingerprintGestureCallback() {
                        public void onGesture(int gesture) {
                            handleHardwareGesture(gesture);
                        }

                        public void onGestureDetectionAvailabilityChanged(boolean available) {
                        }
                    };
                    gestureController.registerFingerprintGestureCallback(gestureCallback, new Handler(Looper.getMainLooper()));
                }
            } catch (Exception ignored) {}
        }

        // Setup Edge Touch Detector right beside the lock button
        setupEdgeSwipeDetector();
    }

    private void setupEdgeSwipeDetector() {
        new Handler(Looper.getMainLooper()).post(new Runnable() {
            @Override
            public void run() {
                try {
                    if (windowManager == null) {
                        windowManager = (WindowManager) getSystemService(WINDOW_SERVICE);
                    }
                    if (edgeSwipeView != null) {
                        try { windowManager.removeView(edgeSwipeView); } catch (Exception ignored) {}
                        edgeSwipeView = null;
                    }

                    edgeSwipeView = new View(FingerprintService.this);
                    
                    SharedPreferences prefs = getSharedPreferences("fp_prefs", MODE_PRIVATE);
                    boolean showGuide = prefs.getBoolean("show_edge_guide", true);

                    if (showGuide) {
                        GradientDrawable gd = new GradientDrawable();
                        gd.setColor(Color.parseColor("#4438BDF8")); // Soft translucent blue guide
                        gd.setCornerRadii(new float[]{dp(8), 0, 0, dp(8), dp(8), 0, 0, dp(8)});
                        edgeSwipeView.setBackground(gd);
                    } else {
                        edgeSwipeView.setBackgroundColor(Color.TRANSPARENT);
                    }

                    edgeSwipeView.setOnTouchListener(new View.OnTouchListener() {
                        @Override
                        public boolean onTouch(View v, MotionEvent event) {
                            switch (event.getAction()) {
                                case MotionEvent.ACTION_DOWN:
                                    downY = event.getRawY();
                                    hasTriggered = false;
                                    return true;

                                case MotionEvent.ACTION_MOVE:
                                    if (!hasTriggered) {
                                        float dy = event.getRawY() - downY;
                                        if (dy > dp(25)) {
                                            hasTriggered = true;
                                            onSwipeDownDetected("Yan Zolaq Sürüşdürmə ↓");
                                            return true;
                                        } else if (dy < -dp(25)) {
                                            hasTriggered = true;
                                            onSwipeUpDetected("Yan Zolaq Sürüşdürmə ↑");
                                            return true;
                                        }
                                    }
                                    return true;

                                case MotionEvent.ACTION_UP:
                                    if (!hasTriggered) {
                                        long now = System.currentTimeMillis();
                                        if (now - lastTapTime < 380) {
                                            lastTapTime = 0;
                                            onSwipeDownDetected("Yan Zolaq 2 Toxunuş");
                                        } else {
                                            lastTapTime = now;
                                        }
                                    }
                                    return true;
                            }
                            return false;
                        }
                    });

                    WindowManager.LayoutParams lp = new WindowManager.LayoutParams(
                            dp(22), // 22dp width on the screen edge
                            dp(260), // centered around side power button
                            WindowManager.LayoutParams.TYPE_ACCESSIBILITY_OVERLAY,
                            WindowManager.LayoutParams.FLAG_NOT_FOCUSABLE
                                    | WindowManager.LayoutParams.FLAG_LAYOUT_IN_SCREEN,
                            PixelFormat.TRANSLUCENT
                    );
                    lp.gravity = Gravity.END | Gravity.CENTER_VERTICAL;

                    windowManager.addView(edgeSwipeView, lp);
                } catch (Exception ignored) {}
            }
        });
    }

    public void updateEdgeAppearance(final boolean showGuide) {
        new Handler(Looper.getMainLooper()).post(new Runnable() {
            @Override
            public void run() {
                if (edgeSwipeView != null) {
                    if (showGuide) {
                        GradientDrawable gd = new GradientDrawable();
                        gd.setColor(Color.parseColor("#4438BDF8"));
                        gd.setCornerRadii(new float[]{dp(8), 0, 0, dp(8), dp(8), 0, 0, dp(8)});
                        edgeSwipeView.setBackground(gd);
                    } else {
                        edgeSwipeView.setBackgroundColor(Color.TRANSPARENT);
                    }
                } else {
                    setupEdgeSwipeDetector();
                }
            }
        });
    }

    private void onSwipeDownDetected(String source) {
        triggerVibration();
        performGlobalAction(GLOBAL_ACTION_NOTIFICATIONS);
        lastEventInfo = source + " -> Bildiriş açıldı";
        showToast("↓ Bildiriş açıldı (" + source + ")");
    }

    private void onSwipeUpDetected(String source) {
        triggerVibration();
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S) {
            performGlobalAction(GLOBAL_ACTION_DISMISS_NOTIFICATION_SHADE);
        } else {
            performGlobalAction(GLOBAL_ACTION_BACK);
        }
        lastEventInfo = source + " -> Bildiriş bağlandı";
        showToast("↑ Bildiriş bağlandı (" + source + ")");
    }

    private void handleHardwareGesture(int gesture) {
        if (gesture == FingerprintGestureController.FINGERPRINT_GESTURE_SWIPE_DOWN) {
            onSwipeDownDetected("Hardware Sensor ↓");
        } else if (gesture == FingerprintGestureController.FINGERPRINT_GESTURE_SWIPE_UP) {
            onSwipeUpDetected("Hardware Sensor ↑");
        }
    }

    @Override
    protected boolean onKeyEvent(KeyEvent event) {
        if (event.getAction() == KeyEvent.ACTION_DOWN) {
            lastEventInfo = "Düymə sıxıldı: KeyCode " + event.getKeyCode();
        }
        return super.onKeyEvent(event);
    }

    private void triggerVibration() {
        try {
            if (vibrator != null && vibrator.hasVibrator()) {
                if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
                    vibrator.vibrate(VibrationEffect.createOneShot(25, VibrationEffect.DEFAULT_AMPLITUDE));
                } else {
                    vibrator.vibrate(25);
                }
            }
        } catch (Exception ignored) {}
    }

    private void showToast(final String msg) {
        new Handler(Looper.getMainLooper()).post(() -> {
            try {
                Toast.makeText(getApplicationContext(), msg, Toast.LENGTH_SHORT).show();
            } catch (Exception ignored) {}
        });
    }

    @Override
    public void onAccessibilityEvent(AccessibilityEvent event) {
        instance = this;
    }

    @Override
    public void onInterrupt() {
    }

    @Override
    public void onDestroy() {
        if (edgeSwipeView != null && windowManager != null) {
            try { windowManager.removeView(edgeSwipeView); } catch (Exception ignored) {}
            edgeSwipeView = null;
        }
        if (gestureController != null && gestureCallback != null) {
            try {
                gestureController.unregisterFingerprintGestureCallback(gestureCallback);
            } catch (Exception ignored) {}
        }
        if (instance == this) {
            instance = null;
        }
        super.onDestroy();
    }
}
