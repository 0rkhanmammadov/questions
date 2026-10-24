package com.quiz.helper;

import android.accessibilityservice.AccessibilityService;
import android.view.accessibility.AccessibilityEvent;
import android.view.accessibility.AccessibilityNodeInfo;
import android.view.accessibility.AccessibilityWindowInfo;

import java.util.HashSet;
import java.util.List;
import java.util.Set;

public class ScreenReaderService extends AccessibilityService {

    public static volatile ScreenReaderService instance;

    @Override
    protected void onServiceConnected() {
        super.onServiceConnected();
        instance = this;
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
        if (instance == this) {
            instance = null;
        }
        super.onDestroy();
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
