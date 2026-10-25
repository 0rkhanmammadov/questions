package com.fp.panel;

import android.accessibilityservice.AccessibilityService;
import android.accessibilityservice.AccessibilityServiceInfo;
import android.accessibilityservice.FingerprintGestureController;
import android.accessibilityservice.FingerprintGestureController.FingerprintGestureCallback;
import android.os.Build;
import android.os.Handler;
import android.os.Looper;
import android.os.VibrationEffect;
import android.os.Vibrator;
import android.view.KeyEvent;
import android.view.accessibility.AccessibilityEvent;
import android.widget.Toast;

public class FingerprintService extends AccessibilityService {

    public static volatile FingerprintService instance;
    public static volatile String lastEventInfo = "Hələ hadisə qeydə alınmayıb";
    private FingerprintGestureController gestureController;
    private FingerprintGestureCallback gestureCallback;
    private Vibrator vibrator;

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

        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            try {
                gestureController = getFingerprintGestureController();
                if (gestureController != null) {
                    gestureCallback = new FingerprintGestureCallback() {
                        public void onGesture(int gesture) {
                            handleGesture(gesture);
                        }

                        public void onGestureDetectionAvailabilityChanged(boolean available) {
                            lastEventInfo = "Jest sensor statusu dəyişdi: " + (available ? "Aktiv" : "Deaktiv");
                        }
                    };
                    gestureController.registerFingerprintGestureCallback(gestureCallback, new Handler(Looper.getMainLooper()));
                }
            } catch (Exception ignored) {}
        }
    }

    private void handleGesture(int gesture) {
        String gestureName = "Bilinməyən (" + gesture + ")";
        if (gesture == FingerprintGestureController.FINGERPRINT_GESTURE_SWIPE_DOWN) {
            gestureName = "AŞAĞI SÜRÜŞDÜRMƏ (Açılır)";
            triggerVibration();
            performGlobalAction(GLOBAL_ACTION_NOTIFICATIONS);
        } else if (gesture == FingerprintGestureController.FINGERPRINT_GESTURE_SWIPE_UP) {
            gestureName = "YUXARI SÜRÜŞDÜRMƏ (Bağlanır)";
            triggerVibration();
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S) {
                performGlobalAction(GLOBAL_ACTION_DISMISS_NOTIFICATION_SHADE);
            } else {
                performGlobalAction(GLOBAL_ACTION_BACK);
            }
        } else if (gesture == FingerprintGestureController.FINGERPRINT_GESTURE_SWIPE_LEFT) {
            gestureName = "SOLA SÜRÜŞDÜRMƏ";
        } else if (gesture == FingerprintGestureController.FINGERPRINT_GESTURE_SWIPE_RIGHT) {
            gestureName = "SAĞA SÜRÜŞDÜRMƏ";
        }

        lastEventInfo = "Son jest: " + gestureName;
        final String msg = "Jest qəbul edildi: " + gestureName;
        new Handler(Looper.getMainLooper()).post(() -> {
            try {
                Toast.makeText(getApplicationContext(), msg, Toast.LENGTH_SHORT).show();
            } catch (Exception ignored) {}
        });
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

    @Override
    public void onAccessibilityEvent(AccessibilityEvent event) {
        instance = this;
    }

    @Override
    public void onInterrupt() {
    }

    @Override
    public void onDestroy() {
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

    public String getDiagnosticStatus() {
        if (gestureController == null) {
            return "✕ Controller mövcud deyil";
        }
        try {
            boolean avail = gestureController.isGestureDetectionAvailable();
            if (avail) {
                return "✓ Sensor sürüşdürmə jestini tam dəstəkləyir";
            } else {
                return "✕ Cihaz / Driver jest dəstəyini bloklayır (isGestureDetectionAvailable = false)";
            }
        } catch (Exception e) {
            return "Xəta: " + e.getMessage();
        }
    }
}
