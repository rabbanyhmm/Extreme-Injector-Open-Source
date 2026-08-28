using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using ExtremeInjector.Config;

namespace ExtremeInjector.UI
{
    public class SettingsForm : Form
    {
        // 1. Injection Method
        private GroupBox grpInjectionMethod = null!;
        private ComboBox cmbMethod = null!;
        private Button btnAdvMethod = null!;

        // 2. Scrambling Options
        private GroupBox grpScrambling = null!;
        private ComboBox cmbScramble = null!;
        private Button btnAdvScramble = null!;

        // 3. Injection Options
        private GroupBox grpInjectionOptions = null!;
        private CheckBox chkAutoInject = null!;
        private CheckBox chkCloseOnInject = null!;
        private CheckBox chkStealthInject = null!;
        private Label lblInjectDelay = null!;
        private NumericUpDown numInjectDelay = null!;
        private Label lblDelayBetween = null!;
        private NumericUpDown numDelayBetween = null!;

        // 4. Post-Inject Options
        private GroupBox grpPostInject = null!;
        private CheckBox chkErasePE = null!;
        private CheckBox chkHideModule = null!;

        // 5. Theme Options
        private GroupBox grpTheme = null!;
        private Label lblTextColor = null!;
        private Panel pnlTextColor = null!;
        private Label lblBgColor1 = null!;
        private Panel pnlBgColor1 = null!;
        private Label lblBgColor2 = null!;
        private Panel pnlBgColor2 = null!;

        // 6. Tools
        private GroupBox grpTools = null!;
        private Button btnViewProcessInfo = null!;
        private Button btnScrambleDll = null!;
        private Button btnStartSecureMode = null!;

        // 7. Bottom Buttons
        private Button btnReset = null!;
        private Button btnOk = null!;

        public SettingsForm()
        {
            InitializeComponent();
            LoadFromConfig();
        }

        private void InitializeComponent()
        {
            Text = "Settings";
            ClientSize = new Size(395, 369);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowIcon = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 9f);
            BackColor = Color.FromArgb(240, 240, 240);

            // =========================================================================
            // 1. INJECTION METHOD GROUPBOX (TOP LEFT)
            // =========================================================================
            grpInjectionMethod = new GroupBox
            {
                Text = "Injection Method:",
                Location = new Point(12, 10),
                Size = new Size(180, 88)
            };

            cmbMethod = new ComboBox
            {
                Location = new Point(10, 22),
                Size = new Size(160, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbMethod.Items.AddRange(new object[] {
                "Standard Injection",
                "Thread Hijacking",
                "LdrLoadDll Stub",
                "LdrpLoadDll Stub",
                "Manual Map"
            });
            cmbMethod.SelectedIndex = 0;

            btnAdvMethod = new Button
            {
                Text = "Advanced",
                Location = new Point(10, 52),
                Size = new Size(160, 25),
                FlatStyle = FlatStyle.System,
                UseVisualStyleBackColor = true
            };
            btnAdvMethod.Click += (s, e) =>
            {
                using var dlg = new AdvancedInjectionSettingsForm();
                dlg.ShowDialog(this);
            };

            grpInjectionMethod.Controls.AddRange(new Control[] { cmbMethod, btnAdvMethod });

            // =========================================================================
            // 2. SCRAMBLING OPTIONS GROUPBOX (TOP RIGHT)
            // =========================================================================
            grpScrambling = new GroupBox
            {
                Text = "Scrambling Options:",
                Location = new Point(202, 10),
                Size = new Size(180, 88)
            };

            cmbScramble = new ComboBox
            {
                Location = new Point(10, 22),
                Size = new Size(160, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbScramble.Items.AddRange(new object[] {
                "None",
                "Basic",
                "Standard",
                "Extreme",
                "Custom"
            });
            cmbScramble.SelectedIndex = 0;
            cmbScramble.SelectedIndexChanged += CmbScramble_SelectedIndexChanged;

            btnAdvScramble = new Button
            {
                Text = "Advanced",
                Location = new Point(10, 52),
                Size = new Size(160, 25),
                FlatStyle = FlatStyle.System,
                UseVisualStyleBackColor = true
            };
            btnAdvScramble.Click += (s, e) =>
            {
                using var dlg = new AdvancedScrambleSettingsForm();
                dlg.ShowDialog(this);
                UpdateScramblePresetFromConfig();
            };

            grpScrambling.Controls.AddRange(new Control[] { cmbScramble, btnAdvScramble });

            // =========================================================================
            // 3. INJECTION OPTIONS GROUPBOX (MIDDLE LEFT)
            // =========================================================================
            grpInjectionOptions = new GroupBox
            {
                Text = "Injection Options:",
                Location = new Point(12, 104),
                Size = new Size(180, 150)
            };

            chkAutoInject = new CheckBox { Text = "Auto Inject", Location = new Point(10, 20), AutoSize = true };
            chkCloseOnInject = new CheckBox { Text = "Close on inject", Location = new Point(10, 43), AutoSize = true };
            chkStealthInject = new CheckBox { Text = "Stealth Inject", Location = new Point(10, 66), AutoSize = true };

            lblInjectDelay = new Label { Text = "Inject delay:", Location = new Point(10, 93), AutoSize = true };
            numInjectDelay = new NumericUpDown
            {
                Location = new Point(100, 91),
                Size = new Size(70, 23),
                Maximum = 60000,
                Value = 0
            };

            lblDelayBetween = new Label { Text = "Delay between:", Location = new Point(10, 120), AutoSize = true };
            numDelayBetween = new NumericUpDown
            {
                Location = new Point(100, 118),
                Size = new Size(70, 23),
                Maximum = 60000,
                Value = 0
            };

            grpInjectionOptions.Controls.AddRange(new Control[] {
                chkAutoInject,
                chkCloseOnInject,
                chkStealthInject,
                lblInjectDelay,
                numInjectDelay,
                lblDelayBetween,
                numDelayBetween
            });

            // =========================================================================
            // 4. POST-INJECT OPTIONS (BOTTOM LEFT)
            // =========================================================================
            grpPostInject = new GroupBox
            {
                Text = "Post-Inject Options:",
                Location = new Point(12, 260),
                Size = new Size(180, 56)
            };

            chkErasePE = new CheckBox { Text = "Erase PE", Location = new Point(10, 22), AutoSize = true };
            chkHideModule = new CheckBox { Text = "Hide Module", Location = new Point(85, 22), AutoSize = true };

            grpPostInject.Controls.AddRange(new Control[] { chkErasePE, chkHideModule });

            // =========================================================================
            // 5. THEME OPTIONS GROUPBOX (MIDDLE RIGHT)
            // =========================================================================
            grpTheme = new GroupBox
            {
                Text = "Theme Options:",
                Location = new Point(202, 104),
                Size = new Size(180, 100)
            };

            lblTextColor = new Label { Text = "Text Color:", Location = new Point(10, 22), AutoSize = true };
            pnlTextColor = CreateColorBox(new Point(144, 20), Color.White);

            lblBgColor1 = new Label { Text = "Background Color #1:", Location = new Point(10, 46), AutoSize = true };
            pnlBgColor1 = CreateColorBox(new Point(144, 44), Color.DodgerBlue);

            lblBgColor2 = new Label { Text = "Background Color #2:", Location = new Point(10, 70), AutoSize = true };
            pnlBgColor2 = CreateColorBox(new Point(144, 68), Color.DeepSkyBlue);

            grpTheme.Controls.AddRange(new Control[] {
                lblTextColor, pnlTextColor,
                lblBgColor1, pnlBgColor1,
                lblBgColor2, pnlBgColor2
            });

            // =========================================================================
            // 6. TOOLS GROUPBOX (BOTTOM RIGHT)
            // =========================================================================
            grpTools = new GroupBox
            {
                Text = "Tools:",
                Location = new Point(202, 210),
                Size = new Size(180, 106)
            };

            btnViewProcessInfo = new Button
            {
                Text = "View Process Information",
                Location = new Point(10, 18),
                Size = new Size(160, 24),
                FlatStyle = FlatStyle.System,
                UseVisualStyleBackColor = true,
                Enabled = !string.IsNullOrWhiteSpace(SettingsManager.Current.ProcessName)
            };
            btnViewProcessInfo.Click += BtnViewProcessInfo_Click;

            btnScrambleDll = new Button
            {
                Text = "Scramble DLL",
                Location = new Point(10, 46),
                Size = new Size(160, 24),
                FlatStyle = FlatStyle.System,
                UseVisualStyleBackColor = true,
                Enabled = false
            };
            btnScrambleDll.Click += BtnScrambleDll_Click;

            btnStartSecureMode = new Button
            {
                Text = "Start in Secure Mode",
                Location = new Point(10, 74),
                Size = new Size(160, 24),
                FlatStyle = FlatStyle.System,
                UseVisualStyleBackColor = true
            };
            btnStartSecureMode.Click += BtnStartSecureMode_Click;

            grpTools.Controls.AddRange(new Control[] {
                btnViewProcessInfo,
                btnScrambleDll,
                btnStartSecureMode
            });

            // =========================================================================
            // 7. BOTTOM ACTION BUTTONS (RESET & OK)
            // =========================================================================
            btnReset = new Button
            {
                Text = "Reset",
                Location = new Point(12, 328),
                Size = new Size(108, 26),
                FlatStyle = FlatStyle.System,
                UseVisualStyleBackColor = true
            };
            btnReset.Click += BtnReset_Click;

            btnOk = new Button
            {
                Text = "OK",
                Location = new Point(274, 328),
                Size = new Size(108, 26),
                FlatStyle = FlatStyle.System,
                UseVisualStyleBackColor = true
            };
            btnOk.Click += (s, e) => SaveAndClose();

            Controls.AddRange(new Control[] {
                grpInjectionMethod,
                grpScrambling,
                grpInjectionOptions,
                grpPostInject,
                grpTheme,
                grpTools,
                btnReset,
                btnOk
            });
        }

        private Panel CreateColorBox(Point location, Color initialColor)
        {
            var pnl = new Panel
            {
                Location = location,
                Size = new Size(20, 20),
                BackColor = initialColor,
                BorderStyle = BorderStyle.FixedSingle,
                Cursor = Cursors.Hand
            };

            pnl.Click += (s, e) =>
            {
                using var cd = new ColorDialog
                {
                    Color = pnl.BackColor,
                    FullOpen = false,
                    AllowFullOpen = true
                };
                if (cd.ShowDialog(this) == DialogResult.OK)
                {
                    pnl.BackColor = cd.Color;
                }
            };

            return pnl;
        }

        private void CmbScramble_SelectedIndexChanged(object? sender, EventArgs e)
        {
            int index = cmbScramble.SelectedIndex;
            var sc = SettingsManager.Current.Options.Scramble;

            switch (index)
            {
                case 0: // None
                    btnScrambleDll.Enabled = false;
                    SetScramblePresetNone(sc);
                    break;
                case 1: // Basic
                    btnScrambleDll.Enabled = true;
                    SetScramblePresetBasic(sc);
                    break;
                case 2: // Standard
                    btnScrambleDll.Enabled = true;
                    SetScramblePresetStandard(sc);
                    break;
                case 3: // Extreme
                    btnScrambleDll.Enabled = true;
                    SetScramblePresetExtreme(sc);
                    break;
                case 4: // Custom
                    btnScrambleDll.Enabled = true;
                    break;
            }

            btnAdvScramble.Enabled = true; // Always enabled for user customization
            SettingsManager.Save();
        }

        private void UpdateScramblePresetFromConfig()
        {
            var sc = SettingsManager.Current.Options.Scramble;

            bool allFalse = !sc.ScrambleHeaderFields && !sc.RemoveUselessData && !sc.InsertExtraSections &&
                            !sc.ShiftSectionData && !sc.ModifyAssemblyCode && !sc.RenameSections &&
                            !sc.ShiftSectionMemory && !sc.StripSectionCharacteristics && !sc.CreateNewEntryPoint &&
                            !sc.ModifyImportTable && !sc.RemoveDebugData && !sc.MoveRelocationTable &&
                            !sc.CreateFakeDebugDirectory;

            bool allTrue = sc.ScrambleHeaderFields && sc.RemoveUselessData && sc.InsertExtraSections &&
                           sc.ShiftSectionData && sc.ModifyAssemblyCode && sc.RenameSections &&
                           sc.ShiftSectionMemory && sc.StripSectionCharacteristics && sc.CreateNewEntryPoint &&
                           sc.ModifyImportTable && sc.RemoveDebugData && sc.MoveRelocationTable &&
                           sc.CreateFakeDebugDirectory;

            bool isBasic = sc.ScrambleHeaderFields && sc.RemoveUselessData && sc.ModifyImportTable && sc.RemoveDebugData &&
                           !sc.InsertExtraSections && !sc.ShiftSectionData && !sc.ModifyAssemblyCode &&
                           !sc.RenameSections && !sc.ShiftSectionMemory && !sc.StripSectionCharacteristics &&
                           !sc.CreateNewEntryPoint && !sc.MoveRelocationTable && !sc.CreateFakeDebugDirectory;

            bool isStandard = sc.ScrambleHeaderFields && sc.RemoveUselessData && sc.ModifyImportTable && sc.RemoveDebugData &&
                              sc.ShiftSectionData && sc.ModifyAssemblyCode && sc.RenameSections &&
                              !sc.InsertExtraSections && !sc.ShiftSectionMemory && !sc.StripSectionCharacteristics &&
                              !sc.CreateNewEntryPoint && !sc.MoveRelocationTable && !sc.CreateFakeDebugDirectory;

            cmbScramble.SelectedIndexChanged -= CmbScramble_SelectedIndexChanged;

            if (allFalse)
            {
                cmbScramble.SelectedIndex = 0; // None
                btnScrambleDll.Enabled = false;
            }
            else if (isBasic)
            {
                cmbScramble.SelectedIndex = 1; // Basic
                btnScrambleDll.Enabled = true;
            }
            else if (isStandard)
            {
                cmbScramble.SelectedIndex = 2; // Standard
                btnScrambleDll.Enabled = true;
            }
            else if (allTrue)
            {
                cmbScramble.SelectedIndex = 3; // Extreme
                btnScrambleDll.Enabled = true;
            }
            else
            {
                cmbScramble.SelectedIndex = 4; // Custom
                btnScrambleDll.Enabled = true;
            }

            btnAdvScramble.Enabled = true;
            cmbScramble.SelectedIndexChanged += CmbScramble_SelectedIndexChanged;
        }

        private static void SetScramblePresetNone(ScrambleConfig sc)
        {
            sc.ScrambleHeaderFields = false;
            sc.RemoveUselessData = false;
            sc.InsertExtraSections = false;
            sc.ShiftSectionData = false;
            sc.ModifyAssemblyCode = false;
            sc.RenameSections = false;
            sc.ShiftSectionMemory = false;
            sc.StripSectionCharacteristics = false;
            sc.CreateNewEntryPoint = false;
            sc.ModifyImportTable = false;
            sc.RemoveDebugData = false;
            sc.MoveRelocationTable = false;
            sc.CreateFakeDebugDirectory = false;
        }

        private static void SetScramblePresetBasic(ScrambleConfig sc)
        {
            SetScramblePresetNone(sc);
            sc.ScrambleHeaderFields = true;
            sc.RemoveUselessData = true;
            sc.ModifyImportTable = true;
            sc.RemoveDebugData = true;
        }

        private static void SetScramblePresetStandard(ScrambleConfig sc)
        {
            SetScramblePresetBasic(sc);
            sc.ShiftSectionData = true;
            sc.ModifyAssemblyCode = true;
            sc.RenameSections = true;
        }

        private static void SetScramblePresetExtreme(ScrambleConfig sc)
        {
            sc.ScrambleHeaderFields = true;
            sc.RemoveUselessData = true;
            sc.InsertExtraSections = true;
            sc.ShiftSectionData = true;
            sc.ModifyAssemblyCode = true;
            sc.RenameSections = true;
            sc.ShiftSectionMemory = true;
            sc.StripSectionCharacteristics = true;
            sc.CreateNewEntryPoint = true;
            sc.ModifyImportTable = true;
            sc.RemoveDebugData = true;
            sc.MoveRelocationTable = true;
            sc.CreateFakeDebugDirectory = true;
        }

        private void BtnScrambleDll_Click(object? sender, EventArgs e)
        {
            if (!SettingsManager.Current.Warnings.Scramble)
            {
                MessageBox.Show(
                    "Extreme Injector v3 automatically scrambles DLLs on injection.\n" +
                    "You only need to use this if you are using another injector.",
                    "Scramble DLL",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                SettingsManager.Current.Warnings.Scramble = true;
                SettingsManager.Save();
            }

            using var ofd = new OpenFileDialog
            {
                Filter = "Dynamic Link Library (*.dll)|*.dll|All Files (*.*)|*.*",
                Title = "Select DLL to Scramble"
            };

            if (ofd.ShowDialog(this) == DialogResult.OK)
            {
                string originalPath = ofd.FileName;
                string dir = Path.GetDirectoryName(originalPath) ?? "";
                string fileNameNoExt = Path.GetFileNameWithoutExtension(originalPath);
                string ext = Path.GetExtension(originalPath);

                using var sfd = new SaveFileDialog
                {
                    Filter = "Dynamic Link Library (*.dll)|*.dll|All Files (*.*)|*.*",
                    Title = "Save Scrambled DLL As",
                    FileName = $"{fileNameNoExt}_scrambled{ext}",
                    InitialDirectory = dir
                };

                if (sfd.ShowDialog(this) == DialogResult.OK)
                {
                    bool success = ExtremeInjector.Core.ScramblerEngine.ScrambleFile(
                        originalPath,
                        sfd.FileName,
                        SettingsManager.Current.Options.Scramble,
                        out string error
                    );

                    if (success)
                    {
                        MessageBox.Show(
                            $"DLL '{Path.GetFileName(originalPath)}' successfully scrambled and saved to:\n\n{sfd.FileName}",
                            "Scramble DLL",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information
                        );
                    }
                    else
                    {
                        MessageBox.Show(
                            $"An error occurred while scrambling the DLL:\n\n{error}",
                            "Scramble DLL",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error
                        );
                    }
                }
            }
        }

        private void BtnStartSecureMode_Click(object? sender, EventArgs e)
        {
            try
            {
                string currentExePath = Application.ExecutablePath;
                if (!File.Exists(currentExePath)) return;

                byte[] exeBytes = File.ReadAllBytes(currentExePath);

                // Generate random alphanumeric filename in %TEMP%
                string randomName = $"EI_{Guid.NewGuid():N}.exe";
                string tempDir = Path.GetTempPath();
                string targetPath = Path.Combine(tempDir, randomName);

                // Mutate binary signature with random overlay padding (16-64 bytes)
                var rand = new Random();
                int padCount = rand.Next(16, 64);
                byte[] padBytes = new byte[padCount];
                rand.NextBytes(padBytes);

                byte[] mutatedBytes = new byte[exeBytes.Length + padCount];
                Buffer.BlockCopy(exeBytes, 0, mutatedBytes, 0, exeBytes.Length);
                Buffer.BlockCopy(padBytes, 0, mutatedBytes, exeBytes.Length, padCount);

                File.WriteAllBytes(targetPath, mutatedBytes);

                // Copy settings.xml if it exists so configuration carries over
                string localSettings = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.xml");
                string tempSettings = Path.Combine(tempDir, "settings.xml");
                if (File.Exists(localSettings))
                {
                    try { File.Copy(localSettings, tempSettings, true); } catch { }
                }

                // Launch randomized clone process
                var psi = new ProcessStartInfo
                {
                    FileName = targetPath,
                    WorkingDirectory = tempDir,
                    UseShellExecute = true
                };

                Process.Start(psi);

                // Terminate current instance immediately
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to start in Secure Mode: {ex.Message}", "Secure Mode", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnViewProcessInfo_Click(object? sender, EventArgs e)
        {
            string pName = SettingsManager.Current.ProcessName;
            if (string.IsNullOrWhiteSpace(pName)) return;

            int pid = 0;
            try
            {
                var procs = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(pName));
                if (procs.Length > 0) pid = procs[0].Id;
            }
            catch { }

            if (pid > 0)
            {
                using var dlg = new ProcessInformationForm(pid, pName);
                dlg.ShowDialog(this);
            }
            else
            {
                MessageBox.Show($"Selected process '{pName}' is not currently running.", "Process Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void BtnReset_Click(object? sender, EventArgs e)
        {
            cmbMethod.SelectedIndex = 0;
            cmbScramble.SelectedIndex = 0;
            chkAutoInject.Checked = false;
            chkCloseOnInject.Checked = false;
            chkStealthInject.Checked = false;
            numInjectDelay.Value = 0;
            numDelayBetween.Value = 0;
            chkErasePE.Checked = false;
            chkHideModule.Checked = false;

            pnlTextColor.BackColor = Color.White;
            pnlBgColor1.BackColor = Color.DodgerBlue;
            pnlBgColor2.BackColor = Color.DeepSkyBlue;
        }

        private void LoadFromConfig()
        {
            var opt = SettingsManager.Current.Options;
            cmbMethod.SelectedIndex = Math.Max(0, Math.Min(opt.Method, 4));

            UpdateScramblePresetFromConfig();

            btnViewProcessInfo.Enabled = !string.IsNullOrWhiteSpace(SettingsManager.Current.ProcessName);

            chkAutoInject.Checked = opt.AutoInject;
            chkCloseOnInject.Checked = opt.CloseOnInject;
            chkStealthInject.Checked = opt.StealthInject;
            numInjectDelay.Value = Math.Max(0, Math.Min(opt.Delay, 60000));
            numDelayBetween.Value = Math.Max(0, Math.Min(opt.DelayBetween, 60000));

            chkErasePE.Checked = opt.ErasePE;
            chkHideModule.Checked = opt.HideModule;

            try { pnlBgColor1.BackColor = ColorTranslator.FromHtml(opt.Background1); } catch { pnlBgColor1.BackColor = Color.DodgerBlue; }
            try { pnlBgColor2.BackColor = ColorTranslator.FromHtml(opt.Background2); } catch { pnlBgColor2.BackColor = Color.DeepSkyBlue; }
            try { pnlTextColor.BackColor = ColorTranslator.FromHtml(opt.TextColor); } catch { pnlTextColor.BackColor = Color.White; }
        }

        private void SaveAndClose()
        {
            var opt = SettingsManager.Current.Options;
            opt.Method = cmbMethod.SelectedIndex;
            opt.AutoInject = chkAutoInject.Checked;
            opt.CloseOnInject = chkCloseOnInject.Checked;
            opt.StealthInject = chkStealthInject.Checked;
            opt.Delay = (int)numInjectDelay.Value;
            opt.DelayBetween = (int)numDelayBetween.Value;

            opt.ErasePE = chkErasePE.Checked;
            opt.HideModule = chkHideModule.Checked;

            opt.Background1 = ColorTranslator.ToHtml(pnlBgColor1.BackColor);
            opt.Background2 = ColorTranslator.ToHtml(pnlBgColor2.BackColor);
            opt.TextColor = ColorTranslator.ToHtml(pnlTextColor.BackColor);

            ThemeManager.UpdateColors(opt.Background1, opt.Background2, opt.TextColor);
            SettingsManager.Save();

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
