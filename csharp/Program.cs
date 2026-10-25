using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace QuizAssistant
{
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
        private static NotifyIcon trayIcon;

        private const int HK_SNIPER = 1;
        private const int HK_SNIPER_ALT = 2;
        private const int HK_CLIPBOARD = 3;
        private const int HK_HIDE = 4;
        private const int HK_DOT = 5;
        private const int HK_QUIT = 6;

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
            // Ctrl+Shift+Z (Alternative region capture)
            hotkeyWnd.Register(HK_SNIPER_ALT, HotkeyWindow.MOD_CONTROL | HotkeyWindow.MOD_SHIFT, Keys.Z);
            // Ctrl+Shift+C (Solve Clipboard text)
            hotkeyWnd.Register(HK_CLIPBOARD, HotkeyWindow.MOD_CONTROL | HotkeyWindow.MOD_SHIFT, Keys.C);
            // Ctrl+Shift+H (Hide / Toggle overlay)
            hotkeyWnd.Register(HK_HIDE, HotkeyWindow.MOD_CONTROL | HotkeyWindow.MOD_SHIFT, Keys.H);
            // Ctrl+Shift+D (Toggle Dot Mode)
            hotkeyWnd.Register(HK_DOT, HotkeyWindow.MOD_CONTROL | HotkeyWindow.MOD_SHIFT, Keys.D);
            // Ctrl+Shift+Q (Quit)
            hotkeyWnd.Register(HK_QUIT, HotkeyWindow.MOD_CONTROL | HotkeyWindow.MOD_SHIFT, Keys.Q);

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
                case HK_SNIPER_ALT:
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
                        overlay.BeginInvoke(new Action(() =>
                        {
                            overlay.SetAnswer(answer, autoHide: false);
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
                    overlay.BeginInvoke(new Action(() =>
                    {
                        overlay.SetAnswer(answer, autoHide: false);
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

        private static void ExitApp()
        {
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
