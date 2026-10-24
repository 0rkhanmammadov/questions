import tkinter as tk
from pathlib import Path
from unittest.mock import patch

import pytest
from pynput import keyboard, mouse

from ai_client import AIClientError, solve_image, solve_text
from capture import capture_region_png, normalize_box
from config import load_config, save_config
from hotkeys import VK_C, VK_Q, VK_S, HotkeyManager, is_key_letter
from overlay import StealthOverlay


def test_config_roundtrip(tmp_path: Path) -> None:
    cfg_file = tmp_path / "test_config.json"
    cfg = load_config(cfg_file)
    assert cfg["provider"] in ("gemini", "free")
    cfg["provider"] = "openai"
    cfg["openai_api_key"] = "sk-test-123"
    save_config(cfg, cfg_file)

    loaded = load_config(cfg_file)
    assert loaded["provider"] == "openai"
    assert loaded["openai_api_key"] == "sk-test-123"


def test_normalize_box() -> None:
    # Too small
    assert normalize_box(100, 100, 110, 105) is None
    # Valid normal drag
    assert normalize_box(100, 200, 300, 280) == (100, 200, 200, 80)
    # Valid reverse drag (bottom-right to top-left)
    assert normalize_box(300, 280, 100, 200) == (100, 200, 200, 80)


def test_capture_region_png() -> None:
    from PIL import Image
    fake_img = Image.new("RGB", (110, 80), color=(255, 255, 255))
    with patch("capture.ImageGrab.grab", return_value=fake_img):
        png_data = capture_region_png(10, 10, 120, 90)
        assert png_data is not None
        assert png_data.startswith(b"\x89PNG\r\n\x1a\n")
    # Too small region returns None
    assert capture_region_png(10, 10, 20, 15) is None


def test_ai_client_missing_key() -> None:
    with pytest.raises(AIClientError):
        solve_text("Sual?", {"provider": "gemini", "gemini_api_key": ""})
    with pytest.raises(AIClientError):
        solve_text("Sual?", {"provider": "openai", "openai_api_key": ""})


def test_ai_client_gemini_and_openai_mock() -> None:
    gemini_cfg = {"provider": "gemini", "gemini_api_key": "fake-key"}
    with patch("ai_client._post_json") as mock_post:
        mock_post.return_value = {
            "candidates": [{"content": {"parts": [{"text": "14) B) Bakı\n"}]}}]
        }
        ans = solve_text("14. Azərbaycanın paytaxtı?", gemini_cfg)
        assert ans == "14) B"

        img_ans = solve_image(b"fake_png_bytes", gemini_cfg)
        assert img_ans == "14) B"

    openai_cfg = {"provider": "openai", "openai_api_key": "sk-fake"}
    with patch("ai_client._post_json") as mock_post:
        mock_post.return_value = {
            "choices": [{"message": {"content": "15) 1, 2, 3"}}]
        }
        ans = solve_text("15. Cavab nədir?", openai_cfg)
        assert ans == "15) 1-2-3"


def test_hotkey_state_machine() -> None:
    events = []
    hm = HotkeyManager(
        on_region_capture=lambda x1, y1, x2, y2: events.append(("region", x1, y1, x2, y2)),
        on_clipboard_capture=lambda x, y: events.append(("clipboard",)),
        on_show_overlay=lambda x, y: events.append(("show",)),
        on_hide_overlay=lambda: events.append(("hide",)),
        on_open_settings=lambda: events.append(("settings",)),
        on_quit=lambda: events.append(("quit",)),
    )

    # Test Ctrl + Shift hold shows overlay, releasing Shift hides overlay
    hm._on_key_press(keyboard.Key.ctrl_l)
    assert not hm.overlay_shown
    hm._on_key_press(keyboard.Key.shift_l)
    assert hm.overlay_shown
    assert ("show",) in events

    hm._on_key_release(keyboard.Key.shift_l)
    assert not hm.overlay_shown
    assert ("hide",) in events

    # Test Ctrl + Left Mouse Drag triggers region capture
    hm._on_mouse_click(100, 100, mouse.Button.left, True)
    hm._on_mouse_click(250, 180, mouse.Button.left, False)
    assert ("region", 100, 100, 250, 180) in events

    hm._on_key_release(keyboard.Key.ctrl_l)

    # Test Ctrl+C control char recognition
    ctrl_c_key = keyboard.KeyCode.from_char("\x03")
    assert is_key_letter(ctrl_c_key, "C", VK_C)
    ctrl_s_key = keyboard.KeyCode.from_char("\x13")
    assert is_key_letter(ctrl_s_key, "S", VK_S)
    ctrl_q_key = keyboard.KeyCode.from_char("\x11")
    assert is_key_letter(ctrl_q_key, "Q", VK_Q)


def test_stealth_overlay_states() -> None:
    root = tk.Tk()
    try:
        cfg = {
            "opacity": 0.75,
            "font_size": 10,
            "position_mode": "cursor",
            "exclude_from_capture": True,
            "flash_indicator_enabled": True,
        }
        ov = StealthOverlay(root, lambda: cfg)
        assert ov.status_state == "ready"

        ov.set_loading("Oxunur...")
        ov.show(300, 300)
        ov._process_queue()
        assert ov.status_state == "loading"
        assert ov.is_visible

        ov.set_answer("A) Doğru variant")
        ov._process_queue()
        assert ov.status_state == "answer"
        assert ov.text_label.cget("text") == "A) Doğru variant"

        ov.hide()
        ov._process_queue()
        assert not ov.is_visible
    finally:
        root.destroy()


def test_settings_window_build() -> None:
    from settings_ui import SettingsWindow
    root = tk.Tk()
    try:
        root.withdraw()
        cfg = load_config()
        saved = {}
        sw = SettingsWindow(root, lambda: cfg, lambda new_c: saved.update(new_c))
        sw.open_or_focus()
        assert sw.win is not None
        assert sw.win.winfo_exists()
        collected = sw._collect_form_config()
        assert "provider" in collected
        sw.win.destroy()
    finally:
        root.destroy()


def test_local_db_and_dot_mode(tmp_path: Path) -> None:
    from ai_client import format_with_dots
    from local_db import search_local_db

    db_file = tmp_path / "baza.txt"
    db_file.write_text(
        "14. Azərbaycanın paytaxtı hansı şəhərdir?\nCavab: B\n---\n15. 5 + 5 neçə edir?\n* C\n",
        encoding="utf-8",
    )
    hit = search_local_db("14. Azərbaycanın paytaxtı hansı şəhərdir? A) Gəncə B) Bakı", db_file)
    assert hit == "B"

    hit2 = search_local_db("15. 5 + 5 neçə edir?", db_file)
    assert hit2 == "C"

    assert format_with_dots("14) B ✓✓", "dots_only") == "••"
    assert format_with_dots("14) C ✓✓", "both") == "14) C (•••) ✓✓"
    assert format_with_dots("14) B ✓✓", "off") == "14) B ✓✓"


