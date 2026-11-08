using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using CostcoDeals.Models;

namespace CostcoDeals.Services
{
    /// <summary>
    /// Legacy / compatibility parser.
    /// The main Costco scraper now extracts fields directly from the DOM,
    /// but this parser is kept so the existing DI setup and any fallback use still compile.
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
                var lines = rawText
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Select(l => Regex.Replace(l.Trim('\r').Trim(), @"\s+", " "))
                    .Where(l => !string.IsNullOrWhiteSpace(l))
                    .ToArray();

                if (lines.Length == 0)
                    return null;

                // Keep structured support if anything still uses it
                var structured = TryParseStructured(lines);
                if (structured != null)
                    return structured;

                var costcoId = Regex.IsMatch(lines[0], @"^\d{3,8}$")
                    ? lines[0]
                    : ExtractCostcoId(string.Join(" ", lines));

                if (string.IsNullOrWhiteSpace(costcoId))
                    return null;

                var remaining = Regex.IsMatch(lines[0], @"^\d{3,8}$")
                    ? lines.Skip(1).ToArray()
                    : lines;

                if (remaining.Length == 0)
                    return null;

                var rawUpper = string.Join(" ", remaining).ToUpperInvariant();

                var dto = new ScrapedProduct
                {
                    CostcoId = costcoId,
                    Name = "",
                    FullPrice = "",
                    Discount = "",
                    ExpirationDate = ExtractExpiration(string.Join(" ", remaining)),
                    FinalPrice = ""
                };

                // --- Type: package rebate only ---
                // Example:
                // 21828
                // STRIP LOIN (WHOLE)
                // Less In-Store Rebate
                // 50.00 OFF
                // Per Package at Register
                if (rawUpper.Contains("LESS IN-STORE REBATE") && !rawUpper.Contains("PRICE AT REGISTER"))
                {
                    int lessIdx = Array.FindIndex(remaining, l => l.ToUpperInvariant().Contains("LESS IN-STORE REBATE"));

                    dto.Name = JoinName(remaining.Take(lessIdx >= 0 ? lessIdx : remaining.Length).ToArray());

                    var discountLine = remaining.FirstOrDefault(l =>
                        l.ToUpperInvariant().Contains("OFF") || IsStandaloneMoney(l));

                    dto.Discount = ExtractMoney(discountLine ?? "");
                    dto.FinalPrice = "Discount at Register";

                    _logger.LogInformation("Parsed package-rebate item {Id}: {Name} → Discount={Discount}",
                        dto.CostcoId, dto.Name, dto.Discount);

                    return dto;
                }

                // --- Type: regular price / instant savings / price at register ---
                // Example:
                // 1570675
                // SWEET KALE SALAD 2 PACK PRODUCT OF
                // USA
                // 8.99
                // Instant Savings
                // EXP.2026-04-12
                // -2.00
                // PRICE AT REGISTER
                // 6.99
                if (rawUpper.Contains("PRICE AT REGISTER") || rawUpper.Contains("REGULAR PRICE") || rawUpper.Contains("INSTANT SAVINGS"))
                {
                    int firstPriceIdx = Array.FindIndex(remaining, IsStandaloneMoney);
                    int registerIdx = Array.FindIndex(remaining, l => l.ToUpperInvariant().Contains("PRICE AT REGISTER"));

                    int nameStopIdx = firstPriceIdx >= 0 ? firstPriceIdx : remaining.Length;
                    dto.Name = JoinName(remaining.Take(nameStopIdx).ToArray());

                    // Full price = first plain money line before register line
                    if (registerIdx >= 0)
                    {
                        for (int i = 0; i < registerIdx; i++)
                        {
                            if (IsStandaloneMoney(remaining[i]))
                            {
                                dto.FullPrice = ExtractMoney(remaining[i]);
                                break;
                            }
                        }
                    }
                    else
                    {
                        var firstMoney = remaining.FirstOrDefault(IsStandaloneMoney);
                        dto.FullPrice = ExtractMoney(firstMoney ?? "");
                    }

                    // Discount = explicit negative or OFF line
                    var discountLine = remaining.FirstOrDefault(l =>
                        l.TrimStart().StartsWith("-") ||
                        l.ToUpperInvariant().Contains("OFF"));

                    dto.Discount = ExtractMoney(discountLine ?? "");

                    // Final price = first plain money line after PRICE AT REGISTER
                    if (registerIdx >= 0)
                    {
                        for (int i = registerIdx + 1; i < remaining.Length; i++)
                        {
                            if (IsStandaloneMoney(remaining[i]))
                            {
                                dto.FinalPrice = ExtractMoney(remaining[i]);
                                break;
                            }
                        }
                    }

                    // Fallback if not found but we have two price lines
                    if (string.IsNullOrWhiteSpace(dto.FinalPrice))
                    {
                        var moneyLines = remaining
                            .Where(IsStandaloneMoney)
                            .Select(ExtractMoney)
                            .Where(v => !string.IsNullOrWhiteSpace(v))
                            .ToList();

                        if (moneyLines.Count >= 2)
                        {
                            dto.FullPrice = string.IsNullOrWhiteSpace(dto.FullPrice) ? moneyLines[0] : dto.FullPrice;
                            dto.FinalPrice = moneyLines[^1];
                        }
                        else if (moneyLines.Count == 1)
                        {
                            dto.FinalPrice = moneyLines[0];
                        }
                    }

                    _logger.LogInformation("Parsed instant-savings item {Id}: {Name} → Final={FinalPrice}, Full={FullPrice}, Discount={Discount}, Exp={ExpirationDate}",
                        dto.CostcoId, dto.Name, dto.FinalPrice, dto.FullPrice, dto.Discount, dto.ExpirationDate);

                    return dto;
                }

                // --- Type: clearance / no explicit discount ---
                // Price endings like .97, or simple single-price cards
                {
                    int firstPriceIdx = Array.FindIndex(remaining, IsStandaloneMoney);
                    dto.Name = JoinName(remaining.Take(firstPriceIdx >= 0 ? firstPriceIdx : remaining.Length).ToArray());

                    var moneyValues = remaining
                        .Where(IsStandaloneMoney)
                        .Select(ExtractMoney)
                        .Where(v => !string.IsNullOrWhiteSpace(v))
                        .Distinct()
                        .ToList();

                    if (moneyValues.Count >= 2)
                    {
                        dto.FinalPrice = moneyValues.FirstOrDefault(v => v.EndsWith(".97", StringComparison.Ordinal))
                            ?? moneyValues[^1];

                        dto.FullPrice = moneyValues.FirstOrDefault(v => v != dto.FinalPrice) ?? "";
                    }
                    else if (moneyValues.Count == 1)
                    {
                        dto.FinalPrice = moneyValues[0];
                    }

                    if (decimal.TryParse(dto.FullPrice, NumberStyles.Any, CultureInfo.InvariantCulture, out var fp) &&
                        decimal.TryParse(dto.FinalPrice, NumberStyles.Any, CultureInfo.InvariantCulture, out var cp) &&
                        fp > cp)
                    {
                        dto.Discount = (fp - cp).ToString("F2", CultureInfo.InvariantCulture);
                    }

                    _logger.LogInformation("Parsed fallback item {Id}: {Name} → Final={FinalPrice}, Full={FullPrice}",
                        dto.CostcoId, dto.Name, dto.FinalPrice, dto.FullPrice);

                    return dto;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing product block");
                return null;
            }
        }

        private ScrapedProduct? TryParseStructured(string[] lines)
        {
            var dto = new ScrapedProduct();
            bool foundAnyStructuredField = false;

            foreach (var line in lines)
            {
                if (TryReadField(line, "CostcoId", out var costcoId))
                {
                    dto.CostcoId = costcoId;
                    foundAnyStructuredField = true;
                    continue;
                }

                if (TryReadField(line, "Name", out var name))
                {
                    dto.Name = name;
                    foundAnyStructuredField = true;
                    continue;
                }

                if (TryReadField(line, "FullPrice", out var fullPrice))
                {
                    dto.FullPrice = fullPrice;
                    foundAnyStructuredField = true;
                    continue;
                }

                if (TryReadField(line, "Discount", out var discount))
                {
                    dto.Discount = discount;
                    foundAnyStructuredField = true;
                    continue;
                }

                if (TryReadField(line, "ExpirationDate", out var expirationDate))
                {
                    dto.ExpirationDate = expirationDate;
                    foundAnyStructuredField = true;
                    continue;
                }

                if (TryReadField(line, "FinalPrice", out var finalPrice))
                {
                    dto.FinalPrice = finalPrice;
                    foundAnyStructuredField = true;
                    continue;
                }
            }

            if (!foundAnyStructuredField)
                return null;

            if (string.IsNullOrWhiteSpace(dto.CostcoId) || string.IsNullOrWhiteSpace(dto.Name))
                return null;

            return dto;
        }

        private static bool TryReadField(string line, string fieldName, out string value)
        {
            value = "";

            if (!line.StartsWith(fieldName + ":", StringComparison.OrdinalIgnoreCase))
                return false;

            value = line[(fieldName.Length + 1)..].Trim();
            return true;
        }

            private static string[] CollapseMirroredOverlay(string[] lines)
        {
            if (lines.Length < 2 || lines.Length % 2 != 0)
                return lines;

            int half = lines.Length / 2;
            var firstHalf = lines.Take(half).ToArray();
            var secondHalf = lines.Skip(half).ToArray();

            return firstHalf.SequenceEqual(secondHalf, StringComparer.OrdinalIgnoreCase)
                ? firstHalf
                : lines;
        }

        private static int FindFirstIndex(string[] items, Func<string, bool> predicate)
        {
            for (int i = 0; i < items.Length; i++)
            {
                if (predicate(items[i]))
                    return i;
            }

            return -1;
        }

        private static string JoinName(string[] parts)
        {
            return string.Join(" ", parts
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p.Trim()))
                .Trim();
        }

        private static bool IsMoneyLike(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            var cleaned = text.Trim()
                .Replace("$", "")
                .Replace(",", "");

            return Regex.IsMatch(cleaned, @"^-?\d+\.\d{2}$");
        }

        private static string ExtractMoney(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return "";

            var match = Regex.Match(
                input.Replace(",", ""),
                @"-?\$?\d+\.\d{2}",
                RegexOptions.IgnoreCase);

            if (!match.Success)
                return "";

            return match.Value
                .Replace("$", "")
                .Replace("-", "")
                .Trim();
        }
        private static string ExtractCostcoId(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "";

            var lines = text
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .ToArray();

            if (lines.Length > 0 && Regex.IsMatch(lines[0], @"^\d{3,8}$"))
                return lines[0];

            var itmMatch = Regex.Match(text, @"ITM\s*(\d+)", RegexOptions.IgnoreCase);
            if (itmMatch.Success)
                return itmMatch.Groups[1].Value;

            var longNumberMatch = Regex.Match(text, @"\b\d{5,8}\b");
            return longNumberMatch.Success ? longNumberMatch.Value : "";
        }

        private static string ExtractExpiration(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "";

            var expShort = Regex.Match(
                text,
                @"\bEXP\.?\s*([0-9]{4}-[0-9]{2}-[0-9]{2})",
                RegexOptions.IgnoreCase);

            if (expShort.Success)
                return expShort.Groups[1].Value.Trim();

            var expLong = Regex.Match(
                text,
                @"Expires\s+([A-Za-z]+\s+\d{1,2},\s+\d{4})",
                RegexOptions.IgnoreCase);

            return expLong.Success ? expLong.Groups[1].Value.Trim() : "";
        }
        private static bool IsStandaloneMoney(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            var cleaned = text.Trim()
                .Replace("$", "")
                .Replace(",", "");

            return Regex.IsMatch(cleaned, @"^-?\d+\.\d{2}$");
        }
        private static string CleanPrice(string input)
        {
            return ExtractMoney(input);
        }
    }
}