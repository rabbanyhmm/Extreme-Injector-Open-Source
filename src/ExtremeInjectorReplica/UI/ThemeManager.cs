using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ExtremeInjector.UI
{
    public static class ThemeManager
    {
        public static Color Background1 { get; set; } = Color.FromArgb(0, 150, 255);
        public static Color Background2 { get; set; } = Color.FromArgb(0, 180, 255);
        public static Color TextColor { get; set; } = Color.White;

        public static event Action? ThemeChanged;

        private static Icon? _appIcon;
        public static Icon AppIcon
        {
            get
            {
                if (_appIcon != null) return _appIcon;

                // 1. Extract directly from embedded assembly manifest resource
                try
                {
                    using var stream = typeof(ThemeManager).Assembly.GetManifestResourceStream("ExtremeInjector.ExtremeInjector.ico");
                    if (stream != null)
                    {
                        _appIcon = new Icon(stream);
                        return _appIcon;
                    }
                }
                catch { }

                // 2. Extract from own .exe binary directly
                try
                {
                    _appIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                    if (_appIcon != null) return _appIcon;
                }
                catch { }

                return SystemIcons.Application;
            }
        }

        public static void UpdateColors(string bg1, string bg2, string text)
        {
            try { Background1 = ColorTranslator.FromHtml(bg1); } catch { Background1 = Color.FromArgb(0, 150, 255); }
            try { Background2 = ColorTranslator.FromHtml(bg2); } catch { Background2 = Color.FromArgb(0, 180, 255); }
            try { TextColor = ColorTranslator.FromHtml(text); } catch { TextColor = Color.White; }
            ThemeChanged?.Invoke();
        }

        public static void DrawHeaderBanner(Graphics g, Rectangle bounds, string title, string subtitle = "")
        {
            if (bounds.Width <= 0 || bounds.Height <= 0) return;

            using (var brush = new System.Drawing.Drawing2D.LinearGradientBrush(bounds, Background1, Background2, System.Drawing.Drawing2D.LinearGradientMode.Horizontal))
            {
                g.FillRectangle(brush, bounds);
            }

            using (var font = new Font("Segoe UI", 12f, FontStyle.Bold))
            using (var textBrush = new SolidBrush(TextColor))
            {
                g.DrawString(title, font, textBrush, new PointF(bounds.Left + 10, bounds.Top + 6));
            }

            if (!string.IsNullOrEmpty(subtitle))
            {
                using (var subFont = new Font("Segoe UI", 8.25f, FontStyle.Regular))
                using (var subBrush = new SolidBrush(Color.FromArgb(220, TextColor)))
                {
                    g.DrawString(subtitle, subFont, subBrush, new PointF(bounds.Left + 12, bounds.Top + 26));
                }
            }
        }
    }

    public class TransparentLabel : Label
    {
        public TransparentLabel()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = Color.Transparent;
            ForeColor = Color.White;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (ClientRectangle.Width <= 0 || ClientRectangle.Height <= 0) return;
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, ForeColor,
                TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPadding);
        }
    }

    public class CustomGroupBox : GroupBox
    {
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public Color BorderColor { get; set; } = Color.FromArgb(0, 110, 200);

        public CustomGroupBox()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            ForeColor = Color.White;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width <= 0 || Height <= 0) return;
            var g = e.Graphics;
            var textSize = TextRenderer.MeasureText(Text, Font);

            using (var pen = new Pen(BorderColor, 1))
            {
                // Top-left line
                g.DrawLine(pen, 0, 7, 8, 7);
                // Top-right line
                g.DrawLine(pen, 8 + textSize.Width + 4, 7, Width - 1, 7);
                // Left line
                g.DrawLine(pen, 0, 7, 0, Height - 1);
                // Right line
                g.DrawLine(pen, Width - 1, 7, Width - 1, Height - 1);
                // Bottom line
                g.DrawLine(pen, 0, Height - 1, Width - 1, Height - 1);
            }

            TextRenderer.DrawText(g, Text, Font, new Point(10, 0), ForeColor, TextFormatFlags.Left);
        }
    }

    public class FlatCustomButton : Button
    {
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public bool IsPrimary { get; set; } = false;

        public FlatCustomButton()
        {
            FlatStyle = FlatStyle.System;
            Font = new Font("Segoe UI", 9f, FontStyle.Regular);
            UseVisualStyleBackColor = true;
        }
    }

    public class CustomTitleBar : Panel
    {
        private readonly Form _parentForm;
        private Image? _iconImage;
        private string _titleText;
        private bool _isActive = true;
        private int _hoveredButton = 0; // 0=none, 1=min, 2=max, 3=close
        private int _pressedButton = 0;

        public CustomTitleBar(Form parentForm, string title, Image? icon = null)
        {
            _parentForm = parentForm;
            _titleText = title;
            _iconImage = icon;
            Dock = DockStyle.Top;
            Height = 24;
            BackColor = Color.White;
            DoubleBuffered = true;

            _parentForm.Activated += (s, e) => { _isActive = true; Invalidate(); };
            _parentForm.Deactivate += (s, e) => { _isActive = false; Invalidate(); };

            MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left && e.X < Width - 96)
                {
                    ReleaseCapture();
                    SendMessage(_parentForm.Handle, 0x0112 /* WM_SYSCOMMAND */, (IntPtr)0xF012 /* SC_MOVE | 0x2 */, IntPtr.Zero);
                }
            };
        }

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int newHover = 0;
            if (e.X >= Width - 32) newHover = 3; // Close
            else if (e.X >= Width - 64) newHover = 2; // Maximize (disabled)
            else if (e.X >= Width - 96) newHover = 1; // Minimize

            if (newHover != _hoveredButton)
            {
                _hoveredButton = newHover;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoveredButton != 0 || _pressedButton != 0)
            {
                _hoveredButton = 0;
                _pressedButton = 0;
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                if (e.X >= Width - 32) _pressedButton = 3;
                else if (e.X >= Width - 64) _pressedButton = 2;
                else if (e.X >= Width - 96) _pressedButton = 1;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left)
            {
                if (_pressedButton == 3 && e.X >= Width - 32)
                {
                    _parentForm.Close();
                }
                else if (_pressedButton == 1 && e.X >= Width - 96 && e.X < Width - 64)
                {
                    _parentForm.WindowState = FormWindowState.Minimized;
                }
                _pressedButton = 0;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;

            // 1. Background
            g.Clear(Color.White);

            // 2. Icon (16x16)
            if (_iconImage != null)
            {
                g.DrawImage(_iconImage, new Rectangle(6, 4, 16, 16));
            }

            // 3. Title Text - Gray when unfocused, Black when focused
            Color textColor = _isActive ? Color.Black : Color.FromArgb(135, 135, 135);
            using (var font = new Font("Segoe UI", 9f, FontStyle.Regular))
            using (var brush = new SolidBrush(textColor))
            {
                int textX = (_iconImage != null) ? 26 : 8;
                g.DrawString(_titleText, font, brush, new PointF(textX, 4));
            }

            // 4. Buttons (32px each)
            int btnW = 32;
            int btnH = Height;
            Color btnIconColor = _isActive ? Color.FromArgb(50, 50, 50) : Color.FromArgb(145, 145, 145);

            // Minimize Button (_)
            var minRect = new Rectangle(Width - 96, 0, btnW, btnH);
            if (_hoveredButton == 1)
            {
                using var hBrush = new SolidBrush(_pressedButton == 1 ? Color.FromArgb(200, 200, 200) : Color.FromArgb(230, 230, 230));
                g.FillRectangle(hBrush, minRect);
            }
            using (var pen = new Pen(btnIconColor, 1))
            {
                int cx = minRect.Left + btnW / 2;
                int cy = minRect.Top + btnH / 2;
                g.DrawLine(pen, cx - 5, cy + 3, cx + 5, cy + 3);
            }

            // Maximize Button (▢) - Disabled
            var maxRect = new Rectangle(Width - 64, 0, btnW, btnH);
            Color maxBoxColor = _isActive ? Color.FromArgb(175, 175, 175) : Color.FromArgb(205, 205, 205);
            using (var pen = new Pen(maxBoxColor, 1))
            {
                int cx = maxRect.Left + btnW / 2;
                int cy = maxRect.Top + btnH / 2;
                g.DrawRectangle(pen, cx - 4, cy - 4, 8, 8);
            }

            // Close Button (✕)
            var closeRect = new Rectangle(Width - 32, 0, btnW, btnH);
            if (_hoveredButton == 3)
            {
                using var hBrush = new SolidBrush(_pressedButton == 3 ? Color.FromArgb(200, 15, 30) : Color.FromArgb(232, 17, 35));
                g.FillRectangle(hBrush, closeRect);
            }
            Color closeColor = (_hoveredButton == 3) ? Color.White : btnIconColor;
            using (var pen = new Pen(closeColor, 1.2f))
            {
                int cx = closeRect.Left + btnW / 2;
                int cy = closeRect.Top + btnH / 2;
                g.DrawLine(pen, cx - 4, cy - 4, cx + 4, cy + 4);
                g.DrawLine(pen, cx + 4, cy - 4, cx - 4, cy + 4);
            }

            // Bottom border of title bar
            using var borderPen = new Pen(Color.FromArgb(225, 225, 225));
            g.DrawLine(borderPen, 0, Height - 1, Width, Height - 1);
        }
    }
}
