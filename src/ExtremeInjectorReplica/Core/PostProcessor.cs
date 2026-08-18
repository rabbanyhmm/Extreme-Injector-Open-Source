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

        /// <summary>
        /// Unlinks the loaded DLL from the remote PEB_LDR_DATA chains (InLoadOrder, InMemoryOrder, InInitializationOrder).
        /// Hides the module from standard user-mode module enumeration tools.
        /// </summary>
        public static bool HideModule(int processId, string dllPath, out string errorMessage)
        {
            errorMessage = "";
            IntPtr moduleBase = FindRemoteModuleBase(processId, dllPath);
            if (moduleBase == IntPtr.Zero)
            {
                errorMessage = $"Could not locate remote module base for '{Path.GetFileName(dllPath)}' (PID: {processId}).";
                return false;
            }

            const uint PROCESS_ACCESS = NativeMethods.PROCESS_QUERY_INFORMATION |
                                         NativeMethods.PROCESS_VM_READ |
                                         NativeMethods.PROCESS_VM_WRITE |
                                         NativeMethods.PROCESS_VM_OPERATION |
                                         NativeMethods.THREAD_SUSPEND_RESUME;

            IntPtr hProcess = NativeMethods.OpenProcess(PROCESS_ACCESS, false, processId);
            if (hProcess == IntPtr.Zero)
            {
                int err = Marshal.GetLastWin32Error();
                errorMessage = $"Failed to open process (PID: {processId}) for Hide Module.\nWin32 Error {err}: {new Win32Exception(err).Message}";
                return false;
            }

            try
            {
                bool isTarget64 = Environment.Is64BitOperatingSystem;
                if (Environment.Is64BitOperatingSystem && NativeMethods.IsWow64Process(hProcess, out bool isWow64))
                {
                    isTarget64 = !isWow64;
                }

                return HideModule(hProcess, isTarget64, moduleBase, out errorMessage);
            }
            finally
            {
                NativeMethods.CloseHandle(hProcess);
            }
        }

        /// <summary>
        /// Unlinks a module from remote PEB loader lists using an open process handle.
        /// </summary>
        public static bool HideModule(IntPtr hProcess, bool isTarget64, IntPtr moduleBase, out string errorMessage)
        {
            errorMessage = "";
            if (hProcess == IntPtr.Zero || moduleBase == IntPtr.Zero)
            {
                errorMessage = "Invalid process handle or module base pointer.";
                return false;
            }

            bool suspended = false;
            try
            {
                // 1. Freeze remote process threads to prevent race conditions during list modifications
                suspended = (NativeMethods.NtSuspendProcess(hProcess) == 0);

                if (isTarget64)
                {
                    return UnlinkModule64(hProcess, moduleBase, out errorMessage);
                }
                else
                {
                    return UnlinkModule32(hProcess, moduleBase, out errorMessage);
                }
            }
            catch (Exception ex)
            {
                errorMessage = $"Unexpected exception in HideModule: {ex.Message}";
                return false;
            }
            finally
            {
                // 2. Unconditionally resume target process
                if (suspended)
                {
                    NativeMethods.NtResumeProcess(hProcess);
                }
            }
        }

        private static bool UnlinkModule64(IntPtr hProcess, IntPtr moduleBase, out string errorMessage)
        {
            errorMessage = "";
            int status = NativeMethods.NtQueryInformationProcess(
                hProcess,
                NativeMethods.ProcessBasicInformation,
                out NativeMethods.PROCESS_BASIC_INFORMATION pbi,
                (uint)Marshal.SizeOf(typeof(NativeMethods.PROCESS_BASIC_INFORMATION)),
                out _
            );

            if (status != 0 || pbi.PebBaseAddress == IntPtr.Zero)
            {
                errorMessage = $"Failed to query 64-bit PEB address (NTSTATUS 0x{status:X}).";
                return false;
            }

            // PEB64.Ldr at offset 0x18
            byte[] ldrPtrBytes = new byte[8];
            if (!NativeMethods.ReadProcessMemory(hProcess, (IntPtr)(pbi.PebBaseAddress.ToInt64() + 0x18), ldrPtrBytes, (UIntPtr)8, out _))
            {
                errorMessage = "Failed to read PEB64.Ldr pointer.";
                return false;
            }
            long pLdrData = BitConverter.ToInt64(ldrPtrBytes, 0);
            if (pLdrData == 0)
            {
                errorMessage = "PEB_LDR_DATA pointer is NULL.";
                return false;
            }

            // InLoadOrderModuleList head at pLdrData + 0x10
            IntPtr inLoadOrderHead = (IntPtr)(pLdrData + 0x10);
            byte[] linkBytes = new byte[8];
            if (!NativeMethods.ReadProcessMemory(hProcess, inLoadOrderHead, linkBytes, (UIntPtr)8, out _))
            {
                errorMessage = "Failed to read InLoadOrderModuleList head.";
                return false;
            }

            long currentFlink = BitConverter.ToInt64(linkBytes, 0);
            long listHeadAddr = inLoadOrderHead.ToInt64();
            int maxModules = 500;
            int counter = 0;

            while (currentFlink != 0 && currentFlink != listHeadAddr && counter++ < maxModules)
            {
                IntPtr entryAddr = (IntPtr)currentFlink;

                // LDR_DATA_TABLE_ENTRY_64:
                // 0x00: InLoadOrderLinks (Flink 0x00, Blink 0x08)
                // 0x10: InMemoryOrderLinks (Flink 0x10, Blink 0x18)
                // 0x20: InInitializationOrderLinks (Flink 0x20, Blink 0x28)
                // 0x30: DllBase (8 bytes)
                byte[] entryHeader = new byte[0x38];
                if (!NativeMethods.ReadProcessMemory(hProcess, entryAddr, entryHeader, (UIntPtr)0x38, out _))
                    break;

                long dllBase = BitConverter.ToInt64(entryHeader, 0x30);
                long nextFlink = BitConverter.ToInt64(entryHeader, 0x00);

                if (dllBase == moduleBase.ToInt64())
                {
                    // Unlink from InLoadOrderLinks (offset 0x00)
                    UnlinkEntry64(hProcess, entryAddr, 0x00);

                    // Unlink from InMemoryOrderLinks (offset 0x10)
                    UnlinkEntry64(hProcess, entryAddr, 0x10);

                    // Unlink from InInitializationOrderLinks (offset 0x20)
                    UnlinkEntry64(hProcess, entryAddr, 0x20);

                    // Zero-fill entry structure to prevent residual heuristics
                    byte[] zeroBytes = new byte[0x60];
                    NativeMethods.WriteProcessMemory(hProcess, entryAddr, zeroBytes, (UIntPtr)zeroBytes.Length, out _);

                    return true;
                }

                currentFlink = nextFlink;
            }

            errorMessage = $"Module 0x{moduleBase.ToInt64():X} not found in remote 64-bit loader lists.";
            return false;
        }

        private static bool UnlinkModule32(IntPtr hProcess, IntPtr moduleBase, out string errorMessage)
        {
            errorMessage = "";
            int status = NativeMethods.NtQueryInformationProcess(
                hProcess,
                NativeMethods.ProcessWow64Information,
                out IntPtr pebBase32,
                (uint)IntPtr.Size,
                out _
            );

            if (status != 0 || pebBase32 == IntPtr.Zero)
            {
                errorMessage = $"Failed to query 32-bit (WoW64) PEB address (NTSTATUS 0x{status:X}).";
                return false;
            }

            // PEB32.Ldr at offset 0x0C
            byte[] ldrPtrBytes = new byte[4];
            if (!NativeMethods.ReadProcessMemory(hProcess, (IntPtr)(pebBase32.ToInt64() + 0x0C), ldrPtrBytes, (UIntPtr)4, out _))
            {
                errorMessage = "Failed to read PEB32.Ldr pointer.";
                return false;
            }
            uint pLdrData32 = BitConverter.ToUInt32(ldrPtrBytes, 0);
            if (pLdrData32 == 0)
            {
                errorMessage = "32-bit PEB_LDR_DATA pointer is NULL.";
                return false;
            }

            // InLoadOrderModuleList head at pLdrData32 + 0x0C
            IntPtr inLoadOrderHead = (IntPtr)pLdrData32 + 0x0C;
            byte[] linkBytes = new byte[4];
            if (!NativeMethods.ReadProcessMemory(hProcess, inLoadOrderHead, linkBytes, (UIntPtr)4, out _))
            {
                errorMessage = "Failed to read 32-bit InLoadOrderModuleList head.";
                return false;
            }

            uint currentFlink = BitConverter.ToUInt32(linkBytes, 0);
            uint listHeadAddr = (uint)inLoadOrderHead.ToInt64();
            int maxModules = 500;
            int counter = 0;
            uint targetBase32 = (uint)moduleBase.ToInt32();

            while (currentFlink != 0 && currentFlink != listHeadAddr && counter++ < maxModules)
            {
                IntPtr entryAddr = (IntPtr)currentFlink;

                // LDR_DATA_TABLE_ENTRY_32:
                // 0x00: InLoadOrderLinks (Flink 0x00, Blink 0x04)
                // 0x08: InMemoryOrderLinks (Flink 0x08, Blink 0x0C)
                // 0x10: InInitializationOrderLinks (Flink 0x10, Blink 0x14)
                // 0x18: DllBase (4 bytes)
                byte[] entryHeader = new byte[0x1C];
                if (!NativeMethods.ReadProcessMemory(hProcess, entryAddr, entryHeader, (UIntPtr)0x1C, out _))
                    break;

                uint dllBase = BitConverter.ToUInt32(entryHeader, 0x18);
                uint nextFlink = BitConverter.ToUInt32(entryHeader, 0x00);

                if (dllBase == targetBase32)
                {
                    // Unlink from InLoadOrderLinks (offset 0x00)
                    UnlinkEntry32(hProcess, entryAddr, 0x00);

                    // Unlink from InMemoryOrderLinks (offset 0x08)
                    UnlinkEntry32(hProcess, entryAddr, 0x08);

                    // Unlink from InInitializationOrderLinks (offset 0x10)
                    UnlinkEntry32(hProcess, entryAddr, 0x10);

                    // Zero-fill entry structure
                    byte[] zeroBytes = new byte[0x30];
                    NativeMethods.WriteProcessMemory(hProcess, entryAddr, zeroBytes, (UIntPtr)zeroBytes.Length, out _);

                    return true;
                }

                currentFlink = nextFlink;
            }

            errorMessage = $"Module 0x{moduleBase.ToInt64():X} not found in remote 32-bit loader lists.";
            return false;
        }

        private static void UnlinkEntry64(IntPtr hProcess, IntPtr entryBase, int linkOffset)
        {
            IntPtr linkAddr = (IntPtr)(entryBase.ToInt64() + linkOffset);
            byte[] buf = new byte[16];
            if (!NativeMethods.ReadProcessMemory(hProcess, linkAddr, buf, (UIntPtr)16, out _))
                return;

            long flink = BitConverter.ToInt64(buf, 0);
            long blink = BitConverter.ToInt64(buf, 8);

            if (flink != 0 && blink != 0)
            {
                // blink->Flink = flink (offset 0x00 of Blink node)
                byte[] flinkBytes = BitConverter.GetBytes(flink);
                NativeMethods.WriteProcessMemory(hProcess, (IntPtr)blink, flinkBytes, (UIntPtr)8, out _);

                // flink->Blink = blink (offset 0x08 of Flink node)
                byte[] blinkBytes = BitConverter.GetBytes(blink);
                NativeMethods.WriteProcessMemory(hProcess, (IntPtr)(flink + 8), blinkBytes, (UIntPtr)8, out _);
            }
        }

        private static void UnlinkEntry32(IntPtr hProcess, IntPtr entryBase, int linkOffset)
        {
            IntPtr linkAddr = (IntPtr)(entryBase.ToInt64() + linkOffset);
            byte[] buf = new byte[8];
            if (!NativeMethods.ReadProcessMemory(hProcess, linkAddr, buf, (UIntPtr)8, out _))
                return;

            uint flink = BitConverter.ToUInt32(buf, 0);
            uint blink = BitConverter.ToUInt32(buf, 4);

            if (flink != 0 && blink != 0)
            {
                // blink->Flink = flink (offset 0x00 of Blink node)
                byte[] flinkBytes = BitConverter.GetBytes(flink);
                NativeMethods.WriteProcessMemory(hProcess, (IntPtr)blink, flinkBytes, (UIntPtr)4, out _);

                // flink->Blink = blink (offset 0x04 of Flink node)
                byte[] blinkBytes = BitConverter.GetBytes(blink);
                NativeMethods.WriteProcessMemory(hProcess, (IntPtr)(flink + 4), blinkBytes, (UIntPtr)4, out _);
            }
        }
    }
}
