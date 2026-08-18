using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExtremeInjector.Config;

namespace ExtremeInjector.Core
{
    public static class InjectionOrchestrator
    {
        public static async Task<InjectionResult> ExecuteInjectionAsync(string processName, System.Collections.Generic.List<string> dllPaths, OptionsConfig options)
        {
            var result = new InjectionResult();

            var processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(processName));
            if (processes.Length == 0)
            {
                result.Success = false;
                result.ErrorMessage = $"Target process '{processName}' was not found running.";
                return result;
            }

            var targetProcess = processes.FirstOrDefault(p => PrivilegeManager.CanQueryProcess(p.Id));
            if (targetProcess == null)
            {
                result.Success = false;
                result.ErrorMessage = $"Access Denied: Target process '{processName}' requires elevated Administrator privileges to access.";
                return result;
            }

            int pid = targetProcess.Id;

            if (options.Delay > 0)
            {
                await Task.Delay(options.Delay);
            }

            int successCount = 0;
            foreach (var dll in dllPaths)
            {
                bool injected = false;
                string err = "";

                switch (options.Method)
                {
                    case 0: // Standard
                    case 1: // LdrLoadDll
                    case 2: // Thread Hijacking
                    case 3: // Manual Map
                    default:
                        injected = StandardInjector.Inject(pid, dll, options, out err);
                        break;
                }

                if (injected)
                {
                    successCount++;
                }
                else
                {
                    result.ErrorMessage += $"[{Path.GetFileName(dll)}]: {err}\n";
                }

                if (options.DelayBetween > 0)
                {
                    await Task.Delay(options.DelayBetween);
                }
            }

            result.Success = (successCount == dllPaths.Count);
            result.InjectedCount = successCount;
            result.TotalCount = dllPaths.Count;

            return result;
        }
    }

    public class InjectionResult
    {
        public bool Success { get; set; }
        public int InjectedCount { get; set; }
        public int TotalCount { get; set; }
        public string ErrorMessage { get; set; } = "";
    }
}
