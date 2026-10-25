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
        private const int WM_NCHITTEST = 0x84;
        private const int HTCLIENT = 1;
        private const int HTCAPTION = 2;

        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();
        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        private Label lblAnswer;
        private Label lblStatus;
        private System.Windows.Forms.Timer autoHideTimer;

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WS_EX_TOOLWINDOW;
                cp.ExStyle |= WS_EX_NOACTIVATE;
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
                Text = "Hazır",
                ForeColor = Color.FromArgb(244, 244, 245),
                Font = new Font("Segoe UI", (float)config.FontSize, FontStyle.Bold),
                AutoSize = true,
                MaximumSize = new Size(460, 0),
                Location = new Point(26, 11),
                BackColor = Color.Transparent
            };

            this.Controls.Add(lblStatus);
            this.Controls.Add(lblAnswer);

            autoHideTimer = new System.Windows.Forms.Timer();
            autoHideTimer.Interval = 2500;
            autoHideTimer.Tick += (s, e) =>
            {
                autoHideTimer.Stop();
                this.Hide();
            };

            this.MouseDown += OnFormMouseDown;
            lblAnswer.MouseDown += OnFormMouseDown;
            lblStatus.MouseDown += OnFormMouseDown;
        }

        private void OnFormMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(this.Handle, 0xA1, 0x2, 0);
            }
        }

        public void SetLoading()
        {
            lblStatus.Text = "◌";
            lblStatus.ForeColor = Color.FromArgb(234, 179, 8); // Yellow
            lblAnswer.Text = "Oxunur...";
            lblAnswer.ForeColor = Color.FromArgb(212, 212, 216);
            AdjustSize();
            this.Show();
        }

        public void SetAnswer(string answer, bool autoHide = false)
        {
            lblStatus.Text = "●";
            lblStatus.ForeColor = Color.FromArgb(34, 197, 94); // Green
            lblAnswer.Text = answer;
            lblAnswer.ForeColor = Color.FromArgb(250, 250, 250);
            AdjustSize();
            this.Show();

            if (autoHide)
            {
                autoHideTimer.Stop();
                autoHideTimer.Start();
            }
        }

        public void SetError(string error)
        {
            lblStatus.Text = "✕";
            lblStatus.ForeColor = Color.FromArgb(239, 68, 68); // Red
            lblAnswer.Text = error;
            lblAnswer.ForeColor = Color.FromArgb(254, 202, 202);
            AdjustSize();
            this.Show();

            autoHideTimer.Stop();
            autoHideTimer.Interval = 3500;
            autoHideTimer.Start();
        }

        private void AdjustSize()
        {
            int requiredWidth = Math.Max(160, Math.Min(520, lblAnswer.Location.X + lblAnswer.PreferredWidth + 16));
            int requiredHeight = Math.Max(42, lblAnswer.Location.Y + lblAnswer.PreferredHeight + 14);
            this.Size = new Size(requiredWidth, requiredHeight);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            // Draw sleek rounded border
            using (Pen p = new Pen(Color.FromArgb(63, 63, 70), 1))
            {
                e.Graphics.DrawRectangle(p, 0, 0, this.Width - 1, this.Height - 1);
            }
        }
    }
}
