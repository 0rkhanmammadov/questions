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
                        "QAYDA: Heç bir izahat, cümlə və ya söz YAZMA! " +
                        "Yalnız sualın nömrəsini (əgər varsa) və doğru variant hərfini (A, B, C, D, E) və ya rəqəmləri yaz.\n" +
                        "Nümunə formatlar:\n14) B\n15) 1-2-3\nC\n\n" +
                        "Sual:\n" + trimmedQ + "\n\nCavab (yalnız hərf/rəqəm):";

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
                    a1 = f1.get(12, TimeUnit.SECONDS);
                } catch (Exception ignored) {}
                try {
                    a2 = f2.get(12, TimeUnit.SECONDS);
                } catch (Exception ignored) {}

                String finalAns;
                if (a1 != null && a2 != null) {
                    if (a1.equals(a2)) {
                        finalAns = a1 + " ✓✓";
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

                String display = "✓ " + finalAns;
                synchronized (history) {
                    if (history.size() > 1) {
                        StringBuilder sb = new StringBuilder();
                        int max = Math.min(history.size(), 4);
                        for (int i = 1; i < max; i++) {
                            if (i > 1) sb.append(" | ");
                            sb.append(history.get(i));
                        }
                        display += "   [" + sb.toString() + "]";
                    }
                }
                callback.onResult(display);
            } catch (Exception e) {
                callback.onResult("✕ Xəta");
            } finally {
                solving = false;
            }
        });
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
            body.put("system", "Respond ONLY with the question number and option letter (e.g. '14) B' or '15) 1-2-3'). Never write explanations.");
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
            return raw.isEmpty() ? null : cleanAnswer(raw, qText);
        } catch (Exception e) {
            return null;
        } finally {
            if (conn != null) conn.disconnect();
        }
    }

    private static String queryPollinations(String promptText, String qText, int nonce) {
        HttpURLConnection conn = null;
        try {
            URL url = new URL("https://text.pollinations.ai/openai");
            conn = (HttpURLConnection) url.openConnection();
            conn.setRequestMethod("POST");
            conn.setConnectTimeout(10000);
            conn.setReadTimeout(10000);
            conn.setRequestProperty("Content-Type", "application/json");
            conn.setRequestProperty("User-Agent", "Mozilla/5.0 (Linux; Android 13) AppleWebKit/537.36");
            conn.setDoOutput(true);

            JSONObject sysMsg = new JSONObject();
            sysMsg.put("role", "system");
            sysMsg.put("content", "QAYDA: Heç bir izahat yazma! Yalnız sualın nömrəsini və doğru variant hərfini (məs: 14) B və ya 15) 1-2-3) yaz.");

            JSONObject usrMsg = new JSONObject();
            usrMsg.put("role", "user");
            usrMsg.put("content", promptText);

            JSONArray messages = new JSONArray();
            messages.put(sysMsg);
            messages.put(usrMsg);

            JSONObject body = new JSONObject();
            body.put("model", "openai");
            body.put("seed", nonce);
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
            JSONObject data = new JSONObject(resp.toString());
            String content = data.getJSONArray("choices")
                    .getJSONObject(0)
                    .getJSONObject("message")
                    .getString("content");
            return content == null || content.trim().isEmpty() ? null : cleanAnswer(content, qText);
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
        String text = (raw == null ? "" : raw).replace("*", "").trim();
        String[] lines = text.split("\\r?\\n");
        String first = text;
        for (String l : lines) {
            if (!l.trim().isEmpty()) {
                first = l.trim();
                break;
            }
        }
        String qNum = extractQuestionNum(qText);

        Matcher mPref = Pattern.compile("^(\\d{1,3})\\s*[\\)\\.\\-:]\\s*([A-Ea-e])\\b").matcher(first);
        if (mPref.find()) {
            return mPref.group(1) + ") " + mPref.group(2).toUpperCase();
        }

        Matcher mSeqPref = Pattern.compile("^(\\d{1,3})\\s*[\\)\\.\\-:]\\s*(\\d+(?:\\s*[-,]\\s*\\d+)+)").matcher(first);
        if (mSeqPref.find()) {
            String seq = mSeqPref.group(2).replaceAll("\\s*,\\s*", "-").replaceAll("\\s+", "");
            return mSeqPref.group(1) + ") " + seq;
        }

        Matcher mOpt = Pattern.compile("(?:^|Cavab\\s*[:\\-]?\\s*|Variant\\s*[:\\-]?\\s*)([A-Ea-e])(?:\\s*[\\)\\.\\-:]|\\s*$)", Pattern.CASE_INSENSITIVE).matcher(first);
        if (mOpt.find()) {
            String letter = mOpt.group(1).toUpperCase();
            return !qNum.isEmpty() ? qNum + ") " + letter : letter;
        }

        Matcher mSeq = Pattern.compile("^(\\d+(?:\\s*[-,]\\s*\\d+)+)$").matcher(first);
        if (mSeq.find()) {
            String seq = mSeq.group(1).replaceAll("\\s*,\\s*", "-").replaceAll("\\s+", "");
            return !qNum.isEmpty() ? qNum + ") " + seq : seq;
        }

        String shortAns = first.length() > 38 ? first.substring(0, 38).trim() : first;
        if (!qNum.isEmpty() && !shortAns.startsWith(qNum + ")")) {
            return qNum + ") " + shortAns;
        }
        return shortAns;
    }
}
