using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace ExtremeInjector.UI
{
    /// <summary>
    /// Interactive Window Drag Picker Tool with desktop DC border highlighting.
    /// Allows users to click and drag a target crosshair over any application/game window
    /// to instantly identify and select its process.
    /// </summary>
    public class WindowPickerForm : Form
    {
        private PictureBox picTarget = null!;
        private Label lblInstructions = null!;
        private GroupBox grpSelectedInfo = null!;
        private Label lblTitle = null!;
        private Label lblProcess = null!;
        private Label lblPid = null!;
        private Label lblHwnd = null!;
        private Button btnSelect = null!;
        private Button btnCancel = null!;

        private bool _isDragging = false;
        private IntPtr _lastHighlightedHwnd = IntPtr.Zero;
        private Bitmap _crosshairNormal = null!;
        private Bitmap _crosshairActive = null!;

        public string SelectedProcessName { get; private set; } = "";
        public int SelectedProcessId { get; private set; } = 0;
        public string SelectedWindowTitle { get; private set; } = "";

        public WindowPickerForm()
        {
            InitializeComponent();
            GenerateCrosshairIcons();
            picTarget.Image = _crosshairNormal;
        }

        private void InitializeComponent()
        {
            Text = "Window Drag Picker";
            ClientSize = new Size(330, 260);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowIcon = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 9f);
            BackColor = Color.FromArgb(240, 240, 240);

            // 1. Target Finder PictureBox (Large interactive target)
            picTarget = new PictureBox
            {
                Location = new Point(14, 16),
                Size = new Size(54, 54),
                SizeMode = PictureBoxSizeMode.CenterImage,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Cursor = Cursors.Hand
            };
            picTarget.MouseDown += PicTarget_MouseDown;
            picTarget.MouseMove += PicTarget_MouseMove;
            picTarget.MouseUp += PicTarget_MouseUp;

            // 2. Instructions Label
            lblInstructions = new Label
            {
                Text = "Drag the crosshair icon over any window on your screen to select its process, then release the mouse.",
                Location = new Point(78, 16),
                Size = new Size(238, 54),
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(50, 50, 50)
            };

            // 3. Selected Window Info GroupBox
            grpSelectedInfo = new GroupBox
            {
                Text = "Target Window Details",
                Location = new Point(14, 78),
                Size = new Size(302, 130),
                Font = new Font("Segoe UI", 9f)
            };

            lblTitle = new Label
            {
                Text = "Title: (None)",
                Location = new Point(10, 22),
                Size = new Size(282, 18),
                Font = new Font("Segoe UI", 8.5f),
                AutoEllipsis = true
            };

            lblProcess = new Label
            {
                Text = "Process: (None)",
                Location = new Point(10, 46),
                Size = new Size(282, 18),
                Font = new Font("Segoe UI", 8.5f),
                AutoEllipsis = true
            };

            lblPid = new Label
            {
                Text = "PID: (None)",
                Location = new Point(10, 70),
                Size = new Size(282, 18),
                Font = new Font("Segoe UI", 8.5f)
            };

            lblHwnd = new Label
            {
                Text = "Handle: (None)",
                Location = new Point(10, 94),
                Size = new Size(282, 18),
                Font = new Font("Segoe UI", 8.5f)
            };

            grpSelectedInfo.Controls.AddRange(new Control[] {
                lblTitle,
                lblProcess,
                lblPid,
                lblHwnd
            });

            // 4. Action Buttons
            btnSelect = new Button
            {
                Text = "Select",
                Location = new Point(138, 220),
                Size = new Size(86, 26),
                FlatStyle = FlatStyle.System,
                UseVisualStyleBackColor = true,
                Enabled = false
            };
            btnSelect.Click += (s, e) =>
            {
                if (SelectedProcessId > 0 && !string.IsNullOrEmpty(SelectedProcessName))
                {
                    DialogResult = DialogResult.OK;
                    Close();
                }
            };

            btnCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(230, 220),
                Size = new Size(86, 26),
                FlatStyle = FlatStyle.System,
                UseVisualStyleBackColor = true
            };
            btnCancel.Click += (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            };

            Controls.AddRange(new Control[] {
                picTarget,
                lblInstructions,
                grpSelectedInfo,
                btnSelect,
                btnCancel
            });
        }

        private void GenerateCrosshairIcons()
        {
            _crosshairNormal = new Bitmap(48, 48);
            using (var g = Graphics.FromImage(_crosshairNormal))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.White);
                using var pen = new Pen(Color.FromArgb(0, 120, 215), 2);
                g.DrawEllipse(pen, 8, 8, 32, 32);
                g.DrawLine(pen, 24, 2, 24, 18);
                g.DrawLine(pen, 24, 30, 24, 46);
                g.DrawLine(pen, 2, 24, 18, 24);
                g.DrawLine(pen, 30, 24, 46, 24);
                using var brush = new SolidBrush(Color.FromArgb(0, 120, 215));
                g.FillEllipse(brush, 22, 22, 5, 5);
            }

            _crosshairActive = new Bitmap(48, 48);
            using (var g = Graphics.FromImage(_crosshairActive))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.FromArgb(230, 240, 255));
                using var pen = new Pen(Color.Red, 2);
                g.DrawEllipse(pen, 8, 8, 32, 32);
                g.DrawLine(pen, 24, 2, 24, 18);
                g.DrawLine(pen, 24, 30, 24, 46);
                g.DrawLine(pen, 2, 24, 18, 24);
                g.DrawLine(pen, 30, 24, 46, 24);
                using var brush = new SolidBrush(Color.Red);
                g.FillEllipse(brush, 21, 21, 7, 7);
            }
        }

        private void PicTarget_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _isDragging = true;
                picTarget.Image = _crosshairActive;
                Cursor = Cursors.Cross;
                Capture = true;
            }
        }

        private void PicTarget_MouseMove(object? sender, MouseEventArgs e)
        {
            if (!_isDragging) return;

            Point screenPt = Cursor.Position;
            IntPtr hWnd = WindowFromPoint(new POINT { X = screenPt.X, Y = screenPt.Y });

            if (hWnd != IntPtr.Zero)
            {
                IntPtr rootHwnd = GetAncestor(hWnd, 2 /* GA_ROOT */);
                if (rootHwnd != IntPtr.Zero) hWnd = rootHwnd;

                // Don't highlight own form
                if (hWnd != Handle)
                {
                    if (hWnd != _lastHighlightedHwnd)
                    {
                        ClearHighlight(_lastHighlightedHwnd);
                        _lastHighlightedHwnd = hWnd;
                        DrawHighlight(hWnd);
                    }

                    UpdateTargetInfo(hWnd);
                }
            }
        }

        private void PicTarget_MouseUp(object? sender, MouseEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                Capture = false;
                Cursor = Cursors.Default;
                picTarget.Image = _crosshairNormal;

                if (_lastHighlightedHwnd != IntPtr.Zero)
                {
                    ClearHighlight(_lastHighlightedHwnd);
                    _lastHighlightedHwnd = IntPtr.Zero;
                }

                btnSelect.Enabled = (SelectedProcessId > 0 && !string.IsNullOrEmpty(SelectedProcessName));
            }
        }

        private void UpdateTargetInfo(IntPtr hWnd)
        {
            GetWindowThreadProcessId(hWnd, out uint pid);
            if (pid == 0 || pid == (uint)Process.GetCurrentProcess().Id) return;

            int titleLen = GetWindowTextLength(hWnd);
            var sb = new StringBuilder(titleLen + 1);
            if (titleLen > 0)
            {
                GetWindowText(hWnd, sb, sb.Capacity);
            }
            string title = sb.ToString();

            string procName = "";
            try
            {
                var proc = Process.GetProcessById((int)pid);
                procName = proc.ProcessName + ".exe";
            }
            catch
            {
                procName = "Unknown";
            }

            SelectedProcessId = (int)pid;
            SelectedProcessName = procName;
            SelectedWindowTitle = title;

            lblTitle.Text = $"Title: {(string.IsNullOrWhiteSpace(title) ? "(None)" : title)}";
            lblProcess.Text = $"Process: {procName}";
            lblPid.Text = $"PID: 0x{pid:X} ({pid})";
            lblHwnd.Text = $"Handle: 0x{hWnd.ToInt64():X}";
        }

        private static void DrawHighlight(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero || !IsWindow(hWnd)) return;

            if (GetWindowRect(hWnd, out RECT rect))
            {
                IntPtr hDC = GetWindowDC(hWnd);
                if (hDC != IntPtr.Zero)
                {
                    try
                    {
                        IntPtr hPen = CreatePen(0 /* PS_SOLID */, 3, 0x0000FF /* Red in BGR */);
                        IntPtr hOldPen = SelectObject(hDC, hPen);
                        IntPtr hNullBrush = GetStockObject(5 /* NULL_BRUSH */);
                        IntPtr hOldBrush = SelectObject(hDC, hNullBrush);

                        int width = rect.Right - rect.Left;
                        int height = rect.Bottom - rect.Top;
                        Rectangle(hDC, 0, 0, width, height);

                        SelectObject(hDC, hOldPen);
                        SelectObject(hDC, hOldBrush);
                        DeleteObject(hPen);
                    }
                    finally
                    {
                        ReleaseDC(hWnd, hDC);
                    }
                }
            }
        }

        private static void ClearHighlight(IntPtr hWnd)
        {
            if (hWnd != IntPtr.Zero && IsWindow(hWnd))
            {
                InvalidateRect(hWnd, IntPtr.Zero, true);
                UpdateWindow(hWnd);
                RedrawWindow(hWnd, IntPtr.Zero, IntPtr.Zero, 0x0001 | 0x0002 | 0x0004 | 0x0080 /* RDW_INVALIDATE | RDW_INTERNALPAINT | RDW_ERASE | RDW_UPDATENOW */);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_lastHighlightedHwnd != IntPtr.Zero)
            {
                ClearHighlight(_lastHighlightedHwnd);
                _lastHighlightedHwnd = IntPtr.Zero;
            }
            _crosshairNormal?.Dispose();
            _crosshairActive?.Dispose();
            base.OnFormClosing(e);
        }

        #region Native Win32 Imports

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(POINT point);

        [DllImport("user32.dll")]
        private static extern IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, bool bErase);

        [DllImport("user32.dll")]
        private static extern bool UpdateWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool RedrawWindow(IntPtr hWnd, IntPtr lprcUpdate, IntPtr hrgnUpdate, uint flags);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreatePen(int fnPenStyle, int nWidth, uint crColor);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll")]
        private static extern bool Rectangle(IntPtr hdc, int nLeftRect, int nTopRect, int nRightRect, int nBottomRect);

        [DllImport("gdi32.dll")]
        private static extern IntPtr GetStockObject(int fnObject);

        #endregion
    }
}
