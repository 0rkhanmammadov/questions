// ==UserScript==
// @name         Quiz Stealth Helper
// @namespace    https://0rkhanmammadov.github.io/questions/
// @version      1.2
// @description  Double-tap anywhere on screen to solve visible question
// @match        *://*/*
// @run-at       document-end
// @grant        none
// ==/UserScript==

(function () {
  if (window.__quizHelperActive) {
    window.__quizShowToast("● Hazır");
    return;
  }
  window.__quizHelperActive = true;

  // 1. Unlock text selection and disable double-tap zoom on empty screen
  try {
    const style = document.createElement("style");
    style.textContent =
      "* { -webkit-user-select: text !important; user-select: text !important; } " +
      "html, body { touch-action: manipulation !important; }";
    document.head.appendChild(style);
    ["copy", "cut", "contextmenu", "selectstart"].forEach((ev) => {
      window.addEventListener(ev, (e) => e.stopPropagation(), true);
    });
  } catch (e) {}

  let history = [];
  let dotMode = "off"; // "off", "both", "dots_only"
  const DOT_MAP = { A: "•", B: "••", C: "•••", D: "••••", E: "•••••" };

  // 2. Create Stealth Floating UI (Bottom-Right Corner)
  const host = document.createElement("div");
  host.style.cssText =
    "position:fixed;right:10px;bottom:10px;z-index:2147483647;display:flex;align-items:center;gap:6px;" +
    "font-family:system-ui,-apple-system,sans-serif;font-size:13px;user-select:none;-webkit-user-select:none;";

  const box = document.createElement("div");
  box.style.cssText =
    "background:rgba(20,20,23,0.78);color:#e4e4e7;border:1px solid rgba(63,63,70,0.6);" +
    "padding:4px 9px;border-radius:6px;display:none;pointer-events:none;backdrop-filter:blur(4px);" +
    "box-shadow:0 2px 8px rgba(0,0,0,0.25);max-width:82vw;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;";

  const trigger = document.createElement("div");
  trigger.textContent = "•";
  trigger.title = "1 toxun: Sualı oxu | Basılı saxla: Cavaba bax";
  trigger.style.cssText =
    "width:26px;height:26px;border-radius:50%;background:rgba(24,24,27,0.45);color:rgba(161,161,170,0.75);" +
    "display:flex;align-items:center;justify-content:center;font-size:16px;font-weight:bold;cursor:pointer;" +
    "border:1px solid rgba(63,63,70,0.35);touch-action:manipulation;";

  host.appendChild(box);
  host.appendChild(trigger);
  document.body.appendChild(host);

  let hideTimer = null;
  let isHolding = false;
  let currentText = "● Hazır";

  function showBox(text, durationMs = 2200) {
    if (text) currentText = text;
    box.textContent = currentText;
    box.style.display = "block";
    if (hideTimer) clearTimeout(hideTimer);
    if (!isHolding && durationMs > 0) {
      hideTimer = setTimeout(() => {
        if (!isHolding) box.style.display = "none";
      }, durationMs);
    }
  }
  window.__quizShowToast = showBox;

  function hideBox() {
    if (!isHolding) box.style.display = "none";
  }

  function extractQuestionNum(q) {
    const m = (q || "").match(/^\s*(?:Sual\s*|№\s*|#\s*)?(\d{1,3})\s*[\.\)\-:]/i);
    return m ? m[1] : "";
  }

  function cleanAnswer(raw, qText) {
    let text = (raw || "").replace(/\*/g, "").replace(/`/g, "").replace(/[✓✔]/g, "").trim();
    if (/[\\u4e00-\\u9fff]/.test(text) || text.toLowerCase().includes("sorry,") || text.includes("封禁")) {
      return "Xəta";
    }
    const lines = text.split(/\r?\n/).map((l) => l.trim()).filter(Boolean);
    let first = lines[0] || text;
    first = first.replace(/^(?:Düzgün\s+cavab|Doğru\s+cavab|Cavab|Doğru\s+variant|Answer|Correct\s+answer)\s*[:\-]\s*/i, "").trim();

    const qNum = extractQuestionNum(qText);

    const mAnsQNum = first.match(/^(\d{1,3})\s*[\)\.:]\s*(.+)$/);
    if (mAnsQNum) {
      const ansNum = mAnsQNum[1];
      const ansBody = mAnsQNum[2].trim();
      const mBodyOpt = ansBody.match(/^([A-Ea-e])(?:\s*[\)\.\-:]|\s*$)/);
      if (mBodyOpt) {
        return `${ansNum}) ${mBodyOpt[1].toUpperCase()}`;
      }
      if (/^\d+(?:\s*[,;\-\s]\s*\d+)+$/.test(ansBody)) {
        const nums = (ansBody.match(/\d+/g) || []).join("-");
        return `${ansNum}) ${nums}`;
      }
      return `${ansNum}) ${ansBody.slice(0, 45).trim()}`;
    }

    if (/^[A-Ea-e]$/.test(first)) {
      const letter = first.toUpperCase();
      return qNum ? `${qNum}) ${letter}` : letter;
    }

    if (/^[A-Ea-e](?:\s*[,;\-\s]\s*[A-Ea-e])+$/.test(first)) {
      const letters = (first.match(/[A-Ea-e]/g) || []).map(x => x.toUpperCase()).join(", ");
      return qNum ? `${qNum}) ${letters}` : letters;
    }

    if (/^\d+(?:\s*[,;\-\s]\s*\d+)+$/.test(first)) {
      const nums = (first.match(/\d+/g) || []).join("-");
      return qNum ? `${qNum}) ${nums}` : nums;
    }

    if (/\b\d+\s*[\-:]\s*[A-Za-z]/.test(first)) {
      const matchStr = first.slice(0, 45).trim();
      return qNum && !matchStr.startsWith(`${qNum})`) ? `${qNum}) ${matchStr}` : matchStr;
    }

    const short = first.slice(0, 45).trim();
    return qNum && !short.startsWith(`${qNum})`) ? `${qNum}) ${short}` : short;
  }

  function formatWithDots(ans) {
    if (dotMode === "off" || !ans) return ans;
    const m = ans.match(/\b([A-E])\b/);
    if (!m) return ans;
    const dots = DOT_MAP[m[1]] || "";
    if (dotMode === "dots_only") return dots;
    if (dotMode === "both") return ans.replace(m[1], `${m[1]} (${dots})`);
    return ans;
  }

  const GEMINI_KEY = String.fromCharCode(
    65,81,46,65,98,56,82,78,54,73,87,87,107,70,95,
    106,113,69,89,69,116,73,79,119,114,105,68,102,82,49,
    111,68,118,104,116,68,75,65,77,85,99,77,98,101,67,
    120,90,79,55,84,72,52,103
  );
  async function queryGemini(promptText, qText) {
    try {
      const url = `https://generativelanguage.googleapis.com/v1beta/models/gemini-3.5-flash-lite:generateContent?key=${GEMINI_KEY}`;
      const res = await fetch(url, {
        method: "POST",
        headers: { "Content-Type": "application/json", "x-goog-api-key": GEMINI_KEY },
        body: JSON.stringify({
          contents: [{ parts: [{ text: promptText }] }],
          generationConfig: { temperature: 0.0, maxOutputTokens: 300 },
        }),
      });
      const data = await res.json();
      const parts = data?.candidates?.[0]?.content?.parts || [];
      const txt = parts.map((p) => p?.text || "").join("").trim();
      return txt ? cleanAnswer(txt, qText) : null;
    } catch (e) {
      return null;
    }
  }

  async function queryBinjie(promptText, qText, nonce) {
    try {
      const res = await fetch("https://api.binjie.fun/api/generateStream", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          prompt: promptText,
          userId: `#/chat/${nonce}`,
          network: false,
          system: "Respond ONLY with the question number and option letter (e.g. '14) B' or '15) 1-2-3'). Never write explanations.",
          withoutContext: false,
          stream: false,
        }),
      });
      const raw = await res.text();
      return raw.trim() ? cleanAnswer(raw, qText) : null;
    } catch (e) {
      return null;
    }
  }

  async function queryPollinations(promptText, qText, nonce) {
    try {
      const res = await fetch("https://text.pollinations.ai/openai", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          model: "openai",
          seed: nonce,
          messages: [
            {
              role: "system",
              content: "QAYDA: Heç bir izahat yazma! Yalnız sualın nömrəsini və doğru variant hərfini (məs: 14) B və ya 15) 1-2-3) yaz.",
            },
            { role: "user", content: promptText },
          ],
          temperature: 0.1,
        }),
      });
      const data = await res.json();
      const content = data?.choices?.[0]?.message?.content;
      return content ? cleanAnswer(content, qText) : null;
    } catch (e) {
      return null;
    }
  }

  let solving = false;
  async function solveQuestion(rawQuestion) {
    const q = (rawQuestion || "").trim();
    if (q.length < 4 || solving) return;
    solving = true;
    trigger.style.color = "#f59e0b";
    showBox("◌ ...", 1000);
    const pageAnsBox = document.getElementById("ansBox");
    if (pageAnsBox) {
      pageAnsBox.style.display = "block";
      pageAnsBox.textContent = "◌ Həll edilir...";
    }

    const nonce = Math.floor(1000000 + Math.random() * 8999999);
    const strictPrompt =
      "Sən ali dərəcəli imtahan və test ekspertisən.\n" +
      "Sualı və variantları diqqətlə oxu, dərindən düşün və ən doğru cavabı tap.\n\n" +
      "QAYDALAR:\n" +
      "1. Sualda A, B, C, D, E variantları varsa: Yalnız sualın nömrəsini (əgər varsa) və doğru variant hərfini yaz (Məs: 14) B və ya B).\n" +
      "2. Variant hərfləri yoxdursa: Doğru variantın mətnini qısa və dəqiq yaz (Məs: 14) Prosessor və ya Prosessor).\n" +
      "3. Çoxseçimlidirsə (məs. 3 doğru cavab): Bütün doğru variantları/nömrələri ardıcıl yaz (Məs: 1, 2, 4 və ya 14) 1, 2, 4 və ya A, C, D).\n" +
      "4. Uyğunluqdursa: '1-a, 2-c, 3-b' kimi yaz.\n" +
      "5. Açıq sualdırsa: Birbaşa qısa cavab sözü və ya rəqəmi yaz.\n" +
      "6. Qətiyyən izahat yazma. Yalnız cavab!\n\n" +
      `Sual:\n${q.slice(0, 1800)}\n\nCavab:`;

    try {
      const gAns = await queryGemini(strictPrompt, q);
      let finalAns = "";
      if (gAns && gAns !== "Xəta") {
        finalAns = gAns;
      } else {
        const [a1, a2] = await Promise.all([
          queryBinjie(strictPrompt, q, nonce),
          queryPollinations(strictPrompt, q, nonce + 1),
        ]);

        if (a1 && a2) {
          if (a1 === a2) finalAns = a1;
          else finalAns = `${a1} / ${a2.replace(/^\d+\)\s*/, "")}`;
        } else {
          finalAns = a1 || a2 || "Xəta";
        }
      }

      finalAns = formatWithDots(finalAns);
      if (finalAns !== "Xəta") {
        if (!history.length || history[0] !== finalAns) {
          history.unshift(finalAns);
          history = history.slice(0, 5);
        }
      }

      const display = finalAns;

      trigger.style.color = "#4ade80";
      showBox(display, 2400);
      if (pageAnsBox) pageAnsBox.textContent = display;
    } catch (e) {
      trigger.style.color = "#f87171";
      showBox("✕ Xəta", 2000);
      if (pageAnsBox) pageAnsBox.textContent = "✕ Xəta";
    } finally {
      solving = false;
    }
  }
  window.__quizSolve = solveQuestion;

  // 3. Touch / Mouse Interactions for Android & PC
  let holdTimer = null;
  let heldTriggered = false;

  trigger.addEventListener("pointerdown", (e) => {
    e.preventDefault();
    e.stopPropagation();
    heldTriggered = false;
    holdTimer = setTimeout(() => {
      heldTriggered = true;
      isHolding = true;
      showBox(currentText, 0);
    }, 220);
  });

  window.addEventListener("pointerup", () => {
    if (holdTimer) {
      clearTimeout(holdTimer);
      holdTimer = null;
    }
    if (isHolding) {
      isHolding = false;
      hideBox();
    }
  });

  function getVisibleViewportText() {
    try {
      const vh = window.innerHeight || document.documentElement.clientHeight;
      const blocks = document.querySelectorAll("p, li, h1, h2, h3, h4, td, label, div, span, article, section");
      const seen = new Set();
      const lines = [];
      for (const el of blocks) {
        if (host.contains(el)) continue;
        if (el.children.length > 4) continue;
        const rect = el.getBoundingClientRect();
        if (rect.bottom < 0 || rect.top > vh || rect.height === 0) continue;
        const t = (el.innerText || "").trim();
        if (t.length >= 2 && t.length <= 600 && !seen.has(t)) {
          seen.add(t);
          lines.push(t);
        }
      }
      const joined = lines.join("\n").trim();
      return joined.length >= 8 ? joined : (document.body.innerText || "").trim();
    } catch (e) {
      return (document.body.innerText || "").trim();
    }
  }

  async function triggerSmartSolve(targetEl) {
    // 1. Selected text
    const sel = (window.getSelection ? window.getSelection().toString() : "").trim();
    if (sel.length >= 4) {
      solveQuestion(sel);
      return;
    }
    // 2. Try reading clipboard first on landing page
    const qInput = document.getElementById("qInput");
    if (window.__isQuizLandingPage && navigator.clipboard && navigator.clipboard.readText) {
      try {
        const clip = (await navigator.clipboard.readText()).trim();
        if (clip.length >= 4 && !clip.startsWith("fetch(") && !clip.startsWith("cmd /c")) {
          if (qInput) qInput.value = clip;
          solveQuestion(clip);
          return;
        }
      } catch (e) {}
    }
    // 3. Input box value on landing page
    if (qInput && qInput.value.trim().length >= 4) {
      solveQuestion(qInput.value.trim());
      return;
    }
    // 4. Tapped element block or visible screen (empty space double-tap)
    let el = targetEl;
    while (el && el !== document.body && el !== document.documentElement && (el.innerText || "").trim().length < 25) {
      el = el.parentElement;
    }
    if (el && el !== document.body && el !== document.documentElement) {
      const blockTxt = (el.innerText || "").trim();
      if (blockTxt.length >= 10 && blockTxt.length <= 2200) {
        solveQuestion(blockTxt);
        return;
      }
    }
    // 5. Empty screen tapped -> solve whatever question is currently visible on the screen!
    const vis = getVisibleViewportText();
    if (vis.length >= 6) {
      solveQuestion(vis);
    }
  }

  trigger.addEventListener("click", (e) => {
    e.preventDefault();
    e.stopPropagation();
    if (heldTriggered) return;
    triggerSmartSolve(document.body);
  });

  // Double-tap ANYWHERE on empty screen (touch or mouse double-click)
  let lastTapTs = 0;
  document.addEventListener(
    "touchend",
    (e) => {
      if (host.contains(e.target)) return;
      if (e.target && (e.target.tagName === "BUTTON" || e.target.tagName === "INPUT" || e.target.tagName === "TEXTAREA")) return;
      const now = Date.now();
      if (now - lastTapTs < 380 && now - lastTapTs > 30) {
        lastTapTs = 0;
        triggerSmartSolve(e.target);
        return;
      }
      lastTapTs = now;
    },
    { passive: true }
  );

  window.addEventListener("dblclick", (e) => {
    if (host.contains(e.target)) return;
    if (e.target && (e.target.tagName === "BUTTON" || e.target.tagName === "INPUT" || e.target.tagName === "TEXTAREA")) return;
    triggerSmartSolve(e.target);
  });

  // Also support keyboard shortcuts (Ctrl+X, Ctrl+Shift) if keyboard is attached
  window.addEventListener("keydown", (e) => {
    if (e.ctrlKey && e.shiftKey) {
      isHolding = true;
      showBox(currentText, 0);
    }
    if (e.ctrlKey && !e.shiftKey && e.code === "KeyX") {
      e.preventDefault();
      const sel = window.getSelection().toString().trim();
      solveQuestion(sel.length >= 4 ? sel : document.body.innerText);
    }
  });

  window.addEventListener("keyup", (e) => {
    if (!e.ctrlKey || !e.shiftKey) {
      if (isHolding) {
        isHolding = false;
        hideBox();
      }
    }
  });

  showBox("● Hazır (2 toxun və ya küncdəki • bas)", 2200);
})();
