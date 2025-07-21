using System;
using System.Globalization;
using Microsoft.Extensions.Logging;

namespace CostcoDeals.Services
{
    public static class PriceParsingHelper
    {
        /// <summary>
        /// Tries to parse a price string like "$12.99" or "12,99", returns 0 on failure.
        /// </summary>
        public static decimal? ParsePrice(string? raw, ILogger? log = null)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                log?.LogWarning("Empty price string provided");
                return null;
            }

            // strip currency, whitespace, normalize decimal point
            var cleaned = raw
                .Replace("$", "")
                .Replace("€", "")
                .Replace(",", ".")
                .Trim();

            if (decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out var result))
                return result;

            log?.LogWarning("Failed to parse price '{Price}'", raw);
            return null;
        }
    }
}
