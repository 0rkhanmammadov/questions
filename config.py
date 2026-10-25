import base64
import json
import os
import sys
from pathlib import Path
from typing import Any, Dict


def get_default_config_path() -> Path:
    if getattr(sys, "frozen", False):
        exe_dir = Path(sys.executable).resolve().parent
        if (exe_dir / "config.json").exists():
            return exe_dir / "config.json"
        if hasattr(sys, "_MEIPASS"):
            bundled = Path(sys._MEIPASS) / "config.json"
            if bundled.exists():
                return bundled
        return exe_dir / "config.json"
    return Path(__file__).resolve().parent / "config.json"


DEFAULT_CONFIG_PATH = get_default_config_path()

_BUILTIN_GEMINI_KEY = base64.b64decode(
    b"QVEuQWI4Uk42SVdXa0ZfanFFWUV0SU93cmlEZlIxb0R2aHRES0FNVWNNYmVDeFpPN1RINGc="
).decode("ascii")

DEFAULT_CONFIG: Dict[str, Any] = {
    "provider": "gemini",  # "free", "gemini", or "openai"
    "gemini_api_key": _BUILTIN_GEMINI_KEY,
    "gemini_model": "gemini-3.5-flash-lite",
    "openai_api_key": "",
    "openai_model": "gpt-4o-mini",
    "opacity": 0.78,
    "font_size": 10,
    "position_mode": "cursor",  # "cursor", "bottom_right", "bottom_left", "top_right"
    "exclude_from_capture": True,
    "ctrl_c_enabled": True,
    "flash_indicator_enabled": True,
    "tray_enabled": True,
    "dual_ai_enabled": True,
    "dot_mode": "off",  # "off", "both", "dots_only"
    "history_enabled": True,
    "local_db_enabled": True,
    "system_prompt": (
        "QAYDA: Yalnız doğru cavab variantındakı tam cümləni / mətni yaz. "
        "Təkcə variant hərfini yazma, mütləq variantdakı cümləni də ver (Məsələn: 'B) Bakı şəhəri' və ya 'Bakı şəhəri'). "
        "Əlavə izahat, nəzəri şərh yazma, yalnız variantdakı cümləni ver."
    ),
}


def load_config(path: Path | str | None = None) -> Dict[str, Any]:
    target = Path(path) if path else get_default_config_path()
    config = dict(DEFAULT_CONFIG)
    if target.exists():
        try:
            with open(target, "r", encoding="utf-8") as f:
                data = json.load(f)
            if isinstance(data, dict):
                config.update(data)
        except Exception:
            pass
    if not config.get("gemini_api_key"):
        if os.environ.get("GEMINI_API_KEY"):
            config["gemini_api_key"] = os.environ["GEMINI_API_KEY"]
        else:
            config["gemini_api_key"] = _BUILTIN_GEMINI_KEY
    if not config.get("openai_api_key") and os.environ.get("OPENAI_API_KEY"):
        config["openai_api_key"] = os.environ["OPENAI_API_KEY"]
    return config


def save_config(config: Dict[str, Any], path: Path | str | None = None) -> None:
    target = Path(path) if path else DEFAULT_CONFIG_PATH
    merged = dict(DEFAULT_CONFIG)
    merged.update(config)
    with open(target, "w", encoding="utf-8") as f:
        json.dump(merged, f, ensure_ascii=False, indent=2)
