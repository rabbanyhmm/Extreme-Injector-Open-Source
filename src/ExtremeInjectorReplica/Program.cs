using System;
using System.Windows.Forms;
using ExtremeInjector.UI;

namespace ExtremeInjector
{
    internal static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Check if already running as Administrator
            bool isAdmin = ExtremeInjector.Core.PrivilegeManager.EnableSeDebugPrivilege();
            bool isNoElevateArg = Array.Exists(args, arg => arg.Equals("--no-elevate", StringComparison.OrdinalIgnoreCase));

            if (!isAdmin && !isNoElevateArg)
            {
                // Attempt to elevate via UAC prompt
                if (ExtremeInjector.Core.PrivilegeManager.RelaunchAsAdministrator())
                {
                    // Exit this non-elevated instance since elevated process was launched
                    return;
                }
                // If user clicked "No" on UAC prompt, execution continues seamlessly in User Mode!
            }

            // Initialize privileges (SeDebugPrivilege if granted)
            ExtremeInjector.Core.PrivilegeManager.InitializePrivileges();

            Application.Run(new MainForm());
        }
    }
}