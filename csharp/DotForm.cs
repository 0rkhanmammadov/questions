using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace QuizAssistant
{
    public static class CursorManager
    {
        private const uint OCR_NORMAL = 32512;
        private const uint OCR_IBEAM = 32513;
        private const uint OCR_HAND = 32649;

        private const uint SPI_SETCURSORS = 0x0057;
        private const uint IMAGE_CURSOR = 2;
        private const uint LR_LOADFROMFILE = 0x0010;

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr LoadImage(IntPtr hinst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

        [DllImport("user32.dll")]
        private static extern bool SetSystemCursor(IntPtr hcur, uint id);

        [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW")]
        private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);

        [DllImport("user32.dll")]
        private static extern IntPtr CopyIcon(IntPtr hIcon);

        [DllImport("user32.dll")]
        private static extern IntPtr LoadCursor(IntPtr hInstance, int lpCursorName);

        private static readonly object syncLock = new object();
        private static bool isHourglassActive = false;
        private static System.Windows.Forms.Timer safetyTimer;

        static CursorManager()
        {
            try
            {
                safetyTimer = new System.Windows.Forms.Timer();
                safetyTimer.Interval = 12000;
                safetyTimer.Tick += (s, e) =>
                {
                    safetyTimer.Stop();
                    RestoreCursor();
                };

                AppDomain.CurrentDomain.ProcessExit += (s, e) => RestoreCursor();
                Application.ApplicationExit += (s, e) => RestoreCursor();
            }
            catch { }
        }

        private static IntPtr LoadOriginalHourglass()
        {
            string[] paths = new string[]
            {
                @"C:\Windows\Cursors\lwait.cur",
                @"C:\Windows\Cursors\wait_m.cur",
                @"C:\Windows\Cursors\wait_i.cur",
                @"C:\Windows\Cursors\busy_i.cur",
                @"C:\Windows\Cursors\aero_busy.ani"
            };

            foreach (string p in paths)
            {
                if (File.Exists(p))
                {
                    IntPtr h = LoadImage(IntPtr.Zero, p, IMAGE_CURSOR, 32, 32, LR_LOADFROMFILE);
                    if (h != IntPtr.Zero) return h;
                }
            }

            IntPtr defWait = LoadCursor(IntPtr.Zero, 32514); // IDC_WAIT
            if (defWait != IntPtr.Zero)
            {
                return CopyIcon(defWait);
            }
            return IntPtr.Zero;
        }

        public static void ShowHourglass()
        {
            lock (syncLock)
            {
                try
                {
                    IntPtr h1 = LoadOriginalHourglass();
                    if (h1 != IntPtr.Zero)
                    {
                        IntPtr h2 = CopyIcon(h1);
                        IntPtr h3 = CopyIcon(h1);

                        SetSystemCursor(h1, OCR_NORMAL);
                        if (h2 != IntPtr.Zero) SetSystemCursor(h2, OCR_IBEAM);
                        if (h3 != IntPtr.Zero) SetSystemCursor(h3, OCR_HAND);

                        isHourglassActive = true;
                        if (safetyTimer != null)
                        {
                            safetyTimer.Stop();
                            safetyTimer.Start();
                        }
                    }
                }
                catch { }
            }
        }

        public static void RestoreCursor()
        {
            lock (syncLock)
            {
                try
                {
                    if (safetyTimer != null) safetyTimer.Stop();
                    if (isHourglassActive)
                    {
                        SystemParametersInfo(SPI_SETCURSORS, 0, IntPtr.Zero, 0);
                        isHourglassActive = false;
                    }
                }
                catch { }
            }
        }
    }

    public class DotForm : Form
    {
        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
                cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
                cp.ExStyle |= 0x00000020; // WS_EX_TRANSPARENT
                cp.ExStyle |= 0x00000008; // WS_EX_TOPMOST
                return cp;
            }
        }

        public DotForm()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.TopMost = true;
            this.StartPosition = FormStartPosition.Manual;
            this.BackColor = Color.Magenta;
            this.TransparencyKey = Color.Magenta;
            this.Size = new Size(1, 1);
            this.Opacity = 0;

            this.CreateControl();
            IntPtr dummy = this.Handle;
        }

        public void ShowSolving()
        {
            CursorManager.ShowHourglass();
        }

        public void FlashReady()
        {
            CursorManager.RestoreCursor();
        }

        public void FlashError()
        {
            CursorManager.RestoreCursor();
        }
    }
}
