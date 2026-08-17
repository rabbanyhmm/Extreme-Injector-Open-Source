using System;
using System.Windows.Forms;
using ExtremeInjector.UI;

namespace ExtremeInjector
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}