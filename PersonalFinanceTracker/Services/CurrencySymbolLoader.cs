// Personal Finance Tracker
// File: PersonalFinanceTracker/Services/CurrencySymbolLoader.cs
// Purpose: Provides an application service shared across repositories and page models.

// Services/CurrencySymbolLoader.cs
// Requires: using System.Text.Json; using System.Text.Json.Nodes;

using System.Text.Json.Nodes;

namespace PersonalFinanceTracker.Services
{
    public static class CurrencySymbolLoader
    {
        /// <summary>
        /// Load code -> symbol map from embedded CLDR JSON (Resources/Raw/cldr_currencies_en.json).
        /// Prefers "symbol"; falls back to "symbol-alt-narrow", then ISO code.
        /// </summary>
        public static Dictionary<string, string> LoadAllSymbols()
        {
            // Read the embedded file as text
            using var stream = FileSystem.OpenAppPackageFileAsync("cldr_currencies_en.json").Result;
            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();

            // Parse with System.Text.Json for speed and low deps
            var root = JsonNode.Parse(json)!;

            // Navigate to: main -> en -> numbers -> currencies
            var currencies = root["main"]?["en-001"]?["numbers"]?["currencies"]?.AsObject();
            if (currencies is null)
                throw new InvalidOperationException("CLDR currencies.json format not as expected.");

            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var kv in currencies)
            {
                var code = kv.Key; // ISO 4217 code, e.g., "EUR"
                var obj = kv.Value?.AsObject();
                if (obj is null) continue;

                // Prefer the normal "symbol". If missing, use "symbol-alt-narrow". Else fallback to code.
                var symbol = obj["symbol"]?.GetValue<string>()
                             ?? obj["symbol-alt-narrow"]?.GetValue<string>()
                             ?? code;

                // Normalize some CLDR placeholder cases that equal the code string
                map[code] = string.IsNullOrWhiteSpace(symbol) ? code : symbol;
            }

            return map;
        }
    }
}

