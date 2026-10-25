package com.quiz.helper;

import android.accessibilityservice.AccessibilityButtonController;
import android.accessibilityservice.AccessibilityService;
import android.accessibilityservice.AccessibilityServiceInfo;
import android.content.ClipData;
import android.content.ClipboardManager;
import android.content.Context;
import android.graphics.Color;
import android.graphics.PixelFormat;
import android.graphics.drawable.GradientDrawable;
import android.os.Build;
import android.os.Handler;
import android.os.Looper;
import android.util.TypedValue;
import android.view.Gravity;
import android.view.KeyEvent;
import android.view.View;
import android.view.WindowManager;
import android.view.accessibility.AccessibilityEvent;
import android.view.accessibility.AccessibilityNodeInfo;
import android.view.accessibility.AccessibilityWindowInfo;
import android.widget.TextView;
import android.widget.Toast;

import java.util.HashSet;
import java.util.List;
import java.util.Set;

public class ScreenReaderService extends AccessibilityService {

    public static volatile ScreenReaderService instance;

    private WindowManager windowManager;
    private TextView overlayView;
    private boolean isOverlayAttached = false;
    private volatile boolean isSolving = false;
    private long lastVolDownTime = 0;

    private final Handler mainHandler = new Handler(Looper.getMainLooper());
    private final Runnable hideOverlayRunnable = new Runnable() {
        @Override
        public void run() {
            hideOverlay();
        }
    };

    private int dp(int v) {
        return (int) TypedValue.applyDimension(
                TypedValue.COMPLEX_UNIT_DIP, v, getResources().getDisplayMetrics());
    }

    @Override
    protected void onServiceConnected() {
        super.onServiceConnected();
        instance = this;

        try {
            AccessibilityServiceInfo info = getServiceInfo();
            if (info != null) {
                info.flags |= AccessibilityServiceInfo.FLAG_REQUEST_ACCESSIBILITY_BUTTON;
                info.flags |= AccessibilityServiceInfo.FLAG_REQUEST_FILTER_KEY_EVENTS;
                info.flags |= AccessibilityServiceInfo.FLAG_RETRIEVE_INTERACTIVE_WINDOWS;
                info.flags |= AccessibilityServiceInfo.FLAG_INCLUDE_NOT_IMPORTANT_VIEWS;
                info.flags |= AccessibilityServiceInfo.FLAG_REPORT_VIEW_IDS;
                setServiceInfo(info);
            }
        } catch (Exception ignored) {}

        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            try {
                AccessibilityButtonController controller = getAccessibilityButtonController();
                if (controller != null) {
                    controller.registerAccessibilityButtonCallback(new AccessibilityButtonController.AccessibilityButtonCallback() {
                        @Override
                        public void onClicked(AccessibilityButtonController controller) {
                            triggerSolveDelayed(100);
                        }

                        @Override
                        public void onAvailabilityChanged(AccessibilityButtonController controller, boolean available) {
                        }
                    });
                }
            } catch (Exception ignored) {}
        }
    }

    @Override
    protected boolean onKeyEvent(KeyEvent event) {
        if (event.getKeyCode() == KeyEvent.KEYCODE_VOLUME_DOWN
                && event.getAction() == KeyEvent.ACTION_DOWN
                && event.getRepeatCount() == 0) {
            long now = System.currentTimeMillis();
            if (now - lastVolDownTime < 450) {
                lastVolDownTime = 0;
                triggerSolveDelayed(50);
                return true;
            }
            lastVolDownTime = now;
        }
        return super.onKeyEvent(event);
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
        hideOverlay();
        if (instance == this) {
            instance = null;
        }
        super.onDestroy();
    }

    public void triggerSolveDelayed(long delayMs) {
        mainHandler.postDelayed(new Runnable() {
            @Override
            public void run() {
                triggerSolve();
            }
        }, delayMs);
    }

    public void triggerSolve() {
        if (isSolving) return;
        isSolving = true;

        final String screenText = readCurrentScreenText();

        captureScreenshot(new ScreenshotCallback() {
            @Override
            public void onScreenshot(final byte[] jpegBytes) {
                showOverlay("◌ ...", 0);

                if (jpegBytes != null && jpegBytes.length > 500) {
                    AiSolver.solveImageAsync(jpegBytes, screenText, new AiSolver.Callback() {
                        @Override
                        public void onResult(final String formattedAnswer) {
                            isSolving = false;
                            deliverResult(formattedAnswer);
                        }
                    });
                } else if (screenText != null && screenText.length() >= 5) {
                    AiSolver.solveAsync(screenText, new AiSolver.Callback() {
                        @Override
                        public void onResult(final String formattedAnswer) {
                            isSolving = false;
                            deliverResult(formattedAnswer);
                        }
                    });
                } else {
                    isSolving = false;
                    deliverResult("✕ Ekran oxunmadı");
                }
            }
        });
    }

    private void deliverResult(final String formattedAnswer) {
        showOverlay(formattedAnswer, 4000);
        try {
            ClipboardManager cm = (ClipboardManager) getSystemService(Context.CLIPBOARD_SERVICE);
            if (cm != null) {
                cm.setPrimaryClip(ClipData.newPlainText("answer", formattedAnswer));
            }
        } catch (Exception ignored) {}
    }

    private void showOverlay(final String text, final int autoHideMs) {
        mainHandler.post(new Runnable() {
            @Override
            public void run() {
                try {
                    if (windowManager == null) {
                        windowManager = (WindowManager) getSystemService(WINDOW_SERVICE);
                    }
                    if (overlayView == null) {
                        overlayView = new TextView(ScreenReaderService.this);
                        overlayView.setTextColor(Color.parseColor("#4ADE80"));
                        overlayView.setTextSize(TypedValue.COMPLEX_UNIT_SP, 15);
                        overlayView.setPadding(dp(16), dp(10), dp(16), dp(10));
                        GradientDrawable bg = new GradientDrawable();
                        bg.setColor(Color.parseColor("#EE141417"));
                        bg.setCornerRadius(dp(10));
                        bg.setStroke(dp(1), Color.parseColor("#993F3F46"));
                        overlayView.setBackground(bg);
                        overlayView.setGravity(Gravity.CENTER);

                        overlayView.setOnClickListener(new View.OnClickListener() {
                            @Override
                            public void onClick(View v) {
                                hideOverlay();
                            }
                        });

                        WindowManager.LayoutParams lp = new WindowManager.LayoutParams(
                                WindowManager.LayoutParams.WRAP_CONTENT,
                                WindowManager.LayoutParams.WRAP_CONTENT,
                                WindowManager.LayoutParams.TYPE_ACCESSIBILITY_OVERLAY,
                                WindowManager.LayoutParams.FLAG_NOT_FOCUSABLE
                                        | WindowManager.LayoutParams.FLAG_NOT_TOUCH_MODAL
                                        | WindowManager.LayoutParams.FLAG_LAYOUT_IN_SCREEN,
                                PixelFormat.TRANSLUCENT
                        );
                        lp.gravity = Gravity.TOP | Gravity.CENTER_HORIZONTAL;
                        lp.y = dp(56);
                        windowManager.addView(overlayView, lp);
                        isOverlayAttached = true;
                    }
                    overlayView.setText(text);
                    overlayView.setVisibility(View.VISIBLE);

                    mainHandler.removeCallbacks(hideOverlayRunnable);
                    if (autoHideMs > 0) {
                        mainHandler.postDelayed(hideOverlayRunnable, autoHideMs);
                    }
                } catch (Exception e) {
                    try {
                        Toast.makeText(getApplicationContext(), text, Toast.LENGTH_SHORT).show();
                    } catch (Exception ignored) {}
                }
            }
        });
    }

    private void hideOverlay() {
        mainHandler.post(new Runnable() {
            @Override
            public void run() {
                if (overlayView != null && isOverlayAttached) {
                    try {
                        overlayView.setVisibility(View.GONE);
                        windowManager.removeView(overlayView);
                    } catch (Exception ignored) {}
                    overlayView = null;
                    isOverlayAttached = false;
                }
            }
        });
    }

    public interface ScreenshotCallback {
        void onScreenshot(byte[] jpegBytes);
    }

    public void captureScreenshot(final ScreenshotCallback cb) {
        if (android.os.Build.VERSION.SDK_INT >= android.os.Build.VERSION_CODES.R) {
            try {
                takeScreenshot(android.view.Display.DEFAULT_DISPLAY, getMainExecutor(), new TakeScreenshotCallback() {
                    @Override
                    public void onSuccess(ScreenshotResult screenshot) {
                        android.hardware.HardwareBuffer hb = null;
                        try {
                            hb = screenshot.getHardwareBuffer();
                            android.graphics.Bitmap hwBitmap = android.graphics.Bitmap.wrapHardwareBuffer(
                                    hb, screenshot.getColorSpace());
                            if (hwBitmap != null) {
                                android.graphics.Bitmap swBitmap = hwBitmap.copy(android.graphics.Bitmap.Config.ARGB_8888, false);
                                if (swBitmap != null) {
                                    java.io.ByteArrayOutputStream baos = new java.io.ByteArrayOutputStream();
                                    swBitmap.compress(android.graphics.Bitmap.CompressFormat.JPEG, 80, baos);
                                    swBitmap.recycle();
                                    hwBitmap.recycle();
                                    if (hb != null) hb.close();
                                    cb.onScreenshot(baos.toByteArray());
                                    return;
                                }
                                hwBitmap.recycle();
                            }
                        } catch (Exception ignored) {}
                        finally {
                            if (hb != null) {
                                try { hb.close(); } catch (Exception ignored) {}
                            }
                        }
                        cb.onScreenshot(null);
                    }

                    @Override
                    public void onFailure(int errorCode) {
                        cb.onScreenshot(null);
                    }
                });
                return;
            } catch (Exception ignored) {}
        }
        cb.onScreenshot(null);
    }

    public String readCurrentScreenText() {
        StringBuilder sb = new StringBuilder();
        Set<String> seen = new HashSet<>();
        try {
            List<AccessibilityWindowInfo> windows = getWindows();
            if (windows != null && !windows.isEmpty()) {
                for (AccessibilityWindowInfo win : windows) {
                    AccessibilityNodeInfo root = win.getRoot();
                    if (root != null) {
                        CharSequence pkg = root.getPackageName();
                        if (pkg == null || !"com.quiz.helper".equals(pkg.toString())) {
                            collectText(root, sb, seen);
                        }
                    }
                }
            }
            if (sb.length() < 8) {
                AccessibilityNodeInfo activeRoot = getRootInActiveWindow();
                if (activeRoot != null) {
                    collectText(activeRoot, sb, seen);
                }
            }
        } catch (Exception ignored) {
        }
        return sb.toString().trim();
    }

    private void collectText(AccessibilityNodeInfo node, StringBuilder sb, Set<String> seen) {
        if (node == null) return;
        try {
            if (node.isVisibleToUser()) {
                CharSequence txt = node.getText();
                if (txt != null) {
                    String s = txt.toString().trim();
                    if (s.length() >= 2 && !s.startsWith("http://") && !s.startsWith("https://") && !seen.contains(s)) {
                        seen.add(s);
                        sb.append(s).append("\n");
                    }
                } else {
                    CharSequence desc = node.getContentDescription();
                    if (desc != null) {
                        String s = desc.toString().trim();
                        if (s.length() >= 3 && s.length() <= 500 && !seen.contains(s)) {
                            seen.add(s);
                            sb.append(s).append("\n");
                        }
                    }
                }
            }
            int count = node.getChildCount();
            for (int i = 0; i < count; i++) {
                AccessibilityNodeInfo child = node.getChild(i);
                if (child != null) {
                    collectText(child, sb, seen);
                }
            }
        } catch (Exception ignored) {
        }
    }
}
