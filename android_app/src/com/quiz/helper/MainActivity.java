package com.quiz.helper;

import android.app.Activity;
import android.content.Intent;
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
    private Button overlayBtn;
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

        // Top Bar: Overlay Activator + URL Bar
        LinearLayout topBar = new LinearLayout(this);
        topBar.setOrientation(LinearLayout.VERTICAL);
        topBar.setPadding(dp(8), dp(6), dp(8), dp(6));
        topBar.setBackgroundColor(Color.parseColor("#18181B"));

        overlayBtn = new Button(this);
        overlayBtn.setAllCaps(false);
        overlayBtn.setTextSize(TypedValue.COMPLEX_UNIT_SP, 13);
        overlayBtn.setTextColor(Color.WHITE);
        GradientDrawable btnBg = new GradientDrawable();
        btnBg.setColor(Color.parseColor("#2563EB"));
        btnBg.setCornerRadius(dp(6));
        overlayBtn.setBackground(btnBg);
        overlayBtn.setPadding(dp(10), dp(6), dp(10), dp(6));
        updateOverlayButtonLabel();

        overlayBtn.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View v) {
                handleOverlayButtonClick();
            }
        });

        LinearLayout.LayoutParams overLp = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT,
                 dp(38)
        );
        overLp.bottomMargin = dp(6);
        topBar.addView(overlayBtn, overLp);

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
        urlRow.addView(goBtn, new LinearLayout.LayoutParams(dp(56), dp(36)));

        topBar.addView(urlRow);
        root.addView(topBar, new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT,
                LinearLayout.LayoutParams.WRAP_CONTENT
        ));

        // WebView + Stealth Answer Box container
        FrameLayout webContainer = new FrameLayout(this);
        webView = new WebView(this);
        setupWebView();
        webContainer.addView(webView, new FrameLayout.LayoutParams(
                FrameLayout.LayoutParams.MATCH_PARENT,
                FrameLayout.LayoutParams.MATCH_PARENT
        ));

        webOverlayBox = new TextView(this);
        webOverlayBox.setTextColor(Color.parseColor("#4ADE80"));
        webOverlayBox.setTextSize(TypedValue.COMPLEX_UNIT_SP, 14);
        webOverlayBox.setPadding(dp(12), dp(6), dp(12), dp(6));
        GradientDrawable toastBg = new GradientDrawable();
        toastBg.setColor(Color.parseColor("#DD141417"));
        toastBg.setCornerRadius(dp(8));
        toastBg.setStroke(dp(1), Color.parseColor("#993F3F46"));
        webOverlayBox.setBackground(toastBg);
        webOverlayBox.setVisibility(View.GONE);

        FrameLayout.LayoutParams toastLp = new FrameLayout.LayoutParams(
                FrameLayout.LayoutParams.WRAP_CONTENT,
                FrameLayout.LayoutParams.WRAP_CONTENT
        );
        toastLp.gravity = Gravity.BOTTOM | Gravity.END;
        toastLp.rightMargin = dp(14);
        toastLp.bottomMargin = dp(14);
        webContainer.addView(webOverlayBox, toastLp);

        root.addView(webContainer, new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.MATCH_PARENT, 0, 1.0f
        ));

        setContentView(root);

        // Load initial instructional + test page
        String startHtml =
                "<!DOCTYPE html><html><head><meta charset='utf-8'>" +
                "<meta name='viewport' content='width=device-width,initial-scale=1'>" +
                "<style>body{background:#121215;color:#e4e4e7;font-family:sans-serif;padding:18px;line-height:1.5;}" +
                ".card{background:#18181b;border:1px solid #27272a;border-radius:10px;padding:16px;margin-bottom:14px;}" +
                "h3{margin:0 0 8px 0;color:#4ade80;font-size:16px;}" +
                "p{margin:0 0 8px 0;font-size:13px;color:#a1a1aa;}</style></head><body>" +
                "<div class='card'><h3>✓ Hazırdır! Boş ekrana 2 dəfə barmaq vur!</h3>" +
                "<p>1. <b>Daxili Brauzer:</b> Yuxarıda istənilən sayt ünvanını yazıb <b>Aç</b> bas. Həmin saytda <b>ekranın boş yerinə 2 dəfə barmaq vuranda</b> sualın cavabı sağ-aşağı küncdə çıxacaq.</p>" +
                "<p>2. <b>Chrome Üstündə İşlətmək:</b> Yuxarıdakı mavi düyməyə basıb icazəni aç — Chrome-un üstündə kiçik <b>•</b> nöqtəsi yaranacaq.</p></div>" +
                "<div class='card'><h3>Yoxlamaq üçün bu ekranda boş yerə 2 dəfə vur:</h3>" +
                "<p style='color:#fff;font-size:15px;'>14. Azərbaycanın paytaxtı hansı şəhərdir?<br>A) Gəncə &nbsp; B) Bakı &nbsp; C) Şəki &nbsp; D) Quba</p></div>" +
                "</body></html>";
        webView.loadDataWithBaseURL("https://www.google.com", startHtml, "text/html", "UTF-8", null);
    }

    @Override
    protected void onResume() {
        super.onResume();
        updateOverlayButtonLabel();
        startOverlayServiceSafely();
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

    private void updateOverlayButtonLabel() {
        boolean canOverlay = Build.VERSION.SDK_INT < Build.VERSION_CODES.M || Settings.canDrawOverlays(this);
        boolean hasAcc = ScreenReaderService.instance != null;
        if (!canOverlay) {
            overlayBtn.setText("1. Chrome Üstündə • Nöqtə İcazəsini Aç");
        } else if (!hasAcc) {
            overlayBtn.setText("2. Chrome Ekran Oxuyucu İcazəsini Aç (Accessibility)");
        } else {
            overlayBtn.setText("✓ Chrome • Nöqtəsi Aktivdir (Yenilə)");
        }
    }

    private void handleOverlayButtonClick() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.M && !Settings.canDrawOverlays(this)) {
            Toast.makeText(this, "Siyahıdan 'Sistem Köməkçisi' seçib icazə verin", Toast.LENGTH_LONG).show();
            Intent intent = new Intent(
                    Settings.ACTION_MANAGE_OVERLAY_PERMISSION,
                    Uri.parse("package:" + getPackageName())
            );
            startActivity(intent);
            return;
        }
        startOverlayServiceSafely();
        if (ScreenReaderService.instance == null) {
            Toast.makeText(this, "Endirilən xidmətlərdən 'Ekran Oxuyucu Köməkçi' aktiv edin", Toast.LENGTH_LONG).show();
            Intent intent = new Intent(Settings.ACTION_ACCESSIBILITY_SETTINGS);
            startActivity(intent);
            return;
        }
        Toast.makeText(this, "✓ Üzən • nöqtə aktivdir! İndi Chrome-a keçə bilərsiniz.", Toast.LENGTH_SHORT).show();
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
