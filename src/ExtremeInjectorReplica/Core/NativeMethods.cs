using System;
using System.Runtime.InteropServices;

namespace ExtremeInjector.Core
{
    public static class NativeMethods
    {
        public const uint PROCESS_ALL_ACCESS = 0x1F0FFF;
        public const uint PROCESS_CREATE_THREAD = 0x0002;
        public const uint PROCESS_VM_OPERATION = 0x0008;
        public const uint PROCESS_VM_READ = 0x0010;
        public const uint PROCESS_VM_WRITE = 0x0020;
        public const uint PROCESS_QUERY_INFORMATION = 0x0400;
        public const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

        public const uint MEM_COMMIT = 0x1000;
        public const uint MEM_RESERVE = 0x2000;
        public const uint MEM_RELEASE = 0x8000;
        public const uint PAGE_EXECUTE_READWRITE = 0x40;
        public const uint PAGE_READWRITE = 0x04;
        public const uint PAGE_READONLY = 0x02;

        public const uint THREAD_ALL_ACCESS = 0x1FFFFF;
        public const uint THREAD_SUSPEND_RESUME = 0x0002;
        public const uint THREAD_GET_CONTEXT = 0x0008;
        public const uint THREAD_SET_CONTEXT = 0x0010;
        public const uint THREAD_QUERY_INFORMATION = 0x0040;

        public const uint CONTEXT_X86_CONTROL = 0x00010001;
        public const uint CONTEXT_X86_INTEGER = 0x00010002;
        public const uint CONTEXT_X86_ALL = 0x0001003F;

        public const uint CONTEXT_AMD64_CONTROL = 0x00100001;
        public const uint CONTEXT_AMD64_INTEGER = 0x00100002;
        public const uint CONTEXT_AMD64_ALL = 0x0010001F;

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct THREADENTRY32
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
        public struct UNICODE_STRING
        {
            public ushort Length;
            public ushort MaximumLength;
            public IntPtr Buffer;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct CONTEXT_X86
        {
            public uint ContextFlags;
            public uint Dr0, Dr1, Dr2, Dr3, Dr6, Dr7;
            public uint FloatSave;
            public uint SegGs, SegFs, SegEs, SegDs;
            public uint Edi, Esi, Ebx, Edx, Ecx, Eax;
            public uint Ebp, Eip, SegCs, EFlags, Esp, SegSs;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 512)]
            public byte[] ExtendedRegisters;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 16)]
        public struct CONTEXT_X64
        {
            public ulong P1Home, P2Home, P3Home, P4Home, P5Home, P6Home;
            public uint ContextFlags;
            public uint MxCsr;
            public ushort SegCs, SegDs, SegEs, SegFs, SegGs, SegSs;
            public uint EFlags;
            public ulong Dr0, Dr1, Dr2, Dr3, Dr6, Dr7;
            public ulong Rax, Rcx, Rdx, Rbx, Rsp, Rbp, Rsi, Rdi;
            public ulong R8, R9, R10, R11, R12, R13, R14, R15;
            public ulong Rip;
            // Additional registers omitted for brevity
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        public static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, UIntPtr dwSize, uint flAllocationType, uint flProtect);

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        public static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress, UIntPtr dwSize, uint dwFreeType);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool VirtualProtectEx(IntPtr hProcess, IntPtr lpAddress, UIntPtr dwSize, uint flNewProtect, out uint lpflOldProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, UIntPtr nSize, out IntPtr lpNumberOfBytesWritten);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, [Out] byte[] lpBuffer, UIntPtr nSize, out IntPtr lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttributes, UIntPtr dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, out uint lpThreadId);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
        public static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GetExitCodeThread(IntPtr hThread, out uint lpExitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr OpenThread(uint dwDesiredAccess, bool bInheritHandle, uint dwThreadId);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern uint SuspendThread(IntPtr hThread);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern uint ResumeThread(IntPtr hThread);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct MODULEENTRY32
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

        public const uint TH32CS_SNAPMODULE = 0x00000008;
        public const uint TH32CS_SNAPMODULE32 = 0x00000010;

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GetThreadContext(IntPtr hThread, ref CONTEXT_X86 lpContext);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool SetThreadContext(IntPtr hThread, ref CONTEXT_X86 lpContext);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool Thread32First(IntPtr hSnapshot, ref THREADENTRY32 lpte);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool Thread32Next(IntPtr hSnapshot, ref THREADENTRY32 lpte);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool Module32First(IntPtr hSnapshot, ref MODULEENTRY32 lpme);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern bool Module32Next(IntPtr hSnapshot, ref MODULEENTRY32 lpme);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool IsWow64Process(IntPtr hProcess, out bool wow64Process);

        public const uint THREAD_CREATE_FLAGS_CREATE_SUSPENDED = 0x00000001;
        public const uint THREAD_CREATE_FLAGS_SKIP_THREAD_ATTACH = 0x00000002;
        public const uint THREAD_CREATE_FLAGS_HIDE_FROM_DEBUGGER = 0x00000004;

        // NTAPI definitions for LdrLoadDll and NtCreateThreadEx
        [DllImport("ntdll.dll", SetLastError = true)]
        public static extern int NtCreateThreadEx(
            out IntPtr hThread,
            uint desiredAccess,
            IntPtr objectAttributes,
            IntPtr processHandle,
            IntPtr startAddress,
            IntPtr parameter,
            uint createFlags,
            UIntPtr stackZeroBits,
            UIntPtr sizeOfStackCommit,
            UIntPtr sizeOfStackReserve,
            IntPtr bytesBuffer
        );

        [DllImport("ntdll.dll", SetLastError = true)]
        public static extern void RtlInitUnicodeString(ref UNICODE_STRING DestinationString, [MarshalAs(UnmanagedType.LPWStr)] string SourceString);

        public const uint CREATE_SUSPENDED = 0x00000004;
        public const uint ThreadHideFromDebugger = 17;

        [DllImport("ntdll.dll", SetLastError = true)]
        public static extern int NtSetInformationThread(
            IntPtr threadHandle,
            uint threadInformationClass,
            IntPtr threadInformation,
            uint threadInformationLength
        );

        public const uint ThreadQuerySetWin32StartAddress = 9;

        [DllImport("ntdll.dll", SetLastError = true)]
        public static extern int NtQueryInformationThread(
            IntPtr threadHandle,
            uint threadInformationClass,
            out IntPtr threadInformation,
            uint threadInformationLength,
            out uint returnLength
        );

        [StructLayout(LayoutKind.Sequential)]
        public struct PROCESS_BASIC_INFORMATION
        {
            public IntPtr ExitStatus;
            public IntPtr PebBaseAddress;
            public IntPtr AffinityMask;
            public IntPtr BasePriority;
            public IntPtr UniqueProcessId;
            public IntPtr InheritedFromUniqueProcessId;
        }

        public const int ProcessBasicInformation = 0;
        public const int ProcessWow64Information = 26;

        [DllImport("ntdll.dll", SetLastError = true)]
        public static extern int NtQueryInformationProcess(
            IntPtr processHandle,
            int processInformationClass,
            out PROCESS_BASIC_INFORMATION processInformation,
            uint processInformationLength,
            out uint returnLength
        );

        [DllImport("ntdll.dll", SetLastError = true)]
        public static extern int NtQueryInformationProcess(
            IntPtr processHandle,
            int processInformationClass,
            out IntPtr processInformation,
            uint processInformationLength,
            out uint returnLength
        );

        [DllImport("ntdll.dll", SetLastError = true)]
        public static extern int NtSuspendProcess(IntPtr processHandle);

        [DllImport("ntdll.dll", SetLastError = true)]
        public static extern int NtResumeProcess(IntPtr processHandle);

        /// <summary>
        /// Creates a remote thread supporting Stealth Inject (SKIP_THREAD_ATTACH) and HideFromDebugger with automatic fallback.
        /// </summary>
        public static bool CreateRemoteThreadSmart(
            IntPtr hProcess,
            IntPtr startAddress,
            IntPtr parameter,
            bool stealthInject,
            bool hideFromDebugger,
            out IntPtr hThread,
            out string errorMessage)
        {
            hThread = IntPtr.Zero;
            errorMessage = "";

            // Method 1: Direct NtCreateThreadEx with Stealth & Anti-Debug Flags
            if (stealthInject || hideFromDebugger)
            {
                uint ntCreateFlags = 0;
                if (stealthInject)
                {
                    ntCreateFlags |= THREAD_CREATE_FLAGS_SKIP_THREAD_ATTACH;
                }
                if (hideFromDebugger)
                {
                    ntCreateFlags |= THREAD_CREATE_FLAGS_HIDE_FROM_DEBUGGER;
                }

                try
                {
                    int status = NtCreateThreadEx(
                        out hThread,
                        0x1FFFFF /* THREAD_ALL_ACCESS */,
                        IntPtr.Zero,
                        hProcess,
                        startAddress,
                        parameter,
                        ntCreateFlags,
                        UIntPtr.Zero,
                        UIntPtr.Zero,
                        UIntPtr.Zero,
                        IntPtr.Zero
                    );

                    if (status == 0 && hThread != IntPtr.Zero)
                    {
                        return true;
                    }
                }
                catch
                {
                    // Fall through to Win32 CreateRemoteThread
                }
            }

            // Method 2: Standard Win32 CreateRemoteThread with optional Suspended + NtSetInformationThread
            uint win32Flags = hideFromDebugger ? CREATE_SUSPENDED : 0;
            hThread = CreateRemoteThread(
                hProcess,
                IntPtr.Zero,
                UIntPtr.Zero,
                startAddress,
                parameter,
                win32Flags,
                out _
            );

            if (hThread == IntPtr.Zero)
            {
                int err = Marshal.GetLastWin32Error();
                errorMessage = $"Failed to create remote thread.\nWin32 Error {err}: {new System.ComponentModel.Win32Exception(err).Message}";
                return false;
            }

            if (hideFromDebugger)
            {
                try
                {
                    NtSetInformationThread(
                        hThread,
                        ThreadHideFromDebugger,
                        IntPtr.Zero,
                        0
                    );
                }
                catch { }

                ResumeThread(hThread);
            }

            return true;
        }
    }
}
