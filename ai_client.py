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


def _post_json(url: str, payload: Dict[str, Any], headers: Dict[str, str], timeout: int = 25) -> Dict[str, Any]:
    data = json.dumps(payload).encode("utf-8")
    req_headers = {"User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64)"}
    req_headers.update(headers)
    req = urllib.request.Request(url, data=data, headers=req_headers, method="POST")
    try:
        with urllib.request.urlopen(req, timeout=timeout) as resp:
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


def _clean_answer(ans: str) -> str:
    text = (ans or "").replace("**", "").strip()
    lines = [line.strip() for line in text.splitlines() if line.strip()]
    cleaned = " | ".join(lines)
    if len(cleaned) > 180:
        cleaned = cleaned[:177] + "..."
    return cleaned


def extract_text_from_image_bytes(image_bytes: bytes) -> str:
    try:
        import winocr

        img = Image.open(io.BytesIO(image_bytes)).convert("RGB")
        w, h = img.size
        # Upscale for crisp OCR recognition
        scale = 2 if (h < 220 or w < 450) else 1
        if scale > 1:
            img = img.resize((w * scale, h * scale), Image.Resampling.LANCZOS)
        # Add border so letters near edges are never clipped
        img_padded = ImageOps.expand(img, border=18, fill=(255, 255, 255))

        res = winocr.recognize_pil_sync(img_padded, "en-US")
        lines = [line.get("text", "").strip() for line in res.get("lines", []) if line.get("text", "").strip()]
        text = "\n".join(lines).strip()
        if not text:
            text = (res.get("text") or "").strip()

        # Fallback: try high-contrast grayscale if text is too short
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
    nonce = random.randint(100000, 999999)
    user_msg = (
        f"[Sorğu ID: {nonce} — Yalnız aşağıdakı yeni sualı həll et, əvvəlki sualları unut]\n"
        f"Sual və variantlar:\n{cleaned_q}\n\n"
        f"Yalnız doğru cavab variantını (hərf və cavab mətni) qısa yaz:"
    )

    # 1. Try fast multi-provider g4f first (no 30s cooldown)
    try:
        import g4f

        resp = g4f.ChatCompletion.create(
            model=g4f.models.default,
            messages=[
                {"role": "system", "content": system_prompt},
                {"role": "user", "content": user_msg},
            ],
        )
        if isinstance(resp, str) and resp.strip():
            cleaned = _clean_answer(resp)
            if cleaned and "rate limit" not in cleaned.lower():
                return cleaned
    except Exception:
        pass

    # 2. Fallback to Pollinations OpenAI endpoint with unique random seed (prevents caching)
    url = "https://text.pollinations.ai/openai"
    headers = {"Content-Type": "application/json"}
    payload = {
        "model": "openai",
        "seed": nonce,
        "messages": [
            {"role": "system", "content": system_prompt},
            {"role": "user", "content": user_msg},
        ],
        "temperature": 0.1,
    }
    for attempt in range(2):
        try:
            res = _post_json(url, payload, headers, timeout=25)
            content = res["choices"][0]["message"]["content"]
            return _clean_answer(content)
        except AIClientError as e:
            err_str = str(e)
            m = re.search(r"Retry after (\d+)s", err_str)
            if attempt == 0 and m:
                wait_sec = min(int(m.group(1)) + 1, 25)
                time.sleep(wait_sec)
                continue
            raise
        except (KeyError, IndexError):
            raise AIClientError("Pulsuz AI-dan cavab oxuna bilmədi")
    raise AIClientError("Pulsuz AI cavab vermədi")


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
                {"role": "user", "content": f"Sual və variantlar:\n{cleaned_q}"},
            ],
            "temperature": 0.1,
            "max_tokens": 200,
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
                        {"text": f"Sual və variantlar:\n{cleaned_q}"}
                    ]
                }
            ],
            "generationConfig": {"temperature": 0.1, "maxOutputTokens": 200},
        }
        res = _post_json(url, payload, headers)
        try:
            parts = res["candidates"][0]["content"]["parts"]
            content = "".join(p.get("text", "") for p in parts)
            return _clean_answer(content)
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
            raise AIClientError("Şəkildə mətn tapılmadı (sualı tam əhatə edin)")
        return _solve_free_text(ocr_text, system_prompt)

    b64_img = base64.b64encode(image_bytes).decode("ascii")
    user_instruction = (
        "Bu şəkildəki imtahan/test sualını və cavab variantlarını oxu. "
        "Yalnız doğru cavab variantını (məsələn: 'C) Cavab') və ya qısa dəqiq cavabı yaz."
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
            "max_tokens": 200,
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
            "generationConfig": {"temperature": 0.1, "maxOutputTokens": 200},
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
        ans = solve_text("2 + 2 neçə edir? A) 3  B) 4  C) 5", config)
        return True, f"Uğurlu! Test cavabı: {ans}"
    except AIClientError as e:
        return False, str(e)
    except Exception as e:
        return False, f"Xəta: {e}"
