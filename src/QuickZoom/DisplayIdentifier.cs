using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace QuickZoom;

internal static class DisplayIdentifier
{
    private static readonly List<Form> VisibleLabels = [];

    internal static void Show(Form owner, ThemePalette palette, Func<string, int, string> labelForDisplay)
    {
        foreach (Form previous in VisibleLabels.ToArray()) previous.Close();
        int index = 1;
        foreach (Screen screen in Screen.AllScreens.OrderByDescending(screen => screen.Primary)
                     .ThenBy(screen => screen.DeviceName, StringComparer.OrdinalIgnoreCase))
        {
            string label = labelForDisplay(screen.DeviceName, index++);
            Rectangle area = screen.WorkingArea;
            var window = new Form
            {
                FormBorderStyle = FormBorderStyle.None,
                ShowInTaskbar = false,
                StartPosition = FormStartPosition.Manual,
                TopMost = true,
                KeyPreview = true,
                BackColor = palette.ControlBackground,
                ForeColor = palette.Text,
                Bounds = new Rectangle(area.Left + area.Width / 4, area.Top + area.Height / 3,
                    area.Width / 2, Math.Max(100, area.Height / 4)),
                AccessibleName = label,
                AccessibleRole = AccessibleRole.Alert
            };
            var text = new Label
            {
                Text = label,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", Math.Clamp(area.Width / 60f, 18f, 48f), FontStyle.Bold),
                Padding = new Padding(20),
                AccessibleName = label
            };
            window.Controls.Add(text);
            var timer = new System.Windows.Forms.Timer { Interval = 3000 };
            timer.Tick += (_, _) => window.Close();
            window.FormClosed += (_, _) =>
            {
                timer.Dispose();
                text.Font.Dispose();
                VisibleLabels.Remove(window);
                window.Dispose();
            };
            text.Click += (_, _) => window.Close();
            window.KeyDown += (_, e) =>
            {
                if (e.KeyCode != Keys.Escape) return;
                e.Handled = true;
                foreach (Form labelWindow in VisibleLabels.ToArray()) labelWindow.Close();
            };
            VisibleLabels.Add(window);
            window.Show(owner);
            timer.Start();
        }
    }
}
