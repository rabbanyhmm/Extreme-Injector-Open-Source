using System;
using System.Diagnostics;
using System.Security.Principal;
using System.Runtime.InteropServices;

namespace ExtremeInjector.Core
{
    public static class PrivilegeManager
    {
        // NTAPI Privilege IDs
        private const int SE_CREATE_TOKEN_PRIVILEGE = 2;
        private const int SE_ASSIGNPRIMARYTOKEN_PRIVILEGE = 3;
        private const int SE_LOCK_MEMORY_PRIVILEGE = 4;
        private const int SE_INCREASE_QUOTA_PRIVILEGE = 5;
        private const int SE_TCB_PRIVILEGE = 7;
        private const int SE_SECURITY_PRIVILEGE = 8;
        private const int SE_TAKE_OWNERSHIP_PRIVILEGE = 9;
        private const int SE_LOAD_DRIVER_PRIVILEGE = 10;
        private const int SE_SYSTEM_PROFILE_PRIVILEGE = 11;
        private const int SE_SYSTEMTIME_PRIVILEGE = 12;
        private const int SE_PROF_SINGLE_PROCESS_PRIVILEGE = 13;
        private const int SE_INC_BASE_PRIORITY_PRIVILEGE = 14;
        private const int SE_CREATE_PAGEFILE_PRIVILEGE = 15;
        private const int SE_BACKUP_PRIVILEGE = 17;
        private const int SE_RESTORE_PRIVILEGE = 18;
        private const int SE_SHUTDOWN_PRIVILEGE = 19;
        private const int SE_DEBUG_PRIVILEGE = 20;
        private const int SE_AUDIT_PRIVILEGE = 21;
        private const int SE_SYSTEM_ENVIRONMENT_PRIVILEGE = 22;
        private const int SE_CHANGE_NOTIFY_PRIVILEGE = 23;
        private const int SE_UNDOCK_PRIVILEGE = 25;
        private const int SE_MANAGE_VOLUME_PRIVILEGE = 28;
        private const int SE_IMPERSONATE_PRIVILEGE = 29;
        private const int SE_CREATE_GLOBAL_PRIVILEGE = 30;

        [DllImport("ntdll.dll", SetLastError = true)]
        private static extern int RtlAdjustPrivilege(
            int privilege,
            bool bEnablePrivilege,
            bool bIsThreadPrivilege,
            out bool previousValue
        );

        public static bool IsAdmin { get; private set; }
        public static bool IsSystem { get; private set; }
        public static bool IsSeDebugEnabled { get; private set; }

        public static void InitializePrivileges()
        {
            IsAdmin = CheckIsAdmin();
            IsSystem = CheckIsSystem();

            // Enable SeDebugPrivilege and auxiliary security privileges dynamically
            EnableAllSecurityPrivileges();
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

        private static bool CheckIsSystem()
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                return identity.IsSystem;
            }
            catch
            {
                return false;
            }
        }

        public static void EnableAllSecurityPrivileges()
        {
            // Enable SeDebugPrivilege (Privilege 20)
            IsSeDebugEnabled = EnablePrivilege(SE_DEBUG_PRIVILEGE);

            // Enable additional high-level system privileges if available
            EnablePrivilege(SE_IMPERSONATE_PRIVILEGE);
            EnablePrivilege(SE_ASSIGNPRIMARYTOKEN_PRIVILEGE);
            EnablePrivilege(SE_INCREASE_QUOTA_PRIVILEGE);
            EnablePrivilege(SE_TCB_PRIVILEGE);
            EnablePrivilege(SE_TAKE_OWNERSHIP_PRIVILEGE);
            EnablePrivilege(SE_LOAD_DRIVER_PRIVILEGE);
            EnablePrivilege(SE_RESTORE_PRIVILEGE);
            EnablePrivilege(SE_BACKUP_PRIVILEGE);
        }

        public static bool EnablePrivilege(int privilegeId)
        {
            try
            {
                int status = RtlAdjustPrivilege(privilegeId, true, false, out _);
                return status == 0; // STATUS_SUCCESS
            }
            catch
            {
                return false;
            }
        }

        public static bool EnableSeDebugPrivilege()
        {
            return EnablePrivilege(SE_DEBUG_PRIVILEGE);
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
