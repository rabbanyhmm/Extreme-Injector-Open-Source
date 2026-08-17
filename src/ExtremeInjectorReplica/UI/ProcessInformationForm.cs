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

        private Button btnKillProcess = null!;
        private Button btnClose = null!;

        private readonly Dictionary<int, bool> _suspendedThreads = new();

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
            Size = new Size(408, 500);
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
                Size = new Size(368, 128),
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
                Location = new Point(50, 20),
                Size = new Size(306, 16),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                AutoEllipsis = true
            };

            lblProcessPath = new Label
            {
                Text = "Path: Loading...",
                Location = new Point(50, 38),
                Size = new Size(306, 32),
                Font = new Font("Segoe UI", 8.25f),
                ForeColor = Color.FromArgb(40, 40, 40),
                AutoEllipsis = true
            };

            lblProcessId = new Label
            {
                Text = $"Process ID: 0x{_processId:X} ({_processId})",
                Location = new Point(50, 74),
                Size = new Size(306, 16),
                Font = new Font("Segoe UI", 9f)
            };

            lblModulesThreads = new Label
            {
                Text = "Modules: 0  Threads: 0",
                Location = new Point(50, 96),
                Size = new Size(306, 16),
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
                Location = new Point(12, 146),
                Size = new Size(368, 272),
                Font = new Font("Segoe UI", 9f)
            };

            // Tab 1: Modules
            tabModules = new TabPage { Text = "Modules", BackColor = Color.White };
            lstModules = new ListView
            {
                Location = new Point(6, 6),
                Size = new Size(348, 196),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                MultiSelect = false,
                Font = new Font("Segoe UI", 8.5f),
                HideSelection = false
            };
            lstModules.Columns.Add("Module Name", 145);
            lstModules.Columns.Add("Module Base", 115);
            lstModules.Columns.Add("Module Size", 82);
            lstModules.SelectedIndexChanged += LstModules_SelectedIndexChanged;

            btnUnloadModule = new Button
            {
                Text = "Unload Module",
                Location = new Point(234, 206),
                Size = new Size(120, 25),
                FlatStyle = FlatStyle.System,
                UseVisualStyleBackColor = true,
                Enabled = false
            };
            btnUnloadModule.Click += BtnUnloadModule_Click;

            tabModules.Controls.AddRange(new Control[] { lstModules, btnUnloadModule });

            // Tab 2: Threads
            tabThreads = new TabPage { Text = "Threads", BackColor = Color.White };
            lstThreads = new ListView
            {
                Location = new Point(6, 6),
                Size = new Size(348, 196),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                MultiSelect = false,
                Font = new Font("Segoe UI", 8.5f),
                HideSelection = false
            };
            lstThreads.Columns.Add("Thread ID", 85);
            lstThreads.Columns.Add("Start Address", 160);
            lstThreads.Columns.Add("Priority", 95);
            lstThreads.SelectedIndexChanged += LstThreads_SelectedIndexChanged;

            btnKillThread = new Button
            {
                Text = "Kill",
                Location = new Point(164, 206),
                Size = new Size(90, 25),
                FlatStyle = FlatStyle.System,
                UseVisualStyleBackColor = true,
                Enabled = false
            };
            btnKillThread.Click += BtnKillThread_Click;

            btnSuspendThread = new Button
            {
                Text = "Suspend",
                Location = new Point(264, 206),
                Size = new Size(90, 25),
                FlatStyle = FlatStyle.System,
                UseVisualStyleBackColor = true,
                Enabled = false
            };
            btnSuspendThread.Click += BtnSuspendThread_Click;

            tabThreads.Controls.AddRange(new Control[] { lstThreads, btnKillThread, btnSuspendThread });

            tabControl.TabPages.Add(tabModules);
            tabControl.TabPages.Add(tabThreads);

            // 3. Bottom Action Buttons
            btnKillProcess = new Button
            {
                Text = "Kill Process",
                Location = new Point(164, 426),
                Size = new Size(105, 26),
                FlatStyle = FlatStyle.System,
                UseVisualStyleBackColor = true
            };
            btnKillProcess.Click += BtnKillProcess_Click;

            btnClose = new Button
            {
                Text = "Close",
                Location = new Point(275, 426),
                Size = new Size(105, 26),
                FlatStyle = FlatStyle.System,
                UseVisualStyleBackColor = true
            };
            btnClose.Click += (s, e) => Close();

            Controls.AddRange(new Control[] {
                grpProcess,
                tabControl,
                btnKillProcess,
                btnClose
            });
        }

        private void LoadProcessIcon()
        {
            try
            {
                var proc = Process.GetProcessById(_processId);
                string path = proc.MainModule?.FileName ?? "";
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    using var ico = Icon.ExtractAssociatedIcon(path);
                    if (ico != null) picProcessIcon.Image = ico.ToBitmap();
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
                var proc = Process.GetProcessById(_processId);
                string fullPath = "";
                try
                {
                    fullPath = proc.MainModule?.FileName ?? "";
                }
                catch
                {
                    fullPath = GetProcessPathWin32(_processId);
                }

                lblProcessPath.Text = string.IsNullOrEmpty(fullPath) ? _processName : fullPath;

                // Load Modules
                var modulesList = new List<ModuleInfo>();
                try
                {
                    foreach (ProcessModule mod in proc.Modules)
                    {
                        modulesList.Add(new ModuleInfo
                        {
                            Name = mod.ModuleName,
                            BaseAddress = mod.BaseAddress,
                            Size = mod.ModuleMemorySize,
                            Path = mod.FileName
                        });
                    }
                }
                catch
                {
                    modulesList = EnumerateModulesFallback(_processId);
                }

                lstModules.BeginUpdate();
                lstModules.Items.Clear();
                foreach (var mod in modulesList)
                {
                    var item = new ListViewItem(mod.Name);
                    string baseAddrStr = IntPtr.Size == 8 
                        ? $"0x{mod.BaseAddress.ToInt64():X12}" 
                        : $"0x{mod.BaseAddress.ToInt64():X8}";
                    item.SubItems.Add(baseAddrStr);
                    item.SubItems.Add(FormatSize(mod.Size));
                    item.Tag = mod;
                    lstModules.Items.Add(item);
                }
                lstModules.EndUpdate();

                // Load Threads
                var threadsList = new List<ThreadInfo>();
                try
                {
                    foreach (ProcessThread th in proc.Threads)
                    {
                        string startAddr = ResolveAddress(th.StartAddress, modulesList);
                        string priority = FormatPriority(th.PriorityLevel);
                        threadsList.Add(new ThreadInfo
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
                    threadsList = EnumerateThreadsFallback(_processId, modulesList);
                }

                lstThreads.BeginUpdate();
                lstThreads.Items.Clear();
                foreach (var th in threadsList)
                {
                    var item = new ListViewItem(th.Id.ToString());
                    item.SubItems.Add(th.StartAddressString);
                    item.SubItems.Add(th.Priority);
                    item.Tag = th;
                    lstThreads.Items.Add(item);
                }
                lstThreads.EndUpdate();

                lblModulesThreads.Text = $"Modules: {modulesList.Count}  Threads: {threadsList.Count}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to inspect process: {ex.Message}", "Process Information", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
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

        private static string ResolveAddress(IntPtr address, List<ModuleInfo> modules)
        {
            long addr = address.ToInt64();
            foreach (var mod in modules)
            {
                long baseAddr = mod.BaseAddress.ToInt64();
                if (addr >= baseAddr && addr < baseAddr + mod.Size)
                {
                    long offset = addr - baseAddr;
                    return $"{mod.Name}+0x{offset:X}";
                }
            }

            return IntPtr.Size == 8 ? $"0x{addr:X12}" : $"0x{addr:X8}";
        }

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

        private static string GetProcessPathWin32(int pid)
        {
            IntPtr hProc = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (hProc == IntPtr.Zero) return "";
            try
            {
                var sb = new StringBuilder(1024);
                int size = sb.Capacity;
                if (QueryFullProcessImageName(hProc, 0, sb, ref size))
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

        #endregion
    }
}
