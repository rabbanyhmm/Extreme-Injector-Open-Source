using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;
using ExtremeInjector.Config;
using ExtremeInjector.Core;

namespace ExtremeInjector.UI
{
    public class MainForm : Form
    {
        private PictureBox picAppIcon = null!;
        private TransparentLabel lblProcess = null!;
        private TextBox txtProcess = null!;
        private Button btnSelect = null!;
        private TransparentLabel lblProcessTitle = null!;
        private TransparentLabel lblProcessPid = null!;

        private CustomGroupBox grpInjectList = null!;
        private Button btnAdd = null!;
        private Button btnEnableDisable = null!;
        private Button btnRemove = null!;
        private Button btnClear = null!;
        private ListView lstDlls = null!;

        private Button btnAbout = null!;
        private Button btnSettings = null!;
        private Button btnInject = null!;

        private ContextMenuStrip ctxMenu = null!;
        private int currentSelectedIndex = -1;
        private int currentSelectedPid = 0;
        private bool isSortAscending = true;
        private ListViewHeaderListener? headerListener;
        private CustomTitleBar titleBar = null!;
        private Panel pnlContent = null!;
        private Panel pnlBottomButtons = null!;

        private System.Windows.Forms.Timer _autoInjectTimer = null!;
        private readonly HashSet<int> _autoInjectedPids = new();
        private bool _isAutoInjecting = false;

        public MainForm()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            InitializeComponent();
            ApplySettings();
            ThemeManager.ThemeChanged += () =>
            {
                lblProcess.ForeColor = ThemeManager.TextColor;
                lblProcessTitle.ForeColor = ThemeManager.TextColor;
                lblProcessPid.ForeColor = ThemeManager.TextColor;
                grpInjectList.ForeColor = ThemeManager.TextColor;
                grpInjectList.BorderColor = Color.FromArgb(
                    Math.Max(0, ThemeManager.Background1.R - 40),
                    Math.Max(0, ThemeManager.Background1.G - 40),
                    Math.Max(0, ThemeManager.Background1.B - 40)
                );
                Invalidate(true);
                titleBar?.Invalidate();
                pnlContent?.Invalidate();
                grpInjectList?.Invalidate();
            };
            Activated += (s, e) => Invalidate();
            Deactivate += (s, e) => Invalidate();
        }

        private void InitializeComponent()
        {
            Text = "Extreme Injector v3.7.3 by rabbanyhmm";
            ClientSize = new Size(365, 277);
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9f);
            DoubleBuffered = true;

            Icon = ThemeManager.AppIcon;

            // 1. Process Icon (Left)
            picAppIcon = new PictureBox
            {
                Location = new Point(12, 14),
                Size = new Size(36, 36),
                SizeMode = PictureBoxSizeMode.StretchImage,
                BackColor = Color.Transparent,
                Visible = false,
                Cursor = Cursors.Hand
            };
            picAppIcon.Click += PicAppIcon_Click;

            // 2. Process Selection Controls
            lblProcess = new TransparentLabel
            {
                Text = "Process Name:",
                Location = new Point(56, 8),
                Size = new Size(120, 15),
                Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                ForeColor = Color.White
            };

            txtProcess = new TextBox
            {
                Location = new Point(56, 25),
                Size = new Size(224, 23),
                Font = new Font("Segoe UI", 9f)
            };
            txtProcess.TextChanged += TxtProcess_TextChanged;

            btnSelect = new Button
            {
                Text = "Select",
                Location = new Point(282, 24),
                Size = new Size(72, 25),
                FlatStyle = FlatStyle.System,
                UseVisualStyleBackColor = true
            };
            btnSelect.Click += BtnSelect_Click;

            lblProcessTitle = new TransparentLabel
            {
                Text = "",
                Location = new Point(56, 50),
                Size = new Size(224, 14),
                Font = new Font("Segoe UI", 8.25f, FontStyle.Italic),
                ForeColor = Color.White,
                Visible = false
            };

            lblProcessPid = new TransparentLabel
            {
                Text = "",
                Location = new Point(56, 65),
                Size = new Size(224, 14),
                Font = new Font("Segoe UI", 8.25f, FontStyle.Italic),
                ForeColor = Color.White,
                Visible = false
            };

            // 3. Inject List Custom Group Box
            grpInjectList = new CustomGroupBox
            {
                Text = "Inject List",
                Location = new Point(10, 84),
                Size = new Size(345, 133),
                BorderColor = Color.FromArgb(0, 95, 175)
            };

            // =========================================================================
            // INJECT LIST SIDE BUTTONS (ADD DLL / ENABLE/DISABLE / REMOVE / CLEAR)
            // =========================================================================
            int sideBtnX = 8;          // ← Left X position inside the group box
            int sideBtnStartY = 18;    // ← Top Y starting position of the first button
            int sideBtnWidth = 90;     // ← Width of all 4 buttons at once
            int sideBtnHeight = 22;    // ← Height of all 4 buttons at once
            double sideBtnSpacing = 5.8; // ← Vertical gap/spacing between buttons (supports decimals)

            btnAdd = new Button
            {
                Text = "Add DLL",
                Location = new Point(sideBtnX, sideBtnStartY),
                Size = new Size(sideBtnWidth, sideBtnHeight),
                FlatStyle = FlatStyle.System,
                UseVisualStyleBackColor = true
            };
            btnAdd.Click += BtnAdd_Click;

            btnEnableDisable = new Button
            {
                Text = "Enable/Disable",
                Location = new Point(sideBtnX, (int)Math.Round(sideBtnStartY + (sideBtnHeight + sideBtnSpacing) * 1)),
                Size = new Size(sideBtnWidth, sideBtnHeight),
                FlatStyle = FlatStyle.System,
                UseVisualStyleBackColor = true
            };
            btnEnableDisable.Click += BtnEnableDisable_Click;

            btnRemove = new Button
            {
                Text = "Remove",
                Location = new Point(sideBtnX, (int)Math.Round(sideBtnStartY + (sideBtnHeight + sideBtnSpacing) * 2)),
                Size = new Size(sideBtnWidth, sideBtnHeight),
                FlatStyle = FlatStyle.System,
                UseVisualStyleBackColor = true
            };
            btnRemove.Click += BtnRemove_Click;

            btnClear = new Button
            {
                Text = "Clear",
                Location = new Point(sideBtnX, (int)Math.Round(sideBtnStartY + (sideBtnHeight + sideBtnSpacing) * 3)),
                Size = new Size(sideBtnWidth, sideBtnHeight),
                FlatStyle = FlatStyle.System,
                UseVisualStyleBackColor = true
            };
            btnClear.Click += BtnClear_Click;

            var imgList = new ImageList { ImageSize = new Size(1, 13), ColorDepth = ColorDepth.Depth32Bit };
            imgList.Images.Add(new Bitmap(1, 13));

            // DLL List View
            lstDlls = new ListView
            {
                Location = new Point(104, 16),
                Size = new Size(232, 107),
                View = View.Details,
                FullRowSelect = true,
                CheckBoxes = false,
                HeaderStyle = ColumnHeaderStyle.Clickable,
                Font = new Font("Segoe UI", 8.5f),
                BackColor = Color.White,
                ForeColor = Color.Black,
                OwnerDraw = true,
                MultiSelect = false
            };
            typeof(ListView).InvokeMember("DoubleBuffered",
                System.Reflection.BindingFlags.SetProperty | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null, lstDlls, new object[] { true });

            var itemImageList = new ImageList { ImageSize = new Size(1, 20) };
            lstDlls.SmallImageList = itemImageList;

            lstDlls.Columns.Add("DLL Name", 228);
            lstDlls.Resize += (s, e) => { if (lstDlls.Columns.Count > 0) lstDlls.Columns[0].Width = lstDlls.ClientSize.Width; };
            lstDlls.ColumnWidthChanging += (s, e) => { e.Cancel = true; e.NewWidth = lstDlls.ClientSize.Width; };
            lstDlls.AllowDrop = true;
            lstDlls.DragEnter += LstDlls_DragEnter;
            lstDlls.DragDrop += LstDlls_DragDrop;
            lstDlls.ColumnClick += LstDlls_ColumnClick;
            lstDlls.DrawColumnHeader += LstDlls_DrawColumnHeader;
            lstDlls.DrawItem += LstDlls_DrawItem;
            lstDlls.DrawSubItem += LstDlls_DrawSubItem;
            lstDlls.MouseDown += LstDlls_MouseDown;
            headerListener = new ListViewHeaderListener(lstDlls);

            // Header resize is applied in OnLoad after all HWNDs are created

            // Context Menu
            ctxMenu = new ContextMenuStrip();
            ctxMenu.Items.Add("Configure DLL...", null, CtxConfigDll_Click);
            ctxMenu.Items.Add(new ToolStripSeparator());
            ctxMenu.Items.Add("Add DLL...", null, (s, e) => BtnAdd_Click(s, e));
            ctxMenu.Items.Add("Remove DLL", null, (s, e) => BtnRemove_Click(s, e));
            ctxMenu.Items.Add("Clear List", null, (s, e) => BtnClear_Click(s, e));
            ctxMenu.Items.Add(new ToolStripSeparator());
            ctxMenu.Items.Add("Move Up", null, CtxMoveUp_Click);
            ctxMenu.Items.Add("Move Down", null, CtxMoveDown_Click);
            ctxMenu.Items.Add(new ToolStripSeparator());
            ctxMenu.Items.Add("Open Containing Folder", null, CtxOpenFolder_Click);
            lstDlls.ContextMenuStrip = ctxMenu;
            lstDlls.DoubleClick += (s, e) => CtxConfigDll_Click(s, e);

            grpInjectList.Controls.AddRange(new Control[] { btnAdd, btnEnableDisable, btnRemove, btnClear, lstDlls });

            // =========================================================================
            // 4. BOTTOM BUTTONS CONTROLS (ABOUT / SETTINGS / INJECT)
            // =========================================================================
            int btnWidth = 94;   // ← ADJUST WIDTH OF ALL 3 BUTTONS AT ONCE HERE
            int btnHeight = 23;  // ← ADJUST HEIGHT OF ALL 3 BUTTONS AT ONCE HERE
            int btnSpacing = 30; // ← ADJUST SPACING BETWEEN BUTTONS HERE

            pnlBottomButtons = new Panel
            {
                Location = new Point(10, 223), // ← ADJUST (X, Y) POSITION OF THE ENTIRE GROUP HERE
                Size = new Size((btnWidth * 3) + (btnSpacing * 2), btnHeight + 2),
                BackColor = Color.Transparent
            };

            btnAbout = new Button
            {
                Text = "About",
                Location = new Point(0, 0),
                Size = new Size(btnWidth, btnHeight),
                FlatStyle = FlatStyle.System,
                UseVisualStyleBackColor = true
            };
            btnAbout.Click += BtnAbout_Click;

            btnSettings = new Button
            {
                Text = "Settings",
                Location = new Point(btnWidth + btnSpacing, 0),
                Size = new Size(btnWidth, btnHeight),
                FlatStyle = FlatStyle.System,
                UseVisualStyleBackColor = true
            };
            btnSettings.Click += BtnSettings_Click;

            btnInject = new Button
            {
                Text = "Inject",
                Location = new Point((btnWidth + btnSpacing) * 2, 0),
                Size = new Size(btnWidth, btnHeight),
                FlatStyle = FlatStyle.System,
                UseVisualStyleBackColor = true,
                Enabled = false
            };
            btnInject.Click += BtnInject_Click;

            pnlBottomButtons.Controls.AddRange(new Control[] {
                btnAbout,
                btnSettings,
                btnInject
            });

            pnlContent = new Panel
            {
                Location = new Point(0, 24),
                Size = new Size(365, 253),
                BackColor = Color.Transparent
            };

            pnlContent.Controls.AddRange(new Control[] {
                picAppIcon,
                lblProcess,
                txtProcess,
                btnSelect,
                lblProcessTitle,
                lblProcessPid,
                grpInjectList,
                pnlBottomButtons
            });

            titleBar = new CustomTitleBar(this, Text, Icon != null ? new Icon(Icon, new Size(16, 16)).ToBitmap() : null)
            {
                Height = 24
            };

            _autoInjectTimer = new System.Windows.Forms.Timer { Interval = 400 };
            _autoInjectTimer.Tick += AutoInjectTimer_Tick;

            Controls.AddRange(new Control[] {
                pnlContent,
                titleBar
            });
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                const int CS_DROPSHADOW = 0x00020000;
                const int WS_MINIMIZEBOX = 0x00020000;
                cp.ClassStyle |= CS_DROPSHADOW;
                cp.Style |= WS_MINIMIZEBOX;
                return cp;
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            ForceHeaderHeight(15);
        }

        private void ForceHeaderHeight(int targetHeight)
        {
            if (!lstDlls.IsHandleCreated) return;
            IntPtr hHeader = SendMessage(lstDlls.Handle, 0x101F /* LVM_GETHEADER */, IntPtr.Zero, IntPtr.Zero);
            if (hHeader != IntPtr.Zero && GetClientRect(hHeader, out RECT rc))
            {
                // SWP_NOZORDER (0x4) | SWP_NOMOVE (0x2) = only resize, keep position
                SetWindowPos(hHeader, IntPtr.Zero, 0, 0, rc.Right - rc.Left, targetHeight, 0x0006);
                // Force the ListView to re-layout
                SendMessage(lstDlls.Handle, 0x0A /*WM_SETREDRAW*/, (IntPtr)0, IntPtr.Zero);
                SendMessage(lstDlls.Handle, 0x0A /*WM_SETREDRAW*/, (IntPtr)1, IntPtr.Zero);
                lstDlls.Refresh();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (ClientRectangle.Width <= 0 || ClientRectangle.Height <= 0) return;
            using (var brush = new LinearGradientBrush(ClientRectangle, ThemeManager.Background1, ThemeManager.Background2, LinearGradientMode.Horizontal))
            {
                e.Graphics.FillRectangle(brush, ClientRectangle);
            }
            Color borderColor = (ActiveForm == this) ? Color.FromArgb(0, 120, 215) : Color.FromArgb(175, 175, 175);
            using (var borderPen = new Pen(borderColor, 1))
            {
                e.Graphics.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);
            }
        }

        private void TxtProcess_TextChanged(object? sender, EventArgs e)
        {
            SettingsManager.Current.ProcessName = txtProcess.Text;
            UpdateProcessDetails();
            UpdateInjectButtonState();
        }

        private void UpdateProcessDetails()
        {
            string name = txtProcess.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                picAppIcon.Visible = false;
                lblProcessTitle.Visible = false;
                lblProcessPid.Visible = false;
                return;
            }

            try
            {
                var processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(name));
                if (processes.Length > 0)
                {
                    var p = processes[0];
                    currentSelectedPid = p.Id;
                    string title = "";
                    try
                    {
                        string fullPath = GetProcessFullPath(p);
                        if (!string.IsNullOrEmpty(fullPath) && File.Exists(fullPath))
                        {
                            var vi = FileVersionInfo.GetVersionInfo(fullPath);
                            if (!string.IsNullOrWhiteSpace(vi.FileDescription))
                            {
                                title = vi.FileDescription;
                            }
                            else if (!string.IsNullOrWhiteSpace(vi.ProductName))
                            {
                                title = vi.ProductName;
                            }
                        }

                        if (string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(p.MainWindowTitle))
                        {
                            title = p.MainWindowTitle;
                        }
                    }
                    catch { }

                    if (string.IsNullOrWhiteSpace(title))
                    {
                        title = p.ProcessName;
                    }

                    lblProcessTitle.Text = title;
                    lblProcessPid.Text = $"Process ID: 0x{p.Id:X} ({p.Id})";
                    lblProcessTitle.Visible = true;
                    lblProcessPid.Visible = true;

                    Image? icon = GetProcessIcon(p);
                    if (icon != null)
                    {
                        picAppIcon.Image = icon;
                        picAppIcon.Visible = true;
                    }
                    else
                    {
                        picAppIcon.Visible = false;
                    }
                }
                else
                {
                    currentSelectedPid = 0;
                    lblProcessTitle.Text = name;
                    lblProcessPid.Text = "Process not running";
                    lblProcessTitle.Visible = true;
                    lblProcessPid.Visible = true;
                    picAppIcon.Visible = false;
                }
            }
            catch
            {
                currentSelectedPid = 0;
                lblProcessTitle.Text = name;
                lblProcessPid.Text = "Process ID: Unknown";
                lblProcessTitle.Visible = true;
                lblProcessPid.Visible = true;
                picAppIcon.Visible = false;
            }
        }

        private Image? GetProcessIcon(Process p)
        {
            try
            {
                string path = GetProcessFullPath(p);
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    var shinfo = new SHFILEINFO();
                    IntPtr res = SHGetFileInfo(path, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), SHGFI_ICON | SHGFI_LARGEICON);
                    if (res != IntPtr.Zero && shinfo.hIcon != IntPtr.Zero)
                    {
                        using var ico = (Icon)Icon.FromHandle(shinfo.hIcon).Clone();
                        DestroyIcon(shinfo.hIcon);
                        return ScaleIconToBitmap(ico, 36, 36);
                    }
                }
            }
            catch { }

            if (p.MainWindowHandle != IntPtr.Zero)
            {
                try
                {
                    IntPtr hIcon = SendMessage(p.MainWindowHandle, WM_GETICON, ICON_BIG, IntPtr.Zero);
                    if (hIcon == IntPtr.Zero)
                        hIcon = SendMessage(p.MainWindowHandle, WM_GETICON, ICON_SMALL, IntPtr.Zero);
                    if (hIcon == IntPtr.Zero)
                        hIcon = GetClassLongPtr(p.MainWindowHandle, GCLP_HICON);

                    if (hIcon != IntPtr.Zero)
                    {
                        using var ico = (Icon)Icon.FromHandle(hIcon).Clone();
                        return ScaleIconToBitmap(ico, 36, 36);
                    }
                }
                catch { }
            }

            try
            {
                string systemPath = Path.Combine(Environment.SystemDirectory, p.ProcessName + ".exe");
                if (File.Exists(systemPath))
                {
                    var shinfo = new SHFILEINFO();
                    IntPtr res = SHGetFileInfo(systemPath, 0, ref shinfo, (uint)Marshal.SizeOf(shinfo), SHGFI_ICON | SHGFI_LARGEICON);
                    if (res != IntPtr.Zero && shinfo.hIcon != IntPtr.Zero)
                    {
                        using var ico = (Icon)Icon.FromHandle(shinfo.hIcon).Clone();
                        DestroyIcon(shinfo.hIcon);
                        return ScaleIconToBitmap(ico, 36, 36);
                    }
                }
            }
            catch { }

            return null;
        }

        private static Bitmap ScaleIconToBitmap(Icon icon, int width, int height)
        {
            var bmp = new Bitmap(width, height);
            using (var g = Graphics.FromImage(bmp))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.DrawIcon(icon, new Rectangle(0, 0, width, height));
            }
            return bmp;
        }

        private string GetProcessFullPath(Process p)
        {
            try
            {
                return p.MainModule?.FileName ?? "";
            }
            catch
            {
                IntPtr hProcess = OpenProcess(0x1000 /* PROCESS_QUERY_LIMITED_INFORMATION */, false, p.Id);
                if (hProcess != IntPtr.Zero)
                {
                    try
                    {
                        var sb = new StringBuilder(1024);
                        int size = sb.Capacity;
                        if (QueryFullProcessImageName(hProcess, 0, sb, ref size))
                        {
                            return sb.ToString();
                        }
                    }
                    finally
                    {
                        CloseHandle(hProcess);
                    }
                }
            }
            return "";
        }

        #region Native Win32 API
        private const uint SHGFI_ICON = 0x100;
        private const uint SHGFI_LARGEICON = 0x0;
        private const uint WM_GETICON = 0x7F;
        private static readonly IntPtr ICON_SMALL = new IntPtr(0);
        private static readonly IntPtr ICON_BIG = new IntPtr(1);
        private const int GCLP_HICON = -14;

        [StructLayout(LayoutKind.Sequential)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", EntryPoint = "GetClassLongPtrW")]
        private static extern IntPtr GetClassLongPtr(IntPtr hWnd, int nIndex);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool QueryFullProcessImageName(IntPtr hProcess, uint dwFlags, [Out] StringBuilder lpExeName, ref int lpdwSize);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _autoInjectTimer?.Stop();
            base.OnFormClosing(e);
        }

        private async void AutoInjectTimer_Tick(object? sender, EventArgs e)
        {
            if (_isAutoInjecting) return;
            if (!SettingsManager.Current.Options.AutoInject) return;

            string procName = txtProcess.Text.Trim();
            if (string.IsNullOrEmpty(procName)) return;

            var enabledDlls = lstDlls.Items.Cast<ListViewItem>()
                .Where(i => i.Checked && i.Tag is ModuleItem)
                .Select(i => ((ModuleItem)i.Tag!).Path)
                .ToList();

            if (enabledDlls.Count == 0) return;

            try
            {
                string simpleName = Path.GetFileNameWithoutExtension(procName);
                var runningProcs = Process.GetProcessesByName(simpleName);

                // Prune terminated PIDs from the set
                var runningPidSet = new HashSet<int>(runningProcs.Select(p => p.Id));
                _autoInjectedPids.RemoveWhere(pid => !runningPidSet.Contains(pid));

                var targetProc = runningProcs.FirstOrDefault(p => PrivilegeManager.CanQueryProcess(p.Id) && !_autoInjectedPids.Contains(p.Id));
                if (targetProc != null)
                {
                    _isAutoInjecting = true;
                    int targetPid = targetProc.Id;
                    _autoInjectedPids.Add(targetPid);

                    var result = await InjectionOrchestrator.ExecuteInjectionAsync(procName, enabledDlls, SettingsManager.Current.Options);

                    _isAutoInjecting = false;

                    if (result.Success)
                    {
                        MessageBox.Show("Injection has completed successfully!", "Extreme Injector v3", MessageBoxButtons.OK, MessageBoxIcon.Information);

                        if (SettingsManager.Current.Options.CloseOnInject)
                        {
                            Close();
                        }
                    }
                }
            }
            catch
            {
                _isAutoInjecting = false;
            }
        }
        #endregion

        private void UpdateInjectButtonState()
        {
            bool hasProcess = !string.IsNullOrWhiteSpace(txtProcess.Text);
            bool hasDll = lstDlls.Items.Cast<ListViewItem>().Any(i => i.Checked);
            btnInject.Enabled = hasProcess && hasDll;
        }

        private void ApplySettings()
        {
            SettingsManager.Load();
            var cfg = SettingsManager.Current;

            ThemeManager.UpdateColors(cfg.Options.Background1, cfg.Options.Background2, cfg.Options.TextColor);
            txtProcess.Text = cfg.ProcessName;

            lstDlls.Items.Clear();
            foreach (var mod in cfg.Modules)
            {
                AddDllToListView(mod.Path, mod.Enable, mod.Export, mod.Parameters);
            }

            currentSelectedIndex = lstDlls.Items.Count > 0 ? 0 : -1;

            UpdateProcessDetails();
            UpdateInjectButtonState();

            if (cfg.Options.AutoInject)
            {
                _autoInjectTimer.Start();
            }
            else
            {
                _autoInjectTimer.Stop();
            }

            Invalidate();
        }

        private void AddDllToListView(string fullPath, bool enabled = true, string? export = null, string? parameters = null)
        {
            if (!File.Exists(fullPath)) return;

            string normalizedPath = Path.GetFullPath(fullPath);

            // Prevent duplicate selection of already added DLLs
            foreach (ListViewItem existing in lstDlls.Items)
            {
                if (existing.Tag is ModuleItem mod && string.Equals(Path.GetFullPath(mod.Path), normalizedPath, StringComparison.OrdinalIgnoreCase))
                {
                    return; // Already added!
                }
            }

            string fileName = Path.GetFileName(fullPath);
            var modItem = new ModuleItem
            {
                Path = fullPath,
                Enable = enabled,
                Export = export,
                Parameters = parameters
            };

            var lvi = new ListViewItem(fileName)
            {
                Checked = enabled,
                Tag = modItem
            };

            lstDlls.Items.Add(lvi);

            if (currentSelectedIndex == -1 && lstDlls.Items.Count > 0)
            {
                currentSelectedIndex = 0;
            }

            UpdateInjectButtonState();
            lstDlls.Invalidate();
        }

        private void LstDlls_ItemChecked(object? sender, ItemCheckedEventArgs e)
        {
            SaveModulesList();
            UpdateInjectButtonState();
        }

        private void SaveModulesList()
        {
            SettingsManager.Current.Modules.Clear();
            foreach (ListViewItem item in lstDlls.Items)
            {
                if (item.Tag is ModuleItem mod)
                {
                    mod.Enable = item.Checked;
                    SettingsManager.Current.Modules.Add(mod);
                }
            }
            SettingsManager.Save();
        }

        private void BtnSelect_Click(object? sender, EventArgs e)
        {
            using var dlg = new ProcessSelectForm();
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                currentSelectedPid = dlg.SelectedProcessId;
                txtProcess.Text = dlg.SelectedProcessName;
                UpdateProcessDetails();
                UpdateInjectButtonState();
            }
        }

        private void PicAppIcon_Click(object? sender, EventArgs e)
        {
            string pName = txtProcess.Text.Trim();
            if (string.IsNullOrEmpty(pName)) return;

            int pid = currentSelectedPid;
            if (pid <= 0)
            {
                try
                {
                    var procs = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(pName));
                    if (procs.Length > 0)
                    {
                        pid = procs[0].Id;
                    }
                }
                catch { }
            }

            if (pid > 0)
            {
                using var dlg = new ProcessInformationForm(pid, pName, picAppIcon.Image);
                dlg.ShowDialog(this);
                UpdateProcessDetails();
            }
        }

        private void BtnAdd_Click(object? sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog
            {
                Filter = "Dynamic Link Library (*.dll)|*.dll|All Files (*.*)|*.*",
                Multiselect = true,
                Title = "Select DLLs to Inject"
            };
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                foreach (var file in ofd.FileNames)
                {
                    AddDllToListView(file, true);
                }
                SaveModulesList();
            }
        }

        private void BtnEnableDisable_Click(object? sender, EventArgs e)
        {
            if (currentSelectedIndex >= 0 && currentSelectedIndex < lstDlls.Items.Count)
            {
                var item = lstDlls.Items[currentSelectedIndex];
                item.Checked = !item.Checked;
                SaveModulesList();
                UpdateInjectButtonState();
                lstDlls.Invalidate();
            }
        }

        private void BtnRemove_Click(object? sender, EventArgs e)
        {
            if (lstDlls.Items.Count == 0) return;

            int targetIndex = currentSelectedIndex >= 0 ? currentSelectedIndex : 0;
            if (targetIndex >= 0 && targetIndex < lstDlls.Items.Count)
            {
                lstDlls.Items.RemoveAt(targetIndex);
            }

            currentSelectedIndex = lstDlls.Items.Count > 0 ? Math.Max(0, Math.Min(targetIndex, lstDlls.Items.Count - 1)) : -1;

            SaveModulesList();
            UpdateInjectButtonState();
            lstDlls.Invalidate();
        }

        private void BtnClear_Click(object? sender, EventArgs e)
        {
            lstDlls.Items.Clear();
            currentSelectedIndex = -1;
            SaveModulesList();
            UpdateInjectButtonState();
            lstDlls.Invalidate();
        }

        private void BtnAbout_Click(object? sender, EventArgs e)
        {
            using var dlg = new AboutForm();
            dlg.ShowDialog(this);
        }

        private void BtnSettings_Click(object? sender, EventArgs e)
        {
            using var dlg = new SettingsForm();
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                ApplySettings();
            }
        }

        private async void BtnInject_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtProcess.Text))
            {
                MessageBox.Show("Please enter or select a target process name.", "Extreme Injector", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var enabledDlls = lstDlls.Items.Cast<ListViewItem>()
                .Where(i => i.Checked && i.Tag is ModuleItem)
                .Select(i => ((ModuleItem)i.Tag!).Path)
                .ToList();

            if (enabledDlls.Count == 0)
            {
                MessageBox.Show("Please add and check at least one DLL to inject.", "Extreme Injector", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            btnInject.Enabled = false;

            var result = await ExtremeInjector.Core.InjectionOrchestrator.ExecuteInjectionAsync(txtProcess.Text, enabledDlls, SettingsManager.Current.Options);

            btnInject.Enabled = true;

            if (result.Success)
            {
                MessageBox.Show("Injection has completed successfully!", "Extreme Injector v3", MessageBoxButtons.OK, MessageBoxIcon.Information);

                if (SettingsManager.Current.Options.CloseOnInject)
                {
                    Close();
                }
            }
            else
            {
                MessageBox.Show($"An injection error occurred:\n\n{result.ErrorMessage.Trim()}", "Extreme Injector", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LstDlls_ColumnClick(object? sender, ColumnClickEventArgs e)
        {
            if (e.Column == 0)
            {
                isSortAscending = !isSortAscending;
                SortDllList();
                lstDlls.Invalidate();
            }
        }

        private void SortDllList()
        {
            if (lstDlls.Items.Count <= 1) return;

            string? selectedDllPath = (currentSelectedIndex >= 0 && currentSelectedIndex < lstDlls.Items.Count)
                ? (lstDlls.Items[currentSelectedIndex].Tag as ModuleItem)?.Path : null;

            var items = lstDlls.Items.Cast<ListViewItem>().ToList();
            if (isSortAscending)
            {
                items = items.OrderBy(i => i.Text, StringComparer.OrdinalIgnoreCase).ToList();
            }
            else
            {
                items = items.OrderByDescending(i => i.Text, StringComparer.OrdinalIgnoreCase).ToList();
            }

            lstDlls.BeginUpdate();
            lstDlls.Items.Clear();
            foreach (var item in items)
            {
                lstDlls.Items.Add(item);
            }

            if (selectedDllPath != null)
            {
                for (int i = 0; i < lstDlls.Items.Count; i++)
                {
                    if (lstDlls.Items[i].Tag is ModuleItem mod && string.Equals(mod.Path, selectedDllPath, StringComparison.OrdinalIgnoreCase))
                    {
                        currentSelectedIndex = i;
                        break;
                    }
                }
            }
            else
            {
                currentSelectedIndex = lstDlls.Items.Count > 0 ? 0 : -1;
            }

            lstDlls.EndUpdate();
            SaveModulesList();
        }

        private void LstDlls_DrawColumnHeader(object? sender, DrawListViewColumnHeaderEventArgs e)
        {
            if (e.Bounds.Width <= 0 || e.Bounds.Height <= 0) return;

            bool isHot = headerListener?.IsHot ?? false;
            bool isPressed = headerListener?.IsPressed ?? false;

            int leftDividerX = e.Bounds.Left + 25;
            int rightDividerX = e.Bounds.Right - 26;
            int hoverWidth = Math.Max(0, rightDividerX - leftDividerX);
            var hoverRect = new Rectangle(leftDividerX, e.Bounds.Top, hoverWidth, e.Bounds.Height);

            // 1. Draw Normal background across the entire header bar
            if (Application.RenderWithVisualStyles && VisualStyleRenderer.IsElementDefined(VisualStyleElement.Header.Item.Normal))
            {
                var normalRenderer = new VisualStyleRenderer(VisualStyleElement.Header.Item.Normal);
                normalRenderer.DrawBackground(e.Graphics, e.Bounds);

                // 2. Draw Hover / Pressed highlight ONLY on hoverRect (strictly inside left and right borders!)
                if ((isHot || isPressed) && hoverWidth > 0 && hoverRect.Height > 0)
                {
                    VisualStyleElement activeElement = isPressed ? VisualStyleElement.Header.Item.Pressed : VisualStyleElement.Header.Item.Hot;
                    if (VisualStyleRenderer.IsElementDefined(activeElement))
                    {
                        var activeRenderer = new VisualStyleRenderer(activeElement);
                        activeRenderer.DrawBackground(e.Graphics, hoverRect);
                    }
                }
            }
            else
            {
                ControlPaint.DrawButton(e.Graphics, e.Bounds, ButtonState.Normal);
                if ((isHot || isPressed) && hoverWidth > 0 && hoverRect.Height > 0)
                {
                    using var bgBrush = new SolidBrush(isPressed ? Color.FromArgb(204, 232, 255) : Color.FromArgb(232, 244, 255));
                    e.Graphics.FillRectangle(bgBrush, hoverRect);
                }
            }

            // 3. Separator lines:
            // When highlighted (hover/pressed), blend seamlessly into the highlight color
            if (!isHot && !isPressed)
            {
                using var divPen = new Pen(Color.FromArgb(215, 215, 215));
                // Left line → column border/separator
                e.Graphics.DrawLine(divPen, leftDividerX, e.Bounds.Top + 2, leftDividerX, e.Bounds.Bottom - 3);
                // Right line → column border/separator
                e.Graphics.DrawLine(divPen, rightDividerX, e.Bounds.Top + 2, rightDividerX, e.Bounds.Bottom - 3);
            }
            else
            {
                // In highlight state, blend seamlessly with the hover gradient
                using var divPen = new Pen(Color.FromArgb(210, 235, 255));
                e.Graphics.DrawLine(divPen, leftDividerX, e.Bounds.Top + 1, leftDividerX, e.Bounds.Bottom - 2);
                e.Graphics.DrawLine(divPen, rightDividerX, e.Bounds.Top + 1, rightDividerX, e.Bounds.Bottom - 2);
            }

            // 4. "DLL Name" text LEFT-ALIGNED with padding
            int textLeft = leftDividerX + 6;
            int textWidth = Math.Max(0, (rightDividerX - 16) - textLeft);
            var textRect = new Rectangle(textLeft, e.Bounds.Top, textWidth, e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, e.Header?.Text ?? "DLL Name", lstDlls.Font, textRect, Color.Black, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            // 5. Sort Arrow INSIDE the right boundary (immediately to the left of rightDividerX)
            int arrowX = rightDividerX - 13;
            int arrowY = e.Bounds.Top + (e.Bounds.Height - 6) / 2;

            Point[] triangle;
            if (isSortAscending)
            {
                // Up arrow ▲ (Apex pointing UP)
                triangle = new Point[]
                {
                    new Point(arrowX + 4, arrowY),
                    new Point(arrowX + 8, arrowY + 5),
                    new Point(arrowX, arrowY + 5)
                };
            }
            else
            {
                // Down arrow ▼ (Apex pointing DOWN)
                triangle = new Point[]
                {
                    new Point(arrowX, arrowY + 1),
                    new Point(arrowX + 8, arrowY + 1),
                    new Point(arrowX + 4, arrowY + 6)
                };
            }

            Color arrowColor = isHot ? Color.FromArgb(40, 40, 40) : Color.FromArgb(130, 130, 130);
            using var arrowBrush = new SolidBrush(arrowColor);
            e.Graphics.FillPolygon(arrowBrush, triangle);
        }

        private void LstDlls_DrawItem(object? sender, DrawListViewItemEventArgs e)
        {
            // Subitems handle drawing
        }

        private void LstDlls_DrawSubItem(object? sender, DrawListViewSubItemEventArgs e)
        {
            if (e.Item == null || e.Bounds.Width <= 0 || e.Bounds.Height <= 0) return;
            bool isSelected = (e.ItemIndex == currentSelectedIndex);
            var rect = e.Bounds;

            // Full-row solid blue selection bar (#0078D7) or white background
            using (var bgBrush = new SolidBrush(isSelected ? Color.FromArgb(0, 120, 215) : Color.White))
            {
                e.Graphics.FillRectangle(bgBrush, rect);
            }

            // 1. Draw native Windows OS Checkbox centered vertically in the 22px row
            var checkState = e.Item.Checked ? CheckBoxState.CheckedNormal : CheckBoxState.UncheckedNormal;
            var glyphSize = CheckBoxRenderer.GetGlyphSize(e.Graphics, checkState);
            var checkPoint = new Point(rect.Left + 4, rect.Top + (rect.Height - glyphSize.Height) / 2);
            CheckBoxRenderer.DrawCheckBox(e.Graphics, checkPoint, checkState);

            // 2. Draw native OS themed "..." button on the right edge of the row
            var btnRect = new Rectangle(rect.Right - 25, rect.Top + 1, 24, rect.Height - 2);
            if (Application.RenderWithVisualStyles)
            {
                ButtonRenderer.DrawButton(e.Graphics, btnRect, "...", lstDlls.Font, false, PushButtonState.Normal);
            }
            else
            {
                ControlPaint.DrawButton(e.Graphics, btnRect, ButtonState.Normal);
                TextRenderer.DrawText(e.Graphics, "...", lstDlls.Font, btnRect, Color.Black, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }

            // 3. Draw Text with proper padding between checkbox and the "..." button
            int textLeft = rect.Left + glyphSize.Width + 8;
            int textWidth = Math.Max(0, btnRect.Left - textLeft - 4);
            var textRect = new Rectangle(textLeft, rect.Top, textWidth, rect.Height);
            Color textColor = isSelected ? Color.White : Color.Black;
            TextRenderer.DrawText(e.Graphics, e.Item.Text, lstDlls.Font, textRect, textColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        private void LstDlls_MouseDown(object? sender, MouseEventArgs e)
        {
            var hit = lstDlls.HitTest(e.Location);
            if (hit.Item != null)
            {
                // 1. If clicking the "..." button on the right edge (last 26px of the row)
                if (e.X >= hit.Item.Bounds.Right - 26)
                {
                    currentSelectedIndex = hit.Item.Index;
                    lstDlls.Invalidate();
                    OpenDllConfig(hit.Item);
                    return;
                }

                // 2. If clicking the Checkbox on the left edge (first 22px)
                if (e.X >= hit.Item.Bounds.Left && e.X <= hit.Item.Bounds.Left + 22)
                {
                    hit.Item.Checked = !hit.Item.Checked;
                    currentSelectedIndex = hit.Item.Index;
                    SaveModulesList();
                    UpdateInjectButtonState();
                    lstDlls.Invalidate();
                    return;
                }

                // 3. Normal row click -> exactly one item highlighted
                currentSelectedIndex = hit.Item.Index;
                lstDlls.Invalidate();
            }
        }

        private void CtxConfigDll_Click(object? sender, EventArgs e)
        {
            if (currentSelectedIndex >= 0 && currentSelectedIndex < lstDlls.Items.Count)
            {
                OpenDllConfig(lstDlls.Items[currentSelectedIndex]);
            }
        }

        private void OpenDllConfig(ListViewItem item)
        {
            var mod = item.Tag as ModuleItem ?? new ModuleItem { Path = item.Text };

            using var dlg = new DllItemConfigForm(mod.Path, mod.Export, mod.Parameters);
            dlg.ShowDialog(this);
            mod.Export = string.IsNullOrEmpty(dlg.ExportName) ? null : dlg.ExportName;
            mod.Parameters = string.IsNullOrEmpty(dlg.Parameters) ? null : dlg.Parameters;
            item.Tag = mod;
            SaveModulesList();
        }

        private void CtxMoveUp_Click(object? sender, EventArgs e)
        {
            if (lstDlls.SelectedItems.Count == 1 && lstDlls.SelectedIndices[0] > 0)
            {
                int idx = lstDlls.SelectedIndices[0];
                var item = lstDlls.SelectedItems[0];
                lstDlls.Items.RemoveAt(idx);
                lstDlls.Items.Insert(idx - 1, item);
                item.Selected = true;
                SaveModulesList();
            }
        }

        private void CtxMoveDown_Click(object? sender, EventArgs e)
        {
            if (lstDlls.SelectedItems.Count == 1 && lstDlls.SelectedIndices[0] < lstDlls.Items.Count - 1)
            {
                int idx = lstDlls.SelectedIndices[0];
                var item = lstDlls.SelectedItems[0];
                lstDlls.Items.RemoveAt(idx);
                lstDlls.Items.Insert(idx + 1, item);
                item.Selected = true;
                SaveModulesList();
            }
        }

        private void CtxOpenFolder_Click(object? sender, EventArgs e)
        {
            if (lstDlls.SelectedItems.Count > 0 && lstDlls.SelectedItems[0].Tag is ModuleItem mod)
            {
                if (File.Exists(mod.Path))
                {
                    Process.Start("explorer.exe", $"/select,\"{mod.Path}\"");
                }
            }
        }

        private void LstDlls_DragEnter(object? sender, DragEventArgs e)
        {
            if (e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
        }

        private void LstDlls_DragDrop(object? sender, DragEventArgs e)
        {
            if (e.Data != null && e.Data.GetData(DataFormats.FileDrop) is string[] files)
            {
                foreach (var file in files)
                {
                    if (file.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    {
                        AddDllToListView(file, true);
                    }
                }
                SaveModulesList();
            }
        }
    }

    public class ListViewHeaderListener : NativeWindow
    {
        private readonly ListView _listView;
        public bool IsHot { get; private set; }
        public bool IsPressed { get; private set; }

        public ListViewHeaderListener(ListView listView)
        {
            _listView = listView;
            if (listView.IsHandleCreated)
            {
                AttachHeader();
            }
            listView.HandleCreated += (s, e) => AttachHeader();
        }

        private void AttachHeader()
        {
            IntPtr headerHandle = SendMessage(_listView.Handle, 0x101F /* LVM_GETHEADER */, IntPtr.Zero, IntPtr.Zero);
            if (headerHandle != IntPtr.Zero && Handle == IntPtr.Zero)
            {
                AssignHandle(headerHandle);
            }
        }

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct HDLAYOUT
        {
            public IntPtr prc;
            public IntPtr pwpos;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WINDOWPOS
        {
            public IntPtr hwnd;
            public IntPtr hwndInsertAfter;
            public int x;
            public int y;
            public int cx;
            public int cy;
            public uint flags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        protected override void WndProc(ref Message m)
        {
            const int HDM_LAYOUT = 0x1205;
            const int WM_MOUSEMOVE = 0x0200;
            const int WM_MOUSELEAVE = 0x02A3;
            const int WM_LBUTTONDOWN = 0x0201;
            const int WM_LBUTTONUP = 0x0202;

            if (m.Msg == HDM_LAYOUT)
            {
                base.WndProc(ref m);
                if (m.LParam != IntPtr.Zero)
                {
                    try
                    {
                        var layout = Marshal.PtrToStructure<HDLAYOUT>(m.LParam);
                        if (layout.pwpos != IntPtr.Zero && layout.prc != IntPtr.Zero)
                        {
                            var wpos = Marshal.PtrToStructure<WINDOWPOS>(layout.pwpos);
                            var rc = Marshal.PtrToStructure<RECT>(layout.prc);

                            int headerHeight = 20;
                            wpos.cy = headerHeight;
                            rc.Top = headerHeight;

                            Marshal.StructureToPtr(wpos, layout.pwpos, false);
                            Marshal.StructureToPtr(rc, layout.prc, false);
                        }
                    }
                    catch { }
                }
                return;
            }

            if (m.Msg == WM_MOUSEMOVE)
            {
                int x = unchecked((short)(long)m.LParam);
                int leftDividerX = 25;
                int rightDividerX = _listView.Columns.Count > 0 ? _listView.Columns[0].Width - 26 : 202;
                bool newHot = (x >= leftDividerX && x <= rightDividerX);
                if (newHot != IsHot)
                {
                    IsHot = newHot;
                    _listView.Invalidate();
                }
            }
            else if (m.Msg == WM_MOUSELEAVE)
            {
                if (IsHot || IsPressed)
                {
                    IsHot = false;
                    IsPressed = false;
                    _listView.Invalidate();
                }
            }
            else if (m.Msg == WM_LBUTTONDOWN)
            {
                IsPressed = true;
                _listView.Invalidate();
            }
            else if (m.Msg == WM_LBUTTONUP)
            {
                IsPressed = false;
                _listView.Invalidate();
            }

            base.WndProc(ref m);
        }
    }
}
