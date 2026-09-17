param([string]$RepositoryRoot = (Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference = 'Stop'
$localeMaps = @{}
foreach ($language in @('en', 'da', 'sv', 'no', 'fi')) {
    $path = Join-Path $RepositoryRoot "locales\$language.json"
    $document = [System.Text.Json.JsonDocument]::Parse([IO.File]::ReadAllText($path))
    try {
        $map = [Collections.Generic.Dictionary[string,string]]::new([StringComparer]::Ordinal)
        foreach ($property in $document.RootElement.EnumerateObject()) {
            if (!$map.TryAdd($property.Name, $property.Value.GetString())) { throw "Duplicate $language key: $($property.Name)" }
            if ([string]::IsNullOrWhiteSpace($map[$property.Name])) { throw "Empty $language value: $($property.Name)" }
            if ($map[$property.Name].Contains([char]0xFFFD)) { throw "Encoding error: $language / $($property.Name)" }
        }
        $localeMaps[$language] = $map
    } finally { $document.Dispose() }
}
$english = $localeMaps['en']
foreach ($key in $english.Keys) {
    if ($key -notlike 'Settings.SearchAliases.*' -and $english[$key] -match '\b(colors?|behaviors?|gray)\b') {
        throw "Use consistent British English in visible text: $key"
    }
}
foreach ($language in @('da', 'sv', 'no', 'fi')) {
    $map = $localeMaps[$language]
    $difference = Compare-Object @($english.Keys | Sort-Object) @($map.Keys | Sort-Object)
    if ($difference) { throw "Locale key mismatch in ${language}: $difference" }
    foreach ($key in $english.Keys) {
        $expected = @([regex]::Matches($english[$key], '(?<!\{)\{\d+(?:[^}]*)\}(?!\})').Value | Sort-Object) -join '|'
        $actual = @([regex]::Matches($map[$key], '(?<!\{)\{\d+(?:[^}]*)\}(?!\})').Value | Sort-Object) -join '|'
        if ($expected -ne $actual) { throw "Placeholder mismatch: $language / $key" }
    }
    Write-Output "PASS: $language ($($map.Count) keys; placeholders and encoding match English)"
}
