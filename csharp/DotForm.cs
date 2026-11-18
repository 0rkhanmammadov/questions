using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace QuizAssistant
{
    public class DotForm : Form
    {
        private Color dotColor = Color.FromArgb(234, 179, 8); // Yellow
        private System.Windows.Forms.Timer flashTimer;

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
                cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
                cp.ExStyle |= 0x00000020; // WS_EX_TRANSPARENT (click-through)
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

            flashTimer = new System.Windows.Forms.Timer();
            flashTimer.Interval = 350;
            flashTimer.Tick += (s, e) =>
            {
                flashTimer.Stop();
                this.Hide();
            };
        }

        public void ShowSolving(Point cursorPt)
        {
            flashTimer.Stop();
            dotColor = Color.FromArgb(234, 179, 8); // Yellow
            this.Location = new Point(cursorPt.X + 14, cursorPt.Y + 14);
            this.Show();
            this.Invalidate();
        }

        public void FlashReady()
        {
            flashTimer.Stop();
            Point cur = Cursor.Position;
            this.Location = new Point(cur.X + 14, cur.Y + 14);
            dotColor = Color.FromArgb(34, 197, 94); // Green
            this.Show();
            this.Invalidate();
            flashTimer.Interval = 350;
            flashTimer.Start();
        }

        public void FlashError()
        {
            flashTimer.Stop();
            Point cur = Cursor.Position;
            this.Location = new Point(cur.X + 14, cur.Y + 14);
            dotColor = Color.FromArgb(239, 68, 68); // Red
            this.Show();
            this.Invalidate();
            flashTimer.Interval = 400;
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
