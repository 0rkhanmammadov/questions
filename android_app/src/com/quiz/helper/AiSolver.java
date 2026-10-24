package com.quiz.helper;

import org.json.JSONArray;
import org.json.JSONObject;

import java.io.BufferedReader;
import java.io.InputStreamReader;
import java.io.OutputStream;
import java.net.HttpURLConnection;
import java.net.URL;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.List;
import java.util.Random;
import java.util.concurrent.Callable;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.Future;
import java.util.concurrent.TimeUnit;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

public class AiSolver {

    public interface Callback {
        void onResult(String formattedAnswer);
    }

    private static final ExecutorService executor = Executors.newCachedThreadPool();
    private static final List<String> history = new ArrayList<>();
    private static volatile boolean solving = false;

    private static final String GEMINI_KEY = new String(new char[]{
        'A','Q','.','A','b','8','R','N','6','I','W','W','k','F','_',
        'j','q','E','Y','E','t','I','O','w','r','i','D','f','R','1',
        'o','D','v','h','t','D','K','A','M','U','c','M','b','e','C',
        'x','Z','O','7','T','H','4','g'
    });

    public static void solveAsync(final String rawQuestion, final Callback callback) {
        final String q = rawQuestion == null ? "" : rawQuestion.trim();
        if (q.length() < 4 || solving) {
            return;
        }
        solving = true;
        executor.submit(() -> {
            try {
                final String trimmedQ = q.length() > 2000 ? q.substring(0, 2000) : q;
                final int nonce = 1000000 + new Random().nextInt(8999999);
                final String strictPrompt =
                        "Sən ali dərəcəli imtahan və test ekspertisən.\n" +
                        "Sualı və variantları diqqətlə oxu, dərindən düşün və ən doğru cavabı tap.\n\n" +
                        "QAYDALAR:\n" +
                        "1. Sualda A, B, C, D, E variantları varsa: Yalnız sualın nömrəsini (əgər varsa) və doğru variant hərfini yaz (Məsələn: 14) B və ya B).\n" +
                        "2. Variant hərfləri yoxdursa (yalnız mətn variantlarıdırsa): Doğru variantın mətnini qısa və dəqiq yaz (Məsələn: 14) Prosessor və ya Prosessor).\n" +
                        "3. Çoxseçimlidirsə (birdən çox doğru cavab varsa, məsələn 3 doğru cavab): Bütün doğru variantları/nömrələri ardıcıl yaz (Məsələn: 1, 2, 4 və ya 14) 1, 2, 4 və ya A, C, D).\n" +
                        "4. Uyğunluqdursa: '1-a, 2-c, 3-b' kimi yaz.\n" +
                        "5. Açıq sualdırsa: Birbaşa qısa cavab sözü və ya rəqəmi yaz.\n" +
                        "6. QƏTİYYƏN heç bir izahat və ya əlavə cümlə YAZMA. Yalnız tək və dəqiq cavab!\n\n" +
                        "Sual:\n" + trimmedQ + "\n\nCavab:";

                String gAns = queryGemini(strictPrompt, trimmedQ);
                if (gAns != null && !gAns.equals("Xəta")) {
                    handleSuccess(gAns, callback);
                    return;
                }

                Future<String> f1 = executor.submit(new Callable<String>() {
                    @Override
                    public String call() {
                        return queryBinjie(strictPrompt, trimmedQ, nonce);
                    }
                });

                Future<String> f2 = executor.submit(new Callable<String>() {
                    @Override
                    public String call() {
                        return queryPollinations(strictPrompt, trimmedQ, nonce + 1);
                    }
                });

                String a1 = null;
                String a2 = null;
                try {
                    a1 = f1.get(10, TimeUnit.SECONDS);
                } catch (Exception ignored) {}
                try {
                    a2 = f2.get(10, TimeUnit.SECONDS);
                } catch (Exception ignored) {}

                String finalAns;
                if (a1 != null && a2 != null) {
                    if (a1.equals(a2)) {
                        finalAns = a1;
                    } else {
                        String a2Short = a2.replaceFirst("^\\d+\\)\\s*", "");
                        finalAns = a1 + " / " + a2Short;
                    }
                } else if (a1 != null) {
                    finalAns = a1;
                } else if (a2 != null) {
                    finalAns = a2;
                } else {
                    finalAns = "Xəta";
                }

                handleSuccess(finalAns, callback);
            } catch (Exception e) {
                callback.onResult("✕ Xəta");
            } finally {
                solving = false;
            }
        });
    }

    public static void solveImageAsync(final byte[] imageBytes, final Callback callback) {
        solveImageAsync(imageBytes, "", callback);
    }

    public static void solveImageAsync(final byte[] imageBytes, final String hintText, final Callback callback) {
        if (imageBytes == null || imageBytes.length == 0 || solving) {
            return;
        }
        solving = true;
        executor.submit(() -> {
            try {
                String ans = queryGeminiVision(imageBytes, hintText);
                if (ans != null && !ans.equals("Xəta")) {
                    handleSuccess(ans, callback);
                } else {
                    callback.onResult("✕ Şəkildən sual tapılmadı");
                }
            } catch (Exception e) {
                callback.onResult("✕ Xəta");
            } finally {
                solving = false;
            }
        });
    }

    private static void handleSuccess(String finalAns, Callback callback) {
        synchronized (history) {
            if (!"Xəta".equals(finalAns)) {
                if (history.isEmpty() || !history.get(0).equals(finalAns)) {
                    history.add(0, finalAns);
                    if (history.size() > 5) {
                        history.remove(history.size() - 1);
                    }
                }
            }
        }
        callback.onResult(finalAns);
    }

    public static String queryGeminiVision(byte[] imageBytes) {
        return queryGeminiVision(imageBytes, "");
    }

    public static String queryGeminiVision(byte[] imageBytes, String hintText) {
        HttpURLConnection conn = null;
        try {
            String b64 = android.util.Base64.encodeToString(imageBytes, android.util.Base64.NO_WRAP);
            String urlStr = "https://generativelanguage.googleapis.com/v1beta/models/gemini-3.5-flash-lite:generateContent?key=" + GEMINI_KEY;
            URL url = new URL(urlStr);
            conn = (HttpURLConnection) url.openConnection();
            conn.setRequestMethod("POST");
            conn.setConnectTimeout(14000);
            conn.setReadTimeout(14000);
            conn.setRequestProperty("Content-Type", "application/json");
            conn.setRequestProperty("x-goog-api-key", GEMINI_KEY);
            conn.setDoOutput(true);

            String prompt = "Sən ali dərəcəli imtahan və test ekspertisən. Şəkildəki sualı və variantları diqqətlə oxu, ən doğru cavabı tap.\n\n" +
                    "QAYDALAR:\n" +
                    "1. Sualda A, B, C, D, E variantları varsa: Yalnız sualın nömrəsini (əgər varsa) və doğru variant hərfini yaz (məs: 14) B və ya B).\n" +
                    "2. Variant hərfləri yoxdursa: Doğru variantın mətnini qısa və dəqiq yaz (məs: 14) Prosessor və ya Prosessor).\n" +
                    "3. Çoxseçimlidirsə (məs. 3 doğru cavab): Bütün doğru variantları/nömrələri ardıcıl yaz (məs: 1, 2, 4 və ya 14) 1, 2, 4 və ya A, C, D).\n" +
                    "4. Uyğunluqdursa: '1-a, 2-c, 3-b' kimi yaz.\n" +
                    "5. Açıq sualdırsa: Birbaşa qısa cavab sözü və ya rəqəmi yaz.\n" +
                    "6. Qətiyyən heç bir izahat YAZMA. Yalnız tək və dəqiq cavab!";

            if (hintText != null && !hintText.trim().isEmpty()) {
                prompt += "\n\nQEYD: İstifadəçinin seçdiyi/fokuslandığı sual mətni budur:\n\"" + hintText.trim() + "\"\nŞəkildə bu sualı və onun variantlarını tapıb həll et.";
            }

            JSONObject textPart = new JSONObject();
            textPart.put("text", prompt);

            JSONObject inlineData = new JSONObject();
            inlineData.put("mimeType", "image/jpeg");
            inlineData.put("data", b64);

            JSONObject imagePart = new JSONObject();
            imagePart.put("inlineData", inlineData);

            JSONArray parts = new JSONArray();
            parts.put(textPart);
            parts.put(imagePart);

            JSONObject content = new JSONObject();
            content.put("parts", parts);
            JSONArray contents = new JSONArray();
            contents.put(content);

            JSONObject body = new JSONObject();
            body.put("contents", contents);
            JSONObject genConfig = new JSONObject();
            genConfig.put("temperature", 0.0);
            genConfig.put("maxOutputTokens", 300);
            body.put("generationConfig", genConfig);

            byte[] out = body.toString().getBytes(StandardCharsets.UTF_8);
            try (OutputStream os = conn.getOutputStream()) {
                os.write(out);
            }

            StringBuilder resp = new StringBuilder();
            try (BufferedReader br = new BufferedReader(new InputStreamReader(conn.getInputStream(), StandardCharsets.UTF_8))) {
                String line;
                while ((line = br.readLine()) != null) {
                    resp.append(line).append("\n");
                }
            }
            JSONObject data = new JSONObject(resp.toString());
            JSONArray candidates = data.optJSONArray("candidates");
            if (candidates == null || candidates.length() == 0) return null;
            JSONObject contentObj = candidates.getJSONObject(0).optJSONObject("content");
            if (contentObj == null) return null;
            JSONArray partsArray = contentObj.optJSONArray("parts");
            if (partsArray == null || partsArray.length() == 0) return null;
            StringBuilder sbText = new StringBuilder();
            for (int i = 0; i < partsArray.length(); i++) {
                sbText.append(partsArray.getJSONObject(i).optString("text", ""));
            }
            String rawText = sbText.toString().trim();
            return rawText.isEmpty() ? null : cleanAnswer(rawText, "");
        } catch (Exception e) {
            return null;
        } finally {
            if (conn != null) conn.disconnect();
        }
    }

    private static String queryGemini(String promptText, String qText) {
        HttpURLConnection conn = null;
        try {
            String urlStr = "https://generativelanguage.googleapis.com/v1beta/models/gemini-3.5-flash-lite:generateContent?key=" + GEMINI_KEY;
            URL url = new URL(urlStr);
            conn = (HttpURLConnection) url.openConnection();
            conn.setRequestMethod("POST");
            conn.setConnectTimeout(8000);
            conn.setReadTimeout(8000);
            conn.setRequestProperty("Content-Type", "application/json");
            conn.setRequestProperty("x-goog-api-key", GEMINI_KEY);
            conn.setDoOutput(true);

            JSONObject part = new JSONObject();
            part.put("text", promptText);
            JSONArray parts = new JSONArray();
            parts.put(part);
            JSONObject content = new JSONObject();
            content.put("parts", parts);
            JSONArray contents = new JSONArray();
            contents.put(content);

            JSONObject body = new JSONObject();
            body.put("contents", contents);
            JSONObject genConfig = new JSONObject();
            genConfig.put("temperature", 0.0);
            genConfig.put("maxOutputTokens", 300);
            body.put("generationConfig", genConfig);

            byte[] out = body.toString().getBytes(StandardCharsets.UTF_8);
            try (OutputStream os = conn.getOutputStream()) {
                os.write(out);
            }

            StringBuilder resp = new StringBuilder();
            try (BufferedReader br = new BufferedReader(new InputStreamReader(conn.getInputStream(), StandardCharsets.UTF_8))) {
                String line;
                while ((line = br.readLine()) != null) {
                    resp.append(line).append("\n");
                }
            }
            JSONObject data = new JSONObject(resp.toString());
            JSONArray candidates = data.optJSONArray("candidates");
            if (candidates == null || candidates.length() == 0) return null;
            JSONObject contentObj = candidates.getJSONObject(0).optJSONObject("content");
            if (contentObj == null) return null;
            JSONArray partsArray = contentObj.optJSONArray("parts");
            if (partsArray == null || partsArray.length() == 0) return null;
            StringBuilder sbText = new StringBuilder();
            for (int i = 0; i < partsArray.length(); i++) {
                sbText.append(partsArray.getJSONObject(i).optString("text", ""));
            }
            String text = sbText.toString().trim();
            return text.isEmpty() ? null : cleanAnswer(text, qText);
        } catch (Exception e) {
            return null;
        } finally {
            if (conn != null) conn.disconnect();
        }
    }

    private static String queryBinjie(String promptText, String qText, int nonce) {
        HttpURLConnection conn = null;
        try {
            URL url = new URL("https://api.binjie.fun/api/generateStream");
            conn = (HttpURLConnection) url.openConnection();
            conn.setRequestMethod("POST");
            conn.setConnectTimeout(9000);
            conn.setReadTimeout(9000);
            conn.setRequestProperty("Content-Type", "application/json");
            conn.setRequestProperty("Origin", "https://chat18.aichatos8.com");
            conn.setRequestProperty("Referer", "https://chat18.aichatos8.com/");
            conn.setRequestProperty("User-Agent", "Mozilla/5.0 (Linux; Android 13) AppleWebKit/537.36");
            conn.setDoOutput(true);

            JSONObject body = new JSONObject();
            body.put("prompt", promptText);
            body.put("userId", "#/chat/" + nonce);
            body.put("network", false);
            body.put("system", "Respond ONLY with 1 single question number and option letter (e.g. '14) B'). Never write explanations.");
            body.put("withoutContext", false);
            body.put("stream", false);

            byte[] out = body.toString().getBytes(StandardCharsets.UTF_8);
            try (OutputStream os = conn.getOutputStream()) {
                os.write(out);
            }

            StringBuilder resp = new StringBuilder();
            try (BufferedReader br = new BufferedReader(new InputStreamReader(conn.getInputStream(), StandardCharsets.UTF_8))) {
                String line;
                while ((line = br.readLine()) != null) {
                    resp.append(line).append("\n");
                }
            }
            String raw = resp.toString().trim();
            if (raw.isEmpty() || raw.matches("(?s).*[\\u4e00-\\u9fff].*") || raw.contains("sorry,") || raw.contains("封禁")) {
                return null;
            }
            return cleanAnswer(raw, qText);
        } catch (Exception e) {
            return null;
        } finally {
            if (conn != null) conn.disconnect();
        }
    }

    private static String queryPollinations(String promptText, String qText, int nonce) {
        HttpURLConnection conn = null;
        try {
            URL url = new URL("https://text.pollinations.ai/");
            conn = (HttpURLConnection) url.openConnection();
            conn.setRequestMethod("POST");
            conn.setConnectTimeout(10000);
            conn.setReadTimeout(10000);
            conn.setRequestProperty("Content-Type", "application/json");
            conn.setRequestProperty("User-Agent", "Mozilla/5.0 (Linux; Android 13) AppleWebKit/537.36");
            conn.setDoOutput(true);

            JSONObject usrMsg = new JSONObject();
            usrMsg.put("role", "user");
            usrMsg.put("content", promptText);

            JSONArray messages = new JSONArray();
            messages.put(usrMsg);

            JSONObject body = new JSONObject();
            body.put("model", "openai-fast");
            body.put("messages", messages);
            body.put("temperature", 0.1);

            byte[] out = body.toString().getBytes(StandardCharsets.UTF_8);
            try (OutputStream os = conn.getOutputStream()) {
                os.write(out);
            }

            StringBuilder resp = new StringBuilder();
            try (BufferedReader br = new BufferedReader(new InputStreamReader(conn.getInputStream(), StandardCharsets.UTF_8))) {
                String line;
                while ((line = br.readLine()) != null) {
                    resp.append(line).append("\n");
                }
            }
            String raw = resp.toString().trim();
            if (raw.isEmpty() || raw.matches("(?s).*[\\u4e00-\\u9fff].*") || raw.contains("sorry,") || raw.contains("Payment Required")) {
                return null;
            }
            return cleanAnswer(raw, qText);
        } catch (Exception e) {
            return null;
        } finally {
            if (conn != null) conn.disconnect();
        }
    }

    private static String extractQuestionNum(String q) {
        if (q == null) return "";
        Pattern p = Pattern.compile("^\\s*(?:Sual\\s*|№\\s*|#\\s*)?(\\d{1,3})\\s*[\\.\\)\\-:]", Pattern.CASE_INSENSITIVE);
        Matcher m = p.matcher(q);
        return m.find() ? m.group(1) : "";
    }

    public static String cleanAnswer(String raw, String qText) {
        String text = (raw == null ? "" : raw).replace("*", "").replace("`", "").replace("✓", "").replace("✔", "").trim();
        if (text.matches("(?s).*[\\u4e00-\\u9fff].*") || text.contains("sorry,") || text.contains("封禁")) {
            return "Xəta";
        }
        String[] lines = text.split("\\r?\\n");
        String first = text;
        for (String l : lines) {
            if (!l.trim().isEmpty()) {
                first = l.trim();
                break;
            }
        }
        // Strip prefixes like "Doğru cavab:", "Cavab:"
        first = first.replaceAll("(?i)^(?:Düzgün\\s+cavab|Doğru\\s+cavab|Cavab|Doğru\\s+variant|Answer|Correct\\s+answer)\\s*[:\\-]\\s*", "").trim();

        String qNum = extractQuestionNum(qText);

        // Numbered answer like "14) B" or "14. Prosessor"
        Matcher mAnsQNum = Pattern.compile("^(\\d{1,3})\\s*[\\)\\.:]\\s*(.+)$").matcher(first);
        if (mAnsQNum.find()) {
            String ansNum = mAnsQNum.group(1);
            String ansBody = mAnsQNum.group(2).trim();
            Matcher mBodyOpt = Pattern.compile("^([A-Ea-e])(?:\\s*[\\)\\.\\-:]|\\s*$)").matcher(ansBody);
            if (mBodyOpt.find()) {
                return ansNum + ") " + mBodyOpt.group(1).toUpperCase();
            }
            if (ansBody.matches("^\\d+(?:\\s*[,;\\-\\s]\\s*\\d+)+$")) {
                Matcher mn = Pattern.compile("\\d+").matcher(ansBody);
                StringBuilder sbN = new StringBuilder();
                while (mn.find()) {
                    if (sbN.length() > 0) sbN.append("-");
                    sbN.append(mn.group());
                }
                return ansNum + ") " + sbN.toString();
            }
            String shortBody = ansBody.length() > 45 ? ansBody.substring(0, 45).trim() : ansBody;
            return ansNum + ") " + shortBody;
        }

        // Single letter A, B, C, D, E
        if (first.matches("^[A-Ea-e]$")) {
            String letter = first.toUpperCase();
            return !qNum.isEmpty() ? qNum + ") " + letter : letter;
        }

        // Multi-letter like A, C, D or A-C-D
        if (first.matches("^[A-Ea-e](?:\\s*[,;\\-\\s]\\s*[A-Ea-e])+$")) {
            Matcher ml = Pattern.compile("[A-Ea-e]").matcher(first);
            StringBuilder sbL = new StringBuilder();
            while (ml.find()) {
                if (sbL.length() > 0) sbL.append(", ");
                sbL.append(ml.group().toUpperCase());
            }
            return !qNum.isEmpty() ? qNum + ") " + sbL.toString() : sbL.toString();
        }

        // Multi-number like 1, 2, 4 or 1-3-5
        if (first.matches("^\\d+(?:\\s*[,;\\-\\s]\\s*\\d+)+$")) {
            Matcher mn = Pattern.compile("\\d+").matcher(first);
            StringBuilder sbN = new StringBuilder();
            while (mn.find()) {
                if (sbN.length() > 0) sbN.append("-");
                sbN.append(mn.group());
            }
            return !qNum.isEmpty() ? qNum + ") " + sbN.toString() : sbN.toString();
        }

        // Matching like 1-a, 2-c, 3-b
        if (Pattern.compile("\\b\\d+\\s*[\\-:]\\s*[A-Za-z]").matcher(first).find()) {
            String matchStr = first.length() > 45 ? first.substring(0, 45).trim() : first;
            return (!qNum.isEmpty() && !matchStr.startsWith(qNum + ")")) ? qNum + ") " + matchStr : matchStr;
        }

        String shortAns = first.length() > 45 ? first.substring(0, 45).trim() : first;
        if (!qNum.isEmpty() && !shortAns.startsWith(qNum + ")")) {
            return qNum + ") " + shortAns;
        }
        return shortAns;
    }
}
