import ctypes
import queue
import tkinter as tk
from typing import Any, Callable, Dict, Optional

GWL_EXSTYLE = -20
WS_EX_TOOLWINDOW = 0x00000080
WS_EX_NOACTIVATE = 0x08000000
WS_EX_TRANSPARENT = 0x00000020
WS_EX_LAYERED = 0x00080000
GA_ROOT = 2

WDA_NONE = 0x00000000
WDA_EXCLUDEFROMCAPTURE = 0x00000011


def _get_root_hwnd(widget: tk.Misc) -> int:
    widget.update_idletasks()
    hwnd = widget.winfo_id()
    try:
        root_hwnd = ctypes.windll.user32.GetAncestor(hwnd, GA_ROOT)
        return root_hwnd or hwnd
    except Exception:
        return hwnd


def apply_stealth_window_styles(widget: tk.Misc, exclude_from_capture: bool = True, click_through: bool = True) -> None:
    try:
        hwnd = _get_root_hwnd(widget)
        user32 = ctypes.windll.user32
        style = user32.GetWindowLongW(hwnd, GWL_EXSTYLE)
        new_style = style | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_LAYERED
        if click_through:
            new_style |= WS_EX_TRANSPARENT
        user32.SetWindowLongW(hwnd, GWL_EXSTYLE, new_style)
        affinity = WDA_EXCLUDEFROMCAPTURE if exclude_from_capture else WDA_NONE
        user32.SetWindowDisplayAffinity(hwnd, affinity)
    except Exception:
        pass


class StealthOverlay:
    def __init__(self, root: tk.Tk, config_getter: Callable[[], Dict[str, Any]]) -> None:
        self.root = root
        self.get_config = config_getter
        self.cmd_queue: "queue.Queue[tuple]" = queue.Queue()

        self.status_state: str = "ready"  # "ready", "loading", "answer", "error"
        self.answer_text: str = ""
        self.is_visible: bool = False
        self.held_by_user: bool = False
        self._flash_after_id: Optional[str] = None
        self._autohide_after_id: Optional[str] = None

        # Main root window stays withdrawn
        self.root.withdraw()

        # Overlay Toplevel for answer / status
        self.popup = tk.Toplevel(self.root)
        self.popup.withdraw()
        self.popup.overrideredirect(True)
        self.popup.attributes("-topmost", True)
        self.popup.configure(bg="#141417")

        self.frame = tk.Frame(
            self.popup,
            bg="#141417",
            highlightbackground="#2e2e36",
            highlightthickness=1,
            padx=7,
            pady=4,
        )
        self.frame.pack(fill="both", expand=True)

        self.dot_label = tk.Label(
            self.frame,
            text="●",
            fg="#22c55e",
            bg="#141417",
            font=("Segoe UI", 9, "bold"),
        )
        self.dot_label.pack(side="left", padx=(0, 5))

        self.text_label = tk.Label(
            self.frame,
            text="Hazır",
            fg="#d4d4d8",
            bg="#141417",
            font=("Segoe UI", 10),
            justify="left",
            wraplength=420,
        )
        self.text_label.pack(side="left")

        # Micro-dot Toplevel for 0.3s capture confirmation
        self.flash_win = tk.Toplevel(self.root)
        self.flash_win.withdraw()
        self.flash_win.overrideredirect(True)
        self.flash_win.attributes("-topmost", True)
        self.flash_win.configure(bg="#141417")
        self.flash_label = tk.Label(
            self.flash_win,
            text="•",
            fg="#38bdf8",
            bg="#141417",
            font=("Segoe UI", 10, "bold"),
            padx=3,
            pady=0,
        )
        self.flash_label.pack()

        self.refresh_styles()
        self.root.after(25, self._process_queue)

    def refresh_styles(self) -> None:
        cfg = self.get_config()
        opacity = float(cfg.get("opacity", 0.78))
        opacity = max(0.15, min(1.0, opacity))
        font_size = int(cfg.get("font_size", 10))
        exclude = bool(cfg.get("exclude_from_capture", True))

        self.popup.attributes("-alpha", opacity)
        self.flash_win.attributes("-alpha", min(0.85, opacity))
        self.text_label.configure(font=("Segoe UI", font_size))
        self.dot_label.configure(font=("Segoe UI", max(8, font_size - 1), "bold"))

        apply_stealth_window_styles(self.popup, exclude_from_capture=exclude, click_through=True)
        apply_stealth_window_styles(self.flash_win, exclude_from_capture=exclude, click_through=True)

    # Thread-safe public methods
    def set_loading(self, mode_label: str = "Həll edilir...") -> None:
        self.cmd_queue.put(("set_state", "loading", mode_label))

    def set_answer(self, answer: str) -> None:
        self.cmd_queue.put(("set_state", "answer", answer))

    def set_error(self, err: str) -> None:
        self.cmd_queue.put(("set_state", "error", err))

    def show(self, cursor_x: int, cursor_y: int) -> None:
        self.cmd_queue.put(("show", cursor_x, cursor_y))

    def hide(self) -> None:
        self.cmd_queue.put(("hide",))

    def flash_capture(self, x: int, y: int) -> None:
        self.cmd_queue.put(("flash", x, y))

    def invoke_main(self, callback: Callable[[], None]) -> None:
        self.cmd_queue.put(("callback", callback))

    # Internal queue handler on Tk thread
    def _process_queue(self) -> None:
        try:
            while True:
                item = self.cmd_queue.get_nowait()
                cmd = item[0]
                if cmd == "set_state":
                    _, state, text = item
                    self.status_state = state
                    self.answer_text = text
                    self._update_labels()
                    # Automatically peek for 1.8s when loading/answer/error updates so user sees immediate feedback
                    px = self.root.winfo_pointerx()
                    py = self.root.winfo_pointery()
                    self._do_show(px, py, held=self.held_by_user)
                    if not self.held_by_user:
                        if self._autohide_after_id:
                            try:
                                self.root.after_cancel(self._autohide_after_id)
                            except Exception:
                                pass
                        dur = 900 if state == "loading" else 2200
                        self._autohide_after_id = self.root.after(dur, self._auto_hide_if_not_held)
                elif cmd == "show":
                    _, cx, cy = item
                    self.held_by_user = True
                    if self._autohide_after_id:
                        try:
                            self.root.after_cancel(self._autohide_after_id)
                        except Exception:
                            pass
                        self._autohide_after_id = None
                    self._do_show(cx, cy, held=True)
                elif cmd == "hide":
                    self.held_by_user = False
                    self._do_hide()
                elif cmd == "flash":
                    _, fx, fy = item
                    self._do_flash(fx, fy)
                elif cmd == "callback":
                    _, cb = item
                    cb()
        except queue.Empty:
            pass
        self.root.after(25, self._process_queue)

    def _update_labels(self) -> None:
        if self.status_state == "ready":
            self.dot_label.configure(text="●", fg="#22c55e")
            self.text_label.configure(text="Hazır (Sual gözlənilir)", fg="#a1a1aa")
        elif self.status_state == "loading":
            self.dot_label.configure(text="◌", fg="#f59e0b")
            self.text_label.configure(text=self.answer_text or "Həll edilir...", fg="#fde68a")
        elif self.status_state == "answer":
            self.dot_label.configure(text="✓", fg="#4ade80")
            self.text_label.configure(text=self.answer_text, fg="#f4f4f5")
        elif self.status_state == "error":
            self.dot_label.configure(text="✕", fg="#f87171")
            self.text_label.configure(text=self.answer_text, fg="#fca5a5")

    def _compute_position(self, cursor_x: int, cursor_y: int) -> tuple[int, int]:
        cfg = self.get_config()
        mode = cfg.get("position_mode", "cursor")
        self.popup.update_idletasks()
        w = max(self.popup.winfo_reqwidth(), 90)
        h = max(self.popup.winfo_reqheight(), 28)
        sw = self.root.winfo_screenwidth()
        sh = self.root.winfo_screenheight()

        if mode == "bottom_right":
            x = sw - w - 20
            y = sh - h - 55
        elif mode == "bottom_left":
            x = 20
            y = sh - h - 55
        elif mode == "top_right":
            x = sw - w - 20
            y = 20
        else:
            x = cursor_x + 16
            y = cursor_y + 18
            if x + w > sw - 10:
                x = max(10, cursor_x - w - 12)
            if y + h > sh - 45:
                y = max(10, cursor_y - h - 12)
        return int(x), int(y)

    def _reposition_KeepCurrent(self) -> None:
        try:
            px = self.root.winfo_pointerx()
            py = self.root.winfo_pointery()
            x, y = self._compute_position(px, py)
            self.popup.geometry(f"+{x}+{y}")
        except Exception:
            pass

    def _auto_hide_if_not_held(self) -> None:
        self._autohide_after_id = None
        if not self.held_by_user:
            self._do_hide()

    def _do_show(self, cursor_x: int, cursor_y: int, held: bool = False) -> None:
        self._update_labels()
        self.refresh_styles()
        x, y = self._compute_position(cursor_x, cursor_y)
        self.popup.geometry(f"+{x}+{y}")
        self.popup.deiconify()
        self.popup.lift()
        apply_stealth_window_styles(
            self.popup,
            exclude_from_capture=bool(self.get_config().get("exclude_from_capture", True)),
            click_through=True,
        )
        self.is_visible = True

    def _do_hide(self) -> None:
        self.popup.withdraw()
        self.is_visible = False

    def _do_flash(self, x: int, y: int) -> None:
        cfg = self.get_config()
        if not cfg.get("flash_indicator_enabled", True):
            return
        if self._flash_after_id:
            try:
                self.root.after_cancel(self._flash_after_id)
            except Exception:
                pass
        self.flash_win.geometry(f"+{int(x) + 12}+{int(y) + 12}")
        self.flash_win.deiconify()
        self.flash_win.lift()
        apply_stealth_window_styles(
            self.flash_win,
            exclude_from_capture=bool(cfg.get("exclude_from_capture", True)),
            click_through=True,
        )
        self._flash_after_id = self.root.after(320, self.flash_win.withdraw)
