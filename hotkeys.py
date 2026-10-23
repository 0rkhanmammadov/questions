import inspect
import threading
import time
from typing import Callable, Optional, Tuple

from pynput import keyboard, mouse

VK_C = 0x43
VK_X = 0x58
VK_S = 0x53
VK_Q = 0x51

CTRL_CHARS = {
    "\x03": "C",
    "\x18": "X",
    "\x13": "S",
    "\x11": "Q",
}


def is_key_letter(key: object, letter: str, vk_code: int) -> bool:
    target = letter.upper()
    vk = getattr(key, "vk", None)
    if vk == vk_code:
        return True
    ch = getattr(key, "char", None)
    if ch:
        if ch.upper() == target:
            return True
        if CTRL_CHARS.get(ch) == target:
            return True
    return False


def is_ctrl_key(key: object) -> bool:
    return key in (keyboard.Key.ctrl, keyboard.Key.ctrl_l, keyboard.Key.ctrl_r)


def is_shift_key(key: object) -> bool:
    return key in (keyboard.Key.shift, keyboard.Key.shift_l, keyboard.Key.shift_r)


class HotkeyManager:
    def __init__(
        self,
        on_region_capture: Callable[[int, int, int, int], None],
        on_clipboard_capture: Callable[..., None],
        on_show_overlay: Callable[[int, int], None],
        on_hide_overlay: Callable[[], None],
        on_open_settings: Callable[[], None],
        on_quit: Callable[[], None],
        on_fullscreen_capture: Optional[Callable[[int, int], None]] = None,
    ) -> None:
        self.on_region_capture = on_region_capture
        self.on_clipboard_capture = on_clipboard_capture
        self.on_show_overlay = on_show_overlay
        self.on_hide_overlay = on_hide_overlay
        self.on_open_settings = on_open_settings
        self.on_quit = on_quit
        self.on_fullscreen_capture = on_fullscreen_capture

        self.ctrl_held: bool = False
        self.shift_held: bool = False
        self.mouse_left_held: bool = False
        self.overlay_shown: bool = False

        self.drag_start: Optional[Tuple[int, int]] = None
        self.drag_with_ctrl: bool = False
        self.last_drag_box: Optional[Tuple[int, int, int, int, float]] = None

        self._last_action_ts: float = 0.0
        self._mouse_ctrl = mouse.Controller()
        self._kb_listener: Optional[keyboard.Listener] = None
        self._mouse_listener: Optional[mouse.Listener] = None

    def start(self) -> None:
        self._kb_listener = keyboard.Listener(
            on_press=self._on_key_press,
            on_release=self._on_key_release,
        )
        self._mouse_listener = mouse.Listener(
            on_click=self._on_mouse_click,
        )
        self._kb_listener.daemon = True
        self._mouse_listener.daemon = True
        self._kb_listener.start()
        self._mouse_listener.start()

    def stop(self) -> None:
        if self._kb_listener:
            self._kb_listener.stop()
        if self._mouse_listener:
            self._mouse_listener.stop()

    def _get_cursor_pos(self) -> Tuple[int, int]:
        try:
            pos = self._mouse_ctrl.position
            return int(pos[0]), int(pos[1])
        except Exception:
            return 400, 400

    def _invoke_clipboard_cb(self, cx: int, cy: int) -> None:
        box = None
        if self.last_drag_box is not None:
            x1, y1, x2, y2, ts = self.last_drag_box
            if time.monotonic() - ts <= 15.0:
                box = (x1, y1, x2, y2)
        try:
            sig = inspect.signature(self.on_clipboard_capture)
            if len(sig.parameters) >= 3:
                self.on_clipboard_capture(cx, cy, box)
            else:
                self.on_clipboard_capture(cx, cy)
        except Exception:
            self.on_clipboard_capture(cx, cy)

    def _on_key_press(self, key: object) -> None:
        if is_ctrl_key(key):
            self.ctrl_held = True
            if self.mouse_left_held and not self.shift_held:
                self.drag_with_ctrl = True
            self._check_overlay_hold()
            return

        if is_shift_key(key):
            self.shift_held = True
            self._check_overlay_hold()
            return

        now = time.monotonic()

        # PrintScreen key -> instant screenshot solve
        if key == keyboard.Key.print_screen and self.on_fullscreen_capture:
            if now - self._last_action_ts > 0.3:
                self._last_action_ts = now
                cx, cy = self._get_cursor_pos()
                self.on_fullscreen_capture(cx, cy)
            return

        # Check Ctrl + Shift + S (Settings) and Ctrl + Shift + Q (Quit)
        if self.ctrl_held and self.shift_held:
            if is_key_letter(key, "S", VK_S):
                if now - self._last_action_ts > 0.35:
                    self._last_action_ts = now
                    self._hide_if_shown()
                    self.on_open_settings()
                return
            if is_key_letter(key, "Q", VK_Q):
                if now - self._last_action_ts > 0.35:
                    self._last_action_ts = now
                    self._hide_if_shown()
                    self.on_quit()
                return

        # Check Ctrl + X (Direct Screenshot solve) when Shift is NOT held
        if self.ctrl_held and not self.shift_held and is_key_letter(key, "X", VK_X):
            if self.on_fullscreen_capture and now - self._last_action_ts > 0.3:
                self._last_action_ts = now
                cx, cy = self._get_cursor_pos()
                self.on_fullscreen_capture(cx, cy)
            return

        # Check Ctrl + C (Clipboard capture + auto screenshot fallback if copy is blocked!)
        if self.ctrl_held and not self.shift_held and is_key_letter(key, "C", VK_C):
            if now - self._last_action_ts > 0.25:
                self._last_action_ts = now
                cx, cy = self._get_cursor_pos()
                threading.Timer(0.18, lambda: self._invoke_clipboard_cb(cx, cy)).start()

    def _on_key_release(self, key: object) -> None:
        if is_ctrl_key(key):
            self.ctrl_held = False
            self._hide_if_shown()
        elif is_shift_key(key):
            self.shift_held = False
            self._hide_if_shown()

    def _check_overlay_hold(self) -> None:
        if self.ctrl_held and self.shift_held and not self.overlay_shown:
            self.overlay_shown = True
            cx, cy = self._get_cursor_pos()
            self.on_show_overlay(cx, cy)

    def _hide_if_shown(self) -> None:
        if self.overlay_shown:
            self.overlay_shown = False
            self.on_hide_overlay()

    def _on_mouse_click(self, x: int, y: int, button: mouse.Button, pressed: bool) -> None:
        if button != mouse.Button.left:
            return

        if pressed:
            self.mouse_left_held = True
            self.drag_start = (int(x), int(y))
            self.drag_with_ctrl = bool(self.ctrl_held and not self.shift_held)
        else:
            self.mouse_left_held = False
            if self.drag_start is not None:
                x1, y1 = self.drag_start
                x2, y2 = int(x), int(y)
                was_ctrl = self.ctrl_held or self.drag_with_ctrl
                self.drag_start = None
                self.drag_with_ctrl = False

                if abs(x2 - x1) >= 15 and abs(y2 - y1) >= 10:
                    # Always remember the last dragged box so Ctrl+C can screenshot it if copy is blocked
                    self.last_drag_box = (x1, y1, x2, y2, time.monotonic())
                    if was_ctrl and not self.shift_held:
                        self.on_region_capture(x1, y1, x2, y2)
            else:
                self.drag_start = None
                self.drag_with_ctrl = False
