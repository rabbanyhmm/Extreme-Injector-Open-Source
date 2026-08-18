using System;
using System.Diagnostics;
using System.Security.Principal;
using System.Runtime.InteropServices;

namespace ExtremeInjector.Core
{
    public static class PrivilegeManager
    {
        [DllImport("ntdll.dll", SetLastError = true)]
        private static extern int RtlAdjustPrivilege(
            int privilege,
            bool bEnablePrivilege,
            bool bIsThreadPrivilege,
            out bool previousValue
        );

        public static bool IsAdmin { get; private set; }
        public static bool IsSeDebugEnabled { get; private set; }

        public static void InitializePrivileges()
        {
            IsAdmin = CheckIsAdmin();
            IsSeDebugEnabled = EnableSeDebugPrivilege();
        }

        private static bool CheckIsAdmin()
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        public static bool EnableSeDebugPrivilege()
        {
            try
            {
                // Privilege 20 = SeDebugPrivilege
                int status = RtlAdjustPrivilege(20, true, false, out _);
                return status == 0; // STATUS_SUCCESS
            }
            catch
            {
                return false;
            }
        }

        public static bool RelaunchAsAdministrator()
        {
            try
            {
                var currentProcess = Process.GetCurrentProcess();
                var mainModule = currentProcess.MainModule;
                if (mainModule == null || string.IsNullOrEmpty(mainModule.FileName))
                    return false;

                var startInfo = new ProcessStartInfo
                {
                    FileName = mainModule.FileName,
                    UseShellExecute = true,
                    Verb = "runas" // Triggers UAC Prompt
                };

                Process.Start(startInfo);
                return true;
            }
            catch
            {
                // User clicked "No" on UAC prompt or canceled
                return false;
            }
        }
        public static bool CanOpenProcessAccess(int processId, uint requiredAccess)
        {
            if (processId <= 4) return false;

            IntPtr hProc = NativeMethods.OpenProcess(requiredAccess, false, processId);
            if (hProc != IntPtr.Zero)
            {
                NativeMethods.CloseHandle(hProc);
                return true;
            }
            return false;
        }

        public static bool CanInjectProcess(int processId)
        {
            const uint INJECT_ACCESS = NativeMethods.PROCESS_CREATE_THREAD |
                                       NativeMethods.PROCESS_VM_OPERATION |
                                       NativeMethods.PROCESS_VM_WRITE |
                                       NativeMethods.PROCESS_VM_READ;

            return CanOpenProcessAccess(processId, INJECT_ACCESS);
        }

        public static bool CanQueryProcess(int processId)
        {
            return CanOpenProcessAccess(processId, NativeMethods.PROCESS_QUERY_INFORMATION) ||
                   CanOpenProcessAccess(processId, NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION);
        }
    }
}
