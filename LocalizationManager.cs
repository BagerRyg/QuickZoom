using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace QuickZoom;

internal sealed class LocalizationManager
{
    private readonly object _sync = new();
    private readonly Dictionary<UiLanguage, IReadOnlyDictionary<string, string>> _cache = new();
    private readonly HashSet<string> _missingKeyLog = new(StringComparer.Ordinal);
    private readonly HashSet<string> _missingLocaleLog = new(StringComparer.OrdinalIgnoreCase);

    private string LocalesDirectory => Path.Combine(AppContext.BaseDirectory, "locales");

    internal string Get(UiLanguage language, string key, params object[] args)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        string? value = TryGetValue(language, key) ?? TryGetValue(UiLanguage.English, key);
        if (value == null)
        {
            LogMissingKey(language, key);
            return key;
        }

        if (args.Length == 0)
        {
            return value;
        }

        try
        {
            return string.Format(CultureInfo.CurrentCulture, value, args);
        }
        catch (FormatException ex)
        {
            ErrorLog.Write("LocalizationManager", $"Invalid format string for key '{key}' in language '{GetLanguageCode(language)}'. {ex.Message}");
            return value;
        }
    }

    private string? TryGetValue(UiLanguage language, string key)
    {
        IReadOnlyDictionary<string, string> table = GetTable(language);
        return table.TryGetValue(key, out string? value) ? value : null;
    }

    private IReadOnlyDictionary<string, string> GetTable(UiLanguage language)
    {
        lock (_sync)
        {
            if (_cache.TryGetValue(language, out IReadOnlyDictionary<string, string>? table))
            {
                return table;
            }

            table = LoadTable(language);
            _cache[language] = table;
            return table;
        }
    }

    private IReadOnlyDictionary<string, string> LoadTable(UiLanguage language)
    {
        string filePath = Path.Combine(LocalesDirectory, GetLanguageCode(language) + ".json");
        if (File.Exists(filePath))
        {
            try
            {
                using FileStream stream = File.OpenRead(filePath);
                return DeserializeTable(stream);
            }
            catch (Exception ex)
            {
                ErrorLog.Write("LocalizationManager", $"Could not load locale file '{filePath}'. {ex}");
            }
        }

        string resourceName = "QuickZoom.locales." + GetLanguageCode(language) + ".json";
        try
        {
            using Stream? stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
            if (stream != null)
            {
                return DeserializeTable(stream);
            }
        }
        catch (Exception ex)
        {
            ErrorLog.Write("LocalizationManager", $"Could not load embedded locale resource '{resourceName}'. {ex}");
        }

        LogMissingLocale(language, filePath);
        return new Dictionary<string, string>(StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, string> DeserializeTable(Stream stream)
    {
        var values = JsonSerializer.Deserialize<Dictionary<string, string>>(stream);
        return values != null
            ? new Dictionary<string, string>(values, StringComparer.Ordinal)
            : new Dictionary<string, string>(StringComparer.Ordinal);
    }

    private void LogMissingKey(UiLanguage language, string key)
    {
        string composite = GetLanguageCode(language) + ":" + key;
        lock (_sync)
        {
            if (!_missingKeyLog.Add(composite))
            {
                return;
            }
        }

        ErrorLog.Write("LocalizationManager", $"Missing translation key '{key}' for language '{GetLanguageCode(language)}'.");
    }

    private void LogMissingLocale(UiLanguage language, string filePath)
    {
        string code = GetLanguageCode(language);
        lock (_sync)
        {
            if (!_missingLocaleLog.Add(code))
            {
                return;
            }
        }

        ErrorLog.Write("LocalizationManager", $"Missing locale file for language '{code}' at '{filePath}'.");
    }

    internal static string GetLanguageCode(UiLanguage language) => language switch
    {
        UiLanguage.Danish => "da",
        UiLanguage.Swedish => "sv",
        UiLanguage.Norwegian => "no",
        UiLanguage.German => "de",
        _ => "en"
    };
}
