import json
import os
from pathlib import Path
from typing import Any, Dict

DEFAULT_CONFIG_PATH = Path(__file__).resolve().parent / "config.json"

DEFAULT_CONFIG: Dict[str, Any] = {
    "provider": "free",  # "free", "gemini", or "openai"
    "gemini_api_key": "",
    "gemini_model": "gemini-2.5-flash",
    "openai_api_key": "",
    "openai_model": "gpt-4o-mini",
    "opacity": 0.78,
    "font_size": 10,
    "position_mode": "cursor",  # "cursor", "bottom_right", "bottom_left", "top_right"
    "exclude_from_capture": True,
    "ctrl_c_enabled": True,
    "flash_indicator_enabled": True,
    "tray_enabled": True,
    "system_prompt": (
        "QAYDA: Heç bir izahat, cümlə və ya söz YAZMA! "
        "Yalnız sualın nömrəsini (əgər varsa) və doğru variant hərfini və ya rəqəmləri yaz. "
        "Nümunə: '14) B' və ya '15) 1-2-3' və ya 'C'."
    ),
}


def load_config(path: Path | str | None = None) -> Dict[str, Any]:
    target = Path(path) if path else DEFAULT_CONFIG_PATH
    config = dict(DEFAULT_CONFIG)
    if target.exists():
        try:
            with open(target, "r", encoding="utf-8") as f:
                data = json.load(f)
            if isinstance(data, dict):
                config.update(data)
        except Exception:
            pass
    if not config.get("gemini_api_key") and os.environ.get("GEMINI_API_KEY"):
        config["gemini_api_key"] = os.environ["GEMINI_API_KEY"]
    if not config.get("openai_api_key") and os.environ.get("OPENAI_API_KEY"):
        config["openai_api_key"] = os.environ["OPENAI_API_KEY"]
    return config


def save_config(config: Dict[str, Any], path: Path | str | None = None) -> None:
    target = Path(path) if path else DEFAULT_CONFIG_PATH
    merged = dict(DEFAULT_CONFIG)
    merged.update(config)
    with open(target, "w", encoding="utf-8") as f:
        json.dump(merged, f, ensure_ascii=False, indent=2)
