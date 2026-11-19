using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace QuizAssistant
{
    public static class CaptureHelper
    {
        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);

        [DllImport("user32.dll")]
        public static extern IntPtr WindowFromPoint(Point pt);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left, Top, Right, Bottom;
        }

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        public static Rectangle GetActiveWindowOrScreenRect()
        {
            Point curPt = Cursor.Position;
            Rectangle curScreen = Screen.FromPoint(curPt).Bounds;

            IntPtr fgWnd = GetForegroundWindow();
            if (fgWnd != IntPtr.Zero)
            {
                uint fgPid;
                GetWindowThreadProcessId(fgWnd, out fgPid);
                uint myPid = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;

                if (fgPid != myPid)
                {
                    RECT r;
                    if (GetWindowRect(fgWnd, out r))
                    {
                        int w = r.Right - r.Left;
                        int h = r.Bottom - r.Top;
                        if (w >= 300 && h >= 200)
                        {
                            Rectangle winRect = new Rectangle(r.Left, r.Top, w, h);
                            Rectangle inter = Rectangle.Intersect(curScreen, winRect);
                            if (inter.Width >= 300 && inter.Height >= 200)
                            {
                                return inter;
                            }
                        }
                    }
                }
            }

            return curScreen;
        }

        public const uint WDA_NONE = 0x00000000;
        public const uint PW_RENDERFULLCONTENT = 0x00000002;

        public static bool IsMostlyBlack(Bitmap bmp)
        {
            if (bmp == null || bmp.Width < 2 || bmp.Height < 2) return false;

            int totalSamples = 0;
            int blackSamples = 0;

            int stepX = Math.Max(1, bmp.Width / 15);
            int stepY = Math.Max(1, bmp.Height / 15);

            for (int x = 0; x < bmp.Width; x += stepX)
            {
                for (int y = 0; y < bmp.Height; y += stepY)
                {
                    Color c = bmp.GetPixel(x, y);
                    totalSamples++;
                    if (c.R < 14 && c.G < 14 && c.B < 14)
                    {
                        blackSamples++;
                    }
                }
            }
            return totalSamples > 0 && ((double)blackSamples / (double)totalSamples) >= 0.90;
        }

        public static void UnlockWindowAffinity(IntPtr hWnd)
        {
            try
            {
                if (hWnd != IntPtr.Zero)
                {
                    SetWindowDisplayAffinity(hWnd, WDA_NONE);
                }
            }
            catch { }
        }

        public static Bitmap CaptureWithBypass(Rectangle screenRect)
        {
            Bitmap bmp = new Bitmap(screenRect.Width, screenRect.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(screenRect.Left, screenRect.Top, 0, 0, screenRect.Size, CopyPixelOperation.SourceCopy);
            }

            if (!IsMostlyBlack(bmp))
            {
                return bmp;
            }

            // Screen is blacked out by lockdown/DRM browser!
            IntPtr targetHwnd = WindowFromPoint(new Point(screenRect.Left + screenRect.Width / 2, screenRect.Top + screenRect.Height / 2));
            if (targetHwnd == IntPtr.Zero) targetHwnd = GetForegroundWindow();

            UnlockWindowAffinity(targetHwnd);
            IntPtr fg = GetForegroundWindow();
            if (fg != targetHwnd) UnlockWindowAffinity(fg);

            // Re-capture via GDI
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(screenRect.Left, screenRect.Top, 0, 0, screenRect.Size, CopyPixelOperation.SourceCopy);
            }

            if (!IsMostlyBlack(bmp))
            {
                return bmp;
            }

            // GDI still black: bypass via PrintWindow PW_RENDERFULLCONTENT (internal window memory)
            if (targetHwnd != IntPtr.Zero)
            {
                RECT r;
                if (GetWindowRect(targetHwnd, out r))
                {
                    int winW = r.Right - r.Left;
                    int winH = r.Bottom - r.Top;
                    if (winW > 10 && winH > 10)
                    {
                        Bitmap winBmp = new Bitmap(winW, winH, PixelFormat.Format32bppArgb);
                        using (Graphics gWin = Graphics.FromImage(winBmp))
                        {
                            IntPtr hdc = gWin.GetHdc();
                            try
                            {
                                PrintWindow(targetHwnd, hdc, PW_RENDERFULLCONTENT);
                            }
                            finally
                            {
                                gWin.ReleaseHdc(hdc);
                            }
                        }

                        int relX = Math.Max(0, screenRect.Left - r.Left);
                        int relY = Math.Max(0, screenRect.Top - r.Top);
                        int cropW = Math.Min(screenRect.Width, winW - relX);
                        int cropH = Math.Min(screenRect.Height, winH - relY);

                        if (cropW > 10 && cropH > 10 && !IsMostlyBlack(winBmp))
                        {
                            Bitmap cropped = new Bitmap(cropW, cropH, PixelFormat.Format32bppArgb);
                            using (Graphics gc = Graphics.FromImage(cropped))
                            {
                                gc.DrawImage(winBmp, new Rectangle(0, 0, cropW, cropH), new Rectangle(relX, relY, cropW, cropH), GraphicsUnit.Pixel);
                            }
                            bmp.Dispose();
                            winBmp.Dispose();
                            return cropped;
                        }
                        winBmp.Dispose();
                    }
                }
            }

            return bmp;
        }

        public static byte[] EncodeToJpeg(Bitmap bmp, long quality = 85L)
        {
            if (bmp == null) return null;
            using (MemoryStream ms = new MemoryStream())
            {
                ImageCodecInfo jpgEncoder = null;
                foreach (ImageCodecInfo codec in ImageCodecInfo.GetImageEncoders())
                {
                    if (codec.FormatID == ImageFormat.Jpeg.Guid)
                    {
                        jpgEncoder = codec;
                        break;
                    }
                }

                if (jpgEncoder != null)
                {
                    EncoderParameters encParams = new EncoderParameters(1);
                    encParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);
                    bmp.Save(ms, jpgEncoder, encParams);
                }
                else
                {
                    bmp.Save(ms, ImageFormat.Jpeg);
                }
                return ms.ToArray();
            }
        }
    }
}
