import threading
import tkinter as tk
from tkinter import ttk
from typing import Any, Callable, Dict, Optional

from ai_client import test_connection
from config import save_config


class SettingsWindow:
    def __init__(
        self,
        root: tk.Tk,
        config_getter: Callable[[], Dict[str, Any]],
        on_config_saved: Callable[[Dict[str, Any]], None],
    ) -> None:
        self.root = root
        self.get_config = config_getter
        self.on_config_saved = on_config_saved
        self.win: Optional[tk.Toplevel] = None

    def open_or_focus(self) -> None:
        if self.win is not None and self.win.winfo_exists():
            self.win.deiconify()
            self.win.lift()
            self.win.focus_force()
            return
        self._build_window()

    def _build_window(self) -> None:
        cfg = self.get_config()
        win = tk.Toplevel(self.root)
        self.win = win
        win.title("Gizli İmtahan Köməkçisi — Ayarlar")
        win.geometry("520x680")
        win.resizable(False, False)
        win.configure(bg="#18181b")
        win.attributes("-topmost", True)

        pad = {"padx": 16, "pady": 4}
        lbl_fg = "#e4e4e7"
        sub_fg = "#a1a1aa"
        bg = "#18181b"
        input_bg = "#27272a"

        title = tk.Label(
            win,
            text="Ayarlar və Super Modullar",
            fg="#f4f4f5",
            bg=bg,
            font=("Segoe UI", 12, "bold"),
        )
        title.pack(anchor="w", padx=16, pady=(12, 6))

        # Provider selection
        prov_frame = tk.Frame(win, bg=bg)
        prov_frame.pack(fill="x", **pad)
        tk.Label(prov_frame, text="AI Xidməti:", fg=lbl_fg, bg=bg, font=("Segoe UI", 9, "bold")).pack(side="left")

        self.provider_var = tk.StringVar(value=cfg.get("provider", "free"))
        rb_free = tk.Radiobutton(
            prov_frame,
            text="Pulsuz AI (Açarsız)",
            variable=self.provider_var,
            value="free",
            fg="#4ade80",
            bg=bg,
            selectcolor=input_bg,
            activebackground=bg,
            activeforeground="#4ade80",
        )
        rb_free.pack(side="left", padx=(10, 6))
        rb_gemini = tk.Radiobutton(
            prov_frame,
            text="Gemini",
            variable=self.provider_var,
            value="gemini",
            fg=lbl_fg,
            bg=bg,
            selectcolor=input_bg,
            activebackground=bg,
            activeforeground=lbl_fg,
        )
        rb_gemini.pack(side="left", padx=(4, 6))
        rb_openai = tk.Radiobutton(
            prov_frame,
            text="OpenAI",
            variable=self.provider_var,
            value="openai",
            fg=lbl_fg,
            bg=bg,
            selectcolor=input_bg,
            activebackground=bg,
            activeforeground=lbl_fg,
        )
        rb_openai.pack(side="left")

        # Gemini API Key & Model
        g_frame = tk.LabelFrame(win, text=" Google Gemini ", fg="#38bdf8", bg=bg, font=("Segoe UI", 9, "bold"), padx=10, pady=6)
        g_frame.pack(fill="x", padx=16, pady=4)

        tk.Label(g_frame, text="API Açarı (Key):", fg=lbl_fg, bg=bg).grid(row=0, column=0, sticky="w", pady=2)
        self.gemini_key_var = tk.StringVar(value=cfg.get("gemini_api_key", ""))
        g_entry = tk.Entry(g_frame, textvariable=self.gemini_key_var, width=42, bg=input_bg, fg="#ffffff", insertbackground="#ffffff", relief="flat")
        g_entry.grid(row=0, column=1, sticky="ew", padx=(8, 0), pady=2)

        tk.Label(g_frame, text="Model:", fg=lbl_fg, bg=bg).grid(row=1, column=0, sticky="w", pady=2)
        self.gemini_model_var = tk.StringVar(value=cfg.get("gemini_model", "gemini-2.5-flash"))
        g_models = ttk.Combobox(
            g_frame,
            textvariable=self.gemini_model_var,
            values=["gemini-2.5-flash", "gemini-2.0-flash", "gemini-1.5-flash", "gemini-2.5-pro"],
            width=39,
        )
        g_models.grid(row=1, column=1, sticky="ew", padx=(8, 0), pady=2)
        g_frame.columnconfigure(1, weight=1)

        # OpenAI API Key & Model
        o_frame = tk.LabelFrame(win, text=" OpenAI (ChatGPT) ", fg="#4ade80", bg=bg, font=("Segoe UI", 9, "bold"), padx=10, pady=6)
        o_frame.pack(fill="x", padx=16, pady=4)

        tk.Label(o_frame, text="API Açarı (Key):", fg=lbl_fg, bg=bg).grid(row=0, column=0, sticky="w", pady=2)
        self.openai_key_var = tk.StringVar(value=cfg.get("openai_api_key", ""))
        o_entry = tk.Entry(o_frame, textvariable=self.openai_key_var, width=42, bg=input_bg, fg="#ffffff", insertbackground="#ffffff", relief="flat")
        o_entry.grid(row=0, column=1, sticky="ew", padx=(8, 0), pady=2)

        tk.Label(o_frame, text="Model:", fg=lbl_fg, bg=bg).grid(row=1, column=0, sticky="w", pady=2)
        self.openai_model_var = tk.StringVar(value=cfg.get("openai_model", "gpt-4o-mini"))
        o_models = ttk.Combobox(
            o_frame,
            textvariable=self.openai_model_var,
            values=["gpt-4o-mini", "gpt-4o", "gpt-4.1-mini"],
            width=39,
        )
        o_models.grid(row=1, column=1, sticky="ew", padx=(8, 0), pady=2)
        o_frame.columnconfigure(1, weight=1)

        # Display & Super Modules
        d_frame = tk.LabelFrame(win, text=" Gizlilik və Super Modullar ", fg="#c084fc", bg=bg, font=("Segoe UI", 9, "bold"), padx=10, pady=6)
        d_frame.pack(fill="x", padx=16, pady=4)

        tk.Label(d_frame, text="Cavabın yeri:", fg=lbl_fg, bg=bg).grid(row=0, column=0, sticky="w", pady=2)
        self.pos_var = tk.StringVar(value=cfg.get("position_mode", "cursor"))
        pos_combo = ttk.Combobox(
            d_frame,
            textvariable=self.pos_var,
            values=["cursor", "bottom_right", "bottom_left", "top_right"],
            state="readonly",
            width=18,
        )
        pos_combo.grid(row=0, column=1, sticky="w", padx=(8, 0), pady=2)

        tk.Label(d_frame, text="Nöqtə Şifrələmə (Ctrl+Shift+D):", fg=lbl_fg, bg=bg).grid(row=1, column=0, sticky="w", pady=2)
        self.dot_mode_var = tk.StringVar(value=cfg.get("dot_mode", "off"))
        dot_combo = ttk.Combobox(
            d_frame,
            textvariable=self.dot_mode_var,
            values=["off", "both", "dots_only"],
            state="readonly",
            width=18,
        )
        dot_combo.grid(row=1, column=1, sticky="w", padx=(8, 0), pady=2)

        tk.Label(d_frame, text="Şəffaflıq (0.2 - 1.0):", fg=lbl_fg, bg=bg).grid(row=2, column=0, sticky="w", pady=2)
        self.opacity_var = tk.DoubleVar(value=float(cfg.get("opacity", 0.78)))
        op_scale = tk.Scale(
            d_frame,
            from_=0.2,
            to=1.0,
            resolution=0.05,
            orient="horizontal",
            variable=self.opacity_var,
            bg=bg,
            fg=lbl_fg,
            highlightthickness=0,
            troughcolor=input_bg,
            length=180,
        )
        op_scale.grid(row=2, column=1, sticky="w", padx=(8, 0))

        self.dual_ai_var = tk.BooleanVar(value=bool(cfg.get("dual_ai_enabled", True)))
        tk.Checkbutton(
            d_frame,
            text="İkiqat AI Yoxlaması (2 mühərrik paralel — ✓✓ dəqiqlik)",
            variable=self.dual_ai_var,
            fg="#4ade80",
            bg=bg,
            selectcolor=input_bg,
            activebackground=bg,
            activeforeground="#4ade80",
        ).grid(row=3, column=0, columnspan=2, sticky="w", pady=1)

        self.history_var = tk.BooleanVar(value=bool(cfg.get("history_enabled", True)))
        tk.Checkbutton(
            d_frame,
            text="Son sualların cavab tarixçəsini göstər (məs: 14) B  [13) D | 12) A])",
            variable=self.history_var,
            fg=lbl_fg,
            bg=bg,
            selectcolor=input_bg,
            activebackground=bg,
            activeforeground=lbl_fg,
        ).grid(row=4, column=0, columnspan=2, sticky="w", pady=1)

        self.local_db_var = tk.BooleanVar(value=bool(cfg.get("local_db_enabled", True)))
        tk.Checkbutton(
            d_frame,
            text="Lokal Baza axtarışı (baza.txt faylından 0.01 san offline cavab)",
            variable=self.local_db_var,
            fg=lbl_fg,
            bg=bg,
            selectcolor=input_bg,
            activebackground=bg,
            activeforeground=lbl_fg,
        ).grid(row=5, column=0, columnspan=2, sticky="w", pady=1)

        self.exclude_capture_var = tk.BooleanVar(value=bool(cfg.get("exclude_from_capture", True)))
        tk.Checkbutton(
            d_frame,
            text="Skrinşot və ekran paylaşımından gizlət (Anti-Capture)",
            variable=self.exclude_capture_var,
            fg=lbl_fg,
            bg=bg,
            selectcolor=input_bg,
            activebackground=bg,
            activeforeground=lbl_fg,
        ).grid(row=6, column=0, columnspan=2, sticky="w", pady=1)

        self.ctrl_c_var = tk.BooleanVar(value=bool(cfg.get("ctrl_c_enabled", True)))
        tk.Checkbutton(
            d_frame,
            text="Ctrl+C basıldıqda mətni oxu (bağlıdırsa avto-skrinşot çək)",
            variable=self.ctrl_c_var,
            fg=lbl_fg,
            bg=bg,
            selectcolor=input_bg,
            activebackground=bg,
            activeforeground=lbl_fg,
        ).grid(row=7, column=0, columnspan=2, sticky="w", pady=1)

        self.flash_var = tk.BooleanVar(value=bool(cfg.get("flash_indicator_enabled", True)))
        tk.Checkbutton(
            d_frame,
            text="Sual tutulanda kursor yanında 0.3 san nöqtə (•) göstər",
            variable=self.flash_var,
            fg=lbl_fg,
            bg=bg,
            selectcolor=input_bg,
            activebackground=bg,
            activeforeground=lbl_fg,
        ).grid(row=8, column=0, columnspan=2, sticky="w", pady=1)

        # Status message label
        self.status_lbl = tk.Label(win, text="", fg="#38bdf8", bg=bg, font=("Segoe UI", 9))
        self.status_lbl.pack(fill="x", padx=16, pady=(2, 2))

        # Action Buttons
        btn_frame = tk.Frame(win, bg=bg)
        btn_frame.pack(fill="x", padx=16, pady=6)

        test_btn = tk.Button(
            btn_frame,
            text="API Yoxla (Test)",
            command=self._on_test_api,
            bg="#27272a",
            fg="#38bdf8",
            activebackground="#3f3f46",
            activeforeground="#ffffff",
            relief="flat",
            padx=14,
            pady=6,
        )
        test_btn.pack(side="left")

        save_btn = tk.Button(
            btn_frame,
            text="Yadda Saxla və Bağla",
            command=self._on_save,
            bg="#2563eb",
            fg="#ffffff",
            activebackground="#1d4ed8",
            activeforeground="#ffffff",
            font=("Segoe UI", 9, "bold"),
            relief="flat",
            padx=16,
            pady=6,
        )
        save_btn.pack(side="right")

        # Hotkey help footer
        help_text = (
            "Qısa yollar:\n"
            "• Ctrl + X (və ya PrintScreen) : Tam ekran skrinşotundan sualı oxu\n"
            "• Ctrl + Mausla Çəkmə : Seçilən sahənin skrinşotundan sualı oxu\n"
            "• Ctrl + C : Mətni oxu (kopyalama bağlıdırsa avto-skrinşot çəkir)\n"
            "• Ctrl + Shift : Cavabı göstər | Ctrl+Shift+D : Nöqtə rejimi (•/••/•••)\n"
            "• Ctrl + Backspace : Təcili Panik Çıxış (Yaddaşı təmizləyib bağlayır)"
        )
        tk.Label(win, text=help_text, fg=sub_fg, bg=bg, justify="left", font=("Segoe UI", 8)).pack(
            anchor="w", padx=16, pady=(2, 8)
        )

    def _collect_form_config(self) -> Dict[str, Any]:
        cfg = dict(self.get_config())
        cfg["provider"] = self.provider_var.get().strip()
        cfg["gemini_api_key"] = self.gemini_key_var.get().strip()
        cfg["gemini_model"] = self.gemini_model_var.get().strip() or "gemini-2.5-flash"
        cfg["openai_api_key"] = self.openai_key_var.get().strip()
        cfg["openai_model"] = self.openai_model_var.get().strip() or "gpt-4o-mini"
        cfg["position_mode"] = self.pos_var.get().strip() or "cursor"
        cfg["dot_mode"] = self.dot_mode_var.get().strip() or "off"
        cfg["opacity"] = float(self.opacity_var.get())
        cfg["dual_ai_enabled"] = bool(self.dual_ai_var.get())
        cfg["history_enabled"] = bool(self.history_var.get())
        cfg["local_db_enabled"] = bool(self.local_db_var.get())
        cfg["exclude_from_capture"] = bool(self.exclude_capture_var.get())
        cfg["ctrl_c_enabled"] = bool(self.ctrl_c_var.get())
        cfg["flash_indicator_enabled"] = bool(self.flash_var.get())
        return cfg

    def _on_test_api(self) -> None:
        temp_cfg = self._collect_form_config()
        self.status_lbl.configure(text="Yoxlanılır, gözləyin...", fg="#fde68a")

        def worker() -> None:
            ok, msg = test_connection(temp_cfg)
            if self.win and self.win.winfo_exists():
                color = "#4ade80" if ok else "#f87171"
                self.win.after(0, lambda: self.status_lbl.configure(text=msg, fg=color))

        threading.Thread(target=worker, daemon=True).start()

    def _on_save(self) -> None:
        new_cfg = self._collect_form_config()
        save_config(new_cfg)
        self.on_config_saved(new_cfg)
        if self.win and self.win.winfo_exists():
            self.win.destroy()
            self.win = None
