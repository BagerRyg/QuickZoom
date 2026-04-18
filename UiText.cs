using System;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace QuickZoom;

internal enum UiLanguage
{
    English = 0,
    Danish = 1
}

internal static class UiText
{
    private static readonly LocalizationManager Manager = new();

    internal static UiLanguage GetDefaultLanguage()
    {
        string name = CultureInfo.InstalledUICulture.TwoLetterISOLanguageName;
        if (string.IsNullOrWhiteSpace(name))
        {
            name = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        }

        return name.ToLowerInvariant() switch
        {
            "da" => UiLanguage.Danish,
            _ => UiLanguage.English
        };
    }

    internal static UiLanguage GetStartupLanguage()
    {
        try
        {
            foreach (string candidatePath in new[] { AppPaths.SettingsPath, AppPaths.LegacySettingsPath })
            {
                if (!File.Exists(candidatePath))
                {
                    continue;
                }

                using FileStream stream = File.OpenRead(candidatePath);
                using JsonDocument document = JsonDocument.Parse(stream);
                if (document.RootElement.TryGetProperty("Language", out JsonElement languageElement) &&
                    languageElement.ValueKind == JsonValueKind.Number &&
                    languageElement.TryGetInt32(out int languageValue) &&
                    Enum.IsDefined(typeof(UiLanguage), languageValue))
                {
                    return (UiLanguage)languageValue;
                }
            }
        }
        catch (Exception ex)
        {
            ErrorLog.Write("UiText", $"Could not determine the startup language. {ex.Message}");
        }

        return GetDefaultLanguage();
    }

    internal static string Get(UiLanguage language, string key, params object[] args)
    {
        return Manager.Get(language, key, args);
    }

    internal static string GetLanguageDisplayName(UiLanguage displayLanguage, UiLanguage currentUiLanguage)
    {
        return Get(currentUiLanguage, displayLanguage switch
        {
            UiLanguage.Danish => "Settings.Danish",
            _ => "Settings.English"
        });
    }

    internal static UiLanguage ParseLanguageDisplayName(UiLanguage currentUiLanguage, string value)
    {
        foreach (UiLanguage language in Enum.GetValues<UiLanguage>())
        {
            if (string.Equals(GetLanguageDisplayName(language, currentUiLanguage), value, StringComparison.Ordinal))
            {
                return language;
            }
        }

        return UiLanguage.English;
    }
}
