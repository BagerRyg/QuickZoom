using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace QuickZoom;

internal sealed partial class TrayContext
{
    internal static void CaptureUiScreenshots(string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        using var context = new TrayContext(screenshotMode: true);

        foreach (bool useDarkTheme in new[] { true, false })
        {
            foreach (UiLanguage language in Enum.GetValues<UiLanguage>())
            {
                context.CaptureUiScreenshotSet(outputDirectory, language, useDarkTheme);
            }
        }
    }

    private void CaptureUiScreenshotSet(string outputDirectory, UiLanguage language, bool useDarkTheme)
    {
        _language = language;
        _themeMode = useDarkTheme ? ThemeMode.Dark : ThemeMode.Light;
        _useDarkTheme = useDarkTheme;

        string languageCode = LocalizationManager.GetLanguageCode(language);
        string themeName = useDarkTheme ? "dark" : "light";
        string variantDirectory = Path.Combine(outputDirectory, themeName, languageCode);
        Directory.CreateDirectory(variantDirectory);

        CaptureSettingsPages(variantDirectory);
        CaptureTrayMenu(variantDirectory);
    }

    private void CaptureSettingsPages(string outputDirectory)
    {
        SettingsForm? form = null;
        try
        {
            _resetDefaultsButton = new ModernButton
            {
                Text = L("Settings.Reset"),
                MinimumSize = new Size(170, 38)
            };
            ApplyResetDefaultsButtonTheme();

            form = new SettingsForm(
                CurrentTheme,
                _useDarkTheme,
                L("Settings.Title"),
                GetSettingsClientSize(),
                L("Common.AppName"),
                L("Settings.Done"),
                _resetDefaultsButton,
                BuildSettingsPageDefinitions());

            _settingsWindow = form;
            WindowChrome.TrySetDarkTitleBar(form, _useDarkTheme);
            PlaceCaptureWindow(form);
            form.Show();
            WaitForUi();

            foreach (SettingsPage page in Enum.GetValues<SettingsPage>())
            {
                form.ShowPage(GetSettingsPageType(page));
                WaitForUi(page is SettingsPage.Display or SettingsPage.About ? 900 : 250);
                CaptureWindow(form, Path.Combine(outputDirectory, "settings-" + GetSettingsPageFileName(page) + ".png"));
            }
        }
        finally
        {
            if (form != null)
            {
                form.Close();
                form.Dispose();
            }

            _settingsWindow = null;
            _resetDefaultsButton = null;
            _displaySelectionSettingsSection = null;
        }
    }

    private void CaptureTrayMenu(string outputDirectory)
    {
        try
        {
            Rectangle area = Screen.PrimaryScreen?.WorkingArea ?? Screen.FromPoint(Cursor.Position).WorkingArea;
            Point anchor = new(area.Right - 24, area.Bottom - 24);
            ShowTrayPopup(anchor);
            if (_trayPopup == null)
            {
                return;
            }

            _trayPopup.IgnoreDeactivateClose = true;
            _trayPopup.TopMost = true;
            _trayPopup.BringToFront();
            WaitForUi(250);
            CaptureWindow(_trayPopup, Path.Combine(outputDirectory, "tray-menu.png"));
        }
        finally
        {
            CloseTrayPopup();
        }
    }

    private static string GetSettingsPageFileName(SettingsPage page) => page switch
    {
        SettingsPage.Display => "display",
        SettingsPage.Appearance => "appearance",
        SettingsPage.Cursor => "cursor",
        SettingsPage.Zoom => "zoom",
        SettingsPage.Input => "shortcuts",
        SettingsPage.About => "about",
        _ => "general"
    };

    private static void PlaceCaptureWindow(Form form)
    {
        Rectangle area = Screen.PrimaryScreen?.WorkingArea ?? Screen.FromPoint(Cursor.Position).WorkingArea;
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(
            area.Left + Math.Max(0, (area.Width - form.Width) / 2),
            area.Top + Math.Max(0, (area.Height - form.Height) / 2));
    }

    private static void CaptureWindow(Form form, string path)
    {
        form.BringToFront();
        form.Activate();
        WaitForUi(120);

        Control captureTarget = form.Controls.Count > 0 ? form.Controls[0] : form;
        Size size = captureTarget.Size;
        using var bitmap = new Bitmap(Math.Max(1, size.Width), Math.Max(1, size.Height));
        captureTarget.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
        bitmap.Save(path, ImageFormat.Png);
    }

    private static void WaitForUi(int delayMs = 150)
    {
        DateTime deadline = DateTime.UtcNow.AddMilliseconds(delayMs);
        do
        {
            Application.DoEvents();
            Thread.Sleep(15);
        }
        while (DateTime.UtcNow < deadline);

        Application.DoEvents();
    }
}
