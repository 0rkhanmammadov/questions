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
import android.view.accessibility.AccessibilityEvent;

public class FingerprintService extends AccessibilityService {

    public static volatile FingerprintService instance;
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
                        }
                    };
                    gestureController.registerFingerprintGestureCallback(gestureCallback, new Handler(Looper.getMainLooper()));
                }
            } catch (Exception ignored) {}
        }
    }

    private void handleGesture(int gesture) {
        if (gesture == FingerprintGestureController.FINGERPRINT_GESTURE_SWIPE_DOWN) {
            triggerVibration();
            performGlobalAction(GLOBAL_ACTION_NOTIFICATIONS);
        } else if (gesture == FingerprintGestureController.FINGERPRINT_GESTURE_SWIPE_UP) {
            triggerVibration();
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S) {
                performGlobalAction(GLOBAL_ACTION_DISMISS_NOTIFICATION_SHADE);
            } else {
                performGlobalAction(GLOBAL_ACTION_BACK);
            }
        }
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

    public boolean isHardwareSupported() {
        if (gestureController != null) {
            try {
                return gestureController.isGestureDetectionAvailable();
            } catch (Exception ignored) {}
        }
        return false;
    }
}
