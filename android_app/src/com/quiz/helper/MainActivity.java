package com.quiz.helper;

import android.app.Activity;
import android.content.Intent;
import android.graphics.Color;
import android.os.Bundle;
import android.provider.Settings;
import android.util.TypedValue;
import android.view.Gravity;
import android.view.View;
import android.widget.FrameLayout;
import android.widget.Toast;

public class MainActivity extends Activity {

    private int dp(int v) {
        return (int) TypedValue.applyDimension(
                TypedValue.COMPLEX_UNIT_DIP, v, getResources().getDisplayMetrics());
    }

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);

        FrameLayout root = new FrameLayout(this);
        root.setBackgroundColor(Color.parseColor("#121215"));

        // Bottom invisible button for accessibility service activation
        View stealthBtn = new View(this);
        stealthBtn.setBackgroundColor(Color.TRANSPARENT);
        stealthBtn.setClickable(true);
        stealthBtn.setFocusable(true);

        stealthBtn.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View v) {
                openAccessibility();
            }
        });

        stealthBtn.setOnLongClickListener(new View.OnLongClickListener() {
            @Override
            public boolean onLongClick(View v) {
                openAccessibilityDirect();
                return true;
            }
        });

        FrameLayout.LayoutParams lp = new FrameLayout.LayoutParams(
                FrameLayout.LayoutParams.MATCH_PARENT,
                dp(100)
        );
        lp.gravity = Gravity.BOTTOM;
        root.addView(stealthBtn, lp);

        setContentView(root);
    }

    private void openAccessibility() {
        if (ScreenReaderService.instance != null) {
            Toast.makeText(this, "Maps aktivdir", Toast.LENGTH_SHORT).show();
            return;
        }
        openAccessibilityDirect();
    }

    private void openAccessibilityDirect() {
        try {
            Intent intent = new Intent(Settings.ACTION_ACCESSIBILITY_SETTINGS);
            startActivity(intent);
        } catch (Exception e) {
            Toast.makeText(this, "Parametrlər açıla bilmədi", Toast.LENGTH_SHORT).show();
        }
    }
}
