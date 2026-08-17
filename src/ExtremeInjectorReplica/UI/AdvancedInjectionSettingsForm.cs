using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using ExtremeInjector.Config;

namespace ExtremeInjector.UI
{
    public class AdvancedInjectionSettingsForm : Form
    {
        private CheckBox chkHideDebugger = null!;
        private CheckBox chkManualImports = null!;
        private CheckBox chkDisableException = null!;
        private CheckBox chkDisableSEH = null!;

        public AdvancedInjectionSettingsForm()
        {
            InitializeComponent();
            LoadFromConfig();
        }

        private void InitializeComponent()
        {
            Text = "Advanced Settings";
            ClientSize = new Size(232, 178);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowIcon = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 9f);
            BackColor = Color.FromArgb(240, 240, 240);

            // 1. General GroupBox
            var grpGeneral = new GroupBox
            {
                Text = "General",
                Location = new Point(12, 8),
                Size = new Size(208, 48),
                Font = new Font("Segoe UI", 9f)
            };

            chkHideDebugger = new CheckBox
            {
                Text = "Hide threads from debugger",
                Location = new Point(10, 20),
                AutoSize = true
            };
            grpGeneral.Controls.Add(chkHideDebugger);

            // 2. Manual Map Options GroupBox
            var grpManualMap = new GroupBox
            {
                Text = "Manual Map Options",
                Location = new Point(12, 62),
                Size = new Size(208, 102),
                Font = new Font("Segoe UI", 9f)
            };

            chkManualImports = new CheckBox
            {
                Text = "Manually map imports",
                Location = new Point(10, 22),
                AutoSize = true
            };

            chkDisableException = new CheckBox
            {
                Text = "Disable exception support",
                Location = new Point(10, 47),
                AutoSize = true
            };

            chkDisableSEH = new CheckBox
            {
                Text = "Disable SEH handler validation",
                Location = new Point(10, 72),
                AutoSize = true
            };

            grpManualMap.Controls.AddRange(new Control[] {
                chkManualImports,
                chkDisableException,
                chkDisableSEH
            });

            Controls.AddRange(new Control[] {
                grpGeneral,
                grpManualMap
            });

            FormClosing += (s, e) => SaveToConfig();
        }

        private void LoadFromConfig()
        {
            var adv = SettingsManager.Current.Options.Advanced;
            chkHideDebugger.Checked = adv.HideFromDebugger;
            chkManualImports.Checked = adv.ManualResolveImports;
            chkDisableException.Checked = adv.DisableExceptionSupport;
            chkDisableSEH.Checked = adv.DisableSEHValidation;
        }

        private void SaveToConfig()
        {
            var adv = SettingsManager.Current.Options.Advanced;
            adv.HideFromDebugger = chkHideDebugger.Checked;
            adv.ManualResolveImports = chkManualImports.Checked;
            adv.DisableExceptionSupport = chkDisableException.Checked;
            adv.DisableSEHValidation = chkDisableSEH.Checked;
            SettingsManager.Save();
        }
    }
}
