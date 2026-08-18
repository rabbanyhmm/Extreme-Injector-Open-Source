using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using ExtremeInjector.Config;

namespace ExtremeInjector.Core
{
    public static class StandardInjector
    {
        public static bool Inject(int processId, string dllPath, OptionsConfig? options, out string errorMessage)
        {
            errorMessage = "";
            IntPtr hProcess = IntPtr.Zero;
            IntPtr remoteMem = IntPtr.Zero;
            IntPtr hThread = IntPtr.Zero;
            bool shouldFreeMem = true;

            if (!File.Exists(dllPath))
            {
                errorMessage = $"DLL file does not exist: {dllPath}";
                return false;
            }

            // 1. Architecture Check (PE Machine Header vs Target Process Architecture)
            if (!ValidateArchitecture(processId, dllPath, out bool isTarget64, out string archError))
            {
                errorMessage = archError;
                return false;
            }

            // 2. Open Target Process with Lowest Level Access Flags
            const uint PROCESS_ACCESS = NativeMethods.PROCESS_CREATE_THREAD |
                                         NativeMethods.THREAD_QUERY_INFORMATION |
                                         NativeMethods.PROCESS_VM_OPERATION |
                                         NativeMethods.PROCESS_VM_WRITE |
                                         NativeMethods.PROCESS_VM_READ;

            hProcess = NativeMethods.OpenProcess(PROCESS_ACCESS, false, processId);
            if (hProcess == IntPtr.Zero)
            {
                int err = Marshal.GetLastWin32Error();
                errorMessage = $"Failed to open target process (PID: {processId}).\nWin32 Error {err}: {GetWin32ErrorMessage(err)}";
                return false;
            }

            try
            {
                // 3. Prepare Unicode DLL Path Buffer
                byte[] pathBytes = Encoding.Unicode.GetBytes(dllPath + "\0");
                UIntPtr size = (UIntPtr)pathBytes.Length;

                // 4. Allocate Memory in Remote Process
                remoteMem = NativeMethods.VirtualAllocEx(
                    hProcess,
                    IntPtr.Zero,
                    size,
                    NativeMethods.MEM_COMMIT | NativeMethods.MEM_RESERVE,
                    NativeMethods.PAGE_READWRITE
                );

                if (remoteMem == IntPtr.Zero)
                {
                    int err = Marshal.GetLastWin32Error();
                    errorMessage = $"Failed to allocate {size} bytes in target process.\nWin32 Error {err}: {GetWin32ErrorMessage(err)}";
                    return false;
                }

                // 5. Write DLL Path String to Allocated Memory
                if (!NativeMethods.WriteProcessMemory(hProcess, remoteMem, pathBytes, size, out _))
                {
                    int err = Marshal.GetLastWin32Error();
                    errorMessage = $"Failed to write DLL path into target process memory.\nWin32 Error {err}: {GetWin32ErrorMessage(err)}";
                    return false;
                }

                // 6. Resolve LoadLibraryW Address (Supports native x64 and WoW64 x86)
                IntPtr loadLibraryAddr = RemoteExportResolver.GetProcAddress(processId, isTarget64, "kernel32.dll", "LoadLibraryW");
                if (loadLibraryAddr == IntPtr.Zero)
                {
                    errorMessage = "Failed to resolve LoadLibraryW export address in target process kernel32.dll.";
                    return false;
                }

                // 7. Create Remote Thread (Supporting Stealth Inject & HideFromDebugger)
                bool stealthInject = options?.StealthInject ?? false;
                bool hideFromDebugger = options?.Advanced?.HideFromDebugger ?? false;

                if (!NativeMethods.CreateRemoteThreadSmart(
                    hProcess,
                    loadLibraryAddr,
                    remoteMem,
                    stealthInject,
                    hideFromDebugger,
                    out hThread,
                    out string threadError))
                {
                    errorMessage = threadError;
                    return false;
                }

                // 8. Wait for Thread Execution
                uint waitResult = NativeMethods.WaitForSingleObject(hThread, 10000); // 10 sec timeout
                if (waitResult == 0x00000102 /* WAIT_TIMEOUT */)
                {
                    shouldFreeMem = false; // Do NOT unmap memory under a still-running thread
                    errorMessage = "Remote thread execution timed out after 10 seconds. The injected DLL's DllMain may still be initializing.";
                    return false;
                }

                // 9. Inspect Remote Thread Exit Code (LoadLibraryW return value = HMODULE)
                if (NativeMethods.GetExitCodeThread(hThread, out uint exitCode))
                {
                    if (exitCode == 0)
                    {
                        errorMessage = "LoadLibraryW failed inside target process (returned NULL HMODULE). Common causes: missing DLL dependencies, architecture mismatch, or DllMain returned FALSE.";
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"Unexpected injection failure: {ex.Message}";
                return false;
            }
            finally
            {
                // 10. Clean Up Handles and Allocated Memory
                if (hThread != IntPtr.Zero)
                    NativeMethods.CloseHandle(hThread);

                if (shouldFreeMem && remoteMem != IntPtr.Zero && hProcess != IntPtr.Zero)
                    NativeMethods.VirtualFreeEx(hProcess, remoteMem, UIntPtr.Zero, NativeMethods.MEM_RELEASE);

                if (hProcess != IntPtr.Zero)
                    NativeMethods.CloseHandle(hProcess);
            }
        }

        private static bool ValidateArchitecture(int processId, string dllPath, out bool isTarget64, out string error)
        {
            isTarget64 = false;
            error = "";
            try
            {
                // Determine target process bitness
                IntPtr hProcess = NativeMethods.OpenProcess(0x1000 /* PROCESS_QUERY_LIMITED_INFORMATION */, false, processId);
                if (hProcess == IntPtr.Zero)
                {
                    hProcess = NativeMethods.OpenProcess(NativeMethods.PROCESS_QUERY_INFORMATION, false, processId);
                }
                if (hProcess != IntPtr.Zero)
                {
                    try
                    {
                        if (Environment.Is64BitOperatingSystem)
                        {
                            if (NativeMethods.IsWow64Process(hProcess, out bool isWow64))
                            {
                                isTarget64 = !isWow64;
                            }
                            else
                            {
                                isTarget64 = Environment.Is64BitProcess;
                            }
                        }
                        else
                        {
                            isTarget64 = false;
                        }
                    }
                    finally
                    {
                        NativeMethods.CloseHandle(hProcess);
                    }
                }

                // Determine DLL bitness from PE header
                ushort machine = GetDllMachineType(dllPath);
                bool isDll64 = (machine == 0x8664 /* IMAGE_FILE_MACHINE_AMD64 */);
                bool isDll32 = (machine == 0x014C /* IMAGE_FILE_MACHINE_I386 */);

                if (isTarget64 && isDll32)
                {
                    error = $"Architecture Mismatch: Target process is 64-bit, but DLL '{Path.GetFileName(dllPath)}' is 32-bit (x86).";
                    return false;
                }
                if (!isTarget64 && isDll64)
                {
                    error = $"Architecture Mismatch: Target process is 32-bit, but DLL '{Path.GetFileName(dllPath)}' is 64-bit (x64).";
                    return false;
                }
            }
            catch { }

            return true;
        }

        private static ushort GetDllMachineType(string filePath)
        {
            try
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var br = new BinaryReader(fs);

                if (br.ReadUInt16() != 0x5A4D) return 0; // 'MZ'
                fs.Seek(0x3C, SeekOrigin.Begin);
                uint e_lfanew = br.ReadUInt32();

                fs.Seek(e_lfanew, SeekOrigin.Begin);
                if (br.ReadUInt32() != 0x00004550) return 0; // 'PE\0\0'

                return br.ReadUInt16(); // Machine header
            }
            catch
            {
                return 0;
            }
        }

        private static string GetWin32ErrorMessage(int errorCode)
        {
            try
            {
                return new Win32Exception(errorCode).Message;
            }
            catch
            {
                return "Unknown Win32 Error";
            }
        }
    }
}
