import os
import threading
import tkinter as tk
from typing import Any, Dict, Optional, Tuple

import pyperclip
import pystray
from PIL import Image, ImageDraw

from ai_client import AIClientError, extract_text_from_image_bytes, solve_image, solve_text
from capture import (
    capture_fullscreen_png,
    capture_region_png,
    enable_dpi_awareness,
    get_clipboard_text,
)
from config import load_config
from hotkeys import HotkeyManager
from overlay import StealthOverlay
from settings_ui import SettingsWindow


def create_tray_icon_image() -> Image.Image:
    img = Image.new("RGBA", (64, 64), (0, 0, 0, 0))
    draw = ImageDraw.Draw(img)
    draw.ellipse((12, 12, 52, 52), fill=(39, 39, 42, 235), outline=(82, 82, 91, 255), width=3)
    draw.ellipse((24, 24, 40, 40), fill=(34, 197, 94, 255))
    return img


def _short_preview(text: str, max_len: int = 38) -> str:
    clean = " ".join((text or "").split())
    if len(clean) > max_len:
        return clean[:max_len] + "..."
    return clean


class QuizAssistantApp:
    def __init__(self) -> None:
        enable_dpi_awareness()
        self.config: Dict[str, Any] = load_config()
        self._req_counter: int = 0
        self._last_solved_clipboard: str = get_clipboard_text()
        self._lock = threading.Lock()

        self.root = tk.Tk()
        self.root.title("System Host Service")
        self.overlay = StealthOverlay(self.root, self.get_config)
        self.settings_ui = SettingsWindow(self.root, self.get_config, self.on_config_updated)

        self.hotkeys = HotkeyManager(
            on_region_capture=self.handle_region_capture,
            on_clipboard_capture=self.handle_clipboard_capture,
            on_show_overlay=self.overlay.show,
            on_hide_overlay=self.overlay.hide,
            on_open_settings=lambda: self.overlay.invoke_main(self.settings_ui.open_or_focus),
            on_quit=lambda: self.overlay.invoke_main(self.quit_app),
            on_fullscreen_capture=self.handle_fullscreen_capture,
        )
        self.tray_icon: Optional[pystray.Icon] = None

    def get_config(self) -> Dict[str, Any]:
        return self.config

    def on_config_updated(self, new_cfg: Dict[str, Any]) -> None:
        self.config = new_cfg
        self.overlay.refresh_styles()
        if self._has_active_api_key():
            if self.overlay.status_state == "error":
                self.overlay.status_state = "ready"
                self.overlay.answer_text = ""

    def _has_active_api_key(self) -> bool:
        prov = self.config.get("provider", "free").lower()
        if prov == "free":
            return True
        if prov == "openai":
            return bool((self.config.get("openai_api_key") or "").strip())
        return bool((self.config.get("gemini_api_key") or "").strip())

    def _next_request_id(self) -> int:
        with self._lock:
            self._req_counter += 1
            return self._req_counter

    def _solve_png_bytes_async(self, req_id: int, png_bytes: bytes, label: str = "Skrinşot") -> None:
        def worker() -> None:
            try:
                prov = (self.config.get("provider") or "free").lower()
                if prov == "free":
                    ocr_text = extract_text_from_image_bytes(png_bytes)
                    if not ocr_text or len(ocr_text.strip()) < 2:
                        if req_id == self._req_counter:
                            self.overlay.set_error(f"[#{req_id}] Şəkildə mətn tapılmadı")
                        return
                    preview = _short_preview(ocr_text)
                    if req_id == self._req_counter:
                        self.overlay.set_loading(f"[#{req_id}] Şəkildən həll edilir: {preview}")
                    ans = solve_text(ocr_text, self.config)
                    if req_id == self._req_counter:
                        self.overlay.set_answer(f"[#{req_id}] {ans}  ({preview})")
                else:
                    ans = solve_image(png_bytes, self.config)
                    if req_id == self._req_counter:
                        self.overlay.set_answer(f"[#{req_id}] {ans}")
            except AIClientError as e:
                if req_id == self._req_counter:
                    self.overlay.set_error(f"[#{req_id}] {e}")
            except Exception as e:
                if req_id == self._req_counter:
                    self.overlay.set_error(f"[#{req_id}] Xəta: {str(e)[:60]}")

        threading.Thread(target=worker, daemon=True).start()

    def handle_region_capture(self, x1: int, y1: int, x2: int, y2: int) -> None:
        req_id = self._next_request_id()
        self.overlay.flash_capture(x2, y2)
        self.overlay.set_loading(f"[#{req_id}] Skrinşot oxunur...")

        png_bytes = capture_region_png(x1, y1, x2, y2)
        if not png_bytes:
            self.overlay.set_error(f"[#{req_id}] Sahə çox kiçikdir, sualı tam əhatə edin")
            return
        self._solve_png_bytes_async(req_id, png_bytes, "Sahə")

    def handle_fullscreen_capture(self, cx: int, cy: int) -> None:
        req_id = self._next_request_id()
        self.overlay.flash_capture(cx, cy)
        self.overlay.set_loading(f"[#{req_id}] Tam ekran skrinşotu oxunur...")

        png_bytes = capture_fullscreen_png()
        if not png_bytes:
            self.overlay.set_error(f"[#{req_id}] Ekran şəkli çəkilə bilmədi")
            return
        self._solve_png_bytes_async(req_id, png_bytes, "Ekran")

    def handle_clipboard_capture(
        self,
        cx: int,
        cy: int,
        last_drag_box: Optional[Tuple[int, int, int, int]] = None,
    ) -> None:
        if not self.config.get("ctrl_c_enabled", True):
            return
        text = get_clipboard_text()

        # If copying is blocked on the page (clipboard empty or unchanged), automatically screenshot!
        if len(text) < 3 or text == self._last_solved_clipboard:
            if last_drag_box is not None:
                x1, y1, x2, y2 = last_drag_box
                self.handle_region_capture(x1, y1, x2, y2)
            else:
                self.handle_fullscreen_capture(cx, cy)
            return

        self._last_solved_clipboard = text
        try:
            pyperclip.copy("")
            self._last_solved_clipboard = ""
        except Exception:
            pass

        req_id = self._next_request_id()
        preview = _short_preview(text)
        self.overlay.flash_capture(cx, cy)
        self.overlay.set_loading(f"[#{req_id}] Mətn həll edilir: {preview}")

        def worker() -> None:
            try:
                ans = solve_text(text, self.config)
                if req_id == self._req_counter:
                    self.overlay.set_answer(f"[#{req_id}] {ans}  ({preview})")
            except AIClientError as e:
                if req_id == self._req_counter:
                    self.overlay.set_error(f"[#{req_id}] {e}")
            except Exception as e:
                if req_id == self._req_counter:
                    self.overlay.set_error(f"[#{req_id}] Xəta: {str(e)[:60]}")

        threading.Thread(target=worker, daemon=True).start()

    def _start_tray(self) -> None:
        if not self.config.get("tray_enabled", True):
            return
        menu = pystray.Menu(
            pystray.MenuItem(
                "Ayarlar (Ctrl+Shift+S)",
                lambda icon, item: self.overlay.invoke_main(self.settings_ui.open_or_focus),
                default=True,
            ),
            pystray.MenuItem(
                "Çıxış (Ctrl+Shift+Q)",
                lambda icon, item: self.overlay.invoke_main(self.quit_app),
            ),
        )
        self.tray_icon = pystray.Icon(
            "WinAudioService",
            create_tray_icon_image(),
            "Sistem Xidməti (Hazır)",
            menu,
        )
        threading.Thread(target=self.tray_icon.run, daemon=True).start()

    def quit_app(self) -> None:
        try:
            self.hotkeys.stop()
        except Exception:
            pass
        try:
            if self.tray_icon:
                self.tray_icon.stop()
        except Exception:
            pass
        try:
            self.root.quit()
            self.root.destroy()
        except Exception:
            pass
        os._exit(0)

    def run(self) -> None:
        self.hotkeys.start()
        self._start_tray()
        if not self._has_active_api_key():
            self.overlay.set_error("API açarı yoxdur (Ctrl+Shift+S)")
            self.root.after(200, self.settings_ui.open_or_focus)
        else:
            sw = self.root.winfo_screenwidth()
            sh = self.root.winfo_screenheight()
            self.root.after(150, lambda: self.overlay.show(sw // 2 - 60, sh // 2 - 20))
            self.root.after(2000, self.overlay.hide)
        self.root.mainloop()


if __name__ == "__main__":
    app = QuizAssistantApp()
    app.run()
