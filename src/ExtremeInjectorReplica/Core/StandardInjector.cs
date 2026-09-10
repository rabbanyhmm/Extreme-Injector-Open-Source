using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using ExtremeInjector.Config;

namespace ExtremeInjector.Core
{
    /// <summary>
    /// Standard DLL injection via LoadLibraryW remote thread.
    /// Direct C# port of the working C++ InjectStandard / FindAndHijackHandle implementation.
    /// </summary>
    public static class StandardInjector
    {
        // ── P/Invokes (matching exactly what the C++ injector uses) ──────────────

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr GetModuleHandleW(string lpModuleName);

        [DllImport("kernel32.dll", EntryPoint = "GetProcAddress", SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateRemoteThread(
            IntPtr hProcess,
            IntPtr lpThreadAttributes,
            UIntPtr dwStackSize,
            IntPtr lpStartAddress,
            IntPtr lpParameter,
            uint dwCreationFlags,
            IntPtr lpThreadId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetExitCodeThread(IntPtr hThread, out uint lpExitCode);

        [DllImport("ntdll.dll", SetLastError = true)]
        private static extern int RtlCreateUserThread(
            IntPtr processHandle,
            IntPtr securityDescriptor,
            bool createSuspended,
            uint stackZeroBits,
            UIntPtr stackReserved,
            UIntPtr stackCommit,
            IntPtr startAddress,
            IntPtr parameter,
            out IntPtr threadHandle,
            IntPtr clientBuffer);

        [DllImport("ntdll.dll", SetLastError = true)]
        private static extern int NtCreateThreadEx(
            out IntPtr hThread,
            uint desiredAccess,
            IntPtr objectAttributes,
            IntPtr processHandle,
            IntPtr startAddress,
            IntPtr parameter,
            bool createSuspended,
            uint stackZeroBits,
            uint sizeOfStackCommit,
            uint sizeOfStackReserve,
            IntPtr bytesBuffer);

        public static bool Inject(int processId, string dllPath, OptionsConfig? options, out string errorMessage)
        {
            errorMessage = "";
            IntPtr hProcess  = IntPtr.Zero;
            IntPtr remoteMem = IntPtr.Zero;
            IntPtr hThread   = IntPtr.Zero;

            if (!File.Exists(dllPath))
            {
                errorMessage = $"DLL file does not exist: {dllPath}";
                return false;
            }

            // Obtain process handle via direct OpenProcess or handle hijacking fallback
            hProcess = HandleHijacker.OpenProcessSmart(processId, NativeMethods.PROCESS_ALL_ACCESS, out _);
            if (hProcess == IntPtr.Zero)
            {
                int err = Marshal.GetLastWin32Error();
                errorMessage = $"Failed to open or hijack handle to target process (PID: {processId}).\nWin32 Error {err}: {new Win32Exception(err).Message}";
                return false;
            }

            try
            {
                // Allocate memory for the DLL path string
                byte[] pathBytes = Encoding.Unicode.GetBytes(dllPath + "\0");
                UIntPtr pathSize = (UIntPtr)pathBytes.Length;

                remoteMem = NativeMethods.VirtualAllocEx(
                    hProcess, IntPtr.Zero, pathSize,
                    NativeMethods.MEM_COMMIT | NativeMethods.MEM_RESERVE,
                    NativeMethods.PAGE_READWRITE);

                if (remoteMem == IntPtr.Zero)
                {
                    int err = Marshal.GetLastWin32Error();
                    errorMessage = $"Failed to allocate memory in target process.\nWin32 Error {err}: {new Win32Exception(err).Message}";
                    return false;
                }

                // Write DLL path to target memory
                if (!NativeMethods.WriteProcessMemory(hProcess, remoteMem, pathBytes, pathSize, out _))
                {
                    int err = Marshal.GetLastWin32Error();
                    errorMessage = $"Failed to write DLL path to target process memory.\nWin32 Error {err}: {new Win32Exception(err).Message}";
                    return false;
                }

                IntPtr pLoadLibraryW = GetProcAddress(GetModuleHandleW("kernel32.dll"), "LoadLibraryW");
                if (pLoadLibraryW == IntPtr.Zero)
                {
                    errorMessage = "Failed to resolve LoadLibraryW from kernel32.dll.";
                    return false;
                }

                // Spawn remote thread with fallbacks (CreateRemoteThread -> RtlCreateUserThread -> NtCreateThreadEx)
                hThread = CreateRemoteThread(hProcess, IntPtr.Zero, UIntPtr.Zero,
                    pLoadLibraryW, remoteMem, 0, IntPtr.Zero);

                if (hThread == IntPtr.Zero)
                {
                    int err = Marshal.GetLastWin32Error();

                    int rtlStatus = RtlCreateUserThread(
                        hProcess, IntPtr.Zero, false, 0,
                        UIntPtr.Zero, UIntPtr.Zero,
                        pLoadLibraryW, remoteMem,
                        out hThread, IntPtr.Zero);

                    if (rtlStatus != 0 || hThread == IntPtr.Zero)
                    {
                        int ntStatus = NtCreateThreadEx(
                            out hThread, 0x001FFFFF, IntPtr.Zero,
                            hProcess, pLoadLibraryW, remoteMem,
                            false, 0, 0, 0, IntPtr.Zero);

                        if (ntStatus != 0 || hThread == IntPtr.Zero)
                        {
                            errorMessage = $"Failed to create remote thread.\nCreateRemoteThread Error {err}\nRtlCreateUserThread NTSTATUS 0x{rtlStatus:X8}\nNtCreateThreadEx NTSTATUS 0x{ntStatus:X8}";
                            return false;
                        }
                    }
                }

                WaitForSingleObject(hThread, 0xFFFFFFFF);

                GetExitCodeThread(hThread, out uint exitCode);
                if (exitCode == 0)
                {
                    errorMessage = "LoadLibraryW failed inside target process (returned NULL). " +
                                   "Common causes: missing DLL dependencies, architecture mismatch, or DllMain returned FALSE.";
                    return false;
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
                if (hThread   != IntPtr.Zero) NativeMethods.CloseHandle(hThread);
                if (remoteMem != IntPtr.Zero && hProcess != IntPtr.Zero)
                    NativeMethods.VirtualFreeEx(hProcess, remoteMem, UIntPtr.Zero, NativeMethods.MEM_RELEASE);
                if (hProcess  != IntPtr.Zero) NativeMethods.CloseHandle(hProcess);
            }
        }
    }
}
