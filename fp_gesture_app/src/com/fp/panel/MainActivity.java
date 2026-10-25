package com.fp.panel;

import android.app.Activity;
import android.content.Intent;
import android.graphics.Color;
import android.graphics.drawable.GradientDrawable;
import android.os.Bundle;
import android.provider.Settings;
import android.util.TypedValue;
import android.view.Gravity;
import android.view.View;
import android.widget.Button;
import android.widget.LinearLayout;
import android.widget.ScrollView;
import android.widget.TextView;
import android.widget.Toast;

public class MainActivity extends Activity {

    private TextView statusText;
    private Button actionBtn;

    private int dp(int v) {
        return (int) TypedValue.applyDimension(
                TypedValue.COMPLEX_UNIT_DIP, v, getResources().getDisplayMetrics());
    }

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);

        ScrollView scroll = new ScrollView(this);
        scroll.setBackgroundColor(Color.parseColor("#0F172A"));

        LinearLayout root = new LinearLayout(this);
        root.setOrientation(LinearLayout.VERTICAL);
        root.setPadding(dp(20), dp(36), dp(20), dp(36));
        scroll.addView(root);

        // Header
        TextView title = new TextView(this);
        title.setText("Barmaq İzi Paneli");
        title.setTextColor(Color.WHITE);
        title.setTextSize(TypedValue.COMPLEX_UNIT_SP, 22);
        title.setTypeface(null, android.graphics.Typeface.BOLD);
        root.addView(title);

        TextView subtitle = new TextView(this);
        subtitle.setText("Sensor jesti ilə bildiriş pərdəsini idarə edin");
        subtitle.setTextColor(Color.parseColor("#94A3B8"));
        subtitle.setTextSize(TypedValue.COMPLEX_UNIT_SP, 13);
        LinearLayout.LayoutParams subLp = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT);
        subLp.topMargin = dp(4);
        subLp.bottomMargin = dp(24);
        root.addView(subtitle, subLp);

        // Card 1: Features
        LinearLayout cardFeatures = new LinearLayout(this);
        cardFeatures.setOrientation(LinearLayout.VERTICAL);
        cardFeatures.setPadding(dp(16), dp(16), dp(16), dp(16));
        GradientDrawable cardBg = new GradientDrawable();
        cardBg.setColor(Color.parseColor("#1E293B"));
        cardBg.setCornerRadius(dp(12));
        cardBg.setStroke(dp(1), Color.parseColor("#334155"));
        cardFeatures.setBackground(cardBg);

        TextView f1 = createBullet("↓ Aşağı sürüşdür", "Bildiriş pərdəsini aşağı endirir");
        TextView f2 = createBullet("↑ Yuxarı sürüşdür", "Bildiriş pərdəsini bağlayır");
        TextView f3 = createBullet("⚡ Haptic Titrəyiş", "Hər jestdə zərif klik effekti");
        cardFeatures.addView(f1);
        cardFeatures.addView(f2);
        cardFeatures.addView(f3);

        LinearLayout.LayoutParams cardLp = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT);
        cardLp.bottomMargin = dp(20);
        root.addView(cardFeatures, cardLp);

        // Card 2: Status
        LinearLayout cardStatus = new LinearLayout(this);
        cardStatus.setOrientation(LinearLayout.VERTICAL);
        cardStatus.setPadding(dp(16), dp(16), dp(16), dp(16));
        GradientDrawable statusBg = new GradientDrawable();
        statusBg.setColor(Color.parseColor("#1E293B"));
        statusBg.setCornerRadius(dp(12));
        statusBg.setStroke(dp(1), Color.parseColor("#334155"));
        cardStatus.setBackground(statusBg);

        statusText = new TextView(this);
        statusText.setTextSize(TypedValue.COMPLEX_UNIT_SP, 14);
        cardStatus.addView(statusText);

        LinearLayout.LayoutParams statLp = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT);
        statLp.bottomMargin = dp(24);
        root.addView(cardStatus, statLp);

        // Action Button
        actionBtn = new Button(this);
        actionBtn.setAllCaps(false);
        actionBtn.setTextSize(TypedValue.COMPLEX_UNIT_SP, 15);
        actionBtn.setTextColor(Color.WHITE);
        actionBtn.setTypeface(null, android.graphics.Typeface.BOLD);
        actionBtn.setPadding(dp(16), dp(14), dp(16), dp(14));
        actionBtn.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View v) {
                openAccessibilitySettings();
            }
        });
        root.addView(actionBtn, new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT));

        setContentView(scroll);
    }

    private TextView createBullet(String title, String desc) {
        TextView tv = new TextView(this);
        tv.setText(title + "\n" + desc);
        tv.setTextColor(Color.parseColor("#F1F5F9"));
        tv.setTextSize(TypedValue.COMPLEX_UNIT_SP, 13);
        tv.setLineSpacing(dp(2), 1.0f);
        LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT);
        lp.bottomMargin = dp(12);
        tv.setLayoutParams(lp);
        return tv;
    }

    @Override
    protected void onResume() {
        super.onResume();
        updateUi();
    }

    private void updateUi() {
        boolean isActive = FingerprintService.instance != null;
        GradientDrawable btnBg = new GradientDrawable();
        btnBg.setCornerRadius(dp(10));

        if (isActive) {
            statusText.setText("✓ Xidmət Aktivdir\nİndi sağ tərəfdəki sensoru aşağı sürüşdürün.");
            statusText.setTextColor(Color.parseColor("#4ADE80"));
            btnBg.setColor(Color.parseColor("#15803D"));
            actionBtn.setText("Xidmət Aktivdir (Tənzimləmələrə keç)");
        } else {
            statusText.setText("✕ Xidmət Deaktivdir\nJestlərin işləməsi üçün Xüsusi imkanlar icazəsi verin.");
            statusText.setTextColor(Color.parseColor("#F87171"));
            btnBg.setColor(Color.parseColor("#2563EB"));
            actionBtn.setText("Xidməti Aktiv Et (Accessibility)");
        }
        actionBtn.setBackground(btnBg);
    }

    private void openAccessibilitySettings() {
        try {
            Intent intent = new Intent(Settings.ACTION_ACCESSIBILITY_SETTINGS);
            startActivity(intent);
        } catch (Exception e) {
            Toast.makeText(this, "Parametrlər açıla bilmədi", Toast.LENGTH_SHORT).show();
        }
    }
}
