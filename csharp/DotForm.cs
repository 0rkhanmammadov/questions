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

        private System.Windows.Forms.Timer followTimer;
        private System.Windows.Forms.Timer animTimer;
        private int animFrame = 0;

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
            this.Size = new Size(24, 28);
            this.DoubleBuffered = true;

            followTimer = new System.Windows.Forms.Timer();
            followTimer.Interval = 25;
            followTimer.Tick += (s, e) => PositionAtCursor();

            animTimer = new System.Windows.Forms.Timer();
            animTimer.Interval = 100;
            animTimer.Tick += (s, e) =>
            {
                animFrame = (animFrame + 1) % 4;
                this.Invalidate();
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
            PositionAtCursor();
            this.Show();
            SetWindowPos(this.Handle, HWND_TOPMOST, Cursor.Position.X + 12, Cursor.Position.Y + 12, 0, 0, SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
            this.Invalidate();
            followTimer.Start();
            animTimer.Start();
        }

        public void FlashReady()
        {
            followTimer.Stop();
            animTimer.Stop();
            this.Hide();
        }

        public void FlashError()
        {
            followTimer.Stop();
            animTimer.Stop();
            this.Hide();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            // Caps (top & bottom rims)
            using (Brush rimBrush = new SolidBrush(Color.FromArgb(45, 52, 60)))
            {
                g.FillRectangle(rimBrush, 4, 2, 16, 3);
                g.FillRectangle(rimBrush, 4, 21, 16, 3);
            }
            using (Pen rimEdge = new Pen(Color.FromArgb(20, 24, 30), 1f))
            {
                g.DrawRectangle(rimEdge, 4, 2, 16, 3);
                g.DrawRectangle(rimEdge, 4, 21, 16, 3);
            }

            // Glass shape
            using (GraphicsPath glassPath = new GraphicsPath())
            {
                glassPath.AddLine(5, 5, 19, 5);
                glassPath.AddLine(19, 5, 14, 13);
                glassPath.AddLine(14, 13, 19, 21);
                glassPath.AddLine(19, 21, 5, 21);
                glassPath.AddLine(5, 21, 10, 13);
                glassPath.AddLine(10, 13, 5, 5);
                glassPath.CloseFigure();

                // Subtle transparent glass backing
                using (Brush glassBg = new SolidBrush(Color.FromArgb(30, 240, 245, 255)))
                {
                    g.FillPath(glassBg, glassPath);
                }

                // Top Sand
                Point[] topSand = new Point[]
                {
                    new Point(6, 6),
                    new Point(18, 6),
                    new Point(14, 11),
                    new Point(10, 11)
                };
                using (Brush sandBrush = new SolidBrush(Color.FromArgb(245, 158, 11)))
                using (Brush sandShadow = new SolidBrush(Color.FromArgb(217, 119, 6)))
                {
                    g.FillPolygon(sandBrush, topSand);

                    // Bottom Sand mound
                    Point[] botSand = new Point[]
                    {
                        new Point(12, 16),
                        new Point(17, 21),
                        new Point(7, 21)
                    };
                    g.FillPolygon(sandShadow, botSand);

                    // Falling sand stream (animated bead)
                    int dropY = 11 + (animFrame * 2);
                    using (Pen tricklePen = new Pen(Color.FromArgb(245, 158, 11), 1f))
                    {
                        g.DrawLine(tricklePen, 12, 11, 12, 16);
                        g.FillRectangle(sandBrush, 11, dropY, 2, 2);
                    }
                }

                // Glass outline
                using (Pen glassPen = new Pen(Color.FromArgb(148, 163, 184), 1.2f))
                {
                    g.DrawPath(glassPen, glassPath);
                }

                // Glass gleam highlight
                using (Pen gleamPen = new Pen(Color.FromArgb(170, 255, 255, 255), 1.0f))
                {
                    g.DrawLine(gleamPen, 7, 7, 10, 11);
                    g.DrawLine(gleamPen, 7, 19, 10, 15);
                }
            }
        }
    }
}
