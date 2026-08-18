using System;
using System.ComponentModel;
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
        /// Finds the base address of a loaded module in a remote process using both EnumProcessModulesEx and Toolhelp32 snapshot.
        /// </summary>
        public static IntPtr FindRemoteModuleBase(int processId, string dllPath)
        {
            if (processId <= 0 || string.IsNullOrWhiteSpace(dllPath))
                return IntPtr.Zero;

            string targetFileName = Path.GetFileName(dllPath);
            string targetFullPath = Path.GetFullPath(dllPath);

            // =========================================================================
            // Method 1: EnumProcessModulesEx (Direct PSAPI Module Walk)
            // =========================================================================
            try
            {
                IntPtr hProcess = NativeMethods.OpenProcess(
                    NativeMethods.PROCESS_QUERY_INFORMATION | NativeMethods.PROCESS_VM_READ,
                    false,
                    processId
                );

                if (hProcess == IntPtr.Zero)
                {
                    // Fallback to limited information query
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
                            int count = (int)(needed / (uint)IntPtr.Size);
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
            }
            catch
            {
                // Fall through to Toolhelp32 snapshot
            }

            // =========================================================================
            // Method 2: Toolhelp32 Snapshot (Fallback for WoW64 / Cross-Architecture)
            // =========================================================================
            try
            {
                uint flags = NativeMethods.TH32CS_SNAPMODULE | NativeMethods.TH32CS_SNAPMODULE32;
                IntPtr hSnapshot = NativeMethods.CreateToolhelp32Snapshot(flags, (uint)processId);

                if (hSnapshot != IntPtr.Zero && hSnapshot != new IntPtr(-1))
                {
                    try
                    {
                        var me = new NativeMethods.MODULEENTRY32 { dwSize = (uint)Marshal.SizeOf(typeof(NativeMethods.MODULEENTRY32)) };
                        if (NativeMethods.Module32First(hSnapshot, ref me))
                        {
                            do
                            {
                                if (string.Equals(me.szModule, targetFileName, StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(me.szExePath, targetFullPath, StringComparison.OrdinalIgnoreCase))
                                {
                                    return me.modBaseAddr;
                                }
                            } while (NativeMethods.Module32Next(hSnapshot, ref me));
                        }
                    }
                    finally
                    {
                        NativeMethods.CloseHandle(hSnapshot);
                    }
                }
            }
            catch
            {
                // Return zero on snapshot failure
            }

            return IntPtr.Zero;
        }

        /// <summary>
        /// Erases the PE header (0x1000 bytes) of an injected module in a target process by DLL path.
        /// </summary>
        public static bool ErasePEHeader(int processId, string dllPath, out string errorMessage)
        {
            errorMessage = "";
            try
            {
                IntPtr moduleBase = FindRemoteModuleBase(processId, dllPath);

                if (moduleBase == IntPtr.Zero)
                {
                    errorMessage = $"Could not locate base address for '{Path.GetFileName(dllPath)}' in target process (PID: {processId}).";
                    return false;
                }

                return ErasePEHeader(processId, moduleBase, out errorMessage);
            }
            catch (Exception ex)
            {
                errorMessage = $"Unexpected error resolving module base: {ex.Message}";
                return false;
            }
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

            IntPtr hProcess = IntPtr.Zero;
            try
            {
                hProcess = NativeMethods.OpenProcess(
                    NativeMethods.PROCESS_VM_OPERATION | NativeMethods.PROCESS_VM_WRITE | NativeMethods.PROCESS_VM_READ,
                    false,
                    processId
                );

                if (hProcess == IntPtr.Zero)
                {
                    int err = Marshal.GetLastWin32Error();
                    errorMessage = $"Failed to open process for Erase PE (PID: {processId}).\nWin32 Error {err}: {new Win32Exception(err).Message}";
                    return false;
                }

                return ErasePEHeader(hProcess, moduleBase, out errorMessage);
            }
            catch (Exception ex)
            {
                errorMessage = $"Unexpected error opening process for Erase PE: {ex.Message}";
                return false;
            }
            finally
            {
                if (hProcess != IntPtr.Zero)
                {
                    NativeMethods.CloseHandle(hProcess);
                }
            }
        }

        /// <summary>
        /// Erases the PE header (first 4096 bytes) using an existing process handle.
        /// </summary>
        public static bool ErasePEHeader(IntPtr hProcess, IntPtr moduleBase, out string errorMessage)
        {
            errorMessage = "";
            const uint HEADER_PAGE_SIZE = 0x1000; // 4096 bytes (standard PE header page size)

            if (hProcess == IntPtr.Zero)
            {
                errorMessage = "Target process handle is invalid (NULL).";
                return false;
            }

            if (moduleBase == IntPtr.Zero)
            {
                errorMessage = "Module base address is NULL.";
                return false;
            }

            try
            {
                // 1. Change memory protection of the header page to PAGE_READWRITE
                if (!NativeMethods.VirtualProtectEx(
                    hProcess,
                    moduleBase,
                    (UIntPtr)HEADER_PAGE_SIZE,
                    NativeMethods.PAGE_READWRITE,
                    out uint oldProtect))
                {
                    int err = Marshal.GetLastWin32Error();
                    errorMessage = $"VirtualProtectEx (PAGE_READWRITE) failed at 0x{moduleBase.ToInt64():X}.\nWin32 Error {err}: {new Win32Exception(err).Message}";
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
                    errorMessage = $"WriteProcessMemory failed to zero PE header at 0x{moduleBase.ToInt64():X}.\nWin32 Error {err}: {new Win32Exception(err).Message}";

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
            catch (Exception ex)
            {
                errorMessage = $"Unexpected exception during Erase PE: {ex.Message}";
                return false;
            }
        }
    }
}
