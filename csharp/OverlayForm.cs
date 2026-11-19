using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace QuizAssistant
{
    public class OverlayForm : Form
    {
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);
        private const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011;

        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();
        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        private Label lblAnswer;
        private Label lblStatus;

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WS_EX_TOOLWINDOW;
                cp.ExStyle |= WS_EX_NOACTIVATE;
                cp.ExStyle |= 0x00000008; // WS_EX_TOPMOST
                return cp;
            }
        }

        public OverlayForm(AppConfig config)
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.TopMost = true;
            this.StartPosition = FormStartPosition.Manual;
            this.BackColor = Color.FromArgb(24, 24, 27);
            this.Opacity = config.Opacity;
            this.Size = new Size(160, 42);
            this.Padding = new Padding(8, 4, 8, 4);

            lblStatus = new Label
            {
                Text = "●",
                ForeColor = Color.FromArgb(34, 197, 94),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(8, 12),
                BackColor = Color.Transparent
            };

            lblAnswer = new Label
            {
                Text = "",
                ForeColor = Color.FromArgb(244, 244, 245),
                Font = new Font("Segoe UI", (float)config.FontSize, FontStyle.Bold),
                AutoSize = true,
                MaximumSize = new Size(460, 0),
                Location = new Point(26, 11),
                BackColor = Color.Transparent
            };

            this.Controls.Add(lblStatus);
            this.Controls.Add(lblAnswer);

            this.MouseDown += OnFormMouseDown;
            lblAnswer.MouseDown += OnFormMouseDown;
            lblStatus.MouseDown += OnFormMouseDown;

            this.CreateControl();
            IntPtr dummy = this.Handle;
            try { SetWindowDisplayAffinity(this.Handle, WDA_EXCLUDEFROMCAPTURE); } catch { }
        }

        private void OnFormMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(this.Handle, 0xA1, 0x2, 0);
            }
        }

        public void ShowAnswer(string answer, Point cursorPt)
        {
            if (string.IsNullOrEmpty(answer)) answer = "Oxunmayıb";
            lblStatus.Text = "●";
            lblStatus.ForeColor = Color.FromArgb(34, 197, 94);
            lblAnswer.Text = answer;
            lblAnswer.ForeColor = Color.FromArgb(250, 250, 250);
            AdjustSize();

            Rectangle screen = Screen.FromPoint(cursorPt).WorkingArea;
            int x = cursorPt.X + 15;
            int y = cursorPt.Y + 15;
            if (x + this.Width > screen.Right) x = cursorPt.X - this.Width - 10;
            if (y + this.Height > screen.Bottom) y = cursorPt.Y - this.Height - 10;
            if (x < screen.Left) x = screen.Left + 10;
            if (y < screen.Top) y = screen.Top + 10;

            this.Location = new Point(x, y);
            this.Show();
            SetWindowPos(this.Handle, HWND_TOPMOST, x, y, this.Width, this.Height, SWP_NOACTIVATE | SWP_SHOWWINDOW);
        }

        public void HideAnswer()
        {
            this.Hide();
        }

        private void AdjustSize()
        {
            int requiredWidth = Math.Max(120, Math.Min(520, lblAnswer.Location.X + lblAnswer.PreferredWidth + 16));
            int requiredHeight = Math.Max(38, lblAnswer.Location.Y + lblAnswer.PreferredHeight + 14);
            this.Size = new Size(requiredWidth, requiredHeight);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Pen p = new Pen(Color.FromArgb(63, 63, 70), 1))
            {
                e.Graphics.DrawRectangle(p, 0, 0, this.Width - 1, this.Height - 1);
            }
        }
    }
}
