using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace ExtremeInjector.UI
{
    public class DllItemConfigForm : Form
    {
        private GroupBox grpExport = null!;
        private Label lblExport = null!;
        private ComboBox cmbExport = null!;
        private Label lblCallingConvention = null!;
        private ComboBox cmbCallingConvention = null!;
        private Label lblParams = null!;
        private ListView lstParams = null!;
        private ComboBox cmbParamType = null!;
        private TextBox txtParamValue = null!;
        private Button btnAddParam = null!;

        public string ExportName => cmbExport.Text.Trim();
        public string Parameters
        {
            get
            {
                var list = new List<string>();
                foreach (ListViewItem item in lstParams.Items)
                {
                    list.Add($"{item.SubItems[0].Text}:{item.SubItems[1].Text}");
                }
                return string.Join(";", list);
            }
        }

        public DllItemConfigForm(string dllPath, string? export = null, string? parameters = null)
        {
            InitializeComponent(dllPath, export, parameters);
        }

        private void InitializeComponent(string dllPath, string? export, string? parameters)
        {
            Text = "Advanced Module Options";
            ClientSize = new Size(269, 275);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = true;
            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ExtremeInjector.ico");
            if (File.Exists(iconPath))
            {
                try { Icon = new Icon(iconPath); } catch { }
            }
            else if (File.Exists("ExtremeInjector.ico"))
            {
                try { Icon = new Icon("ExtremeInjector.ico"); } catch { }
            }
            Font = new Font("Segoe UI", 9f);
            BackColor = SystemColors.Control;

            grpExport = new GroupBox
            {
                Text = "Export Options",
                Location = new Point(8, 6),
                Size = new Size(253, 260)
            };

            lblExport = new Label
            {
                Text = "Export Function/Routine:",
                Location = new Point(8, 18),
                AutoSize = true
            };

            cmbExport = new ComboBox
            {
                Location = new Point(8, 36),
                Size = new Size(237, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            // Load exported functions from PE file
            LoadExports(dllPath);

            if (!string.IsNullOrEmpty(export) && cmbExport.Items.Contains(export))
            {
                cmbExport.SelectedItem = export;
            }
            else if (cmbExport.Items.Count > 0)
            {
                cmbExport.SelectedIndex = 0;
            }

            lblCallingConvention = new Label
            {
                Text = "Calling Convention:",
                Location = new Point(8, 65),
                AutoSize = true
            };

            cmbCallingConvention = new ComboBox
            {
                Location = new Point(8, 83),
                Size = new Size(237, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbCallingConvention.Items.AddRange(new object[] { "StdCall", "Cdecl", "FastCall", "ThisCall" });
            cmbCallingConvention.SelectedIndex = 0;

            lblParams = new Label
            {
                Text = "Parameters/Arguments:",
                Location = new Point(8, 112),
                AutoSize = true
            };

            lstParams = new ListView
            {
                Location = new Point(8, 130),
                Size = new Size(237, 90),
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                HeaderStyle = ColumnHeaderStyle.Nonclickable
            };
            lstParams.Columns.Add("Type", 80);
            lstParams.Columns.Add("Value", 153);

            var ctxParams = new ContextMenuStrip();
            ctxParams.Items.Add("Remove", null, (s, e) =>
            {
                foreach (ListViewItem item in lstParams.SelectedItems)
                {
                    lstParams.Items.Remove(item);
                }
            });
            lstParams.ContextMenuStrip = ctxParams;
            lstParams.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Delete)
                {
                    foreach (ListViewItem item in lstParams.SelectedItems)
                    {
                        lstParams.Items.Remove(item);
                    }
                }
            };

            // Populate existing parameters
            if (!string.IsNullOrEmpty(parameters))
            {
                string[] entries = parameters!.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var entry in entries)
                {
                    int colon = entry.IndexOf(':');
                    if (colon > 0)
                    {
                        var lvi = new ListViewItem(entry.Substring(0, colon));
                        lvi.SubItems.Add(entry.Substring(colon + 1));
                        lstParams.Items.Add(lvi);
                    }
                    else
                    {
                        var lvi = new ListViewItem("LPCSTR");
                        lvi.SubItems.Add(entry);
                        lstParams.Items.Add(lvi);
                    }
                }
            }

            cmbParamType = new ComboBox
            {
                Location = new Point(8, 226),
                Size = new Size(74, 23),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            cmbParamType.Items.AddRange(new object[] { "LPCSTR", "LPCWSTR", "BYTE", "WORD", "DWORD", "QWORD", "FLOAT", "DOUBLE" });
            cmbParamType.SelectedIndex = -1;

            txtParamValue = new TextBox
            {
                Location = new Point(86, 226),
                Size = new Size(108, 23)
            };

            btnAddParam = new Button
            {
                Text = "Add",
                Location = new Point(198, 225),
                Size = new Size(47, 25),
                FlatStyle = FlatStyle.System,
                UseVisualStyleBackColor = true
            };
            btnAddParam.Click += (s, e) =>
            {
                if (cmbParamType.SelectedIndex >= 0 && !string.IsNullOrWhiteSpace(txtParamValue.Text))
                {
                    var lvi = new ListViewItem(cmbParamType.Text);
                    lvi.SubItems.Add(txtParamValue.Text);
                    lstParams.Items.Add(lvi);
                    txtParamValue.Clear();
                }
            };

            grpExport.Controls.AddRange(new Control[]
            {
                lblExport, cmbExport,
                lblCallingConvention, cmbCallingConvention,
                lblParams, lstParams,
                cmbParamType, txtParamValue, btnAddParam
            });

            Controls.Add(grpExport);
        }

        private void LoadExports(string dllPath)
        {
            try
            {
                if (!File.Exists(dllPath)) return;

                var exports = ParsePeExports(dllPath);
                cmbExport.Items.Clear();
                foreach (var exp in exports)
                {
                    cmbExport.Items.Add(exp);
                }
            }
            catch
            {
                // Ignore parse errors
            }
        }

        private static List<string> ParsePeExports(string filePath)
        {
            var exportNames = new List<string>();
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new BinaryReader(fs);

            if (fs.Length < 64) return exportNames;

            // 1. DOS Header
            ushort dosMagic = reader.ReadUInt16();
            if (dosMagic != 0x5A4D) return exportNames; // "MZ"

            fs.Seek(0x3C, SeekOrigin.Begin);
            uint peOffset = reader.ReadUInt32();
            if (peOffset >= fs.Length - 4) return exportNames;

            // 2. NT Headers
            fs.Seek(peOffset, SeekOrigin.Begin);
            uint peSignature = reader.ReadUInt32();
            if (peSignature != 0x00004550) return exportNames; // "PE\0\0"

            ushort machine = reader.ReadUInt16();
            ushort numberOfSections = reader.ReadUInt16();
            fs.Seek(12, SeekOrigin.Current); // Skip TimeDateStamp, PointerToSymbolTable, NumberOfSymbols
            ushort sizeOfOptionalHeader = reader.ReadUInt16();
            ushort characteristics = reader.ReadUInt16();

            long optionalHeaderPos = fs.Position;
            ushort optMagic = reader.ReadUInt16();
            bool is64Bit = (optMagic == 0x20B);

            // Export directory RVA & Size offset in DataDirectory
            // 32-bit: offset 96 from start of OptionalHeader
            // 64-bit: offset 112 from start of OptionalHeader
            int dataDirOffset = is64Bit ? 112 : 96;
            fs.Seek(optionalHeaderPos + dataDirOffset, SeekOrigin.Begin);
            uint exportRva = reader.ReadUInt32();
            uint exportSize = reader.ReadUInt32();

            if (exportRva == 0) return exportNames;

            // 3. Section Headers (to convert RVA -> File Offset)
            long sectionHeadersPos = optionalHeaderPos + sizeOfOptionalHeader;
            fs.Seek(sectionHeadersPos, SeekOrigin.Begin);

            var sections = new (uint VirtualAddress, uint VirtualSize, uint RawPointer, uint RawSize)[numberOfSections];
            for (int i = 0; i < numberOfSections; i++)
            {
                fs.Seek(8, SeekOrigin.Current); // Skip Name
                uint vSize = reader.ReadUInt32();
                uint vAddr = reader.ReadUInt32();
                uint rawSize = reader.ReadUInt32();
                uint rawPtr = reader.ReadUInt32();
                fs.Seek(16, SeekOrigin.Current); // Skip remaining section fields
                sections[i] = (vAddr, vSize, rawPtr, rawSize);
            }

            uint RvaToOffset(uint rva)
            {
                foreach (var sec in sections)
                {
                    if (rva >= sec.VirtualAddress && rva < sec.VirtualAddress + Math.Max(sec.VirtualSize, sec.RawSize))
                    {
                        return sec.RawPointer + (rva - sec.VirtualAddress);
                    }
                }
                return 0;
            }

            uint exportOffset = RvaToOffset(exportRva);
            if (exportOffset == 0 || exportOffset >= fs.Length) return exportNames;

            // 4. IMAGE_EXPORT_DIRECTORY
            fs.Seek(exportOffset + 24, SeekOrigin.Begin);
            uint numberOfNames = reader.ReadUInt32();
            uint addressOfFunctions = reader.ReadUInt32();
            uint addressOfNames = reader.ReadUInt32();

            uint namesOffset = RvaToOffset(addressOfNames);
            if (namesOffset == 0 || namesOffset >= fs.Length) return exportNames;

            fs.Seek(namesOffset, SeekOrigin.Begin);
            var nameRvas = new uint[Math.Min(numberOfNames, 4096)]; // Guard against corrupt files
            for (int i = 0; i < nameRvas.Length; i++)
            {
                nameRvas[i] = reader.ReadUInt32();
            }

            foreach (var nameRva in nameRvas)
            {
                uint nameOffset = RvaToOffset(nameRva);
                if (nameOffset == 0 || nameOffset >= fs.Length) continue;

                fs.Seek(nameOffset, SeekOrigin.Begin);
                var sb = new StringBuilder();
                byte b;
                while ((b = reader.ReadByte()) != 0 && sb.Length < 256)
                {
                    sb.Append((char)b);
                }
                if (sb.Length > 0)
                {
                    exportNames.Add(sb.ToString());
                }
            }

            exportNames.Sort(StringComparer.OrdinalIgnoreCase);
            return exportNames;
        }
    }
}
