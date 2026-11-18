using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace QuizAssistant
{
    public class DotForm : Form
    {
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const uint SWP_NOSIZE = 0x0001;

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private Color dotColor = Color.FromArgb(250, 204, 21); // Yellow
        private System.Windows.Forms.Timer followTimer;
        private System.Windows.Forms.Timer flashTimer;

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
                cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
                cp.ExStyle |= 0x00000020; // WS_EX_TRANSPARENT (clicks pass through)
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
            this.Size = new Size(16, 16);

            followTimer = new System.Windows.Forms.Timer();
            followTimer.Interval = 25;
            followTimer.Tick += (s, e) => PositionAtCursor();

            flashTimer = new System.Windows.Forms.Timer();
            flashTimer.Tick += (s, e) =>
            {
                flashTimer.Stop();
                this.Hide();
            };

            this.CreateControl();
            IntPtr dummy = this.Handle;
        }

        private void PositionAtCursor()
        {
            Point cur = Cursor.Position;
            int x = cur.X + 12;
            int y = cur.Y + 12;
            if (this.Visible)
            {
                SetWindowPos(this.Handle, HWND_TOPMOST, x, y, 0, 0, SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
            }
            else
            {
                this.Location = new Point(x, y);
            }
        }

        public void ShowSolving()
        {
            flashTimer.Stop();
            dotColor = Color.FromArgb(250, 204, 21); // Yellow
            PositionAtCursor();
            this.Show();
            SetWindowPos(this.Handle, HWND_TOPMOST, Cursor.Position.X + 12, Cursor.Position.Y + 12, 0, 0, SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
            this.Invalidate();
            followTimer.Start();
        }

        public void FlashReady()
        {
            followTimer.Stop();
            flashTimer.Stop();
            dotColor = Color.FromArgb(34, 197, 94); // Green flash
            PositionAtCursor();
            this.Show();
            SetWindowPos(this.Handle, HWND_TOPMOST, Cursor.Position.X + 12, Cursor.Position.Y + 12, 0, 0, SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
            this.Invalidate();
            flashTimer.Interval = 400;
            flashTimer.Start();
        }

        public void FlashError()
        {
            followTimer.Stop();
            flashTimer.Stop();
            dotColor = Color.FromArgb(239, 68, 68); // Red error flash
            PositionAtCursor();
            this.Show();
            SetWindowPos(this.Handle, HWND_TOPMOST, Cursor.Position.X + 12, Cursor.Position.Y + 12, 0, 0, SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
            this.Invalidate();
            flashTimer.Interval = 500;
            flashTimer.Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (Brush b = new SolidBrush(dotColor))
            {
                e.Graphics.FillEllipse(b, 2, 2, 12, 12);
            }
        }
    }
}
