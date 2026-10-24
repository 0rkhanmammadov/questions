package com.quiz.helper;

import android.app.Activity;
import android.content.Intent;
import android.content.SharedPreferences;
import android.graphics.Color;
import android.graphics.drawable.GradientDrawable;
import android.net.Uri;
import android.os.Build;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.provider.Settings;
import android.util.TypedValue;
import android.view.Gravity;
import android.view.KeyEvent;
import android.view.View;
import android.view.inputmethod.EditorInfo;
import android.webkit.CookieManager;
import android.webkit.JavascriptInterface;
import android.webkit.WebChromeClient;
import android.webkit.WebSettings;
import android.webkit.WebView;
import android.webkit.WebViewClient;
import android.widget.Button;
import android.widget.EditText;
import android.widget.FrameLayout;
import android.widget.LinearLayout;
import android.widget.TextView;
import android.widget.Toast;

public class MainActivity extends Activity {

    private WebView webView;
    private EditText urlInput;
    private Button accBtn;
    private Button dotBtn;
    private TextView webOverlayBox;
    private final Handler mainHandler = new Handler(Looper.getMainLooper());

    private final Runnable hideWebBoxRunnable = new Runnable() {
        @Override
        public void run() {
            if (webOverlayBox != null) {
                webOverlayBox.setVisibility(View.GONE);
            }
        }
    };

    private int dp(int v) {
        return (int) TypedValue.applyDimension(
                TypedValue.COMPLEX_UNIT_DIP, v, getResources().getDisplayMetrics());
    }

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);

        LinearLayout root = new LinearLayout(this);
        root.setOrientation(LinearLayout.VERTICAL);
        root.setBackgroundColor(Color.parseColor("#121215"));

        // Top Bar: Accessibility Button + Dot Toggle Button + URL Bar
        LinearLayout topBar = new LinearLayout(this);
        topBar.setOrientation(LinearLayout.VERTICAL);
        topBar.setPadding(dp(8), dp(6), dp(8), dp(6));
        topBar.setBackgroundColor(Color.parseColor("#18181B"));

        // Button 1: Accessibility (Screen reader for Share & Cavabı Tap)
        accBtn = new Button(this);
        accBtn.setAllCaps(false);
        accBtn.setTextSize(TypedValue.COMPLEX_UNIT_SP, 12);
        accBtn.setTextColor(Color.WHITE);
        accBtn.setPadding(dp(10), dp(6), dp(10), dp(6));
        accBtn.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View v) {
                handleAccButtonClick();
            }
        });
        LinearLayout.LayoutParams accLp = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT, dp(38));
        accLp.bottomMargin = dp(4);
        topBar.addView(accBtn, accLp);

        // Button 2: Floating dot toggle (default: hidden)
        dotBtn = new Button(this);
        dotBtn.setAllCaps(false);
        dotBtn.setTextSize(TypedValue.COMPLEX_UNIT_SP, 12);
        dotBtn.setTextColor(Color.WHITE);
        dotBtn.setPadding(dp(10), dp(6), dp(10), dp(6));
        dotBtn.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View v) {
                handleDotButtonClick();
            }
        });
        LinearLayout.LayoutParams dotLp = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT, dp(36));
        dotLp.bottomMargin = dp(6);
        topBar.addView(dotBtn, dotLp);

        // URL row
        LinearLayout urlRow = new LinearLayout(this);
        urlRow.setOrientation(LinearLayout.HORIZONTAL);
        urlRow.setGravity(Gravity.CENTER_VERTICAL);

        urlInput = new EditText(this);
        urlInput.setSingleLine(true);
        urlInput.setHint("Sayt ünvanı yaz (məs: google.com)...");
        urlInput.setHintTextColor(Color.parseColor("#71717A"));
        urlInput.setTextColor(Color.WHITE);
        urlInput.setTextSize(TypedValue.COMPLEX_UNIT_SP, 13);
        urlInput.setPadding(dp(10), dp(6), dp(10), dp(6));
        urlInput.setImeOptions(EditorInfo.IME_ACTION_GO);
        GradientDrawable inputBg = new GradientDrawable();
        inputBg.setColor(Color.parseColor("#27272A"));
        inputBg.setCornerRadius(dp(6));
        urlInput.setBackground(inputBg);

        urlInput.setOnEditorActionListener(new TextView.OnEditorActionListener() {
            @Override
            public boolean onEditorAction(TextView v, int actionId, KeyEvent event) {
                loadUserUrl();
                return true;
            }
        });

        LinearLayout.LayoutParams urlLp = new LinearLayout.LayoutParams(
                0, dp(36), 1.0f
        );
        urlLp.setMarginEnd(dp(6));
        urlRow.addView(urlInput, urlLp);

        Button goBtn = new Button(this);
        goBtn.setText("Aç");
        goBtn.setAllCaps(false);
        goBtn.setTextColor(Color.WHITE);
        goBtn.setTextSize(TypedValue.COMPLEX_UNIT_SP, 13);
        GradientDrawable goBg = new GradientDrawable();
        goBg.setColor(Color.parseColor("#3F3F46"));
        goBg.setCornerRadius(dp(6));
        goBtn.setBackground(goBg);
        goBtn.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View v) {
                loadUserUrl();
            }
        });
        urlRow.addView(goBtn, new LinearLayout.LayoutParams(dp(54), dp(36)));

        topBar.addView(urlRow);
        root.addView(topBar);

        // Frame: WebView + On-screen floating answer box for embedded web
        FrameLayout contentFrame = new FrameLayout(this);
        LinearLayout.LayoutParams frameLp = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT, 0, 1.0f);
        root.addView(contentFrame, frameLp);

        webView = new WebView(this);
        setupWebView();
        contentFrame.addView(webView, new FrameLayout.LayoutParams(
                FrameLayout.LayoutParams.MATCH_PARENT,
                FrameLayout.LayoutParams.MATCH_PARENT
        ));

        webOverlayBox = new TextView(this);
        webOverlayBox.setTextColor(Color.parseColor("#4ADE80"));
        webOverlayBox.setTextSize(TypedValue.COMPLEX_UNIT_SP, 14);
        webOverlayBox.setPadding(dp(14), dp(8), dp(14), dp(8));
        GradientDrawable boxBg = new GradientDrawable();
        boxBg.setColor(Color.parseColor("#EE18181B"));
        boxBg.setCornerRadius(dp(8));
        boxBg.setStroke(dp(1), Color.parseColor("#993F3F46"));
        webOverlayBox.setBackground(boxBg);
        webOverlayBox.setVisibility(View.GONE);

        FrameLayout.LayoutParams boxLp = new FrameLayout.LayoutParams(
                FrameLayout.LayoutParams.WRAP_CONTENT,
                FrameLayout.LayoutParams.WRAP_CONTENT
        );
        boxLp.gravity = Gravity.BOTTOM | Gravity.END;
        boxLp.bottomMargin = dp(24);
        boxLp.setMarginEnd(dp(16));
        contentFrame.addView(webOverlayBox, boxLp);

        setContentView(root);

        // Load initial instructional + test page
        String startHtml =
                "<!DOCTYPE html><html><head><meta charset='utf-8'>" +
                "<meta name='viewport' content='width=device-width,initial-scale=1'>" +
                "<style>body{background:#121215;color:#e4e4e7;font-family:sans-serif;padding:18px;line-height:1.5;}" +
                ".card{background:#18181b;border:1px solid #27272a;border-radius:10px;padding:16px;margin-bottom:14px;}" +
                "h3{margin:0 0 8px 0;color:#4ade80;font-size:16px;}" +
                "p{margin:0 0 8px 0;font-size:13px;color:#a1a1aa;}</style></head><body>" +
                "<div class='card'><h3>✓ 100% Gizli Rejim (Qalereyasız!)</h3>" +
                "<p>1. Yuxarıdakı <b>1-ci düymə</b> ilə 'Ekran Oxuyucu' xidmətini aktiv edin.</p>" +
                "<p>2. Brauzerdə imtahanda olarkən: sualın mətni üstündə basıb saxlayın -> <b>'Cavabı Tap'</b> və ya <b>'Paylaş'</b> vurun. Arxa planda ekran oxunacaq və cavab çıxacaq!</p>" +
                "<p>3. Qalereyaya girməyə ehtiyac yoxdur. Ekranda heç bir göy nöqtə də görünmür.</p></div>" +
                "<div class='card'><h3>Yoxlamaq üçün bu ekranda boş yerə 2 dəfə vur:</h3>" +
                "<p style='color:#fff;font-size:15px;'>14. Azərbaycanın paytaxtı hansı şəhərdir?<br>A) Gəncə &nbsp; B) Bakı &nbsp; C) Şəki &nbsp; D) Quba</p></div>" +
                "</body></html>";
        webView.loadDataWithBaseURL("https://www.google.com", startHtml, "text/html", "UTF-8", null);
    }

    @Override
    protected void onResume() {
        super.onResume();
        updateButtons();
        boolean showDot = getSharedPreferences("quiz_prefs", MODE_PRIVATE).getBoolean("show_floating_dot", false);
        if (showDot) {
            startOverlayServiceSafely();
        } else {
            try {
                stopService(new Intent(this, FloatingOverlayService.class));
            } catch (Exception ignored) {}
        }
    }

    private void updateButtons() {
        boolean hasAcc = ScreenReaderService.instance != null;
        GradientDrawable accBg = new GradientDrawable();
        accBg.setCornerRadius(dp(6));
        if (hasAcc) {
            accBg.setColor(Color.parseColor("#15803D"));
            accBtn.setText("✓ 1. Ekran Oxuyucu Aktivdir (Cavabı Tap Hazır)");
        } else {
            accBg.setColor(Color.parseColor("#2563EB"));
            accBtn.setText("1. Ekran Oxuyucunu Aktiv Et (Accessibility)");
        }
        accBtn.setBackground(accBg);

        boolean showDot = getSharedPreferences("quiz_prefs", MODE_PRIVATE).getBoolean("show_floating_dot", false);
        GradientDrawable dotBg = new GradientDrawable();
        dotBg.setCornerRadius(dp(6));
        if (showDot) {
            dotBg.setColor(Color.parseColor("#D97706"));
            dotBtn.setText("2. Ekranda Göy Nöqtə: AÇIQDIR (Toxun = Gizlət)");
        } else {
            dotBg.setColor(Color.parseColor("#27272A"));
            dotBtn.setText("2. Ekranda Göy Nöqtə: GİZLİDİR (Toxun = Göstər)");
        }
        dotBtn.setBackground(dotBg);
    }

    private void handleAccButtonClick() {
        if (ScreenReaderService.instance != null) {
            Toast.makeText(this, "✓ Ekran oxuyucu aktivdir! Brauzerdə mətni seçib 'Cavabı Tap' və ya 'Paylaş' vurun.", Toast.LENGTH_SHORT).show();
            return;
        }
        Toast.makeText(this, "Məhdudiyyət varsa: Tətbiq haqqında -> ⋮ 3 nöqtə -> İcazə ver", Toast.LENGTH_LONG).show();
        try {
            Intent intent = new Intent(Settings.ACTION_ACCESSIBILITY_SETTINGS);
            startActivity(intent);
        } catch (Exception e) {
            Toast.makeText(this, "Parametrlər açıla bilmədi", Toast.LENGTH_SHORT).show();
        }
    }

    private void handleDotButtonClick() {
        SharedPreferences prefs = getSharedPreferences("quiz_prefs", MODE_PRIVATE);
        boolean current = prefs.getBoolean("show_floating_dot", false);
        if (!current) {
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.M && !Settings.canDrawOverlays(this)) {
                Toast.makeText(this, "Siyahıdan 'Sistem Köməkçisi' seçib icazə verin", Toast.LENGTH_LONG).show();
                Intent intent = new Intent(
                        Settings.ACTION_MANAGE_OVERLAY_PERMISSION,
                        Uri.parse("package:" + getPackageName())
                );
                startActivity(intent);
                return;
            }
            prefs.edit().putBoolean("show_floating_dot", true).apply();
            startOverlayServiceSafely();
            Toast.makeText(this, "Göy nöqtə aktiv edildi", Toast.LENGTH_SHORT).show();
        } else {
            prefs.edit().putBoolean("show_floating_dot", false).apply();
            try {
                stopService(new Intent(this, FloatingOverlayService.class));
            } catch (Exception ignored) {}
            Toast.makeText(this, "Göy nöqtə gizlədildi (Tam gizli)", Toast.LENGTH_SHORT).show();
        }
        updateButtons();
    }

    private void startOverlayServiceSafely() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.M && !Settings.canDrawOverlays(this)) {
            return;
        }
        try {
            Intent intent = new Intent(this, FloatingOverlayService.class);
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
                startForegroundService(intent);
            } else {
                startService(intent);
            }
        } catch (Exception ignored) {}
    }

    private void loadUserUrl() {
        String u = urlInput.getText().toString().trim();
        if (u.isEmpty()) return;
        if (!u.startsWith("http://") && !u.startsWith("https://")) {
            if (u.contains(".") && !u.contains(" ")) {
                u = "https://" + u;
            } else {
                u = "https://www.google.com/search?q=" + Uri.encode(u);
            }
        }
        webView.loadUrl(u);
    }

    private void setupWebView() {
        WebSettings ws = webView.getSettings();
        ws.setJavaScriptEnabled(true);
        ws.setDomStorageEnabled(true);
        ws.setDatabaseEnabled(true);
        ws.setLoadsImagesAutomatically(true);
        ws.setBuiltInZoomControls(true);
        ws.setDisplayZoomControls(false);
        ws.setUseWideViewPort(true);
        ws.setLoadWithOverviewMode(true);
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.LOLLIPOP) {
            ws.setMixedContentMode(WebSettings.MIXED_CONTENT_ALWAYS_ALLOW);
            CookieManager.getInstance().setAcceptThirdPartyCookies(webView, true);
        }

        webView.addJavascriptInterface(new JsBridge(), "AndroidQuizBridge");
        webView.setWebChromeClient(new WebChromeClient());
        webView.setWebViewClient(new WebViewClient() {
            @Override
            public void onPageFinished(WebView view, String url) {
                super.onPageFinished(view, url);
                if (url != null && !url.startsWith("data:") && !url.equals("https://www.google.com")) {
                    urlInput.setText(url);
                }
                injectDoubleTapScript(view);
            }
        });
    }

    private void injectDoubleTapScript(WebView view) {
        String js =
                "(function(){" +
                "if(window.__apkDoubleTapInstalled)return;" +
                "window.__apkDoubleTapInstalled=true;" +
                "try{" +
                "var st=document.createElement('style');" +
                "st.textContent='*{-webkit-user-select:text!important;user-select:text!important;}html,body{touch-action:manipulation!important;}';" +
                "document.head.appendChild(st);" +
                "}catch(e){}" +
                "function getVisText(){" +
                "try{" +
                "var vh=window.innerHeight||document.documentElement.clientHeight;" +
                "var els=document.querySelectorAll('p,li,h1,h2,h3,h4,td,label,div,span,article,section');" +
                "var seen={},lines=[];" +
                "for(var i=0;i<els.length;i++){" +
                "var el=els[i];if(el.children.length>4)continue;" +
                "var r=el.getBoundingClientRect();" +
                "if(r.bottom<0||r.top>vh||r.height===0)continue;" +
                "var t=(el.innerText||'').trim();" +
                "if(t.length>=2&&t.length<=600&&!seen[t]){seen[t]=1;lines.push(t);}" +
                "}" +
                "var j=lines.join('\\n').trim();" +
                "return j.length>=6?j:(document.body.innerText||'').trim();" +
                "}catch(e){return (document.body.innerText||'').trim();}" +
                "}" +
                "function triggerFromEl(target){" +
                "var sel=(window.getSelection?window.getSelection().toString():'').trim();" +
                "if(sel.length>=4){window.AndroidQuizBridge.solve(sel);return;}" +
                "var el=target;" +
                "while(el&&el!==document.body&&el!==document.documentElement&&(el.innerText||'').trim().length<25){el=el.parentElement;}" +
                "if(el&&el!==document.body&&el!==document.documentElement){" +
                "var bt=(el.innerText||'').trim();" +
                "if(bt.length>=10&&bt.length<=2000){window.AndroidQuizBridge.solve(bt);return;}" +
                "}" +
                "var vt=getVisText();" +
                "if(vt.length>=5)window.AndroidQuizBridge.solve(vt);" +
                "}" +
                "var lastTap=0;" +
                "document.addEventListener('touchend',function(e){" +
                "var tag=e.target&&e.target.tagName;" +
                "if(tag==='INPUT'||tag==='TEXTAREA'||tag==='BUTTON')return;" +
                "var now=Date.now();" +
                "if(now-lastTap<380&&now-lastTap>30){lastTap=0;triggerFromEl(e.target);return;}" +
                "lastTap=now;" +
                "},{passive:true});" +
                "window.addEventListener('dblclick',function(e){" +
                "var tag=e.target&&e.target.tagName;" +
                "if(tag==='INPUT'||tag==='TEXTAREA'||tag==='BUTTON')return;" +
                "triggerFromEl(e.target);" +
                "});" +
                "})();";
        view.evaluateJavascript(js, null);
    }

    private void showWebToast(final String text, final long durationMs) {
        mainHandler.post(new Runnable() {
            @Override
            public void run() {
                webOverlayBox.setText(text);
                webOverlayBox.setVisibility(View.VISIBLE);
                mainHandler.removeCallbacks(hideWebBoxRunnable);
                if (durationMs > 0) {
                    mainHandler.postDelayed(hideWebBoxRunnable, durationMs);
                }
            }
        });
    }

    private class JsBridge {
        @JavascriptInterface
        public void solve(String questionText) {
            if (questionText == null || questionText.trim().length() < 4) return;
            showWebToast("◌ ...", 1200);
            AiSolver.solveAsync(questionText, new AiSolver.Callback() {
                @Override
                public void onResult(String formattedAnswer) {
                    showWebToast(formattedAnswer, 3000);
                }
            });
        }
    }

    @Override
    public void onBackPressed() {
        if (webView != null && webView.canGoBack()) {
            webView.goBack();
        } else {
            super.onBackPressed();
        }
    }
}
