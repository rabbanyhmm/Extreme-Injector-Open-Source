using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ExtremeInjector.Core
{
    public static class PostProcessor
    {
        [DllImport("psapi.dll", SetLastError = true)]
        private static extern bool EnumProcessModulesEx(IntPtr hProcess, [Out] IntPtr[]? lphModule, uint cb, out uint lpcbNeeded, uint dwFilterFlag);

        [DllImport("psapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern uint GetModuleFileNameEx(IntPtr hProcess, IntPtr hModule, [Out] StringBuilder lpBaseName, uint nSize);

        [DllImport("psapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern uint GetModuleBaseName(IntPtr hProcess, IntPtr hModule, [Out] StringBuilder lpBaseName, uint nSize);

        /// <summary>
        /// Finds the base address of a loaded module in a remote process.
        /// </summary>
        public static IntPtr FindRemoteModuleBase(int processId, string dllPath)
        {
            string targetFileName = Path.GetFileName(dllPath);
            string targetFullPath = Path.GetFullPath(dllPath);

            IntPtr hProcess = NativeMethods.OpenProcess(
                NativeMethods.PROCESS_QUERY_INFORMATION | NativeMethods.PROCESS_VM_READ,
                false,
                processId
            );

            if (hProcess == IntPtr.Zero)
            {
                // Fallback with QUERY_LIMITED_INFORMATION
                hProcess = NativeMethods.OpenProcess(
                    NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION | NativeMethods.PROCESS_VM_READ,
                    false,
                    processId
                );
            }

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

                            for (int i = 0; i < count; i++)
                            {
                                sbName.Clear();
                                sbPath.Clear();

                                GetModuleBaseName(hProcess, hMods[i], sbName, (uint)sbName.Capacity);
                                GetModuleFileNameEx(hProcess, hMods[i], sbPath, (uint)sbPath.Capacity);

                                string modName = sbName.ToString();
                                string modPath = sbPath.ToString();

                                if (string.Equals(modName, targetFileName, StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(modPath, targetFullPath, StringComparison.OrdinalIgnoreCase))
                                {
                                    return hMods[i];
                                }
                            }
                        }
                    }
                }
                finally
                {
                    NativeMethods.CloseHandle(hProcess);
                }
            }

            return IntPtr.Zero;
        }

        /// <summary>
        /// Erases the PE header (0x1000 bytes) of an injected module in a target process.
        /// </summary>
        public static bool ErasePEHeader(int processId, string dllPath, out string errorMessage)
        {
            errorMessage = "";
            IntPtr moduleBase = FindRemoteModuleBase(processId, dllPath);

            if (moduleBase == IntPtr.Zero)
            {
                errorMessage = $"Could not locate base address for '{Path.GetFileName(dllPath)}' in target process.";
                return false;
            }

            return ErasePEHeader(processId, moduleBase, out errorMessage);
        }

        /// <summary>
        /// Erases the PE header at the specified base address in the target process.
        /// </summary>
        public static bool ErasePEHeader(int processId, IntPtr moduleBase, out string errorMessage)
        {
            errorMessage = "";

            if (moduleBase == IntPtr.Zero)
            {
                errorMessage = "Module base address is NULL.";
                return false;
            }

            IntPtr hProcess = NativeMethods.OpenProcess(
                NativeMethods.PROCESS_VM_OPERATION | NativeMethods.PROCESS_VM_WRITE | NativeMethods.PROCESS_VM_READ,
                false,
                processId
            );

            if (hProcess == IntPtr.Zero)
            {
                int err = Marshal.GetLastWin32Error();
                errorMessage = $"Failed to open process for Erase PE.\nWin32 Error {err}: {new Win32Exception(err).Message}";
                return false;
            }

            try
            {
                return ErasePEHeader(hProcess, moduleBase, out errorMessage);
            }
            finally
            {
                NativeMethods.CloseHandle(hProcess);
            }
        }

        /// <summary>
        /// Erases the PE header (first 4096 bytes) using an existing process handle.
        /// </summary>
        public static bool ErasePEHeader(IntPtr hProcess, IntPtr moduleBase, out string errorMessage)
        {
            errorMessage = "";
            const uint HEADER_PAGE_SIZE = 0x1000; // 4096 bytes (standard PE header page)

            // 1. Change memory protection of the header page to PAGE_READWRITE
            if (!NativeMethods.VirtualProtectEx(
                hProcess,
                moduleBase,
                (UIntPtr)HEADER_PAGE_SIZE,
                NativeMethods.PAGE_READWRITE,
                out uint oldProtect))
            {
                int err = Marshal.GetLastWin32Error();
                errorMessage = $"VirtualProtectEx (PAGE_READWRITE) failed.\nWin32 Error {err}: {new Win32Exception(err).Message}";
                return false;
            }

            // 2. Zero-fill the entire 0x1000 header page
            byte[] zeroBuffer = new byte[HEADER_PAGE_SIZE];
            if (!NativeMethods.WriteProcessMemory(
                hProcess,
                moduleBase,
                zeroBuffer,
                (UIntPtr)HEADER_PAGE_SIZE,
                out _))
            {
                int err = Marshal.GetLastWin32Error();
                errorMessage = $"WriteProcessMemory failed to zero PE header.\nWin32 Error {err}: {new Win32Exception(err).Message}";

                // Attempt to restore original protection on failure
                NativeMethods.VirtualProtectEx(hProcess, moduleBase, (UIntPtr)HEADER_PAGE_SIZE, oldProtect, out _);
                return false;
            }

            // 3. Restore original memory protection
            NativeMethods.VirtualProtectEx(
                hProcess,
                moduleBase,
                (UIntPtr)HEADER_PAGE_SIZE,
                oldProtect,
                out _
            );

            return true;
        }
    }
}
