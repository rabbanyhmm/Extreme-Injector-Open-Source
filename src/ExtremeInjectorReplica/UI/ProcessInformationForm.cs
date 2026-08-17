using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using ExtremeInjector.Core;

namespace ExtremeInjector.UI
{
    public class ProcessInformationForm : Form
    {
        private readonly int _processId;
        private readonly string _processName;

        private GroupBox grpProcess = null!;
        private PictureBox picProcessIcon = null!;
        private Label lblProcessName = null!;
        private Label lblProcessPath = null!;
        private Label lblProcessId = null!;
        private Label lblModulesThreads = null!;

        private TabControl tabControl = null!;
        private TabPage tabModules = null!;
        private TabPage tabThreads = null!;

        private ListView lstModules = null!;
        private Button btnUnloadModule = null!;

        private ListView lstThreads = null!;
        private Button btnKillThread = null!;
        private Button btnSuspendThread = null!;

        private Panel pnlBottomButtons = null!;
        private Button btnKillProcess = null!;
        private Button btnClose = null!;

        private readonly Dictionary<int, bool> _suspendedThreads = new();
        private List<ModuleInfo> _cachedModules = new();
        private List<ThreadInfo> _cachedThreads = new();

        // Sort tracking
        private int _modSortCol = 0;
        private bool _modSortAsc = true;
        private int _thSortCol = 0;
        private bool _thSortAsc = true;

        private static readonly string[] ModHeaders = { "Module Name", "Module Base", "Module Size" };
        private static readonly string[] ThHeaders = { "Thread ID", "Start Address", "Priority" };

        public ProcessInformationForm(int processId, string processName, Image? processIcon = null)
        {
            _processId = processId;
            _processName = processName;

            InitializeComponent();

            if (processIcon != null)
            {
                picProcessIcon.Image = (Image)processIcon.Clone();
            }
            else
            {
                LoadProcessIcon();
            }

            LoadProcessInformation();
        }

        private void InitializeComponent()
        {
            Text = "Process Information";
            Size = new Size(425, 508);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 9f);
            BackColor = Color.FromArgb(240, 240, 240);

            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ExtremeInjector.ico");
            if (File.Exists(iconPath))
            {
                try { Icon = new Icon(iconPath); } catch { }
            }

            // 1. Process Group Box
            grpProcess = new GroupBox
            {
                Text = "Process",
                Location = new Point(12, 10),
                Size = new Size(385, 134),
                Font = new Font("Segoe UI", 9f, FontStyle.Regular)
            };

            picProcessIcon = new PictureBox
            {
                Location = new Point(12, 22),
                Size = new Size(32, 32),
                SizeMode = PictureBoxSizeMode.StretchImage,
                BackColor = Color.Transparent
            };

            lblProcessName = new Label
            {
                Text = _processName,
                Location = new Point(52, 20),
                Size = new Size(323, 16),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                AutoEllipsis = true
            };

            lblProcessPath = new Label
            {
                Text = "Path: Loading...",
                Location = new Point(52, 38),
                Size = new Size(323, 32),
                Font = new Font("Segoe UI", 8.25f),
                ForeColor = Color.FromArgb(40, 40, 40),
                AutoEllipsis = true
            };

            lblProcessId = new Label
            {
                Text = $"Process ID: 0x{_processId:X} ({_processId})",
                Location = new Point(52, 74),
                Size = new Size(323, 16),
                Font = new Font("Segoe UI", 9f)
            };

            lblModulesThreads = new Label
            {
                Text = "Modules: 0  Threads: 0",
                Location = new Point(52, 96),
                Size = new Size(323, 16),
                Font = new Font("Segoe UI", 9f)
            };

            grpProcess.Controls.AddRange(new Control[] {
                picProcessIcon,
                lblProcessName,
                lblProcessPath,
                lblProcessId,
                lblModulesThreads
            });

            // 2. Tab Control
            tabControl = new TabControl
            {
                Location = new Point(12, 152),
                Size = new Size(385, 280),
                Font = new Font("Segoe UI", 9f)
            };

            // =========================================================================
            // SHARED TAB CONTROLS (TABLE SIZE & BUTTON ROW POSITION)
            // =========================================================================
            int tabTableX = 6;
            int tabTableY = 6;
            int tabTableWidth = 365;
            int tabTableHeight = 200; // ← Height of tables in BOTH tabs

            int tabBtnY = 212;        // ← SHARED Y POSITION: Moves Unload Module, Kill, AND Suspend down TOGETHER!
            int tabBtnHeight = 24;    // ← SHARED Button Height

            // -------------------------------------------------------------------------
            // TAB 1: MODULES
            // -------------------------------------------------------------------------
            int unloadBtnWidth = 120;
            int unloadBtnX = tabTableX + tabTableWidth - unloadBtnWidth;

            tabModules = new TabPage { Text = "Modules", BackColor = Color.White };
            lstModules = new ListView
            {
                Location = new Point(tabTableX, tabTableY),
                Size = new Size(tabTableWidth, tabTableHeight),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                MultiSelect = false,
                Font = new Font("Segoe UI", 8.5f),
                HideSelection = false
            };
            lstModules.Columns.Add("Module Name", 145);
            lstModules.Columns.Add("Module Base", 125);
            lstModules.Columns.Add("Module Size", 80);
            lstModules.SelectedIndexChanged += LstModules_SelectedIndexChanged;
            lstModules.ColumnClick += LstModules_ColumnClick;

            btnUnloadModule = new Button
            {
                Text = "Unload Module",
                Location = new Point(unloadBtnX, tabBtnY),
                Size = new Size(unloadBtnWidth, tabBtnHeight),
                FlatStyle = FlatStyle.System,
                UseVisualStyleBackColor = true,
                Enabled = false
            };
            btnUnloadModule.Click += BtnUnloadModule_Click;

            tabModules.Controls.AddRange(new Control[] { lstModules, btnUnloadModule });

            // -------------------------------------------------------------------------
            // TAB 2: THREADS
            // -------------------------------------------------------------------------
            int thBtnWidth = 90;
            int thBtnSpacing = 10;
            int thGroupWidth = (thBtnWidth * 2) + thBtnSpacing;
            int thBtnGroupX = tabTableX + tabTableWidth - thGroupWidth;

            tabThreads = new TabPage { Text = "Threads", BackColor = Color.White };
            lstThreads = new ListView
            {
                Location = new Point(tabTableX, tabTableY),
                Size = new Size(tabTableWidth, tabTableHeight),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                MultiSelect = false,
                Font = new Font("Segoe UI", 8.5f),
                HideSelection = false
            };
            lstThreads.Columns.Add("Thread ID", 85);
            lstThreads.Columns.Add("Start Address", 175);
            lstThreads.Columns.Add("Priority", 90);
            lstThreads.SelectedIndexChanged += LstThreads_SelectedIndexChanged;
            lstThreads.ColumnClick += LstThreads_ColumnClick;

            var pnlThreadButtons = new Panel
            {
                Location = new Point(thBtnGroupX, tabBtnY),
                Size = new Size(thGroupWidth, tabBtnHeight + 2),
                BackColor = Color.Transparent
            };

            btnKillThread = new Button
            {
                Text = "Kill",
                Location = new Point(0, 0),
                Size = new Size(thBtnWidth, tabBtnHeight),
                FlatStyle = FlatStyle.System,
                UseVisualStyleBackColor = true,
                Enabled = false
            };
            btnKillThread.Click += BtnKillThread_Click;

            btnSuspendThread = new Button
            {
                Text = "Suspend",
                Location = new Point(thBtnWidth + thBtnSpacing, 0),
                Size = new Size(thBtnWidth, tabBtnHeight),
                FlatStyle = FlatStyle.System,
                UseVisualStyleBackColor = true,
                Enabled = false
            };
            btnSuspendThread.Click += BtnSuspendThread_Click;

            pnlThreadButtons.Controls.AddRange(new Control[] { btnKillThread, btnSuspendThread });
            tabThreads.Controls.AddRange(new Control[] { lstThreads, pnlThreadButtons });

            tabControl.TabPages.Add(tabModules);
            tabControl.TabPages.Add(tabThreads);

            // =========================================================================
            // 3. BOTTOM ACTION BUTTONS (KILL PROCESS / CLOSE)
            // =========================================================================
            int bottomBtnWidth = 105;   // ← Width of both buttons
            int bottomBtnHeight = 24;   // ← Height of both buttons
            int bottomBtnSpacing = 8;   // ← Spacing between Kill Process and Close

            pnlBottomButtons = new Panel
            {
                Location = new Point(177, 438), // ← Position of bottom action buttons
                Size = new Size((bottomBtnWidth * 2) + bottomBtnSpacing, bottomBtnHeight + 2),
                BackColor = Color.Transparent
            };

            btnKillProcess = new Button
            {
                Text = "Kill Process",
                Location = new Point(0, 0),
                Size = new Size(bottomBtnWidth, bottomBtnHeight),
                FlatStyle = FlatStyle.System,
                UseVisualStyleBackColor = true
            };
            btnKillProcess.Click += BtnKillProcess_Click;

            btnClose = new Button
            {
                Text = "Close",
                Location = new Point(bottomBtnWidth + bottomBtnSpacing, 0),
                Size = new Size(bottomBtnWidth, bottomBtnHeight),
                FlatStyle = FlatStyle.System,
                UseVisualStyleBackColor = true
            };
            btnClose.Click += (s, e) => Close();

            pnlBottomButtons.Controls.AddRange(new Control[] {
                btnKillProcess,
                btnClose
            });

            Controls.AddRange(new Control[] {
                grpProcess,
                tabControl,
                pnlBottomButtons
            });
        }

        private void LoadProcessIcon()
        {
            try
            {
                string path = GetProcessFullPath(_processId);
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    using var ico = Icon.ExtractAssociatedIcon(path);
                    if (ico != null) picProcessIcon.Image = ico.ToBitmap();
                }
                else
                {
                    picProcessIcon.Image = SystemIcons.Application.ToBitmap();
                }
            }
            catch
            {
                picProcessIcon.Image = SystemIcons.Application.ToBitmap();
            }
        }

        private void LoadProcessInformation()
        {
            try
            {
                string fullPath = GetProcessFullPath(_processId);
                lblProcessPath.Text = string.IsNullOrEmpty(fullPath) ? _processName : fullPath;

                // 1. Enumerate Modules using rock-solid Win32 PsApi & Toolhelp fallback
                _cachedModules = EnumerateModulesNative(_processId);

                // 2. Enumerate Threads with Export Symbol Resolution
                _cachedThreads = EnumerateThreadsNative(_processId, _cachedModules);

                lblModulesThreads.Text = $"Modules: {_cachedModules.Count}  Threads: {_cachedThreads.Count}";

                RenderModulesList();
                RenderThreadsList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to inspect process: {ex.Message}", "Process Information", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void RenderModulesList()
        {
            var list = new List<ModuleInfo>(_cachedModules);

            if (_modSortCol == 0) // Module Name
            {
                list = _modSortAsc
                    ? list.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList()
                    : list.OrderByDescending(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();
            }
            else if (_modSortCol == 1) // Module Base
            {
                list = _modSortAsc
                    ? list.OrderBy(m => m.BaseAddress.ToInt64()).ToList()
                    : list.OrderByDescending(m => m.BaseAddress.ToInt64()).ToList();
            }
            else if (_modSortCol == 2) // Module Size
            {
                list = _modSortAsc
                    ? list.OrderBy(m => m.Size).ToList()
                    : list.OrderByDescending(m => m.Size).ToList();
            }

            // Update column header text with right-side arrow indicator
            UpdateRightSideSortHeader(lstModules, _modSortCol, _modSortAsc, ModHeaders);

            lstModules.BeginUpdate();
            lstModules.Items.Clear();
            foreach (var mod in list)
            {
                var item = new ListViewItem(mod.Name);
                string baseAddrStr = $"0x{mod.BaseAddress.ToInt64():X}";
                item.SubItems.Add(baseAddrStr);
                item.SubItems.Add(FormatSize(mod.Size));
                item.Tag = mod;
                lstModules.Items.Add(item);
            }
            lstModules.EndUpdate();
        }

        private void RenderThreadsList()
        {
            var list = new List<ThreadInfo>(_cachedThreads);

            if (_thSortCol == 0) // Thread ID
            {
                list = _thSortAsc ? list.OrderBy(t => t.Id).ToList() : list.OrderByDescending(t => t.Id).ToList();
            }
            else if (_thSortCol == 1) // Start Address
            {
                list = _thSortAsc
                    ? list.OrderBy(t => t.StartAddressString, StringComparer.OrdinalIgnoreCase).ToList()
                    : list.OrderByDescending(t => t.StartAddressString, StringComparer.OrdinalIgnoreCase).ToList();
            }
            else if (_thSortCol == 2) // Priority
            {
                list = _thSortAsc
                    ? list.OrderBy(t => t.Priority, StringComparer.OrdinalIgnoreCase).ToList()
                    : list.OrderByDescending(t => t.Priority, StringComparer.OrdinalIgnoreCase).ToList();
            }

            // Update column header text with right-side arrow indicator
            UpdateRightSideSortHeader(lstThreads, _thSortCol, _thSortAsc, ThHeaders);

            lstThreads.BeginUpdate();
            lstThreads.Items.Clear();
            foreach (var th in list)
            {
                var item = new ListViewItem(th.Id.ToString());
                item.SubItems.Add(th.StartAddressString);
                item.SubItems.Add(th.Priority);
                item.Tag = th;
                lstThreads.Items.Add(item);
            }
            lstThreads.EndUpdate();
        }

        private static void UpdateRightSideSortHeader(ListView lv, int sortCol, bool sortAsc, string[] originalHeaders)
        {
            for (int i = 0; i < lv.Columns.Count; i++)
            {
                string baseTitle = originalHeaders[i];
                if (i == sortCol)
                {
                    // Right-side clean arrow aligned next to the title
                    lv.Columns[i].Text = baseTitle + (sortAsc ? "   ▲" : "   ▼");
                }
                else
                {
                    lv.Columns[i].Text = baseTitle;
                }
            }
        }

        private void LstModules_ColumnClick(object? sender, ColumnClickEventArgs e)
        {
            if (_modSortCol == e.Column)
            {
                _modSortAsc = !_modSortAsc;
            }
            else
            {
                _modSortCol = e.Column;
                _modSortAsc = true;
            }
            RenderModulesList();
        }

        private void LstThreads_ColumnClick(object? sender, ColumnClickEventArgs e)
        {
            if (_thSortCol == e.Column)
            {
                _thSortAsc = !_thSortAsc;
            }
            else
            {
                _thSortCol = e.Column;
                _thSortAsc = true;
            }
            RenderThreadsList();
        }

        private static string FormatSize(int bytes)
        {
            if (bytes < 1024 * 1024)
            {
                return $"{bytes / 1024.0:F0} KB";
            }
            return $"{bytes / (1024.0 * 1024.0):F2} MB";
        }

        private static string FormatPriority(ThreadPriorityLevel priority)
        {
            return priority switch
            {
                ThreadPriorityLevel.Idle => "Idle",
                ThreadPriorityLevel.Lowest => "Lowest",
                ThreadPriorityLevel.BelowNormal => "Below Normal",
                ThreadPriorityLevel.Normal => "Normal",
                ThreadPriorityLevel.AboveNormal => "Above Normal",
                ThreadPriorityLevel.Highest => "Highest",
                ThreadPriorityLevel.TimeCritical => "Time Critical",
                _ => "Normal"
            };
        }

        #region PE Export Parser for Start Address Symbol Resolution

        private static readonly Dictionary<string, List<(string Name, uint RVA)>> _moduleExportsCache = new(StringComparer.OrdinalIgnoreCase);

        private static List<(string Name, uint RVA)> GetModuleExports(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return new();
            if (_moduleExportsCache.TryGetValue(filePath, out var cached)) return cached;

            var exports = new List<(string Name, uint RVA)>();
            try
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new BinaryReader(fs);

                if (reader.ReadUInt16() != 0x5A4D) return exports; // 'MZ'
                fs.Seek(0x3C, SeekOrigin.Begin);
                uint e_lfanew = reader.ReadUInt32();

                fs.Seek(e_lfanew, SeekOrigin.Begin);
                if (reader.ReadUInt32() != 0x00004550) return exports; // 'PE\0\0'

                ushort machine = reader.ReadUInt16();
                ushort numSections = reader.ReadUInt16();
                fs.Seek(12, SeekOrigin.Current);
                ushort sizeOfOptHeader = reader.ReadUInt16();
                ushort characteristics = reader.ReadUInt16();

                long optHeaderStart = fs.Position;
                ushort magic = reader.ReadUInt16();
                bool is64 = (magic == 0x020B);

                uint exportRva = 0;
                if (is64)
                {
                    fs.Seek(optHeaderStart + 112, SeekOrigin.Begin);
                    exportRva = reader.ReadUInt32();
                }
                else
                {
                    fs.Seek(optHeaderStart + 96, SeekOrigin.Begin);
                    exportRva = reader.ReadUInt32();
                }

                if (exportRva == 0) return exports;

                long sectionHeaderStart = optHeaderStart + sizeOfOptHeader;
                var sections = new List<(uint VirtAddr, uint VirtSize, uint RawOffset)>();
                for (int i = 0; i < numSections; i++)
                {
                    fs.Seek(sectionHeaderStart + (i * 40), SeekOrigin.Begin);
                    fs.Seek(8, SeekOrigin.Current);
                    uint virtSize = reader.ReadUInt32();
                    uint virtAddr = reader.ReadUInt32();
                    uint rawSize = reader.ReadUInt32();
                    uint rawOffset = reader.ReadUInt32();
                    sections.Add((virtAddr, virtSize, rawOffset));
                }

                long RvaToOffset(uint rva)
                {
                    foreach (var sec in sections)
                    {
                        if (rva >= sec.VirtAddr && rva < sec.VirtAddr + sec.VirtSize)
                        {
                            return sec.RawOffset + (rva - sec.VirtAddr);
                        }
                    }
                    return -1;
                }

                long exportOffset = RvaToOffset(exportRva);
                if (exportOffset == -1) return exports;

                fs.Seek(exportOffset + 24, SeekOrigin.Begin);
                uint numFunctions = reader.ReadUInt32();
                uint numNames = reader.ReadUInt32();
                uint addrOfFunctions = reader.ReadUInt32();
                uint addrOfNames = reader.ReadUInt32();
                uint addrOfNameOrdinals = reader.ReadUInt32();

                long nameArrayOffset = RvaToOffset(addrOfNames);
                long ordinalArrayOffset = RvaToOffset(addrOfNameOrdinals);
                long funcArrayOffset = RvaToOffset(addrOfFunctions);

                if (nameArrayOffset != -1 && ordinalArrayOffset != -1 && funcArrayOffset != -1)
                {
                    for (uint i = 0; i < numNames; i++)
                    {
                        fs.Seek(nameArrayOffset + (i * 4), SeekOrigin.Begin);
                        uint nameRva = reader.ReadUInt32();
                        long nameOffset = RvaToOffset(nameRva);
                        if (nameOffset == -1) continue;

                        fs.Seek(ordinalArrayOffset + (i * 2), SeekOrigin.Begin);
                        ushort ordinal = reader.ReadUInt16();

                        fs.Seek(funcArrayOffset + (ordinal * 4), SeekOrigin.Begin);
                        uint funcRva = reader.ReadUInt32();

                        fs.Seek(nameOffset, SeekOrigin.Begin);
                        var sb = new StringBuilder();
                        byte b;
                        while ((b = reader.ReadByte()) != 0)
                        {
                            sb.Append((char)b);
                        }

                        exports.Add((sb.ToString(), funcRva));
                    }
                }

                exports.Sort((a, b) => a.RVA.CompareTo(b.RVA));
            }
            catch { }

            _moduleExportsCache[filePath] = exports;
            return exports;
        }

        private static string ResolveAddress(IntPtr address, List<ModuleInfo> modules)
        {
            long addr = address.ToInt64();
            foreach (var mod in modules)
            {
                long baseAddr = mod.BaseAddress.ToInt64();
                if (addr >= baseAddr && addr < baseAddr + mod.Size)
                {
                    uint rva = (uint)(addr - baseAddr);
                    var exports = GetModuleExports(mod.Path);

                    string? bestExport = null;
                    uint bestExportRva = 0;
                    for (int i = exports.Count - 1; i >= 0; i--)
                    {
                        if (exports[i].RVA <= rva)
                        {
                            bestExport = exports[i].Name;
                            bestExportRva = exports[i].RVA;
                            break;
                        }
                    }

                    if (!string.IsNullOrEmpty(bestExport))
                    {
                        uint diff = rva - bestExportRva;
                        if (diff == 0)
                        {
                            return $"{mod.Name}!{bestExport}";
                        }
                        else if (diff < 0x4000)
                        {
                            return $"{mod.Name}!{bestExport}+0x{diff:X}";
                        }
                    }

                    return $"{mod.Name}+0x{rva:X}";
                }
            }

            return $"0x{addr:X}";
        }

        #endregion

        private void LstModules_SelectedIndexChanged(object? sender, EventArgs e)
        {
            btnUnloadModule.Enabled = lstModules.SelectedItems.Count > 0;
        }

        private void LstThreads_SelectedIndexChanged(object? sender, EventArgs e)
        {
            bool hasSelection = lstThreads.SelectedItems.Count > 0;
            btnKillThread.Enabled = hasSelection;
            btnSuspendThread.Enabled = hasSelection;

            if (hasSelection && lstThreads.SelectedItems[0].Tag is ThreadInfo th)
            {
                bool isSuspended = _suspendedThreads.ContainsKey(th.Id) && _suspendedThreads[th.Id];
                btnSuspendThread.Text = isSuspended ? "Resume" : "Suspend";
            }
        }

        private void BtnUnloadModule_Click(object? sender, EventArgs e)
        {
            if (lstModules.SelectedItems.Count == 0 || lstModules.SelectedItems[0].Tag is not ModuleInfo mod)
                return;

            var res = MessageBox.Show($"Are you sure you want to unload module '{mod.Name}' from the target process?",
                "Unload Module", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (res != DialogResult.Yes) return;

            bool success = UnloadRemoteModule(_processId, mod.BaseAddress);
            if (success)
            {
                MessageBox.Show($"Module '{mod.Name}' successfully unloaded.", "Unload Module", MessageBoxButtons.OK, MessageBoxIcon.Information);
                LoadProcessInformation();
            }
            else
            {
                MessageBox.Show($"Failed to unload module '{mod.Name}'.", "Unload Module", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnKillThread_Click(object? sender, EventArgs e)
        {
            if (lstThreads.SelectedItems.Count == 0 || lstThreads.SelectedItems[0].Tag is not ThreadInfo th)
                return;

            var res = MessageBox.Show($"Are you sure you want to terminate thread ID {th.Id}?",
                "Kill Thread", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (res != DialogResult.Yes) return;

            bool success = KillRemoteThread(th.Id);
            if (success)
            {
                LoadProcessInformation();
            }
            else
            {
                MessageBox.Show($"Failed to terminate thread ID {th.Id}.", "Kill Thread", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnSuspendThread_Click(object? sender, EventArgs e)
        {
            if (lstThreads.SelectedItems.Count == 0 || lstThreads.SelectedItems[0].Tag is not ThreadInfo th)
                return;

            bool isSuspended = _suspendedThreads.ContainsKey(th.Id) && _suspendedThreads[th.Id];
            bool success;

            if (isSuspended)
            {
                success = ResumeRemoteThread(th.Id);
                if (success)
                {
                    _suspendedThreads[th.Id] = false;
                    btnSuspendThread.Text = "Suspend";
                }
            }
            else
            {
                success = SuspendRemoteThread(th.Id);
                if (success)
                {
                    _suspendedThreads[th.Id] = true;
                    btnSuspendThread.Text = "Resume";
                }
            }

            if (!success)
            {
                MessageBox.Show($"Failed to {(isSuspended ? "resume" : "suspend")} thread {th.Id}.", "Thread Control", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnKillProcess_Click(object? sender, EventArgs e)
        {
            var res = MessageBox.Show($"Are you sure you want to kill process '{_processName}' (PID: {_processId})?",
                "Kill Process", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (res != DialogResult.Yes) return;

            try
            {
                var proc = Process.GetProcessById(_processId);
                proc.Kill();
                MessageBox.Show($"Process '{_processName}' terminated.", "Kill Process", MessageBoxButtons.OK, MessageBoxIcon.Information);
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to kill process: {ex.Message}", "Kill Process", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #region Win32 Process / Thread / Module Operations

        private class ModuleInfo
        {
            public string Name { get; set; } = "";
            public IntPtr BaseAddress { get; set; }
            public int Size { get; set; }
            public string Path { get; set; } = "";
        }

        private class ThreadInfo
        {
            public int Id { get; set; }
            public IntPtr StartAddress { get; set; }
            public string StartAddressString { get; set; } = "";
            public string Priority { get; set; } = "Normal";
        }

        private static List<ModuleInfo> EnumerateModulesNative(int pid)
        {
            var list = new List<ModuleInfo>();

            IntPtr hProcess = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, false, pid);
            if (hProcess != IntPtr.Zero)
            {
                try
                {
                    if (EnumProcessModulesEx(hProcess, null, 0, out uint needed, 0x03 /* LIST_MODULES_ALL */) && needed > 0)
                    {
                        int count = (int)(needed / IntPtr.Size);
                        var hMods = new IntPtr[count];
                        if (EnumProcessModulesEx(hProcess, hMods, needed, out _, 0x03))
                        {
                            var sbName = new StringBuilder(1024);
                            var sbPath = new StringBuilder(1024);
                            foreach (var hMod in hMods)
                            {
                                if (hMod == IntPtr.Zero) continue;
                                sbName.Clear();
                                sbPath.Clear();
                                GetModuleBaseName(hProcess, hMod, sbName, (uint)sbName.Capacity);
                                GetModuleFileNameEx(hProcess, hMod, sbPath, (uint)sbPath.Capacity);

                                int modSize = 0;
                                if (GetModuleInformation(hProcess, hMod, out MODULEINFO modInfo, (uint)Marshal.SizeOf<MODULEINFO>()))
                                {
                                    modSize = (int)modInfo.SizeOfImage;
                                }

                                string name = sbName.ToString();
                                if (string.IsNullOrEmpty(name)) name = Path.GetFileName(sbPath.ToString());

                                if (!string.IsNullOrEmpty(name))
                                {
                                    list.Add(new ModuleInfo
                                    {
                                        Name = name,
                                        BaseAddress = hMod,
                                        Size = modSize,
                                        Path = sbPath.ToString()
                                    });
                                }
                            }
                        }
                    }
                }
                catch { }
                finally
                {
                    CloseHandle(hProcess);
                }
            }

            if (list.Count == 0)
            {
                list = EnumerateModulesFallback(pid);
            }

            return list;
        }

        private static List<ThreadInfo> EnumerateThreadsNative(int pid, List<ModuleInfo> modules)
        {
            var list = new List<ThreadInfo>();

            try
            {
                var proc = Process.GetProcessById(pid);
                foreach (ProcessThread th in proc.Threads)
                {
                    string startAddr = ResolveAddress(th.StartAddress, modules);
                    string priority = FormatPriority(th.PriorityLevel);
                    list.Add(new ThreadInfo
                    {
                        Id = th.Id,
                        StartAddress = th.StartAddress,
                        StartAddressString = startAddr,
                        Priority = priority
                    });
                }
            }
            catch
            {
                list = EnumerateThreadsFallback(pid, modules);
            }

            return list;
        }

        private static bool UnloadRemoteModule(int pid, IntPtr moduleBase)
        {
            IntPtr hProcess = OpenProcess(PROCESS_CREATE_THREAD | PROCESS_QUERY_INFORMATION | PROCESS_VM_OPERATION | PROCESS_VM_WRITE | PROCESS_VM_READ, false, pid);
            if (hProcess == IntPtr.Zero) return false;

            try
            {
                IntPtr hKernel32 = GetModuleHandle("kernel32.dll");
                IntPtr pFreeLibrary = GetProcAddress(hKernel32, "FreeLibrary");
                if (pFreeLibrary == IntPtr.Zero) return false;

                IntPtr hThread = CreateRemoteThread(hProcess, IntPtr.Zero, 0, pFreeLibrary, moduleBase, 0, out _);
                if (hThread != IntPtr.Zero)
                {
                    WaitForSingleObject(hThread, 3000);
                    CloseHandle(hThread);
                    return true;
                }
            }
            finally
            {
                CloseHandle(hProcess);
            }

            return false;
        }

        private static bool KillRemoteThread(int threadId)
        {
            const uint THREAD_TERMINATE = 0x0001;
            IntPtr hThread = OpenThread(THREAD_TERMINATE, false, threadId);
            if (hThread == IntPtr.Zero) return false;

            try
            {
                return TerminateThread(hThread, 0);
            }
            finally
            {
                CloseHandle(hThread);
            }
        }

        private static bool SuspendRemoteThread(int threadId)
        {
            const uint THREAD_SUSPEND_RESUME = 0x0002;
            IntPtr hThread = OpenThread(THREAD_SUSPEND_RESUME, false, threadId);
            if (hThread == IntPtr.Zero) return false;

            try
            {
                return SuspendThread(hThread) != unchecked((uint)-1);
            }
            finally
            {
                CloseHandle(hThread);
            }
        }

        private static bool ResumeRemoteThread(int threadId)
        {
            const uint THREAD_SUSPEND_RESUME = 0x0002;
            IntPtr hThread = OpenThread(THREAD_SUSPEND_RESUME, false, threadId);
            if (hThread == IntPtr.Zero) return false;

            try
            {
                return ResumeThread(hThread) != unchecked((uint)-1);
            }
            finally
            {
                CloseHandle(hThread);
            }
        }

        private static string GetProcessFullPath(int pid)
        {
            IntPtr hProc = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION | PROCESS_QUERY_INFORMATION, false, pid);
            if (hProc == IntPtr.Zero) return "";
            try
            {
                var sb = new StringBuilder(2048);
                int size = sb.Capacity;
                if (QueryFullProcessImageName(hProc, 0, sb, ref size))
                {
                    return sb.ToString();
                }

                sb.Clear();
                if (GetModuleFileNameEx(hProc, IntPtr.Zero, sb, (uint)sb.Capacity) > 0)
                {
                    return sb.ToString();
                }
            }
            finally
            {
                CloseHandle(hProc);
            }
            return "";
        }

        private static List<ModuleInfo> EnumerateModulesFallback(int pid)
        {
            var list = new List<ModuleInfo>();
            IntPtr hSnap = CreateToolhelp32Snapshot(0x00000008 | 0x00000010 /* TH32CS_SNAPMODULE | TH32CS_SNAPMODULE32 */, (uint)pid);
            if (hSnap == IntPtr.Zero || hSnap == (IntPtr)(-1)) return list;

            try
            {
                MODULEENTRY32 me = new MODULEENTRY32 { dwSize = (uint)Marshal.SizeOf<MODULEENTRY32>() };
                if (Module32First(hSnap, ref me))
                {
                    do
                    {
                        list.Add(new ModuleInfo
                        {
                            Name = me.szModule,
                            BaseAddress = me.modBaseAddr,
                            Size = (int)me.modBaseSize,
                            Path = me.szExePath
                        });
                    } while (Module32Next(hSnap, ref me));
                }
            }
            finally
            {
                CloseHandle(hSnap);
            }
            return list;
        }

        private static List<ThreadInfo> EnumerateThreadsFallback(int pid, List<ModuleInfo> modules)
        {
            var list = new List<ThreadInfo>();
            IntPtr hSnap = CreateToolhelp32Snapshot(0x00000004 /* TH32CS_SNAPTHREAD */, 0);
            if (hSnap == IntPtr.Zero || hSnap == (IntPtr)(-1)) return list;

            try
            {
                THREADENTRY32 te = new THREADENTRY32 { dwSize = (uint)Marshal.SizeOf<THREADENTRY32>() };
                if (Thread32First(hSnap, ref te))
                {
                    do
                    {
                        if (te.th32OwnerProcessID == pid)
                        {
                            list.Add(new ThreadInfo
                            {
                                Id = (int)te.th32ThreadID,
                                StartAddress = IntPtr.Zero,
                                StartAddressString = "Normal",
                                Priority = "Normal"
                            });
                        }
                    } while (Thread32Next(hSnap, ref te));
                }
            }
            finally
            {
                CloseHandle(hSnap);
            }
            return list;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct MODULEENTRY32
        {
            public uint dwSize;
            public uint th32ModuleID;
            public uint th32ProcessID;
            public uint GlblcntUsage;
            public uint ProccntUsage;
            public IntPtr modBaseAddr;
            public uint modBaseSize;
            public IntPtr hModule;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szModule;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szExePath;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct THREADENTRY32
        {
            public uint dwSize;
            public uint cntUsage;
            public uint th32ThreadID;
            public uint th32OwnerProcessID;
            public int tpBasePri;
            public int tpDeltaPri;
            public uint dwFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MODULEINFO
        {
            public IntPtr lpBaseOfDll;
            public uint SizeOfImage;
            public IntPtr EntryPoint;
        }

        private const uint PROCESS_CREATE_THREAD = 0x0002;
        private const uint PROCESS_VM_OPERATION = 0x0008;
        private const uint PROCESS_VM_READ = 0x0010;
        private const uint PROCESS_VM_WRITE = 0x0020;
        private const uint PROCESS_QUERY_INFORMATION = 0x0400;
        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenThread(uint dwDesiredAccess, bool bInheritHandle, int dwThreadId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint SuspendThread(IntPtr hThread);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint ResumeThread(IntPtr hThread);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool TerminateThread(IntPtr hThread, uint dwExitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttributes, uint dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, out uint lpThreadId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool QueryFullProcessImageName(IntPtr hProcess, int flags, StringBuilder text, ref int size);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern bool Module32First(IntPtr hSnapshot, ref MODULEENTRY32 lpme);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern bool Module32Next(IntPtr hSnapshot, ref MODULEENTRY32 lpme);

        [DllImport("kernel32.dll")]
        private static extern bool Thread32First(IntPtr hSnapshot, ref THREADENTRY32 lpte);

        [DllImport("kernel32.dll")]
        private static extern bool Thread32Next(IntPtr hSnapshot, ref THREADENTRY32 lpte);

        [DllImport("psapi.dll", SetLastError = true)]
        private static extern bool EnumProcessModulesEx(IntPtr hProcess, [Out] IntPtr[]? lphModule, uint cb, out uint lpcbNeeded, uint dwFilterFlag);

        [DllImport("psapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern uint GetModuleFileNameEx(IntPtr hProcess, IntPtr hModule, [Out] StringBuilder lpFilename, uint nSize);

        [DllImport("psapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern uint GetModuleBaseName(IntPtr hProcess, IntPtr hModule, [Out] StringBuilder lpBaseName, uint nSize);

        [DllImport("psapi.dll", SetLastError = true)]
        private static extern bool GetModuleInformation(IntPtr hProcess, IntPtr hModule, out MODULEINFO lpmodinfo, uint cb);

        #endregion
    }
}
