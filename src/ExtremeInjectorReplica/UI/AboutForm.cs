using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace ExtremeInjector.UI
{
    public class AboutForm : Form
    {
        public AboutForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            Text = "About Extreme Injector";
            ClientSize = new Size(360, 172);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = true;
            Font = new Font("Segoe UI", 9f);
            BackColor = SystemColors.Control;
            Icon = ThemeManager.AppIcon;

            // 1. Top White Header Panel
            var headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 72,
                BackColor = Color.White
            };
            headerPanel.Paint += (s, e) =>
            {
                using var linePen = new Pen(Color.FromArgb(215, 215, 215));
                e.Graphics.DrawLine(linePen, 0, headerPanel.Height - 1, headerPanel.Width, headerPanel.Height - 1);
            };

            // Syringe Icon
            var picIcon = new PictureBox
            {
                Location = new Point(14, 12),
                Size = new Size(48, 48),
                SizeMode = PictureBoxSizeMode.CenterImage,
                BackColor = Color.Transparent
            };

            try
            {
                picIcon.Image = new Icon(ThemeManager.AppIcon, new Size(48, 48)).ToBitmap();
            }
            catch { }

            // "Extreme Injector" Title
            var lblTitle = new Label
            {
                Text = "Extreme Injector",
                Location = new Point(68, 12),
                AutoSize = true,
                Font = new Font("Segoe UI", 16f, FontStyle.Regular),
                ForeColor = Color.Black,
                BackColor = Color.Transparent
            };

            // Version Label
            var lblVersion = new Label
            {
                Text = "v3.7.3 (Open Source Replica)",
                Location = new Point(68, 42),
                AutoSize = true,
                Font = new Font("Segoe UI", 11f, FontStyle.Regular),
                ForeColor = Color.FromArgb(70, 70, 70),
                BackColor = Color.Transparent
            };

            headerPanel.Controls.AddRange(new Control[] { picIcon, lblTitle, lblVersion });

            // 2. Bottom Content Section
            int contentWidth = ClientSize.Width;

            var lblTagline = new Label
            {
                Text = "An open-source recreation for research & educational debugging.",
                Location = new Point(0, 82),
                Size = new Size(contentWidth, 16),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.Black,
                BackColor = Color.Transparent
            };

            var lblDetails = new Label
            {
                Text = "In active development to achieve full functional parity.",
                Location = new Point(0, 100),
                Size = new Size(contentWidth, 16),
                Font = new Font("Segoe UI", 8.25f, FontStyle.Italic),
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.FromArgb(80, 80, 80),
                BackColor = Color.Transparent
            };

            var lnkReport = new LinkLabel
            {
                Text = "GitHub Repository",
                Location = new Point(0, 122),
                Size = new Size(contentWidth, 16),
                Font = new Font("Segoe UI", 9f, FontStyle.Regular),
                TextAlign = ContentAlignment.MiddleCenter,
                LinkColor = Color.FromArgb(0, 102, 204),
                ActiveLinkColor = Color.FromArgb(0, 80, 180),
                VisitedLinkColor = Color.FromArgb(0, 102, 204),
                BackColor = Color.Transparent
            };
            lnkReport.LinkClicked += (s, e) =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "https://github.com/rabbanyhmm/Extreme-Injector-Open-Source",
                        UseShellExecute = true
                    });
                }
                catch { }
            };

            var lblCopyright = new Label
            {
                Text = "Maintained by rabbanyhmm",
                Location = new Point(0, 144),
                Size = new Size(contentWidth, 16),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Regular),
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.FromArgb(60, 60, 60),
                BackColor = Color.Transparent
            };

            Controls.AddRange(new Control[] { headerPanel, lblTagline, lblDetails, lnkReport, lblCopyright });
        }
    }
}
