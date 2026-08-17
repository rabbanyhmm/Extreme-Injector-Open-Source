using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using ExtremeInjector.Config;

namespace ExtremeInjector.UI
{
    public class AdvancedScrambleSettingsForm : Form
    {
        // Header Options
        private CheckBox chkScrambleHeader = null!;
        private CheckBox chkRemoveUselessData = null!;

        // Section Options
        private CheckBox chkInsertExtraSections = null!;
        private CheckBox chkShiftSectionData = null!;
        private CheckBox chkModifyAssembly = null!;
        private CheckBox chkRenameSections = null!;
        private CheckBox chkShiftSectionMemory = null!;
        private CheckBox chkStripSection = null!;
        private CheckBox chkCreateNewEP = null!;

        // Directory Options
        private CheckBox chkModifyImportTable = null!;
        private CheckBox chkRemoveDebugData = null!;
        private CheckBox chkMoveRelocTable = null!;
        private CheckBox chkCreateFakeDebug = null!;

        public AdvancedScrambleSettingsForm()
        {
            InitializeComponent();
            LoadFromConfig();
        }

        private void InitializeComponent()
        {
            Text = "Advanced Scramble Settings";
            ClientSize = new Size(232, 435);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowIcon = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 9f);
            BackColor = Color.FromArgb(240, 240, 240);

            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ExtremeInjector.ico");
            if (File.Exists(iconPath))
            {
                try { Icon = new Icon(iconPath); } catch { }
            }

            // 1. Header Options
            var grpHeader = new GroupBox
            {
                Text = "Header Options",
                Location = new Point(12, 8),
                Size = new Size(208, 76),
                Font = new Font("Segoe UI", 9f)
            };

            chkScrambleHeader = new CheckBox
            {
                Text = "Scramble header fields",
                Location = new Point(10, 20),
                AutoSize = true
            };

            chkRemoveUselessData = new CheckBox
            {
                Text = "Remove useless data",
                Location = new Point(10, 45),
                AutoSize = true
            };

            grpHeader.Controls.AddRange(new Control[] {
                chkScrambleHeader,
                chkRemoveUselessData
            });

            // 2. Section Options
            var grpSection = new GroupBox
            {
                Text = "Section Options",
                Location = new Point(12, 90),
                Size = new Size(208, 202),
                Font = new Font("Segoe UI", 9f)
            };

            chkInsertExtraSections = new CheckBox { Text = "Insert extra sections", Location = new Point(10, 20), AutoSize = true };
            chkShiftSectionData = new CheckBox { Text = "Shift section data", Location = new Point(10, 44), AutoSize = true };
            chkModifyAssembly = new CheckBox { Text = "Modify assembly code", Location = new Point(10, 68), AutoSize = true };
            chkRenameSections = new CheckBox { Text = "Rename sections", Location = new Point(10, 92), AutoSize = true };
            chkShiftSectionMemory = new CheckBox { Text = "Shift section memory", Location = new Point(10, 116), AutoSize = true };
            chkStripSection = new CheckBox { Text = "Strip section characteristics", Location = new Point(10, 140), AutoSize = true };
            chkCreateNewEP = new CheckBox { Text = "Create new entrypoint", Location = new Point(10, 164), AutoSize = true, Enabled = false };

            grpSection.Controls.AddRange(new Control[] {
                chkInsertExtraSections,
                chkShiftSectionData,
                chkModifyAssembly,
                chkRenameSections,
                chkShiftSectionMemory,
                chkStripSection,
                chkCreateNewEP
            });

            // 3. Directory Options
            var grpDirectory = new GroupBox
            {
                Text = "Directory Options",
                Location = new Point(12, 298),
                Size = new Size(208, 126),
                Font = new Font("Segoe UI", 9f)
            };

            chkModifyImportTable = new CheckBox { Text = "Modify import table", Location = new Point(10, 20), AutoSize = true };
            chkRemoveDebugData = new CheckBox { Text = "Remove debug data", Location = new Point(10, 44), AutoSize = true };
            chkMoveRelocTable = new CheckBox { Text = "Move relocation table", Location = new Point(10, 68), AutoSize = true, Enabled = false };
            chkCreateFakeDebug = new CheckBox { Text = "Create fake debug directory", Location = new Point(10, 92), AutoSize = true, Enabled = false };

            grpDirectory.Controls.AddRange(new Control[] {
                chkModifyImportTable,
                chkRemoveDebugData,
                chkMoveRelocTable,
                chkCreateFakeDebug
            });

            Controls.AddRange(new Control[] {
                grpHeader,
                grpSection,
                grpDirectory
            });

            FormClosing += (s, e) => SaveToConfig();
        }

        private void LoadFromConfig()
        {
            var sc = SettingsManager.Current.Options.Scramble;
            chkScrambleHeader.Checked = sc.ScrambleHeaderFields;
            chkRemoveUselessData.Checked = sc.RemoveUselessData;

            chkInsertExtraSections.Checked = sc.InsertExtraSections;
            chkShiftSectionData.Checked = sc.ShiftSectionData;
            chkModifyAssembly.Checked = sc.ModifyAssemblyCode;
            chkRenameSections.Checked = sc.RenameSections;
            chkShiftSectionMemory.Checked = sc.ShiftSectionMemory;
            chkStripSection.Checked = sc.StripSectionCharacteristics;
            chkCreateNewEP.Checked = sc.CreateNewEntryPoint;

            chkModifyImportTable.Checked = sc.ModifyImportTable;
            chkRemoveDebugData.Checked = sc.RemoveDebugData;
            chkMoveRelocTable.Checked = sc.MoveRelocationTable;
            chkCreateFakeDebug.Checked = sc.CreateFakeDebugDirectory;
        }

        private void SaveToConfig()
        {
            var sc = SettingsManager.Current.Options.Scramble;
            sc.ScrambleHeaderFields = chkScrambleHeader.Checked;
            sc.RemoveUselessData = chkRemoveUselessData.Checked;

            sc.InsertExtraSections = chkInsertExtraSections.Checked;
            sc.ShiftSectionData = chkShiftSectionData.Checked;
            sc.ModifyAssemblyCode = chkModifyAssembly.Checked;
            sc.RenameSections = chkRenameSections.Checked;
            sc.ShiftSectionMemory = chkShiftSectionMemory.Checked;
            sc.StripSectionCharacteristics = chkStripSection.Checked;
            sc.CreateNewEntryPoint = chkCreateNewEP.Checked;

            sc.ModifyImportTable = chkModifyImportTable.Checked;
            sc.RemoveDebugData = chkRemoveDebugData.Checked;
            sc.MoveRelocationTable = chkMoveRelocTable.Checked;
            sc.CreateFakeDebugDirectory = chkCreateFakeDebug.Checked;
            SettingsManager.Save();
        }
    }
}
