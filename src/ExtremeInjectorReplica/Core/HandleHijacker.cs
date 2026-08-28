using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ExtremeInjector.Core
{
    /// <summary>
    /// Handle Hijacking Engine for Protected Processes (e.g. HD-Player / Emulators / Anti-Cheat Protected Targets).
    /// When direct OpenProcess is blocked or hooked by anti-cheat drivers, this engine scans the system handle table
    /// via NtQuerySystemInformation (SystemExtendedHandleInformation = 64) and duplicates an existing privileged handle
    /// from another process (such as system services or background processes).
    /// </summary>
    public static class HandleHijacker
    {
        private const uint SystemExtendedHandleInformation = 64;
        private const uint STATUS_INFO_LENGTH_MISMATCH = 0xC0000004;

        private const uint PROCESS_DUP_HANDLE = 0x0040;
        private const uint DUPLICATE_SAME_ACCESS = 0x00000002;

        [StructLayout(LayoutKind.Sequential)]
        private struct SYSTEM_HANDLE_TABLE_ENTRY_INFO_EX
        {
            public IntPtr Object;
            public UIntPtr UniqueProcessId;
            public UIntPtr HandleValue;
            public uint GrantedAccess;
            public ushort CreatorBackTraceIndex;
            public ushort ObjectTypeIndex;
            public uint HandleAttributes;
            public uint Reserved;
        }

        [DllImport("ntdll.dll", SetLastError = true)]
        private static extern int NtQuerySystemInformation(
            uint SystemInformationClass,
            IntPtr SystemInformation,
            uint SystemInformationLength,
            out uint ReturnLength
        );

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool DuplicateHandle(
            IntPtr hSourceProcessHandle,
            IntPtr hSourceHandle,
            IntPtr hTargetProcessHandle,
            out IntPtr lpTargetHandle,
            uint dwDesiredAccess,
            bool bInheritHandle,
            uint dwOptions
        );

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint GetProcessId(IntPtr hInstance);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        /// <summary>
        /// Attempts to open a target process with desired access.
        /// If direct OpenProcess fails or the returned handle lacks VM write access (protected process),
        /// automatically falls back to handle hijacking via NtQuerySystemInformation.
        /// </summary>
        public static IntPtr OpenProcessSmart(int processId, uint desiredAccess, out bool wasHijacked)
        {
            wasHijacked = false;

            // 1. Enable SeDebugPrivilege upfront — helps with both paths
            PrivilegeManager.EnableAllSecurityPrivileges();

            // 2. Try standard OpenProcess
            IntPtr hProcess = NativeMethods.OpenProcess(desiredAccess, false, processId);
            if (hProcess != IntPtr.Zero)
            {
                // Verify the handle actually has VM write access by probing with a small allocation.
                // Some anti-cheat hooks let OpenProcess succeed but block VirtualAllocEx — same check
                // the reference C++ injector does before trusting any handle.
                IntPtr probe = NativeMethods.VirtualAllocEx(hProcess, IntPtr.Zero, (UIntPtr)0x1000,
                    NativeMethods.MEM_COMMIT | NativeMethods.MEM_RESERVE, NativeMethods.PAGE_READWRITE);
                if (probe != IntPtr.Zero)
                {
                    NativeMethods.VirtualFreeEx(hProcess, probe, UIntPtr.Zero, NativeMethods.MEM_RELEASE);
                    return hProcess; // Direct handle is fully usable
                }

                // Handle is present but VM access is blocked — close it and fall through to hijack
                NativeMethods.CloseHandle(hProcess);
                hProcess = IntPtr.Zero;
            }

            // 3. Fallback: scan system handle table and duplicate a privileged handle
            hProcess = HijackHandle((uint)processId, desiredAccess);
            if (hProcess != IntPtr.Zero)
            {
                wasHijacked = true;
                return hProcess;
            }

            return IntPtr.Zero;
        }

        /// <summary>
        /// Scans the system handle table for any existing handle pointing to targetPid with VM_OPERATION permissions,
        /// duplicates it into our process, and returns the hijacked handle.
        /// </summary>
        public static IntPtr HijackHandle(uint targetPid, uint requiredAccess)
        {
            uint bufferSize = 0x10000;
            IntPtr buffer = Marshal.AllocHGlobal((int)bufferSize);
            try
            {
                int status;
                while ((status = (int)NtQuerySystemInformation(SystemExtendedHandleInformation, buffer, bufferSize, out uint returnLen)) == unchecked((int)STATUS_INFO_LENGTH_MISMATCH))
                {
                    Marshal.FreeHGlobal(buffer);
                    bufferSize *= 2;
                    buffer = Marshal.AllocHGlobal((int)bufferSize);
                }

                if (status != 0)
                {
                    return IntPtr.Zero;
                }

                ulong numberOfHandles = (ulong)Marshal.ReadIntPtr(buffer);
                int currentPid = Process.GetCurrentProcess().Id;
                IntPtr hCurrentProcess = GetCurrentProcess();

                int entrySize = Marshal.SizeOf(typeof(SYSTEM_HANDLE_TABLE_ENTRY_INFO_EX));
                IntPtr pEntryBase = new IntPtr(buffer.ToInt64() + (IntPtr.Size * 2)); // Offset past NumberOfHandles & Reserved

                for (ulong i = 0; i < numberOfHandles; i++)
                {
                    IntPtr pCurrentEntry = new IntPtr(pEntryBase.ToInt64() + (long)(i * (ulong)entrySize));
                    var entry = Marshal.PtrToStructure<SYSTEM_HANDLE_TABLE_ENTRY_INFO_EX>(pCurrentEntry);

                    uint ownerPid = (uint)entry.UniqueProcessId.ToUInt64();
                    if (ownerPid == currentPid) continue;

                    // Filter: Handle must have VM_OPERATION (0x0008) permission at minimum
                    if ((entry.GrantedAccess & NativeMethods.PROCESS_VM_OPERATION) == 0) continue;

                    IntPtr hOwner = OpenProcess(PROCESS_DUP_HANDLE, false, ownerPid);
                    if (hOwner == IntPtr.Zero) continue;

                    try
                    {
                        if (DuplicateHandle(hOwner, (IntPtr)entry.HandleValue.ToUInt64(), hCurrentProcess, out IntPtr hDup, 0, false, DUPLICATE_SAME_ACCESS))
                        {
                            if (GetProcessId(hDup) == targetPid)
                            {
                                // Test allocation on duplicated handle to verify write access
                                IntPtr pTest = NativeMethods.VirtualAllocEx(hDup, IntPtr.Zero, (UIntPtr)0x1000, NativeMethods.MEM_COMMIT | NativeMethods.MEM_RESERVE, NativeMethods.PAGE_READWRITE);
                                if (pTest != IntPtr.Zero)
                                {
                                    NativeMethods.VirtualFreeEx(hDup, pTest, UIntPtr.Zero, NativeMethods.MEM_RELEASE);
                                    return hDup; // Successfully hijacked privileged handle!
                                }
                            }
                            CloseHandle(hDup);
                        }
                    }
                    finally
                    {
                        CloseHandle(hOwner);
                    }
                }
            }
            catch
            {
                // Fallback failed cleanly
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }

            return IntPtr.Zero;
        }
    }
}
