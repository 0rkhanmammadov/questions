import base64
import io
import json
import random
import re
import time
import urllib.error
import urllib.request
from typing import Any, Dict, Tuple

from PIL import Image, ImageOps


class AIClientError(Exception):
    pass


def _urlopen_direct(req: urllib.request.Request, timeout: int = 20):
    import ssl
    ctx = ssl.create_default_context()
    ctx.check_hostname = False
    ctx.verify_mode = ssl.CERT_NONE
    try:
        # First try direct connection bypassing any restrictive Windows system proxy
        opener = urllib.request.build_opener(
            urllib.request.ProxyHandler({}),
            urllib.request.HTTPSHandler(context=ctx),
        )
        return opener.open(req, timeout=timeout)
    except Exception:
        # Fallback to system default proxy if direct connection is not available
        opener_sys = urllib.request.build_opener(urllib.request.HTTPSHandler(context=ctx))
        return opener_sys.open(req, timeout=timeout)


def _post_json(url: str, payload: Dict[str, Any], headers: Dict[str, str], timeout: int = 25) -> Dict[str, Any]:
    data = json.dumps(payload).encode("utf-8")
    req_headers = {"User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64)"}
    req_headers.update(headers)
    req = urllib.request.Request(url, data=data, headers=req_headers, method="POST")
    try:
        with _urlopen_direct(req, timeout=timeout) as resp:
            raw = resp.read().decode("utf-8")
            return json.loads(raw)
    except urllib.error.HTTPError as e:
        try:
            err_body = e.read().decode("utf-8")
            err_json = json.loads(err_body)
            if "error" in err_json and isinstance(err_json["error"], dict):
                msg = err_json["error"].get("message", "")
                if msg:
                    raise AIClientError(f"API ({e.code}): {msg[:90]}")
        except AIClientError:
            raise
        except Exception:
            pass
        if e.code in (401, 403):
            raise AIClientError("API açarı yanlışdır və ya icazə yoxdur")
        elif e.code == 429:
            raise AIClientError("Limit aşılıb (429) - bir neçə saniyə gözləyin")
        raise AIClientError(f"HTTP Xətası: {e.code}")
    except urllib.error.URLError as e:
        raise AIClientError(f"Şəbəkə xətası: {e.reason}")
    except Exception as e:
        raise AIClientError(f"Xəta: {str(e)[:80]}")


def _extract_question_num(question_text: str) -> str:
    if not question_text:
        return ""
    m = re.match(r"^\s*(?:Sual\s*|№\s*|#\s*)?(\d{1,3})\s*[\.\)\-:]", question_text, flags=re.IGNORECASE)
    if m:
        return m.group(1)
    return ""


def _clean_answer(ans: str, question_text: str = "") -> str:
    text = (ans or "").replace("**", "").replace("*", "").strip()
    lines = [line.strip() for line in text.splitlines() if line.strip()]
    first_line = lines[0] if lines else text

    q_num = _extract_question_num(question_text)

    # Check if AI already returned "14) B" or "15) 1-2-3"
    m_prefixed = re.match(r"^(\d{1,3})\s*[\)\.\-:]\s*([A-Ea-e])\b", first_line)
    if m_prefixed:
        return f"{m_prefixed.group(1)}) {m_prefixed.group(2).upper()}"

    m_num_seq = re.match(r"^(\d{1,3})\s*[\)\.\-:]\s*(\d+(?:\s*[-,]\s*\d+)+)", first_line)
    if m_num_seq:
        seq = re.sub(r"\s*[,]\s*", "-", m_num_seq.group(2).strip())
        seq = re.sub(r"\s+", "", seq)
        return f"{m_num_seq.group(1)}) {seq}"

    # Check if first line starts with option letter like "B) Bakı" or "Cavab: B" or just "B"
    m_opt = re.search(r"(?:^|Cavab\s*[:\-]?\s*|Option\s*[:\-]?\s*|Variant\s*[:\-]?\s*)([A-Ea-e])(?:\s*[\)\.\-:]|\s*$)", first_line, flags=re.IGNORECASE)
    if m_opt:
        letter = m_opt.group(1).upper()
        return f"{q_num}) {letter}" if q_num else letter

    # Check if it's a number sequence like "1-2-3" or "1, 2, 3"
    m_seq = re.match(r"^(\d+(?:\s*[-,]\s*\d+)+)$", first_line)
    if m_seq:
        seq = re.sub(r"\s*[,]\s*", "-", m_seq.group(1).strip())
        seq = re.sub(r"\s+", "", seq)
        return f"{q_num}) {seq}" if q_num else seq

    cleaned = first_line[:45].strip()
    if q_num and not cleaned.startswith(f"{q_num})"):
        return f"{q_num}) {cleaned}"
    return cleaned


def extract_text_from_image_bytes(image_bytes: bytes) -> str:
    try:
        import winocr

        img = Image.open(io.BytesIO(image_bytes)).convert("RGB")
        w, h = img.size
        scale = 2 if (h < 220 or w < 450) else 1
        if scale > 1:
            img = img.resize((w * scale, h * scale), Image.Resampling.LANCZOS)
        img_padded = ImageOps.expand(img, border=18, fill=(255, 255, 255))

        res = winocr.recognize_pil_sync(img_padded, "en-US")
        lines = [line.get("text", "").strip() for line in res.get("lines", []) if line.get("text", "").strip()]
        text = "\n".join(lines).strip()
        if not text:
            text = (res.get("text") or "").strip()

        if len(text) < 4:
            gray = ImageOps.autocontrast(img.convert("L")).convert("RGB")
            gray_padded = ImageOps.expand(gray, border=18, fill=(255, 255, 255))
            res2 = winocr.recognize_pil_sync(gray_padded, "en-US")
            lines2 = [line.get("text", "").strip() for line in res2.get("lines", []) if line.get("text", "").strip()]
            text2 = "\n".join(lines2).strip()
            if len(text2) > len(text):
                text = text2

        return text
    except Exception as e:
        raise AIClientError(f"OCR xətası: {str(e)[:60]}")


def _solve_free_text(cleaned_q: str, system_prompt: str) -> str:
    nonce = random.randint(1000000, 9999999)
    strict_prompt = (
        "QAYDA: Heç bir izahat, cümlə və ya söz YAZMA! "
        "Yalnız sualın nömrəsini (əgər varsa) və doğru variant hərfini (A, B, C, D, E) və ya rəqəmləri yaz.\n"
        "Nümunə formatlar:\n"
        "14) B\n"
        "15) 1-2-3\n"
        "C\n\n"
        f"Sual:\n{cleaned_q}\n\n"
        "Cavab (yalnız hərf və ya rəqəm, izahatsız):"
    )

    # 1. Pure HTTP background call to Yqcloud GPT-4 endpoint (0 browser, 0 cooldown)
    try:
        headers = {
            "accept": "application/json, text/plain, */*",
            "content-type": "application/json",
            "origin": "https://chat9.yqcloud.top",
            "referer": "https://chat9.yqcloud.top/",
            "user-agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64)",
        }
        data = json.dumps(
            {
                "prompt": strict_prompt,
                "userId": f"#/chat/{nonce}",
                "network": False,
                "system": "Respond ONLY with the question number and option letter (e.g. '14) B' or '15) 1-2-3'). Never write explanations.",
                "withoutContext": False,
                "stream": False,
            }
        ).encode("utf-8")
        req = urllib.request.Request(
            "https://api.binjie.fun/api/generateStream",
            data=data,
            headers=headers,
            method="POST",
        )
        with _urlopen_direct(req, timeout=15) as resp:
            raw_ans = resp.read().decode("utf-8").strip()
            if raw_ans:
                return _clean_answer(raw_ans, cleaned_q)
    except Exception:
        pass

    # 2. Pure HTTP fallback to Pollinations OpenAI endpoint
    url = "https://text.pollinations.ai/openai"
    headers = {"Content-Type": "application/json"}
    payload = {
        "model": "openai",
        "seed": nonce,
        "messages": [
            {"role": "system", "content": system_prompt},
            {"role": "user", "content": strict_prompt},
        ],
        "temperature": 0.1,
    }
    for attempt in range(2):
        try:
            res = _post_json(url, payload, headers, timeout=25)
            content = res["choices"][0]["message"]["content"]
            return _clean_answer(content, cleaned_q)
        except AIClientError as e:
            err_str = str(e)
            m = re.search(r"Retry after (\d+)s", err_str)
            if attempt == 0 and m:
                wait_sec = min(int(m.group(1)) + 1, 25)
                time.sleep(wait_sec)
                continue
            raise
        except (KeyError, IndexError):
            raise AIClientError("Cavab oxuna bilmədi")
    raise AIClientError("Cavab alınmadı")


def _effective_provider(config: Dict[str, Any]) -> str:
    provider = (config.get("provider") or "free").lower()
    if provider == "openai":
        if not (config.get("openai_api_key") or "").strip():
            raise AIClientError("OpenAI API açarı yoxdur (Ctrl+Shift+S)")
        return "openai"
    if provider == "gemini":
        if not (config.get("gemini_api_key") or "").strip():
            raise AIClientError("Gemini API açarı yoxdur (Ctrl+Shift+S)")
        return "gemini"
    return "free"


def solve_text(question_text: str, config: Dict[str, Any]) -> str:
    cleaned_q = (question_text or "").strip()
    if not cleaned_q:
        raise AIClientError("Boş mətn")

    provider = _effective_provider(config)
    system_prompt = config.get("system_prompt", "")

    if provider == "free":
        return _solve_free_text(cleaned_q, system_prompt)

    strict_user = (
        f"Sual:\n{cleaned_q}\n\n"
        "Yalnız sualın nömrəsini (varsa) və doğru cavab variantını yaz (məsələn: '14) B' və ya '15) 1-2-3'). İzahat yazma."
    )

    if provider == "openai":
        api_key = (config.get("openai_api_key") or "").strip()
        model = config.get("openai_model", "gpt-4o-mini").strip() or "gpt-4o-mini"
        url = "https://api.openai.com/v1/chat/completions"
        headers = {
            "Content-Type": "application/json",
            "Authorization": f"Bearer {api_key}",
        }
        payload = {
            "model": model,
            "messages": [
                {"role": "system", "content": system_prompt},
                {"role": "user", "content": strict_user},
            ],
            "temperature": 0.1,
            "max_tokens": 50,
        }
        res = _post_json(url, payload, headers)
        try:
            content = res["choices"][0]["message"]["content"]
            return _clean_answer(content, cleaned_q)
        except (KeyError, IndexError):
            raise AIClientError("OpenAI-dan cavab oxuna bilmədi")
    else:
        api_key = (config.get("gemini_api_key") or "").strip()
        model = config.get("gemini_model", "gemini-2.5-flash").strip() or "gemini-2.5-flash"
        url = f"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={api_key}"
        headers = {"Content-Type": "application/json"}
        payload = {
            "systemInstruction": {"parts": [{"text": system_prompt}]},
            "contents": [{"parts": [{"text": strict_user}]}],
            "generationConfig": {"temperature": 0.1, "maxOutputTokens": 50},
        }
        res = _post_json(url, payload, headers)
        try:
            parts = res["candidates"][0]["content"]["parts"]
            content = "".join(p.get("text", "") for p in parts)
            return _clean_answer(content, cleaned_q)
        except (KeyError, IndexError):
            raise AIClientError("Gemini-dən cavab oxuna bilmədi")


def solve_image(image_bytes: bytes, config: Dict[str, Any]) -> str:
    if not image_bytes:
        raise AIClientError("Boş şəkil")

    provider = _effective_provider(config)
    system_prompt = config.get("system_prompt", "")

    if provider == "free":
        ocr_text = extract_text_from_image_bytes(image_bytes)
        if not ocr_text or len(ocr_text.strip()) < 2:
            raise AIClientError("Şəkildə mətn tapılmadı")
        return _solve_free_text(ocr_text, system_prompt)

    b64_img = base64.b64encode(image_bytes).decode("ascii")
    user_instruction = (
        "Bu şəkildəki sualı oxu. Heç bir izahat yazma! "
        "Yalnız sualın nömrəsini (varsa) və doğru cavab hərfini və ya rəqəmləri yaz (məsələn: '14) B' və ya '15) 1-2-3')."
    )

    if provider == "openai":
        api_key = (config.get("openai_api_key") or "").strip()
        model = config.get("openai_model", "gpt-4o-mini").strip() or "gpt-4o-mini"
        url = "https://api.openai.com/v1/chat/completions"
        headers = {
            "Content-Type": "application/json",
            "Authorization": f"Bearer {api_key}",
        }
        payload = {
            "model": model,
            "messages": [
                {"role": "system", "content": system_prompt},
                {
                    "role": "user",
                    "content": [
                        {"type": "text", "text": user_instruction},
                        {
                            "type": "image_url",
                            "image_url": {"url": f"data:image/png;base64,{b64_img}"},
                        },
                    ],
                },
            ],
            "temperature": 0.1,
            "max_tokens": 50,
        }
        res = _post_json(url, payload, headers)
        try:
            content = res["choices"][0]["message"]["content"]
            return _clean_answer(content)
        except (KeyError, IndexError):
            raise AIClientError("OpenAI-dan cavab oxuna bilmədi")
    else:
        api_key = (config.get("gemini_api_key") or "").strip()
        model = config.get("gemini_model", "gemini-2.5-flash").strip() or "gemini-2.5-flash"
        url = f"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={api_key}"
        headers = {"Content-Type": "application/json"}
        payload = {
            "systemInstruction": {"parts": [{"text": system_prompt}]},
            "contents": [
                {
                    "parts": [
                        {"text": user_instruction},
                        {
                            "inline_data": {
                                "mime_type": "image/png",
                                "data": b64_img,
                            }
                        },
                    ]
                }
            ],
            "generationConfig": {"temperature": 0.1, "maxOutputTokens": 50},
        }
        res = _post_json(url, payload, headers)
        try:
            parts = res["candidates"][0]["content"]["parts"]
            content = "".join(p.get("text", "") for p in parts)
            return _clean_answer(content)
        except (KeyError, IndexError):
            raise AIClientError("Gemini-dən cavab oxuna bilmədi")


def test_connection(config: Dict[str, Any]) -> Tuple[bool, str]:
    try:
        ans = solve_text("14. 2 + 2 neçə edir? A) 3  B) 4  C) 5", config)
        return True, f"Uğurlu! Test cavabı: {ans}"
    except AIClientError as e:
        return False, str(e)
    except Exception as e:
        return False, f"Xəta: {e}"
