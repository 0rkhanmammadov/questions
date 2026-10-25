using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace QuizAssistant
{
    public class SniperForm : Form
    {
        private Point startPoint;
        private Rectangle selectRect;
        private bool isSelecting = false;
        private Action<byte[]> onCaptured;

        public SniperForm(Action<byte[]> onCaptured)
        {
            this.onCaptured = onCaptured;
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.Manual;
            this.ShowInTaskbar = false;
            this.TopMost = true;
            this.DoubleBuffered = true;
            this.Cursor = Cursors.Cross;

            // Cover all screens
            Rectangle totalBounds = SystemInformation.VirtualScreen;
            this.Bounds = totalBounds;
            this.BackColor = Color.Black;
            this.Opacity = 0.25;

            this.MouseDown += OnFormMouseDown;
            this.MouseMove += OnFormMouseMove;
            this.MouseUp += OnFormMouseUp;
            this.KeyDown += OnFormKeyDown;
        }

        private void OnFormKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                this.Close();
            }
        }

        private void OnFormMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                isSelecting = true;
                startPoint = e.Location;
                selectRect = new Rectangle(e.Location, new Size(0, 0));
                this.Invalidate();
            }
        }

        private void OnFormMouseMove(object sender, MouseEventArgs e)
        {
            if (isSelecting)
            {
                int x = Math.Min(startPoint.X, e.X);
                int y = Math.Min(startPoint.Y, e.Y);
                int w = Math.Abs(startPoint.X - e.X);
                int h = Math.Abs(startPoint.Y - e.Y);
                selectRect = new Rectangle(x, y, w, h);
                this.Invalidate();
            }
        }

        private void OnFormMouseUp(object sender, MouseEventArgs e)
        {
            if (isSelecting && e.Button == MouseButtons.Left)
            {
                isSelecting = false;
                this.Hide();

                if (selectRect.Width > 10 && selectRect.Height > 10)
                {
                    // Convert form client coordinates to screen coordinates
                    Point screenPt = this.PointToScreen(selectRect.Location);
                    CaptureRegion(new Rectangle(screenPt, selectRect.Size));
                }
                this.Close();
            }
        }

        private void CaptureRegion(Rectangle screenRect)
        {
            try
            {
                using (Bitmap bmp = CaptureHelper.CaptureWithBypass(screenRect))
                {
                    using (MemoryStream ms = new MemoryStream())
                    {
                        bmp.Save(ms, ImageFormat.Png);
                        byte[] pngBytes = ms.ToArray();
                        if (onCaptured != null)
                        {
                            onCaptured(pngBytes);
                        }
                    }
                }
            }
            catch { }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (selectRect.Width > 0 && selectRect.Height > 0)
            {
                using (Pen redPen = new Pen(Color.FromArgb(239, 68, 68), 2))
                {
                    e.Graphics.DrawRectangle(redPen, selectRect);
                }
                using (SolidBrush fillBrush = new SolidBrush(Color.FromArgb(40, 239, 68, 68)))
                {
                    e.Graphics.FillRectangle(fillBrush, selectRect);
                }
            }
        }
    }
}
