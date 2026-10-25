package com.fp.panel;

import android.app.Activity;
import android.content.Intent;
import android.graphics.Color;
import android.graphics.drawable.GradientDrawable;
import android.os.Build;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.provider.Settings;
import android.util.TypedValue;
import android.view.View;
import android.widget.Button;
import android.widget.LinearLayout;
import android.widget.ScrollView;
import android.widget.TextView;
import android.widget.Toast;

public class MainActivity extends Activity {

    private TextView statusText;
    private TextView diagText;
    private TextView eventText;
    private Button actionBtn;
    private final Handler handler = new Handler(Looper.getMainLooper());

    private final Runnable refreshRunnable = new Runnable() {
        @Override
        public void run() {
            updateUi();
            handler.postDelayed(this, 1000);
        }
    };

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
        root.setPadding(dp(20), dp(32), dp(20), dp(32));
        scroll.addView(root);

        // Header
        TextView title = new TextView(this);
        title.setText("Barmaq İzi Paneli (Diaqnostika)");
        title.setTextColor(Color.WHITE);
        title.setTextSize(TypedValue.COMPLEX_UNIT_SP, 20);
        title.setTypeface(null, android.graphics.Typeface.BOLD);
        root.addView(title);

        TextView subtitle = new TextView(this);
        subtitle.setText("Cihaz: " + Build.MANUFACTURER + " " + Build.MODEL + " (Android " + Build.VERSION.RELEASE + ")");
        subtitle.setTextColor(Color.parseColor("#94A3B8"));
        subtitle.setTextSize(TypedValue.COMPLEX_UNIT_SP, 13);
        LinearLayout.LayoutParams subLp = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT);
        subLp.topMargin = dp(4);
        subLp.bottomMargin = dp(20);
        root.addView(subtitle, subLp);

        // Diagnostics Card
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

        diagText = new TextView(this);
        diagText.setTextSize(TypedValue.COMPLEX_UNIT_SP, 13);
        LinearLayout.LayoutParams diagLp = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT);
        diagLp.topMargin = dp(10);
        diagText.setLayoutParams(diagLp);
        cardStatus.addView(diagText);

        eventText = new TextView(this);
        eventText.setTextSize(TypedValue.COMPLEX_UNIT_SP, 13);
        eventText.setTextColor(Color.parseColor("#FDE047"));
        LinearLayout.LayoutParams evLp = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT);
        evLp.topMargin = dp(10);
        eventText.setLayoutParams(evLp);
        cardStatus.addView(eventText);

        LinearLayout.LayoutParams statLp = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT);
        statLp.bottomMargin = dp(20);
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

    @Override
    protected void onResume() {
        super.onResume();
        handler.removeCallbacks(refreshRunnable);
        handler.post(refreshRunnable);
    }

    @Override
    protected void onPause() {
        super.onPause();
        handler.removeCallbacks(refreshRunnable);
    }

    private void updateUi() {
        FingerprintService s = FingerprintService.instance;
        boolean isActive = (s != null);
        GradientDrawable btnBg = new GradientDrawable();
        btnBg.setCornerRadius(dp(10));

        if (isActive) {
            statusText.setText("1. Xidmət: Aktivdir ✓");
            statusText.setTextColor(Color.parseColor("#4ADE80"));
            btnBg.setColor(Color.parseColor("#15803D"));
            actionBtn.setText("Xidmət Aktivdir (Tənzimləmələr)");

            diagText.setText("2. Aparat Dəstəyi:\n" + s.getDiagnosticStatus());
            diagText.setTextColor(s.getDiagnosticStatus().contains("✓") 
                    ? Color.parseColor("#4ADE80") 
                    : Color.parseColor("#F87171"));
        } else {
            statusText.setText("1. Xidmət: Deaktivdir ✕\nXüsusi imkanlar (Accessibility) icazəsi lazımdır.");
            statusText.setTextColor(Color.parseColor("#F87171"));
            btnBg.setColor(Color.parseColor("#2563EB"));
            actionBtn.setText("Xidməti Aktiv Et (Accessibility)");

            diagText.setText("2. Aparat Dəstəyi: Xidmət qoşulanda yoxlanılacaq");
            diagText.setTextColor(Color.parseColor("#94A3B8"));
        }
        actionBtn.setBackground(btnBg);

        eventText.setText("3. Son Hadisə:\n" + FingerprintService.lastEventInfo);
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
