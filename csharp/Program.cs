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
        private const int WM_MBUTTONUP = 0x0208;

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
        private LowLevelMouseProc proc;
        private IntPtr hookId = IntPtr.Zero;
        private long lastMDown = 0;

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        public event Action MiddleButtonDown;
        public event Action MiddleButtonUp;
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
            if (nCode >= 0)
            {
                int msg = wParam.ToInt32();
                if (msg == WM_MBUTTONDOWN)
                {
                    long now = Environment.TickCount;
                    if (now - lastMDown <= 450 && now - lastMDown > 40)
                    {
                        lastMDown = 0;
                        if (MiddleButtonDoubleClicked != null)
                        {
                            MiddleButtonDoubleClicked();
                        }
                    }
                    else
                    {
                        lastMDown = now;
                        if (MiddleButtonDown != null)
                        {
                            MiddleButtonDown();
                        }
                    }
                }
                else if (msg == WM_MBUTTONUP)
                {
                    if (MiddleButtonUp != null)
                    {
                        MiddleButtonUp();
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
        private static DotForm dotOverlay;
        private static HotkeyWindow hotkeyWnd;
        private static MouseHook mouseHook;
        private static KeyboardHook keyboardHook;
        private static NotifyIcon trayIcon;
        private static System.Windows.Forms.Timer middleHoldTimer;
        private static bool isMiddleDown = false;
        private static bool isPeekingMiddle = false;
        private static string lastAnswer = "Cavab hələ oxunmayıb";

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool LockWorkStation();

        private const int HK_SNIPER = 1;
        private const int HK_CLIPBOARD = 3;
        private const int HK_HIDE = 4;
        private const int HK_DOT = 5;
        private const int HK_QUIT = 6;
        private const int HK_FULLSCREEN = 7;
        private const int HK_LOCK = 8;

        public static void SafeInvoke(Control ctrl, Action act)
        {
            if (ctrl == null || ctrl.IsDisposed) return;
            try
            {
                if (!ctrl.IsHandleCreated)
                {
                    IntPtr dummy = ctrl.Handle;
                }
                if (ctrl.InvokeRequired)
                {
                    ctrl.BeginInvoke(act);
                }
                else
                {
                    act();
                }
            }
            catch { }
        }

        [STAThread]
        public static void Main()
        {
            try
            {
                int myId = System.Diagnostics.Process.GetCurrentProcess().Id;
                foreach (var proc in System.Diagnostics.Process.GetProcessesByName("quiz"))
                {
                    if (proc.Id != myId)
                    {
                        try { proc.Kill(); proc.WaitForExit(1000); } catch { }
                    }
                }
            }
            catch { }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            config = AppConfig.Load();
            overlay = new OverlayForm(config);
            IntPtr dummy1 = overlay.Handle;

            dotOverlay = new DotForm();
            IntPtr dummy2 = dotOverlay.Handle;

            // Register global hotkeys
            hotkeyWnd = new HotkeyWindow();
            hotkeyWnd.HotkeyPressed += OnHotkeyPressed;

            // Ctrl+Shift+A (Sniper region capture)
            hotkeyWnd.Register(HK_SNIPER, HotkeyWindow.MOD_CONTROL | HotkeyWindow.MOD_SHIFT, Keys.A);
            // Ctrl+Shift+C (Solve Clipboard text)
            hotkeyWnd.Register(HK_CLIPBOARD, HotkeyWindow.MOD_CONTROL | HotkeyWindow.MOD_SHIFT, Keys.C);
            // Ctrl+Shift+H (Hide overlay)
            hotkeyWnd.Register(HK_HIDE, HotkeyWindow.MOD_CONTROL | HotkeyWindow.MOD_SHIFT, Keys.H);
            // Ctrl+Shift+D (Toggle Dot Mode)
            hotkeyWnd.Register(HK_DOT, HotkeyWindow.MOD_CONTROL | HotkeyWindow.MOD_SHIFT, Keys.D);
            // Ctrl+Shift+X (Fullscreen screenshot capture)
            hotkeyWnd.Register(HK_FULLSCREEN, HotkeyWindow.MOD_CONTROL | HotkeyWindow.MOD_SHIFT, Keys.X);
            // Ctrl+Shift+L (Lock workstation)
            hotkeyWnd.Register(HK_LOCK, HotkeyWindow.MOD_CONTROL | HotkeyWindow.MOD_SHIFT, Keys.L);
            // Ctrl+Shift+Q (Quit)
            hotkeyWnd.Register(HK_QUIT, HotkeyWindow.MOD_CONTROL | HotkeyWindow.MOD_SHIFT, Keys.Q);

            // Middle mouse button timer: hold for >150ms = peek answer
            middleHoldTimer = new System.Windows.Forms.Timer();
            middleHoldTimer.Interval = 150;
            middleHoldTimer.Tick += (s, e) =>
            {
                middleHoldTimer.Stop();
                if (isMiddleDown)
                {
                    isPeekingMiddle = true;
                    ShowAnswerAtCursor();
                }
            };

            // Global Mouse Hook (Wheel hold = show answer, Wheel 2x click = solve)
            try
            {
                mouseHook = new MouseHook();
                mouseHook.MiddleButtonDown += () =>
                {
                    isMiddleDown = true;
                    middleHoldTimer.Stop();
                    middleHoldTimer.Start();
                };

                mouseHook.MiddleButtonUp += () =>
                {
                    isMiddleDown = false;
                    middleHoldTimer.Stop();
                    if (isPeekingMiddle)
                    {
                        isPeekingMiddle = false;
                        HideAnswer();
                    }
                };

                mouseHook.MiddleButtonDoubleClicked += () =>
                {
                    isMiddleDown = false;
                    middleHoldTimer.Stop();
                    if (isPeekingMiddle)
                    {
                        isPeekingMiddle = false;
                        HideAnswer();
                    }
                    CaptureFullscreen();
                };
            }
            catch { }

            // Global Keyboard Hook (Hold Ctrl+Shift+Z = show answer, release = hide)
            try
            {
                keyboardHook = new KeyboardHook();
                keyboardHook.PeekRequested += () => ShowAnswerAtCursor();
                keyboardHook.PeekReleased += () => HideAnswer();
            }
            catch { }

            // Tray Icon
            SetupTray();

            // Background Audio Loopback Capture for Listening
            try { AudioCapture.Start(); } catch { }

            // Clean launcher files in background
            ThreadPool.QueueUserWorkItem(state => CleanupLaunchers());

            Application.Run();
        }

        private static void ShowAnswerAtCursor()
        {
            SafeInvoke(overlay, () =>
            {
                Point pt = Cursor.Position;
                overlay.ShowAnswer(lastAnswer, pt);
            });
        }

        private static void HideAnswer()
        {
            SafeInvoke(overlay, () =>
            {
                overlay.HideAnswer();
            });
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
                    HideAnswer();
                    break;
                case HK_DOT:
                    ToggleDotMode();
                    break;
                case HK_FULLSCREEN:
                    CaptureFullscreen();
                    break;
                case HK_LOCK:
                    LockScreen();
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
                SafeInvoke(dotOverlay, () => dotOverlay.ShowSolving());

                ThreadPool.QueueUserWorkItem(state =>
                {
                    try
                    {
                        byte[] wavBytes = null;
                        if (AudioCapture.HasRecentAudio(45))
                        {
                            wavBytes = AudioCapture.GetLastAudioWavBytes(120);
                        }

                        string answer;
                        if (wavBytes != null && wavBytes.Length > 0)
                        {
                            answer = AiClient.SolveImageAndAudio(pngBytes, wavBytes, config);
                        }
                        else
                        {
                            answer = AiClient.SolveImage(pngBytes, config);
                        }

                        lastAnswer = answer;
                        SafeInvoke(dotOverlay, () => dotOverlay.FlashReady());
                    }
                    catch (Exception ex)
                    {
                        lastAnswer = "Xəta: " + ex.Message;
                        SafeInvoke(dotOverlay, () => dotOverlay.FlashError());
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
                SafeInvoke(dotOverlay, () => dotOverlay.FlashError());
                return;
            }

            SafeInvoke(dotOverlay, () => dotOverlay.ShowSolving());

            ThreadPool.QueueUserWorkItem(state =>
            {
                try
                {
                    string answer = AiClient.SolveText(text, config);
                    lastAnswer = answer;
                    SafeInvoke(dotOverlay, () => dotOverlay.FlashReady());
                }
                catch (Exception ex)
                {
                    lastAnswer = "Xəta: " + ex.Message;
                    SafeInvoke(dotOverlay, () => dotOverlay.FlashError());
                }
            });
        }

        private static void CaptureFullscreen()
        {
            SafeInvoke(dotOverlay, () => dotOverlay.ShowSolving());

            ThreadPool.QueueUserWorkItem(state =>
            {
                try
                {
                    Point curPt = Cursor.Position;
                    Rectangle screen = Screen.FromPoint(curPt).Bounds;
                    byte[] imageBytes;
                    using (Bitmap bmp = CaptureHelper.CaptureWithBypass(screen))
                    {
                        imageBytes = CaptureHelper.EncodeToJpeg(bmp, 85L);
                    }

                    byte[] wavBytes = null;
                    if (AudioCapture.HasRecentAudio(45))
                    {
                        wavBytes = AudioCapture.GetLastAudioWavBytes(120);
                    }

                    string answer;
                    if (wavBytes != null && wavBytes.Length > 0)
                    {
                        answer = AiClient.SolveImageAndAudio(imageBytes, wavBytes, config);
                    }
                    else
                    {
                        answer = AiClient.SolveImage(imageBytes, config);
                    }

                    lastAnswer = answer;
                    SafeInvoke(dotOverlay, () => dotOverlay.FlashReady());
                }
                catch (Exception ex)
                {
                    lastAnswer = "Xəta: " + ex.Message;
                    SafeInvoke(dotOverlay, () => dotOverlay.FlashError());
                }
            });
        }

        private static void LockScreen()
        {
            HideAnswer();
            try { LockWorkStation(); } catch { }
        }

        private static void ToggleDotMode()
        {
            if (config.DotMode == "off") config.DotMode = "both";
            else if (config.DotMode == "both") config.DotMode = "dots_only";
            else config.DotMode = "off";
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
                menu.MenuItems.Add("Ekranı Kilidlə (Ctrl+Shift+L)", (s, e) => LockScreen());
                menu.MenuItems.Add("-");
                menu.MenuItems.Add("Çıxış (Ctrl+Shift+Q)", (s, e) => ExitApp());

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

        private static void ExitApp()
        {
            CursorManager.RestoreCursor();
            try { AudioCapture.Stop(); } catch { }
            if (middleHoldTimer != null)
            {
                try { middleHoldTimer.Stop(); middleHoldTimer.Dispose(); } catch { }
            }
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
