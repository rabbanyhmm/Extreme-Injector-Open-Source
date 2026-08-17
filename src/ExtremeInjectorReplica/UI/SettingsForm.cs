using System;
using System.Drawing;
using System.Windows.Forms;
using ExtremeInjector.Config;

namespace ExtremeInjector.UI
{
    public class SettingsForm : Form
    {
        private Panel headerPanel = null!;
        private TabControl tabControl = null!;
        private ComboBox cmbMethod = null!;
        private CheckBox chkAutoInject = null!;
        private CheckBox chkCloseOnInject = null!;
        private CheckBox chkHideModule = null!;
        private CheckBox chkErasePE = null!;
        private CheckBox chkStealthInject = null!;
        private NumericUpDown numDelay = null!;
        private NumericUpDown numDelayBetween = null!;

        // Advanced
        private CheckBox chkDisableException = null!;
        private CheckBox chkDisableSEH = null!;
        private CheckBox chkHideDebugger = null!;
        private CheckBox chkManualImports = null!;

        // Scramble
        private CheckBox chkScrambleHeader = null!;
        private CheckBox chkStripSection = null!;
        private CheckBox chkShiftSectionData = null!;
        private CheckBox chkShiftSectionMemory = null!;
        private CheckBox chkInsertExtraSections = null!;
        private CheckBox chkRemoveDebugData = null!;
        private CheckBox chkRemoveUselessData = null!;
        private CheckBox chkCreateFakeDebug = null!;
        private CheckBox chkCreateNewEP = null!;
        private CheckBox chkModifyAssembly = null!;
        private CheckBox chkModifyImportTable = null!;
        private CheckBox chkMoveRelocTable = null!;
        private CheckBox chkRenameSections = null!;

        // Colors
        private Button btnColor1 = null!;
        private Button btnColor2 = null!;
        private Button btnColorText = null!;

        private FlatCustomButton btnOk = null!;
        private FlatCustomButton btnCancel = null!;

        public SettingsForm()
        {
            InitializeComponent();
            LoadFromConfig();
        }

        private void InitializeComponent()
        {
            Text = "Settings - Extreme Injector";
            Size = new Size(490, 520);
            MinimumSize = new Size(460, 480);
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 9f);
            BackColor = Color.FromArgb(242, 244, 247);
            DoubleBuffered = true;

            // Header Banner
            headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 52
            };
            headerPanel.Paint += (s, e) =>
            {
                ThemeManager.DrawHeaderBanner(e.Graphics, headerPanel.ClientRectangle, "Injector Settings", "Configure injection techniques, stealth, and scrambling");
            };

            // Tab Control
            tabControl = new TabControl
            {
                Location = new Point(12, 62),
                Size = new Size(450, 395),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            // Tab 1: General & Injection
            var tabGeneral = new TabPage("General & Injection") { BackColor = Color.White };
            BuildGeneralTab(tabGeneral);

            // Tab 2: Advanced & Cloaking
            var tabAdvanced = new TabPage("Advanced") { BackColor = Color.White };
            BuildAdvancedTab(tabAdvanced);

            // Tab 3: Scramble
            var tabScramble = new TabPage("Scramble") { BackColor = Color.White };
            BuildScrambleTab(tabScramble);

            // Tab 4: Appearance
            var tabAppearance = new TabPage("Appearance") { BackColor = Color.White };
            BuildAppearanceTab(tabAppearance);

            tabControl.TabPages.AddRange(new TabPage[] { tabGeneral, tabAdvanced, tabScramble, tabAppearance });

            // Bottom Buttons
            var btnPanel = new Panel
            {
                Location = new Point(12, 462),
                Size = new Size(450, 32),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            btnCancel = new FlatCustomButton
            {
                Text = "Cancel",
                Location = new Point(274, 2),
                Size = new Size(82, 26),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            btnOk = new FlatCustomButton
            {
                Text = "OK",
                IsPrimary = true,
                Location = new Point(362, 2),
                Size = new Size(86, 26),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            btnOk.Click += (s, e) => SaveAndClose();

            btnPanel.Controls.AddRange(new Control[] { btnCancel, btnOk });

            Controls.AddRange(new Control[] { headerPanel, tabControl, btnPanel });
        }

        private void BuildGeneralTab(TabPage page)
        {
            var grpMethod = new GroupBox
            {
                Text = "Injection Method",
                Location = new Point(12, 12),
                Size = new Size(420, 70),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            cmbMethod = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(16, 28),
                Size = new Size(385, 23)
            };
            cmbMethod.Items.AddRange(new object[] {
                "Standard (CreateRemoteThread)",
                "LdrLoadDll (NtCreateThreadEx)",
                "Thread Hijacking (SetThreadContext)",
                "Manual Map (In-Memory PE Loader)"
            });
            cmbMethod.SelectedIndex = 0;
            grpMethod.Controls.Add(cmbMethod);

            var grpOptions = new GroupBox
            {
                Text = "Options",
                Location = new Point(12, 90),
                Size = new Size(420, 160),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            chkAutoInject = new CheckBox { Text = "Auto-Inject (Waits for process launch)", Location = new Point(16, 24), AutoSize = true };
            chkCloseOnInject = new CheckBox { Text = "Close on inject", Location = new Point(16, 50), AutoSize = true };
            chkHideModule = new CheckBox { Text = "Hide module (Unlink from PEB)", Location = new Point(16, 76), AutoSize = true };
            chkErasePE = new CheckBox { Text = "Erase PE headers after mapping", Location = new Point(16, 102), AutoSize = true };
            chkStealthInject = new CheckBox { Text = "Stealth inject mode", Location = new Point(16, 128), AutoSize = true };

            grpOptions.Controls.AddRange(new Control[] { chkAutoInject, chkCloseOnInject, chkHideModule, chkErasePE, chkStealthInject });

            var grpDelays = new GroupBox
            {
                Text = "Timing & Delays",
                Location = new Point(12, 258),
                Size = new Size(420, 85),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            var lblDelay = new Label { Text = "Injection Delay (ms):", Location = new Point(16, 24), AutoSize = true };
            numDelay = new NumericUpDown { Location = new Point(160, 22), Size = new Size(90, 23), Maximum = 60000, Increment = 500 };

            var lblDelayBetween = new Label { Text = "Delay Between DLLs (ms):", Location = new Point(16, 52), AutoSize = true };
            numDelayBetween = new NumericUpDown { Location = new Point(160, 50), Size = new Size(90, 23), Maximum = 60000, Increment = 500 };

            grpDelays.Controls.AddRange(new Control[] { lblDelay, numDelay, lblDelayBetween, numDelayBetween });

            page.Controls.AddRange(new Control[] { grpMethod, grpOptions, grpDelays });
        }

        private void BuildAdvancedTab(TabPage page)
        {
            var grpAdv = new GroupBox
            {
                Text = "Advanced Manual Map & Hooking",
                Location = new Point(12, 12),
                Size = new Size(420, 160),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            chkDisableException = new CheckBox { Text = "Disable Exception Support", Location = new Point(16, 28), AutoSize = true };
            chkDisableSEH = new CheckBox { Text = "Disable SEH Validation", Location = new Point(16, 56), AutoSize = true };
            chkHideDebugger = new CheckBox { Text = "Hide From Debugger (ThreadHideFromDebugger)", Location = new Point(16, 84), AutoSize = true };
            chkManualImports = new CheckBox { Text = "Manual Resolve Imports", Location = new Point(16, 112), AutoSize = true };

            grpAdv.Controls.AddRange(new Control[] { chkDisableException, chkDisableSEH, chkHideDebugger, chkManualImports });
            page.Controls.Add(grpAdv);
        }

        private void BuildScrambleTab(TabPage page)
        {
            var panel = new Panel
            {
                AutoScroll = true,
                Dock = DockStyle.Fill,
                Padding = new Padding(10)
            };

            chkScrambleHeader = new CheckBox { Text = "Scramble Header Fields", Location = new Point(16, 14), AutoSize = true };
            chkStripSection = new CheckBox { Text = "Strip Section Characteristics", Location = new Point(16, 38), AutoSize = true };
            chkShiftSectionData = new CheckBox { Text = "Shift Section Data", Location = new Point(16, 62), AutoSize = true };
            chkShiftSectionMemory = new CheckBox { Text = "Shift Section Memory", Location = new Point(16, 86), AutoSize = true };
            chkInsertExtraSections = new CheckBox { Text = "Insert Extra Sections (Entropy)", Location = new Point(16, 110), AutoSize = true };
            chkRemoveDebugData = new CheckBox { Text = "Remove Debug Data", Location = new Point(16, 134), AutoSize = true };
            chkRemoveUselessData = new CheckBox { Text = "Remove Useless Data", Location = new Point(16, 158), AutoSize = true };
            chkCreateFakeDebug = new CheckBox { Text = "Create Fake Debug Directory", Location = new Point(16, 182), AutoSize = true };
            chkCreateNewEP = new CheckBox { Text = "Create New EntryPoint", Location = new Point(16, 206), AutoSize = true };
            chkModifyAssembly = new CheckBox { Text = "Modify Assembly Code", Location = new Point(16, 230), AutoSize = true };
            chkModifyImportTable = new CheckBox { Text = "Modify Import Table", Location = new Point(16, 254), AutoSize = true };
            chkMoveRelocTable = new CheckBox { Text = "Move Relocation Table", Location = new Point(16, 278), AutoSize = true };
            chkRenameSections = new CheckBox { Text = "Rename Sections", Location = new Point(16, 302), AutoSize = true };

            panel.Controls.AddRange(new Control[] {
                chkScrambleHeader, chkStripSection, chkShiftSectionData, chkShiftSectionMemory,
                chkInsertExtraSections, chkRemoveDebugData, chkRemoveUselessData, chkCreateFakeDebug,
                chkCreateNewEP, chkModifyAssembly, chkModifyImportTable, chkMoveRelocTable, chkRenameSections
            });

            page.Controls.Add(panel);
        }

        private void BuildAppearanceTab(TabPage page)
        {
            var grpColors = new GroupBox
            {
                Text = "Header Gradient & Text Colors",
                Location = new Point(12, 12),
                Size = new Size(420, 150),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            var lbl1 = new Label { Text = "Background Gradient 1:", Location = new Point(16, 28), AutoSize = true };
            btnColor1 = new Button { Location = new Point(170, 24), Size = new Size(80, 24), Text = "Pick Color" };
            btnColor1.Click += (s, e) => PickColor(btnColor1);

            var lbl2 = new Label { Text = "Background Gradient 2:", Location = new Point(16, 62), AutoSize = true };
            btnColor2 = new Button { Location = new Point(170, 58), Size = new Size(80, 24), Text = "Pick Color" };
            btnColor2.Click += (s, e) => PickColor(btnColor2);

            var lblText = new Label { Text = "Text Color:", Location = new Point(16, 96), AutoSize = true };
            btnColorText = new Button { Location = new Point(170, 92), Size = new Size(80, 24), Text = "Pick Color" };
            btnColorText.Click += (s, e) => PickColor(btnColorText);

            grpColors.Controls.AddRange(new Control[] { lbl1, btnColor1, lbl2, btnColor2, lblText, btnColorText });
            page.Controls.Add(grpColors);
        }

        private void PickColor(Button btn)
        {
            using var cd = new ColorDialog { Color = btn.BackColor };
            if (cd.ShowDialog() == DialogResult.OK)
            {
                btn.BackColor = cd.Color;
            }
        }

        private void LoadFromConfig()
        {
            var opt = SettingsManager.Current.Options;
            cmbMethod.SelectedIndex = Math.Clamp(opt.Method, 0, 3);
            chkAutoInject.Checked = opt.AutoInject;
            chkCloseOnInject.Checked = opt.CloseOnInject;
            chkHideModule.Checked = opt.HideModule;
            chkErasePE.Checked = opt.ErasePE;
            chkStealthInject.Checked = opt.StealthInject;
            numDelay.Value = opt.Delay;
            numDelayBetween.Value = opt.DelayBetween;

            chkDisableException.Checked = opt.Advanced.DisableExceptionSupport;
            chkDisableSEH.Checked = opt.Advanced.DisableSEHValidation;
            chkHideDebugger.Checked = opt.Advanced.HideFromDebugger;
            chkManualImports.Checked = opt.Advanced.ManualResolveImports;

            var sc = opt.Scramble;
            chkScrambleHeader.Checked = sc.ScrambleHeaderFields;
            chkStripSection.Checked = sc.StripSectionCharacteristics;
            chkShiftSectionData.Checked = sc.ShiftSectionData;
            chkShiftSectionMemory.Checked = sc.ShiftSectionMemory;
            chkInsertExtraSections.Checked = sc.InsertExtraSections;
            chkRemoveDebugData.Checked = sc.RemoveDebugData;
            chkRemoveUselessData.Checked = sc.RemoveUselessData;
            chkCreateFakeDebug.Checked = sc.CreateFakeDebugDirectory;
            chkCreateNewEP.Checked = sc.CreateNewEntryPoint;
            chkModifyAssembly.Checked = sc.ModifyAssemblyCode;
            chkModifyImportTable.Checked = sc.ModifyImportTable;
            chkMoveRelocTable.Checked = sc.MoveRelocationTable;
            chkRenameSections.Checked = sc.RenameSections;

            try { btnColor1.BackColor = ColorTranslator.FromHtml(opt.Background1); } catch { btnColor1.BackColor = Color.DodgerBlue; }
            try { btnColor2.BackColor = ColorTranslator.FromHtml(opt.Background2); } catch { btnColor2.BackColor = Color.DeepSkyBlue; }
            try { btnColorText.BackColor = ColorTranslator.FromHtml(opt.TextColor); } catch { btnColorText.BackColor = Color.White; }
        }

        private void SaveAndClose()
        {
            var opt = SettingsManager.Current.Options;
            opt.Method = cmbMethod.SelectedIndex;
            opt.AutoInject = chkAutoInject.Checked;
            opt.CloseOnInject = chkCloseOnInject.Checked;
            opt.HideModule = chkHideModule.Checked;
            opt.ErasePE = chkErasePE.Checked;
            opt.StealthInject = chkStealthInject.Checked;
            opt.Delay = (int)numDelay.Value;
            opt.DelayBetween = (int)numDelayBetween.Value;

            opt.Advanced.DisableExceptionSupport = chkDisableException.Checked;
            opt.Advanced.DisableSEHValidation = chkDisableSEH.Checked;
            opt.Advanced.HideFromDebugger = chkHideDebugger.Checked;
            opt.Advanced.ManualResolveImports = chkManualImports.Checked;

            var sc = opt.Scramble;
            sc.ScrambleHeaderFields = chkScrambleHeader.Checked;
            sc.StripSectionCharacteristics = chkStripSection.Checked;
            sc.ShiftSectionData = chkShiftSectionData.Checked;
            sc.ShiftSectionMemory = chkShiftSectionMemory.Checked;
            sc.InsertExtraSections = chkInsertExtraSections.Checked;
            sc.RemoveDebugData = chkRemoveDebugData.Checked;
            sc.RemoveUselessData = chkRemoveUselessData.Checked;
            sc.CreateFakeDebugDirectory = chkCreateFakeDebug.Checked;
            sc.CreateNewEntryPoint = chkCreateNewEP.Checked;
            sc.ModifyAssemblyCode = chkModifyAssembly.Checked;
            sc.ModifyImportTable = chkModifyImportTable.Checked;
            sc.MoveRelocationTable = chkMoveRelocTable.Checked;
            sc.RenameSections = chkRenameSections.Checked;

            opt.Background1 = ColorTranslator.ToHtml(btnColor1.BackColor);
            opt.Background2 = ColorTranslator.ToHtml(btnColor2.BackColor);
            opt.TextColor = ColorTranslator.ToHtml(btnColorText.BackColor);

            ThemeManager.UpdateColors(opt.Background1, opt.Background2, opt.TextColor);
            SettingsManager.Save();

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
