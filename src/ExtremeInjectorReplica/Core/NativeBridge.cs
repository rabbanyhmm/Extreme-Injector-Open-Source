using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace ExtremeInjector.Core
{
    /// <summary>
    /// Loads InjectorCore32.dll or InjectorCore64.dll from embedded assembly resources
    /// directly into the process address space — zero files written to disk.
    /// All P/Invoke calls go through the dynamically loaded handle via
    /// LoadLibrary on a temp path extracted once per session.
    /// </summary>
    public static class NativeBridge
    {
        private static IntPtr _hModule = IntPtr.Zero;
        private static string? _tempPath = null;
        private static readonly object _lock = new object();

        // Structs mirroring the C++ header exactly
        [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Unicode)]
        public struct ProcessEntry
        {
            public uint ProcessId;
            public uint ParentProcessId;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string ExeName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 1024)] public string FullPath;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 512)] public string Description;
            public int Is64Bit;
            public IntPtr hIcon;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Unicode)]
        public struct WindowEntry
        {
            public IntPtr hWnd;
            public uint ProcessId;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 512)] public string WindowTitle;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string ClassName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string ExeName;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Unicode)]
        public struct ModuleEntry
        {
            public IntPtr BaseAddress;
            public UIntPtr SizeOfImage;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string ModuleName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 1024)] public string FullPath;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Unicode)]
        public struct ThreadEntry
        {
            public uint ThreadId;
            public uint BasePriority;
            public IntPtr StartAddress;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string StateDescription;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Unicode)]
        public struct ProcessDetail
        {
            public uint ProcessId;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string ExeName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 1024)] public string FullPath;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 512)] public string Description;
            public int Is64Bit;
            public int IsElevated;
            public uint ThreadCount;
            public uint ModuleCount;
            public UIntPtr WorkingSetSize;
            public ulong CreateTime;
        }

        // Delegate signatures matching C++ exports
        private delegate bool EnumProcessListDelegate(IntPtr pOut, uint maxCount, out uint pActual);
        private delegate bool EnumWindowListDelegate(IntPtr pOut, uint maxCount, out uint pActual);
        private delegate bool GetProcessDetailDelegate(uint processId, IntPtr pDetail);
        private delegate bool EnumModuleListDelegate(uint processId, IntPtr pOut, uint maxCount, out uint pActual);
        private delegate bool EnumThreadListDelegate(uint processId, IntPtr pOut, uint maxCount, out uint pActual);
        private delegate bool UnloadRemoteModuleDelegate(uint processId, IntPtr moduleBase);
        private delegate bool SuspendProcessDelegate(uint processId);
        private delegate bool ResumeProcessDelegate(uint processId);
        private delegate bool KillProcessDelegate(uint processId, uint exitCode);

        // Cached delegates
        private static EnumProcessListDelegate? _enumProcessList;
        private static EnumWindowListDelegate? _enumWindowList;
        private static GetProcessDetailDelegate? _getProcessDetail;
        private static EnumModuleListDelegate? _enumModuleList;
        private static EnumThreadListDelegate? _enumThreadList;
        private static UnloadRemoteModuleDelegate? _unloadRemoteModule;
        private static SuspendProcessDelegate? _suspendProcess;
        private static ResumeProcessDelegate? _resumeProcess;
        private static KillProcessDelegate? _killProcess;

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibraryW(string lpFileName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeLibrary(IntPtr hModule);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        private static bool EnsureLoaded()
        {
            if (_hModule != IntPtr.Zero) return true;
            lock (_lock)
            {
                if (_hModule != IntPtr.Zero) return true;
                try
                {
                    bool is64 = IntPtr.Size == 8;
                    string resourceName = is64 ? "InjectorCore64.dll" : "InjectorCore32.dll";

                    // Extract embedded DLL to a temp file (TEMP folder only, single file, overwritten each run)
                    _tempPath = Path.Combine(Path.GetTempPath(), resourceName);
                    using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
                    {
                        if (stream == null) return false;
                        using (var fs = new FileStream(_tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                            stream.CopyTo(fs);
                    }

                    _hModule = LoadLibraryW(_tempPath);
                    if (_hModule == IntPtr.Zero) return false;

                    _enumProcessList   = GetDelegate<EnumProcessListDelegate>("EnumProcessList");
                    _enumWindowList    = GetDelegate<EnumWindowListDelegate>("EnumWindowList");
                    _getProcessDetail  = GetDelegate<GetProcessDetailDelegate>("GetProcessDetail");
                    _enumModuleList    = GetDelegate<EnumModuleListDelegate>("EnumModuleList");
                    _enumThreadList    = GetDelegate<EnumThreadListDelegate>("EnumThreadList");
                    _unloadRemoteModule = GetDelegate<UnloadRemoteModuleDelegate>("UnloadRemoteModule");
                    _suspendProcess    = GetDelegate<SuspendProcessDelegate>("SuspendProcess");
                    _resumeProcess     = GetDelegate<ResumeProcessDelegate>("ResumeProcess");
                    _killProcess       = GetDelegate<KillProcessDelegate>("KillProcess");

                    return true;
                }
                catch { return false; }
            }
        }

        private static T? GetDelegate<T>(string name) where T : Delegate
        {
            IntPtr ptr = GetProcAddress(_hModule, name);
            return ptr != IntPtr.Zero ? Marshal.GetDelegateForFunctionPointer<T>(ptr) : null;
        }

        // -----------------------------------------------------------------------
        // Public API
        // -----------------------------------------------------------------------

        public static ProcessEntry[] EnumProcessList()
        {
            if (!EnsureLoaded() || _enumProcessList == null) return Array.Empty<ProcessEntry>();
            const int MAX = 4096;
            int size = Marshal.SizeOf<ProcessEntry>();
            IntPtr buf = Marshal.AllocHGlobal(size * MAX);
            try
            {
                if (!_enumProcessList(buf, MAX, out uint actual)) return Array.Empty<ProcessEntry>();
                var result = new ProcessEntry[actual];
                for (int i = 0; i < actual; i++)
                    result[i] = Marshal.PtrToStructure<ProcessEntry>(buf + i * size);
                return result;
            }
            finally { Marshal.FreeHGlobal(buf); }
        }

        public static WindowEntry[] EnumWindowList()
        {
            if (!EnsureLoaded() || _enumWindowList == null) return Array.Empty<WindowEntry>();
            const int MAX = 2048;
            int size = Marshal.SizeOf<WindowEntry>();
            IntPtr buf = Marshal.AllocHGlobal(size * MAX);
            try
            {
                if (!_enumWindowList(buf, MAX, out uint actual)) return Array.Empty<WindowEntry>();
                var result = new WindowEntry[actual];
                for (int i = 0; i < actual; i++)
                    result[i] = Marshal.PtrToStructure<WindowEntry>(buf + i * size);
                return result;
            }
            finally { Marshal.FreeHGlobal(buf); }
        }

        public static ProcessDetail? GetProcessDetail(uint processId)
        {
            if (!EnsureLoaded() || _getProcessDetail == null) return null;
            int size = Marshal.SizeOf<ProcessDetail>();
            IntPtr buf = Marshal.AllocHGlobal(size);
            try
            {
                if (!_getProcessDetail(processId, buf)) return null;
                return Marshal.PtrToStructure<ProcessDetail>(buf);
            }
            finally { Marshal.FreeHGlobal(buf); }
        }

        public static ModuleEntry[] EnumModuleList(uint processId)
        {
            if (!EnsureLoaded() || _enumModuleList == null) return Array.Empty<ModuleEntry>();
            const int MAX = 2048;
            int size = Marshal.SizeOf<ModuleEntry>();
            IntPtr buf = Marshal.AllocHGlobal(size * MAX);
            try
            {
                if (!_enumModuleList(processId, buf, MAX, out uint actual)) return Array.Empty<ModuleEntry>();
                var result = new ModuleEntry[actual];
                for (int i = 0; i < actual; i++)
                    result[i] = Marshal.PtrToStructure<ModuleEntry>(buf + i * size);
                return result;
            }
            finally { Marshal.FreeHGlobal(buf); }
        }

        public static ThreadEntry[] EnumThreadList(uint processId)
        {
            if (!EnsureLoaded() || _enumThreadList == null) return Array.Empty<ThreadEntry>();
            const int MAX = 2048;
            int size = Marshal.SizeOf<ThreadEntry>();
            IntPtr buf = Marshal.AllocHGlobal(size * MAX);
            try
            {
                if (!_enumThreadList(processId, buf, MAX, out uint actual)) return Array.Empty<ThreadEntry>();
                var result = new ThreadEntry[actual];
                for (int i = 0; i < actual; i++)
                    result[i] = Marshal.PtrToStructure<ThreadEntry>(buf + i * size);
                return result;
            }
            finally { Marshal.FreeHGlobal(buf); }
        }

        public static bool UnloadRemoteModule(uint processId, IntPtr moduleBase)
        {
            if (!EnsureLoaded() || _unloadRemoteModule == null) return false;
            return _unloadRemoteModule(processId, moduleBase);
        }

        public static bool SuspendProcess(uint processId)
        {
            if (!EnsureLoaded() || _suspendProcess == null) return false;
            return _suspendProcess(processId);
        }

        public static bool ResumeProcess(uint processId)
        {
            if (!EnsureLoaded() || _resumeProcess == null) return false;
            return _resumeProcess(processId);
        }

        public static bool KillProcess(uint processId, uint exitCode = 0)
        {
            if (!EnsureLoaded() || _killProcess == null) return false;
            return _killProcess(processId, exitCode);
        }
    }
}
