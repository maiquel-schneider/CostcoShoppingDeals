using System;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using CostcoDeals.Models;
using System.Globalization;

namespace CostcoDeals.Services
{
    /// <summary>
    /// Parses a raw text block (from the scraper) into a <see cref="ScrapedProduct"/>.
    /// </summary>
    public class ProductParser
    {
        private readonly ILogger<ProductParser> _logger;

        public ProductParser(ILogger<ProductParser> logger)
            => _logger = logger;

        public ScrapedProduct? Parse(string rawText)
        {
            if (string.IsNullOrWhiteSpace(rawText))
                return null;

            try
            {
                // 1) Split on '\n' only, trim carriage returns later
                var lines = rawText
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Select(l => l.Trim('\r').Trim())
                    .ToArray();
                if (lines.Length == 0) return null;

                // 2) Extract ID and the rest
                var costcoId = lines[0];
                var remainingLines = lines.Skip(1).ToArray();
                var rawUpper = string.Join(" ", remainingLines).ToUpperInvariant();

                // 3) Defaults
                var name = "NA";
                var fullPrice = "NA";
                var discount = "NA";
                var expirationDate = "NA";
                var finalPrice = "NA";

                // --- Type 5: REGULAR PRICE per kg ---
                if (rawUpper.Contains("REGULAR PRICE"))
                {
                    _logger.LogDebug("Type5: REGULAR PRICE block");

                    name = string.Join(" ",
                        remainingLines
                          .TakeWhile(l => !l.ToUpperInvariant().Contains("LESS IN-STORE REBATE")))
                          .Trim();

                    var priceLines = lines.Where(l => l.Contains("$")).ToList();
                    if (priceLines.Count >= 2)
                    {
                        discount = CleanPrice(priceLines[0]);
                        fullPrice = CleanPrice(priceLines[1]);
                        finalPrice = "Discount at Register";
                    }
                }
                // --- Type 2: LESS IN-STORE REBATE ---
                else if (rawUpper.Contains("LESS IN-STORE REBATE"))
                {
                    _logger.LogDebug("Type2: LESS IN-STORE REBATE block");

                    name = string.Join(" ",
                                  remainingLines
                                    .TakeWhile(l => !l.ToUpperInvariant().Contains("LESS")))
                                    .Trim();
                    finalPrice = "Discount at Register";
                    var dl = lines.FirstOrDefault(l =>
                               l.Contains("$") || l.ToUpperInvariant().Contains("OFF"));
                    discount = string.IsNullOrEmpty(dl) ? "NA" : dl.Trim();
                }
                // --- Type 1: PRICE AT REGISTER ---
                else if (rawUpper.Contains("PRICE AT REGISTER"))
                {
                    _logger.LogDebug("Type1: PRICE AT REGISTER block");

                    // find first decimal‐parsable line (skipping ID and title)
                    int priceIdx = Array.FindIndex(
                        lines, 2,
                        line => decimal.TryParse(
                            line.Replace("$", "").Trim(),
                            NumberStyles.Number,
                            CultureInfo.InvariantCulture,
                            out _));
                    if (priceIdx > 1)
                    {
                        name = string.Join(" ",
                                      lines.Skip(1).Take(priceIdx - 1)).Trim();
                        fullPrice = CleanPrice(lines[priceIdx]);
                    }

                    foreach (var line in lines)
                    {
                        var txt = line.Trim();
                        if (txt.ToUpperInvariant().StartsWith("EXP."))
                            expirationDate = txt.Substring(4).Trim();
                        else if (txt.StartsWith("-", StringComparison.Ordinal))
                            discount = txt;
                        else if (decimal.TryParse(
                                    txt.Replace("$", ""),
                                    NumberStyles.Number,
                                    CultureInfo.InvariantCulture,
                                    out var parsed)
                                 && txt != fullPrice)
                            finalPrice = txt;
                    }
                }
                // --- Type 3/4: clearance/.97/.99 endings ---
                else
                {
                    _logger.LogDebug("Type3/4: clearance or .97/.99 block");

                    var words = rawUpper.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    var prices = words
                        .Where(w => (w.EndsWith(".97") || w.EndsWith(".99"))
                                    && decimal.TryParse(
                                        w.Replace("$", ""),
                                        NumberStyles.Number,
                                        CultureInfo.InvariantCulture,
                                        out _))
                        .ToList();

                    if (prices.Count >= 2)
                    {
                        finalPrice = prices.First(p => p.EndsWith(".97"));
                        fullPrice = prices.First(p => p.EndsWith(".99"));
                        name = string.Join(" ",
                                      words.Take(words.Length - 2)).Trim();
                    }
                    else if (prices.Count == 1)
                    {
                        finalPrice = prices[0];
                        name = remainingLines.Length > 1
                                     ? string.Join(" ",
                                         remainingLines.Take(remainingLines.Length - 1)).Trim()
                                     : remainingLines.First();
                    }
                }

                _logger.LogInformation("Parsed {Id}: {Name} → {FinalPrice}",
                                        costcoId, name, finalPrice);

                return new ScrapedProduct
                {
                    CostcoId = costcoId,
                    Name = name,
                    FullPrice = fullPrice,
                    Discount = discount,
                    ExpirationDate = expirationDate,
                    FinalPrice = finalPrice
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing product block");
                return null;
            }
        }

        private static string CleanPrice(string input)
            => input
               .Replace("$", "")
               .Replace("REGULAR PRICE:", "", StringComparison.OrdinalIgnoreCase)
               .Replace("/ KG", "", StringComparison.OrdinalIgnoreCase)
               .Trim();
    }

}
