using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace QuickZoom;

internal sealed class LocalizationManager
{
    private const string LanguageTagMetadataKey = "$LanguageTag";
    private const string NativeNameMetadataKey = "$NativeName";
    private const string FormattingCultureMetadataKey = "$FormattingCulture";
    private const string TextDirectionMetadataKey = "$TextDirection";

    private sealed class LocaleTable
    {
        internal required IReadOnlyDictionary<string, string> Values { get; init; }
        internal required string LanguageTag { get; init; }
        internal required string NativeName { get; init; }
        internal required CultureInfo FormattingCulture { get; init; }
        internal required bool RightToLeft { get; init; }
    }

    private readonly object _sync = new();
    private readonly Dictionary<UiLanguage, LocaleTable> _cache = new();
    private readonly HashSet<UiLanguage> _validatedLocales = new();
    private readonly HashSet<string> _missingKeyLog = new(StringComparer.Ordinal);
    private readonly HashSet<string> _missingLocaleLog = new(StringComparer.OrdinalIgnoreCase);

    private string LocalesDirectory => Path.Combine(AppContext.BaseDirectory, "locales");

    internal string Get(UiLanguage language, string key, params object[] args)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        LocaleTable sourceTable = GetTable(language);
        if (!sourceTable.Values.TryGetValue(key, out string? value))
        {
            if (language != UiLanguage.English)
            {
                LogMissingKey(language, key);
            }

            sourceTable = GetTable(UiLanguage.English);
            sourceTable.Values.TryGetValue(key, out value);
        }

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
            return string.Format(sourceTable.FormattingCulture, value, args);
        }
        catch (FormatException ex)
        {
            ErrorLog.Write("LocalizationManager", $"Invalid format string for key '{key}' in locale '{sourceTable.LanguageTag}'. {ex.Message}");
            return value;
        }
    }

    internal string GetLocaleNativeName(UiLanguage language)
    {
        return GetTable(language).NativeName;
    }

    internal bool IsRightToLeft(UiLanguage language)
    {
        return GetTable(language).RightToLeft;
    }

    internal UiLanguage? ResolveLanguageTag(string? languageTag)
    {
        string? candidateTag = NormalizeLanguageTag(languageTag);
        while (!string.IsNullOrWhiteSpace(candidateTag))
        {
            if (candidateTag.Equals("no", StringComparison.OrdinalIgnoreCase) ||
                candidateTag.Equals("nb", StringComparison.OrdinalIgnoreCase) ||
                candidateTag.Equals("nn", StringComparison.OrdinalIgnoreCase))
            {
                return UiLanguage.Norwegian;
            }

            foreach (UiLanguage language in Enum.GetValues<UiLanguage>())
            {
                if (string.Equals(GetTable(language).LanguageTag, candidateTag, StringComparison.OrdinalIgnoreCase))
                {
                    return language;
                }
            }

            candidateTag = GetParentLanguageTag(candidateTag);
        }

        return null;
    }

    private LocaleTable GetTable(UiLanguage language)
    {
        lock (_sync)
        {
            if (_cache.TryGetValue(language, out LocaleTable? table))
            {
                return table;
            }

            table = LoadTable(language);
            _cache[language] = table;

            if (language != UiLanguage.English)
            {
                ValidateKeyParity(language, table, GetTable(UiLanguage.English));
            }

            return table;
        }
    }

    private LocaleTable LoadTable(UiLanguage language)
    {
        string languageCode = GetLanguageCode(language);
        string filePath = Path.Combine(LocalesDirectory, languageCode + ".json");
        if (File.Exists(filePath))
        {
            try
            {
                using FileStream stream = File.OpenRead(filePath);
                return DeserializeTable(stream, languageCode);
            }
            catch (Exception ex)
            {
                ErrorLog.Write("LocalizationManager", $"Could not load locale file '{filePath}'. {ex}");
            }
        }

        string resourceName = "QuickZoom.locales." + languageCode + ".json";
        try
        {
            using Stream? stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
            if (stream != null)
            {
                return DeserializeTable(stream, languageCode);
            }
        }
        catch (Exception ex)
        {
            ErrorLog.Write("LocalizationManager", $"Could not load embedded locale resource '{resourceName}'. {ex}");
        }

        LogMissingLocale(language, filePath);
        return CreateEmptyTable(languageCode);
    }

    private static LocaleTable DeserializeTable(Stream stream, string fallbackLanguageTag)
    {
        var values = JsonSerializer.Deserialize<Dictionary<string, string>>(stream);
        if (values == null)
        {
            return CreateEmptyTable(fallbackLanguageTag);
        }

        string languageTag = NormalizeLanguageTag(GetMetadata(values, LanguageTagMetadataKey))
            ?? fallbackLanguageTag;
        string nativeName = GetMetadata(values, NativeNameMetadataKey) ?? languageTag;
        CultureInfo formattingCulture = GetFormattingCulture(
            GetMetadata(values, FormattingCultureMetadataKey),
            languageTag);
        bool rightToLeft = string.Equals(
            GetMetadata(values, TextDirectionMetadataKey),
            "rtl",
            StringComparison.OrdinalIgnoreCase);

        var localizedValues = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach ((string key, string value) in values)
        {
            if (!key.StartsWith('$'))
            {
                localizedValues[key] = value;
            }
        }

        return new LocaleTable
        {
            Values = localizedValues,
            LanguageTag = languageTag,
            NativeName = nativeName,
            FormattingCulture = formattingCulture,
            RightToLeft = rightToLeft
        };
    }

    private static LocaleTable CreateEmptyTable(string languageTag) => new()
    {
        Values = new Dictionary<string, string>(StringComparer.Ordinal),
        LanguageTag = languageTag,
        NativeName = languageTag,
        FormattingCulture = GetFormattingCulture(null, languageTag),
        RightToLeft = false
    };

    private static string? GetMetadata(IReadOnlyDictionary<string, string> values, string key)
    {
        return values.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;
    }

    private static CultureInfo GetFormattingCulture(string? cultureName, string fallbackLanguageTag)
    {
        foreach (string candidate in new[] { cultureName ?? string.Empty, fallbackLanguageTag })
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(candidate))
                {
                    return CultureInfo.GetCultureInfo(candidate.Replace('_', '-'));
                }
            }
            catch (CultureNotFoundException)
            {
                // Fall through to the next deterministic fallback.
            }
        }

        return CultureInfo.InvariantCulture;
    }

    private static string? NormalizeLanguageTag(string? languageTag)
    {
        if (string.IsNullOrWhiteSpace(languageTag))
        {
            return null;
        }

        try
        {
            return CultureInfo.GetCultureInfo(languageTag.Replace('_', '-')).Name;
        }
        catch (CultureNotFoundException)
        {
            return null;
        }
    }

    private static string? GetParentLanguageTag(string languageTag)
    {
        try
        {
            CultureInfo parent = CultureInfo.GetCultureInfo(languageTag).Parent;
            return string.IsNullOrWhiteSpace(parent.Name) ? null : parent.Name;
        }
        catch (CultureNotFoundException)
        {
            return null;
        }
    }

    private void ValidateKeyParity(UiLanguage language, LocaleTable table, LocaleTable englishTable)
    {
        if (!_validatedLocales.Add(language))
        {
            return;
        }

        var missingKeys = new List<string>();
        foreach (string key in englishTable.Values.Keys)
        {
            if (!table.Values.ContainsKey(key))
            {
                missingKeys.Add(key);
            }
        }

        var unexpectedKeys = new List<string>();
        foreach (string key in table.Values.Keys)
        {
            if (!englishTable.Values.ContainsKey(key))
            {
                unexpectedKeys.Add(key);
            }
        }

        if (missingKeys.Count > 0 || unexpectedKeys.Count > 0)
        {
            missingKeys.Sort(StringComparer.Ordinal);
            unexpectedKeys.Sort(StringComparer.Ordinal);
            ErrorLog.Write(
                "LocalizationManager",
                $"Locale '{table.LanguageTag}' does not match English keys. " +
                $"Missing: [{string.Join(", ", missingKeys)}]. " +
                $"Unexpected: [{string.Join(", ", unexpectedKeys)}].");
        }
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
        UiLanguage.Finnish => "fi",
        _ => "en"
    };
}
