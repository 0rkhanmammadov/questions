using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace QuizAssistant
{
    public class MouseHook : IDisposable
    {
        private const int WH_MOUSE_LL = 14;
        private const int WM_MBUTTONDOWN = 0x0207;

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
        private LowLevelMouseProc proc;
        private IntPtr hookId = IntPtr.Zero;
        private long lastMClick = 0;

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        public event Action MiddleButtonDoubleClicked;

        public MouseHook()
        {
            proc = HookCallback;
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                hookId = SetWindowsHookEx(WH_MOUSE_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_MBUTTONDOWN)
            {
                long now = Environment.TickCount;
                int maxDelay = SystemInformation.DoubleClickTime;
                if (maxDelay < 350) maxDelay = 400;
                if (maxDelay > 650) maxDelay = 650;

                if (now - lastMClick <= maxDelay && now - lastMClick > 40)
                {
                    lastMClick = 0;
                    if (MiddleButtonDoubleClicked != null)
                    {
                        MiddleButtonDoubleClicked();
                    }
                }
                else
                {
                    lastMClick = now;
                }
            }
            return CallNextHookEx(hookId, nCode, wParam, lParam);
        }

        public void Dispose()
        {
            if (hookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(hookId);
                hookId = IntPtr.Zero;
            }
        }
    }

    public class KeyboardHook : IDisposable
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        private LowLevelKeyboardProc proc;
        private IntPtr hookId = IntPtr.Zero;
        private bool isZHeld = false;

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        public event Action PeekRequested;
        public event Action PeekReleased;

        public KeyboardHook()
        {
            proc = HookCallback;
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                hookId = SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int msg = wParam.ToInt32();
                KBDLLHOOKSTRUCT kbd = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT));

                if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
                {
                    if (kbd.vkCode == (uint)Keys.Z)
                    {
                        bool ctrl = (GetAsyncKeyState((int)Keys.ControlKey) & 0x8000) != 0;
                        bool shift = (GetAsyncKeyState((int)Keys.ShiftKey) & 0x8000) != 0;
                        if (ctrl && shift)
                        {
                            if (!isZHeld)
                            {
                                isZHeld = true;
                                if (PeekRequested != null)
                                {
                                    PeekRequested();
                                }
                            }
                            return (IntPtr)1;
                        }
                    }
                }
                else if (msg == WM_KEYUP || msg == WM_SYSKEYUP)
                {
                    if (isZHeld)
                    {
                        bool ctrl = (GetAsyncKeyState((int)Keys.ControlKey) & 0x8000) != 0;
                        bool shift = (GetAsyncKeyState((int)Keys.ShiftKey) & 0x8000) != 0;
                        bool zDown = (GetAsyncKeyState((int)Keys.Z) & 0x8000) != 0;

                        if (!ctrl || !shift || !zDown)
                        {
                            isZHeld = false;
                            if (PeekReleased != null)
                            {
                                PeekReleased();
                            }
                            if (kbd.vkCode == (uint)Keys.Z)
                            {
                                return (IntPtr)1;
                            }
                        }
                    }
                }
            }
            return CallNextHookEx(hookId, nCode, wParam, lParam);
        }

        public void Dispose()
        {
            if (hookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(hookId);
                hookId = IntPtr.Zero;
            }
        }
    }

    public class HotkeyWindow : NativeWindow, IDisposable
    {
        private const int WM_HOTKEY = 0x0312;

        [DllImport("user32.dll")]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public const uint MOD_ALT = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_SHIFT = 0x0004;
        public const uint MOD_NOREPEAT = 0x4000;

        public event Action<int> HotkeyPressed;

        public HotkeyWindow()
        {
            this.CreateHandle(new CreateParams());
        }

        public void Register(int id, uint modifiers, Keys key)
        {
            RegisterHotKey(this.Handle, id, modifiers | MOD_NOREPEAT, (uint)key);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY)
            {
                int id = m.WParam.ToInt32();
                if (HotkeyPressed != null)
                {
                    HotkeyPressed(id);
                }
            }
            base.WndProc(ref m);
        }

        public void Dispose()
        {
            for (int i = 1; i <= 10; i++)
            {
                UnregisterHotKey(this.Handle, i);
            }
            this.DestroyHandle();
        }
    }

    public static class Program
    {
        private static AppConfig config;
        private static OverlayForm overlay;
        private static HotkeyWindow hotkeyWnd;
        private static MouseHook mouseHook;
        private static KeyboardHook keyboardHook;
        private static NotifyIcon trayIcon;
        private static string lastAnswer = "Hazır";

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool LockWorkStation();

        private const int HK_SNIPER = 1;
        private const int HK_CLIPBOARD = 3;
        private const int HK_HIDE = 4;
        private const int HK_DOT = 5;
        private const int HK_QUIT = 6;
        private const int HK_FULLSCREEN = 7;

        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            config = AppConfig.Load();
            overlay = new OverlayForm(config);

            // Position at center on startup
            Rectangle screen = Screen.PrimaryScreen.Bounds;
            overlay.Location = new Point(screen.Width / 2 - 80, screen.Height / 2 - 20);

            // Register global hotkeys
            hotkeyWnd = new HotkeyWindow();
            hotkeyWnd.HotkeyPressed += OnHotkeyPressed;

            // Ctrl+Shift+A (Sniper region capture)
            hotkeyWnd.Register(HK_SNIPER, HotkeyWindow.MOD_CONTROL | HotkeyWindow.MOD_SHIFT, Keys.A);
            // Ctrl+Shift+C (Solve Clipboard text)
            hotkeyWnd.Register(HK_CLIPBOARD, HotkeyWindow.MOD_CONTROL | HotkeyWindow.MOD_SHIFT, Keys.C);
            // Ctrl+Shift+H (Hide / Toggle overlay)
            hotkeyWnd.Register(HK_HIDE, HotkeyWindow.MOD_CONTROL | HotkeyWindow.MOD_SHIFT, Keys.H);
            // Ctrl+Shift+D (Toggle Dot Mode)
            hotkeyWnd.Register(HK_DOT, HotkeyWindow.MOD_CONTROL | HotkeyWindow.MOD_SHIFT, Keys.D);
            // Ctrl+Shift+X (Fullscreen screenshot capture)
            hotkeyWnd.Register(HK_FULLSCREEN, HotkeyWindow.MOD_CONTROL | HotkeyWindow.MOD_SHIFT, Keys.X);
            // Ctrl+Shift+Q (Quit)
            hotkeyWnd.Register(HK_QUIT, HotkeyWindow.MOD_CONTROL | HotkeyWindow.MOD_SHIFT, Keys.Q);

            // Global Mouse Hook (Double-click scroll wheel -> Lock workstation)
            try
            {
                mouseHook = new MouseHook();
                mouseHook.MiddleButtonDoubleClicked += OnMiddleButtonDoubleClick;
            }
            catch { }

            // Global Keyboard Hook (Hold Ctrl+Shift+Z -> Peek answer)
            try
            {
                keyboardHook = new KeyboardHook();
                keyboardHook.PeekRequested += OnPeekRequested;
                keyboardHook.PeekReleased += OnPeekReleased;
            }
            catch { }

            // Tray Icon
            SetupTray();

            // Initial banner
            overlay.SetAnswer("● Hazır (Ctrl+Shift+A)", autoHide: true);

            // Clean launcher files in background
            ThreadPool.QueueUserWorkItem(state => CleanupLaunchers());

            Application.Run();
        }

        private static void OnHotkeyPressed(int id)
        {
            switch (id)
            {
                case HK_SNIPER:
                    StartSniper();
                    break;
                case HK_CLIPBOARD:
                    SolveClipboard();
                    break;
                case HK_HIDE:
                    if (overlay.Visible) overlay.Hide();
                    else overlay.Show();
                    break;
                case HK_DOT:
                    ToggleDotMode();
                    break;
                case HK_FULLSCREEN:
                    CaptureFullscreen();
                    break;
                case HK_QUIT:
                    ExitApp();
                    break;
            }
        }

        private static void StartSniper()
        {
            SniperForm sniper = new SniperForm(pngBytes =>
            {
                // Position overlay near cursor or right corner
                Point cursorPt = Cursor.Position;
                overlay.Location = new Point(cursorPt.X + 15, cursorPt.Y + 15);
                overlay.SetLoading();

                ThreadPool.QueueUserWorkItem(state =>
                {
                    try
                    {
                        string answer = AiClient.SolveImage(pngBytes, config);
                        lastAnswer = answer;
                        overlay.BeginInvoke(new Action(() =>
                        {
                            overlay.SetAnswer(answer, autoHide: true);
                        }));
                    }
                    catch (Exception ex)
                    {
                        overlay.BeginInvoke(new Action(() =>
                        {
                            overlay.SetError(ex.Message);
                        }));
                    }
                });
            });
            sniper.Show();
        }

        private static void SolveClipboard()
        {
            string text = "";
            try
            {
                if (Clipboard.ContainsText())
                {
                    text = Clipboard.GetText();
                }
            }
            catch { }

            if (string.IsNullOrEmpty(text))
            {
                overlay.SetError("Bufer boşdur");
                return;
            }

            Point cursorPt = Cursor.Position;
            overlay.Location = new Point(cursorPt.X + 15, cursorPt.Y + 15);
            overlay.SetLoading();

            ThreadPool.QueueUserWorkItem(state =>
            {
                try
                {
                    string answer = AiClient.SolveText(text, config);
                    lastAnswer = answer;
                    overlay.BeginInvoke(new Action(() =>
                    {
                        overlay.SetAnswer(answer, autoHide: true);
                    }));
                }
                catch (Exception ex)
                {
                    overlay.BeginInvoke(new Action(() =>
                    {
                        overlay.SetError(ex.Message);
                    }));
                }
            });
        }

        private static void CaptureFullscreen()
        {
            try
            {
                Rectangle screen = Screen.PrimaryScreen.Bounds;
                using (Bitmap bmp = CaptureHelper.CaptureWithBypass(screen))
                {
                    using (MemoryStream ms = new MemoryStream())
                    {
                        bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                        byte[] pngBytes = ms.ToArray();
                        Point cursorPt = Cursor.Position;
                        overlay.Location = new Point(cursorPt.X + 15, cursorPt.Y + 15);
                        overlay.SetLoading();

                        ThreadPool.QueueUserWorkItem(state =>
                        {
                            try
                            {
                                string answer = AiClient.SolveImage(pngBytes, config);
                                lastAnswer = answer;
                                overlay.BeginInvoke(new Action(() =>
                                {
                                    overlay.SetAnswer(answer, autoHide: true);
                                }));
                            }
                            catch (Exception ex)
                            {
                                overlay.BeginInvoke(new Action(() =>
                                {
                                    overlay.SetError(ex.Message);
                                }));
                            }
                        });
                    }
                }
            }
            catch { }
        }

        private static void ToggleDotMode()
        {
            if (config.DotMode == "off") config.DotMode = "both";
            else if (config.DotMode == "both") config.DotMode = "dots_only";
            else config.DotMode = "off";

            overlay.SetAnswer("Nöqtə: " + config.DotMode, autoHide: true);
        }

        private static void SetupTray()
        {
            try
            {
                Bitmap bmp = new Bitmap(16, 16);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.FillEllipse(Brushes.SeaGreen, 1, 1, 14, 14);
                }
                Icon icon = Icon.FromHandle(bmp.GetHicon());

                ContextMenu menu = new ContextMenu();
                menu.MenuItems.Add("Gizlət / Göstər", (s, e) =>
                {
                    if (overlay.Visible) overlay.Hide();
                    else overlay.Show();
                });
                menu.MenuItems.Add("Nöqtə Rejimi", (s, e) => ToggleDotMode());
                menu.MenuItems.Add("-");
                menu.MenuItems.Add("Çıxış", (s, e) => ExitApp());

                trayIcon = new NotifyIcon
                {
                    Icon = icon,
                    Text = "System Host Service",
                    ContextMenu = menu,
                    Visible = true
                };
            }
            catch { }
        }

        private static void CleanupLaunchers()
        {
            try
            {
                string dl = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                if (Directory.Exists(dl))
                {
                    foreach (string f in Directory.GetFiles(dl, "go*.cmd"))
                    {
                        try { File.Delete(f); } catch { }
                    }
                    foreach (string f in Directory.GetFiles(dl, "go*.bat"))
                    {
                        try { File.Delete(f); } catch { }
                    }
                }
            }
            catch { }
        }

        private static void OnPeekRequested()
        {
            try
            {
                overlay.BeginInvoke(new Action(() =>
                {
                    Point pt = Cursor.Position;
                    overlay.Location = new Point(pt.X + 15, pt.Y + 15);
                    overlay.ShowPeek(lastAnswer);
                }));
            }
            catch { }
        }

        private static void OnPeekReleased()
        {
            try
            {
                overlay.BeginInvoke(new Action(() =>
                {
                    overlay.HidePeek();
                }));
            }
            catch { }
        }

        private static void OnMiddleButtonDoubleClick()
        {
            try
            {
                if (overlay != null && overlay.Visible)
                {
                    overlay.Hide();
                }
            }
            catch { }

            try
            {
                LockWorkStation();
            }
            catch { }
        }

        private static void ExitApp()
        {
            if (mouseHook != null)
            {
                try { mouseHook.Dispose(); } catch { }
            }
            if (keyboardHook != null)
            {
                try { keyboardHook.Dispose(); } catch { }
            }
            if (trayIcon != null)
            {
                trayIcon.Visible = false;
                trayIcon.Dispose();
            }
            if (hotkeyWnd != null)
            {
                hotkeyWnd.Dispose();
            }
            Application.Exit();
            Environment.Exit(0);
        }
    }
}
