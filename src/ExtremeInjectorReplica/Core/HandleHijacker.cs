using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ExtremeInjector.Core
{
    /// <summary>
    /// Scans system handle table and duplicates privileged process handles
    /// when direct OpenProcess calls are restricted or hooked.
    /// </summary>
    public static class HandleHijacker
    {
        private const uint SystemExtendedHandleInformation = 64;
        private const uint STATUS_INFO_LENGTH_MISMATCH     = 0xC0000004;

        private const uint PROCESS_DUP_HANDLE    = 0x0040;
        private const uint DUPLICATE_SAME_ACCESS = 0x00000002;

        [StructLayout(LayoutKind.Sequential)]
        private struct SYSTEM_HANDLE_TABLE_ENTRY_INFO_EX
        {
            public IntPtr  Object;
            public UIntPtr UniqueProcessId;
            public UIntPtr HandleValue;
            public uint    GrantedAccess;
            public ushort  CreatorBackTraceIndex;
            public ushort  ObjectTypeIndex;
            public uint    HandleAttributes;
            public uint    Reserved;
        }

        [DllImport("ntdll.dll")]
        private static extern int NtQuerySystemInformation(
            uint   SystemInformationClass,
            IntPtr SystemInformation,
            uint   SystemInformationLength,
            out uint ReturnLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool DuplicateHandle(
            IntPtr   hSourceProcessHandle,
            IntPtr   hSourceHandle,
            IntPtr   hTargetProcessHandle,
            out IntPtr lpTargetHandle,
            uint     dwDesiredAccess,
            bool     bInheritHandle,
            uint     dwOptions);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint GetProcessId(IntPtr hProcess);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        /// <summary>
        /// Tries direct OpenProcess first, falling back to handle hijacking if memory allocation fails.
        /// </summary>
        public static IntPtr OpenProcessSmart(int processId, uint desiredAccess, out bool wasHijacked)
        {
            wasHijacked = false;
            PrivilegeManager.EnableAllSecurityPrivileges();

            // Try standard OpenProcess
            IntPtr hProcess = NativeMethods.OpenProcess(desiredAccess, false, processId);
            if (hProcess != IntPtr.Zero)
            {
                // Verify the handle allows memory operations
                IntPtr probe = NativeMethods.VirtualAllocEx(
                    hProcess, IntPtr.Zero, (UIntPtr)0x1000,
                    NativeMethods.MEM_COMMIT | NativeMethods.MEM_RESERVE,
                    NativeMethods.PAGE_READWRITE);

                if (probe != IntPtr.Zero)
                {
                    NativeMethods.VirtualFreeEx(hProcess, probe, UIntPtr.Zero, NativeMethods.MEM_RELEASE);
                    return hProcess;
                }

                NativeMethods.CloseHandle(hProcess);
            }

            // Fallback: search system handle table
            hProcess = FindAndHijackHandle((uint)processId);
            if (hProcess != IntPtr.Zero)
            {
                wasHijacked = true;
                return hProcess;
            }

            return IntPtr.Zero;
        }

        /// <summary>
        /// Searches the handle table for a handle pointing to targetPid with VM access rights.
        /// </summary>
        public static IntPtr FindAndHijackHandle(uint targetPid)
        {
            PrivilegeManager.EnableAllSecurityPrivileges();

            uint   bufferSize = 0x10000;
            IntPtr buffer     = Marshal.AllocHGlobal((int)bufferSize);

            try
            {
                int status;
                while ((status = NtQuerySystemInformation(
                            SystemExtendedHandleInformation,
                            buffer, bufferSize, out _)) == unchecked((int)STATUS_INFO_LENGTH_MISMATCH))
                {
                    Marshal.FreeHGlobal(buffer);
                    bufferSize *= 2;
                    buffer = Marshal.AllocHGlobal((int)bufferSize);
                }

                if (status != 0)
                    return IntPtr.Zero;

                ulong  numberOfHandles = (ulong)(UIntPtr)(ulong)Marshal.ReadIntPtr(buffer);
                uint   currentPid      = (uint)Process.GetCurrentProcess().Id;
                IntPtr hCurrentProcess = GetCurrentProcess();
                int    entrySize       = Marshal.SizeOf<SYSTEM_HANDLE_TABLE_ENTRY_INFO_EX>();
                IntPtr pEntries        = IntPtr.Add(buffer, IntPtr.Size * 2);

                for (ulong i = 0; i < numberOfHandles; i++)
                {
                    var entry = Marshal.PtrToStructure<SYSTEM_HANDLE_TABLE_ENTRY_INFO_EX>(
                        IntPtr.Add(pEntries, (int)(i * (ulong)entrySize)));

                    uint ownerPid = (uint)entry.UniqueProcessId.ToUInt64();
                    if (ownerPid == currentPid) continue;

                    // Must have VM_OPERATION permission
                    if ((entry.GrantedAccess & NativeMethods.PROCESS_VM_OPERATION) == 0) continue;

                    IntPtr hOwner = OpenProcess(0x1000 /* PROCESS_QUERY_LIMITED_INFORMATION */ | PROCESS_DUP_HANDLE, false, ownerPid);
                    if (hOwner == IntPtr.Zero)
                        hOwner = OpenProcess(PROCESS_DUP_HANDLE, false, ownerPid);

                    if (hOwner == IntPtr.Zero) continue;

                    try
                    {
                        if (!DuplicateHandle(
                                hOwner,
                                (IntPtr)entry.HandleValue.ToUInt64(),
                                hCurrentProcess,
                                out IntPtr hDup,
                                0, false,
                                DUPLICATE_SAME_ACCESS))
                            continue;

                        if (GetProcessId(hDup) == targetPid)
                        {
                            // Verify memory allocation works
                            IntPtr pTest = NativeMethods.VirtualAllocEx(
                                hDup, IntPtr.Zero, (UIntPtr)0x1000,
                                NativeMethods.MEM_COMMIT | NativeMethods.MEM_RESERVE,
                                NativeMethods.PAGE_READWRITE);

                            if (pTest != IntPtr.Zero)
                            {
                                NativeMethods.VirtualFreeEx(hDup, pTest, UIntPtr.Zero, NativeMethods.MEM_RELEASE);
                                return hDup;
                            }
                        }

                        CloseHandle(hDup);
                    }
                    finally
                    {
                        CloseHandle(hOwner);
                    }
                }
            }
            catch
            {
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }

            return IntPtr.Zero;
        }

        public static IntPtr HijackHandle(uint targetPid, uint requiredAccess)
            => FindAndHijackHandle(targetPid);
    }
}
