using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace ExtremeInjector.UI
{
    public class ProcessSelectForm : Form
    {
        private ListView lstProcesses = null!;
        private Button btnProcessList = null!;
        private Button btnWindowList = null!;
        private Button btnSelect = null!;
        private Button btnClose = null!;
        private ImageList imgList = null!;
        private Icon defaultExeIcon = null!;

        public string SelectedProcessName { get; private set; } = "";
        public int SelectedProcessId { get; private set; } = 0;
        private bool isWindowMode = false;

        public ProcessSelectForm()
        {
            InitializeComponent();
            LoadDefaultIcon();
            RefreshList(false);
        }

        private void LoadDefaultIcon()
        {
            try
            {
                IntPtr hIcon = ExtractIcon(Process.GetCurrentProcess().Handle, "shell32.dll", 2);
                if (hIcon != IntPtr.Zero)
                {
                    defaultExeIcon = (Icon)Icon.FromHandle(hIcon).Clone();
                    DestroyIcon(hIcon);
                }
                else
                {
                    defaultExeIcon = SystemIcons.Application;
                }
            }
            catch
            {
                defaultExeIcon = SystemIcons.Application;
            }
        }

        private void InitializeComponent()
        {
            Text = "Process List";
            ClientSize = new Size(270, 278);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 9f);
            BackColor = Color.FromArgb(240, 240, 240);
            Icon = ThemeManager.AppIcon;

            imgList = new ImageList
            {
                ImageSize = new Size(24, 24),
                ColorDepth = ColorDepth.Depth32Bit
            };

            // Process List View (258 width x 204 height)
            lstProcesses = new ListView
            {
                Location = new Point(6, 6),
                Size = new Size(258, 204),
                View = View.Details,
                FullRowSelect = true,
                HeaderStyle = ColumnHeaderStyle.None,
                SmallImageList = imgList,
                BackColor = Color.White,
                Font = new Font("Segoe UI", 9f),
                MultiSelect = false,
                HideSelection = false
            };
            lstProcesses.Columns.Add("", 234);
            lstProcesses.DoubleClick += (s, e) => SelectAndClose();

            // Bottom 2x2 Button Grid (Width = 125px each, Height = 24px each)
            btnProcessList = new Button
            {
                Text = "Process List",
                Location = new Point(6, 218),
                Size = new Size(125, 24),
                FlatStyle = FlatStyle.System,
                UseVisualStyleBackColor = true
            };
            btnProcessList.Click += (s, e) => RefreshList(false);

            btnWindowList = new Button
            {
                Text = "Window List",
                Location = new Point(139, 218),
                Size = new Size(125, 24),
                FlatStyle = FlatStyle.System,
                UseVisualStyleBackColor = true
            };
            btnWindowList.Click += (s, e) => RefreshList(true);

            btnSelect = new Button
            {
                Text = "Select",
                Location = new Point(6, 246),
                Size = new Size(125, 24),
                FlatStyle = FlatStyle.System,
                UseVisualStyleBackColor = true
            };
            btnSelect.Click += (s, e) => SelectAndClose();

            btnClose = new Button
            {
                Text = "Close",
                Location = new Point(139, 246),
                Size = new Size(125, 24),
                FlatStyle = FlatStyle.System,
                UseVisualStyleBackColor = true
            };
            btnClose.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            Controls.AddRange(new Control[] {
                lstProcesses,
                btnProcessList,
                btnWindowList,
                btnSelect,
                btnClose
            });
        }

        private void RefreshList(bool windowMode)
        {
            isWindowMode = windowMode;
            Text = "Process List";

            lstProcesses.BeginUpdate();
            lstProcesses.Items.Clear();
            imgList.Images.Clear();

            int iconIdx = 0;

            if (windowMode)
            {
                // Enumerate ALL top-level visible windows via Win32 EnumWindows
                var windows = new List<WindowEntry>();
                EnumWindows((hWnd, lParam) =>
                {
                    if (IsWindowVisible(hWnd))
                    {
                        int length = GetWindowTextLength(hWnd);
                        if (length > 0)
                        {
                            var sb = new StringBuilder(length + 1);
                            GetWindowText(hWnd, sb, length + 1);
                            string title = sb.ToString();

                            // Filter out blank or invisible helper windows
                            if (!string.IsNullOrWhiteSpace(title) && title != "Program Manager")
                            {
                                GetWindowThreadProcessId(hWnd, out uint pid);
                                if (pid != 0 && pid != 4)
                                {
                                    windows.Add(new WindowEntry
                                    {
                                        HWnd = hWnd,
                                        Pid = (int)pid,
                                        Title = title
                                    });
                                }
                            }
                        }
                    }
                    return true;
                }, IntPtr.Zero);

                foreach (var w in windows)
                {
                    try
                    {
                        Image? icon = GetWindowIcon(w.HWnd, w.Pid);
                        if (icon == null)
                        {
                            icon = defaultExeIcon.ToBitmap();
                        }

                        imgList.Images.Add(icon);

                        string displayName = $"{w.Pid:X8}-{w.Title}";

                        string procName = "";
                        try
                        {
                            var proc = Process.GetProcessById(w.Pid);
                            procName = proc.ProcessName + ".exe";
                        }
                        catch { }

                        var lvi = new ListViewItem(displayName, iconIdx++)
                        {
                            Tag = new ProcessInfo { Name = procName, Pid = w.Pid, Title = w.Title }
                        };

                        lstProcesses.Items.Add(lvi);
                    }
                    catch { }
                }
            }
            else
            {
                // Enumerate Processes
                var processes = Process.GetProcesses();
                foreach (var p in processes)
                {
                    try
                    {
                        if (p.Id == 0 || p.Id == 4) continue;

                        Image? icon = GetProcessIcon(p);
                        if (icon == null)
                        {
                            icon = defaultExeIcon.ToBitmap();
                        }

                        imgList.Images.Add(icon);

                        string displayName = $"{p.Id:X8}-{p.ProcessName}.exe";

                        var lvi = new ListViewItem(displayName, iconIdx++)
                        {
                            Tag = new ProcessInfo { Name = p.ProcessName + ".exe", Pid = p.Id, Title = p.MainWindowTitle }
                        };

                        lstProcesses.Items.Add(lvi);
                    }
                    catch { }
                }
            }

            lstProcesses.EndUpdate();

            if (lstProcesses.Items.Count > 0)
            {
                lstProcesses.Items[0].Selected = true;
            }
        }

        private Image? GetWindowIcon(IntPtr hWnd, int pid)
        {
            // 1. Try WM_GETICON directly on window handle (try BIG first for maximum sharpness)
            try
            {
                IntPtr hIcon = SendMessage(hWnd, WM_GETICON, ICON_BIG, IntPtr.Zero);
                if (hIcon == IntPtr.Zero)
                    hIcon = SendMessage(hWnd, WM_GETICON, ICON_SMALL2, IntPtr.Zero);
                if (hIcon == IntPtr.Zero)
                    hIcon = SendMessage(hWnd, WM_GETICON, ICON_SMALL, IntPtr.Zero);
                if (hIcon == IntPtr.Zero)
                    hIcon = GetClassLongPtr(hWnd, GCLP_HICON);
                if (hIcon == IntPtr.Zero)
                    hIcon = GetClassLongPtr(hWnd, GCLP_HICONSM);

                if (hIcon != IntPtr.Zero)
                {
                    using var ico = (Icon)Icon.FromHandle(hIcon).Clone();
                    return ScaleIconToBitmap(ico, 24, 24);
                }
            }
            catch { }

            // 2. Fallback to process executable path
            try
            {
                var proc = Process.GetProcessById(pid);
                return GetProcessIcon(proc);
            }
            catch { }

            return null;
        }

        private Image? GetProcessIcon(Process p)
        {
            // 1. Try SHGetFileInfo on main module path with large icon
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
                        return ScaleIconToBitmap(ico, 24, 24);
                    }
                }
            }
            catch { }

            // 2. Try window message WM_GETICON / GetClassLongPtr
            if (p.MainWindowHandle != IntPtr.Zero)
            {
                try
                {
                    IntPtr hIcon = SendMessage(p.MainWindowHandle, WM_GETICON, ICON_BIG, IntPtr.Zero);
                    if (hIcon == IntPtr.Zero)
                        hIcon = SendMessage(p.MainWindowHandle, WM_GETICON, ICON_SMALL, IntPtr.Zero);
                    if (hIcon == IntPtr.Zero)
                        hIcon = GetClassLongPtr(p.MainWindowHandle, GCLP_HICON);
                    if (hIcon == IntPtr.Zero)
                        hIcon = GetClassLongPtr(p.MainWindowHandle, GCLP_HICONSM);

                    if (hIcon != IntPtr.Zero)
                    {
                        using var ico = (Icon)Icon.FromHandle(hIcon).Clone();
                        return ScaleIconToBitmap(ico, 24, 24);
                    }
                }
                catch { }
            }

            // 3. Fallback to system32 known binaries
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
                        return ScaleIconToBitmap(ico, 24, 24);
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
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
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

        private void SelectAndClose()
        {
            if (lstProcesses.SelectedItems.Count > 0)
            {
                var info = (ProcessInfo)lstProcesses.SelectedItems[0].Tag!;
                SelectedProcessName = info.Name;
                SelectedProcessId = info.Pid;
                DialogResult = DialogResult.OK;
                Close();
            }
        }

        #region Native Win32 API
        private const uint SHGFI_ICON = 0x100;
        private const uint SHGFI_LARGEICON = 0x0;
        private const uint SHGFI_SMALLICON = 0x1;
        private const uint WM_GETICON = 0x7F;
        private static readonly IntPtr ICON_SMALL = new IntPtr(0);
        private static readonly IntPtr ICON_BIG = new IntPtr(1);
        private static readonly IntPtr ICON_SMALL2 = new IntPtr(2);
        private const int GCLP_HICON = -14;
        private const int GCLP_HICONSM = -34;

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, [Out] StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

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

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr ExtractIcon(IntPtr hInst, string lpszExeFileName, int nIconIndex);

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
        #endregion

        private class WindowEntry
        {
            public IntPtr HWnd { get; set; }
            public int Pid { get; set; }
            public string Title { get; set; } = "";
        }

        private class ProcessInfo
        {
            public string Name { get; set; } = "";
            public int Pid { get; set; }
            public string Title { get; set; } = "";
        }
    }
}
