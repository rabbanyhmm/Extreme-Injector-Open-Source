using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ExtremeInjector.Core
{
    public static class StandardInjector
    {
        public static bool Inject(int processId, string dllPath, out string errorMessage)
        {
            errorMessage = "";
            if (!File.Exists(dllPath))
            {
                errorMessage = $"DLL file does not exist: {dllPath}";
                return false;
            }

            IntPtr hProcess = NativeMethods.OpenProcess(NativeMethods.PROCESS_ALL_ACCESS, false, processId);
            if (hProcess == IntPtr.Zero)
            {
                errorMessage = $"Failed to open target process (PID: {processId}). Error: {Marshal.GetLastWin32Error()}";
                return false;
            }

            try
            {
                byte[] pathBytes = Encoding.Unicode.GetBytes(dllPath + "\0");
                UIntPtr size = (UIntPtr)pathBytes.Length;

                IntPtr remoteMem = NativeMethods.VirtualAllocEx(hProcess, IntPtr.Zero, size, NativeMethods.MEM_COMMIT | NativeMethods.MEM_RESERVE, NativeMethods.PAGE_READWRITE);
                if (remoteMem == IntPtr.Zero)
                {
                    errorMessage = $"Failed to allocate memory in target process. Error: {Marshal.GetLastWin32Error()}";
                    return false;
                }

                if (!NativeMethods.WriteProcessMemory(hProcess, remoteMem, pathBytes, size, out _))
                {
                    errorMessage = $"Failed to write DLL path to target process. Error: {Marshal.GetLastWin32Error()}";
                    return false;
                }

                IntPtr hKernel32 = NativeMethods.GetModuleHandle("kernel32.dll");
                IntPtr loadLibraryAddr = NativeMethods.GetProcAddress(hKernel32, "LoadLibraryW");
                if (loadLibraryAddr == IntPtr.Zero)
                {
                    errorMessage = "Failed to resolve LoadLibraryW address.";
                    return false;
                }

                IntPtr hThread = NativeMethods.CreateRemoteThread(hProcess, IntPtr.Zero, UIntPtr.Zero, loadLibraryAddr, remoteMem, 0, out uint threadId);
                if (hThread == IntPtr.Zero)
                {
                    errorMessage = $"CreateRemoteThread failed. Error: {Marshal.GetLastWin32Error()}";
                    return false;
                }

                NativeMethods.WaitForSingleObject(hThread, 5000);
                NativeMethods.CloseHandle(hThread);

                return true;
            }
            finally
            {
                NativeMethods.CloseHandle(hProcess);
            }
        }
    }
}
