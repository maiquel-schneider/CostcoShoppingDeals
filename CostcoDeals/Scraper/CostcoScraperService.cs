using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using CostcoDeals.Models;
using CostcoDeals.Shared.Enums;
using CostcoDeals.Services;

namespace CostcoDeals.Scraper
{
    /// <summary>
    /// Uses Playwright to scrape Costco deals for a given warehouse,
    /// reporting progress (0–100%) back to the caller.
    /// </summary>
    public class CostcoScraperService : IScraperService
    {
        private readonly ILogger<CostcoScraperService> _log;
        private readonly ProductParser _parser; // kept for DI compatibility / fallback
        private IPlaywright? _playwright;
        private IBrowser? _browser;
        private IPage? _page;
        private IReadOnlyList<IElementHandle> _blocks = Array.Empty<IElementHandle>();

        public string? LastUpdateText { get; private set; }

        public CostcoScraperService(
            ILogger<CostcoScraperService> log,
            ProductParser parser)
        {
            _log = log;
            _parser = parser;
        }

        public async Task<IEnumerable<ScrapedProduct>> ScrapeAsync(
            WarehouseLocation warehouse,
            IProgress<int>? progress = null)
        {
            _log.LogInformation("▶️ Starting scrape for {Warehouse}", warehouse);

            var url = WarehouseInfo.GetUrl(warehouse);

            _playwright = await Playwright.CreateAsync();
            _browser = await _playwright.Chromium.LaunchAsync(new() { Headless = false });
            _page = await _browser.NewPageAsync();

            await _page.GotoAsync(url, new() { WaitUntil = WaitUntilState.DOMContentLoaded });
            await _page.WaitForTimeoutAsync(1500);

            // 1) Capture "last updated" text
            try
            {
                var upd = await _page.QuerySelectorAsync("xpath=//*[contains(normalize-space(.), 'Updates for ')]");
                LastUpdateText = upd is null
                    ? null
                    : (await upd.InnerTextAsync()).Trim();

                _log.LogInformation("LastUpdateText: {Text}", LastUpdateText ?? "<null>");
            }
            catch (Exception ex)
            {
                LastUpdateText = null;
                _log.LogWarning(ex, "Could not read last-update text");
            }

            // 2) Scroll through the page and capture each card while it is hydrated.
            // YepSavings leaves a placeholder for every deal in the DOM, but only fills
            // cards that have entered the viewport.
            var scrapedByCostcoId = new Dictionary<string, ScrapedProduct>();
            var capturedDealIndexes = new HashSet<string>();
            await ScrollToBottomAsync(
                _page,
                progress,
                () => CaptureHydratedDealsAsync(_page, scrapedByCostcoId, capturedDealIndexes));

            // 3) Dismiss overlay/banner if present
            await TryCloseBannerAsync(_page);

            // 4) Find every indexed deal card. The site changes card CSS classes while
            // loading, but keeps data-deal-index on each deal container.
            const string productCardXPath =
                "xpath=//*[@data-deal-index]";
            _blocks = await _page.QuerySelectorAllAsync(productCardXPath);
            int total = _blocks.Count;

            _log.LogInformation("Found {Count} indexed deal cards", total);

            // Capture one final time in case the last scroll hydrated another batch.
            await CaptureHydratedDealsAsync(_page, scrapedByCostcoId, capturedDealIndexes);

            var results = scrapedByCostcoId.Values.ToList();
            _log.LogInformation("Scraped {Count} items", results.Count);
            return results;
        }

        private async Task CaptureHydratedDealsAsync(
            IPage page,
            IDictionary<string, ScrapedProduct> scrapedByCostcoId,
            ISet<string> capturedDealIndexes)
        {
            // The [normalize-space()] predicate excludes empty lazy-loading placeholders.
            var hydratedBlocks = await page.QuerySelectorAllAsync(
                "xpath=//*[@data-deal-index][normalize-space()]");

            foreach (var block in hydratedBlocks)
            {
                var dealIndex = await block.GetAttributeAsync("data-deal-index");
                if (string.IsNullOrWhiteSpace(dealIndex) || capturedDealIndexes.Contains(dealIndex))
                    continue;

                try
                {
                    var dto = await ExtractProductAsync(block);
                    if (dto is null || string.IsNullOrWhiteSpace(dto.CostcoId))
                        continue;

                    capturedDealIndexes.Add(dealIndex);
                    scrapedByCostcoId[dto.CostcoId] = dto;
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "Failed to extract indexed deal card {DealIndex}", dealIndex);
                }
            }
        }

        private async Task<ScrapedProduct?> ExtractProductAsync(IElementHandle block)
        {
            var linkEl = await block.QuerySelectorAsync(
                "xpath=.//a[contains(@href, '/products/') and .//img]");
            var href = linkEl is null ? "" : await linkEl.GetAttributeAsync("href") ?? "";
            var dealIndex = await block.GetAttributeAsync("data-deal-index") ?? "<unknown>";

            // Prefer the existing overlay data when it is available.
            var overlayPanel = await block.QuerySelectorAsync(
                "xpath=(.//a[contains(@href, '/products/') and .//img]//div[contains(@class,'shadow') and contains(@class,'bg-[#dcdfda]')])[1]");

            string raw;
            if (overlayPanel != null)
            {
                // Read only leaf divs from the overlay, so the parser gets one logical line per field.
                var leafDivs = await overlayPanel.QuerySelectorAllAsync("xpath=.//div[not(descendant::div)]");
                var rawParts = new List<string>();

                foreach (var leaf in leafDivs)
                {
                    var text = (await leaf.InnerTextAsync()).Trim();
                    text = Regex.Replace(text, @"\s+", " ").Trim();

                    if (!string.IsNullOrWhiteSpace(text))
                        rawParts.Add(text);
                }

                raw = string.Join("\n", rawParts);
            }
            else
            {
                // Newer card variants do not always contain the old overlay/link markup.
                // Their visible card text still has the same logical values the parser expects.
                raw = string.Join("\n", (await block.InnerTextAsync())
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => Regex.Replace(line.Trim(), @"\s+", " ").Trim())
                    .Where(line => !string.IsNullOrWhiteSpace(line)));
            }

            if (string.IsNullOrWhiteSpace(raw))
            {
                _log.LogDebug("Skipping empty indexed deal card {DealIndex}", dealIndex);
                return null;
            }

            var dto = _parser.Parse(raw);
            if (dto == null)
            {
                _log.LogWarning("Skipping indexed deal card {DealIndex} because the parser returned null. Href={Href}. Raw={Raw}", dealIndex, href, raw);
                return null;
            }

            // Fallback CostcoId from href if parser missed it
            if (string.IsNullOrWhiteSpace(dto.CostcoId))
            {
                dto.CostcoId = ExtractCostcoId(raw, href);
            }

            if (string.IsNullOrWhiteSpace(dto.CostcoId))
            {
                _log.LogWarning("Skipping product because CostcoId could not be extracted. Href={Href}. Raw={Raw}", href, raw);
                return null;
            }

            // Fallback name from image alt if needed
            if (string.IsNullOrWhiteSpace(dto.Name))
            {
                try
                {
                    var imgEl = await block.QuerySelectorAsync("img");
                    var alt = imgEl is null ? "" : await imgEl.GetAttributeAsync("alt") ?? "";
                    dto.Name = Regex.Replace(alt, @"^Costco\s+", "", RegexOptions.IgnoreCase).Trim();
                }
                catch
                {
                    // no-op
                }
            }

            // Image
            try
            {
                var imgEl = await block.QuerySelectorAsync("img");
                var src = imgEl is null ? "" : await imgEl.GetAttributeAsync("src") ?? "";

                dto.ImageUrl = src.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                    ? src
                    : $"https://yepsavings.com{src}";
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Image extraction failed for CostcoId {CostcoId}", dto.CostcoId);
            }

            _log.LogInformation(
                "Parsed product {CostcoId}: {Name} → Final={FinalPrice}, Full={FullPrice}, Discount={Discount}, Exp={ExpirationDate}",
                dto.CostcoId ?? "",
                dto.Name ?? "",
                dto.FinalPrice ?? "",
                dto.FullPrice ?? "",
                dto.Discount ?? "",
                dto.ExpirationDate ?? "");

            return dto;
        }

        private async Task ScrollToBottomAsync(
            IPage page,
            IProgress<int>? progress,
            Func<Task>? captureHydratedDeals = null)
        {
            _log.LogInformation("🔃 Scrolling to load all deals…");

            int previousHeight = 0;
            int sameCount = 0;
            int tries = 0;

            const string siteProgressXPath =
                "xpath=//div[contains(@class,'transition-[width]') and contains(@style,'width:')]";

            await page.WaitForTimeoutAsync(6000);
            if (captureHydratedDeals != null)
                await captureHydratedDeals();

            while (tries < 500)
            {
                // Do not jump to the end: the site hydrates a deal card only after it
                // enters the viewport. A viewport-sized step triggers that hydration.
                await page.EvaluateAsync(
                    "() => window.scrollBy(0, Math.max(400, Math.floor(window.innerHeight * 0.8)))");
                await page.WaitForTimeoutAsync(750);

                if (captureHydratedDeals != null)
                    await captureHydratedDeals();

                var height = await page.EvaluateAsync<int>("() => document.body.scrollHeight");
                var atBottom = await page.EvaluateAsync<bool>(
                    "() => window.scrollY + window.innerHeight >= document.body.scrollHeight - 2");

                if (atBottom && height == previousHeight)
                    sameCount++;
                else
                    sameCount = 0;

                previousHeight = height;

                if (sameCount >= 10)
                    break;

                tries++;

                try
                {
                    var el = await page.QuerySelectorAsync(siteProgressXPath);
                    if (el != null)
                    {
                        var style = await el.GetAttributeAsync("style") ?? "";
                        var match = Regex.Match(style, @"width:\s*([\d.]+)%");
                        if (match.Success &&
                            decimal.TryParse(match.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var pctDecimal))
                        {
                            var percent = Math.Clamp((int)Math.Round(pctDecimal), 0, 100);
                            progress?.Report(percent);
                            _log.LogInformation("→ Scrolling a Total of {Percent}%", percent);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _log.LogDebug(ex, "Could not read on-page progress bar while scrolling");
                }
            }

            progress?.Report(100);
            _log.LogInformation("→ Finished scrolling after {Tries} loops", tries);
        }

        private async Task TryCloseBannerAsync(IPage page)
        {
            try
            {
                // old close button
                var oldCloseBtn = await page.QuerySelectorAsync("button.fc-close[aria-label='Close']");
                if (oldCloseBtn != null)
                {
                    await oldCloseBtn.ClickAsync();
                    await page.WaitForTimeoutAsync(300);
                    _log.LogInformation("Banner closed using old selector");
                    return;
                }

                // svg/title close button
                var svgClose = page.Locator(
                    "xpath=//*[name()='svg'][.//*[name()='title' and normalize-space()='banner_ic_del_nor@2x']]");

                if (await svgClose.CountAsync() > 0)
                {
                    await svgClose.First.ClickAsync();
                    await page.WaitForTimeoutAsync(300);
                    _log.LogInformation("Banner closed using svg selector");
                    return;
                }

                _log.LogInformation("Banner not present");
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Failed to close banner overlay");
            }
        }

        /// <summary>
        /// Uses the card block to open the detail popup and scrape its price history table.
        /// </summary>
        public async Task<IReadOnlyList<ScrapedPriceHistory>> ScrapePriceHistoryFor(string costcoId)
        {
            if (_page == null || _blocks.Count == 0)
                return Array.Empty<ScrapedPriceHistory>();

            _log.LogInformation("Started ScrapePriceHistoryFor for {CostcoId}", costcoId);

            IElementHandle? targetBlock = null;

            foreach (var block in _blocks)
            {
                try
                {
                    var text = await block.InnerTextAsync();
                    if (!string.IsNullOrWhiteSpace(text) &&
                        text.Contains(costcoId, StringComparison.OrdinalIgnoreCase))
                    {
                        targetBlock = block;
                        break;
                    }
                }
                catch
                {
                    // ignore single block failures while searching
                }
            }

            if (targetBlock == null)
            {
                _log.LogInformation("No block found for CostcoId {CostcoId}", costcoId);
                return Array.Empty<ScrapedPriceHistory>();
            }

            _log.LogInformation("Waiting for new tab (popup) for CostcoId {CostcoId}...", costcoId);

            IPage popupPage;

            try
            {
                var clickTarget =
                    await targetBlock.QuerySelectorAsync("xpath=.//a[contains(@href,'/products/')]")
                    ?? targetBlock;

                popupPage = await _page.RunAndWaitForPopupAsync(() => clickTarget.ClickAsync());
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Failed to open popup for CostcoId {CostcoId}", costcoId);
                return Array.Empty<ScrapedPriceHistory>();
            }

            _log.LogInformation("Popup opened: {Url}", popupPage.Url);

            try
            {
                await popupPage.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
                await popupPage.WaitForTimeoutAsync(1200);

                // Find the Price History section
                var historyContainer = await popupPage.QuerySelectorAsync(
                    "xpath=//div[normalize-space()='Price History']/ancestor::div[contains(@style,'border')][1]");

                _log.LogInformation("Found history container: {Found}", historyContainer != null);

                if (historyContainer == null)
                {
                    _log.LogInformation("❌ No Price History container found on detail page.");
                    await popupPage.CloseAsync();
                    return Array.Empty<ScrapedPriceHistory>();
                }

                // Get all data rows inside the history section
                var rows = await historyContainer.QuerySelectorAllAsync(
                    "xpath=.//div[contains(@class,'flex') and contains(@class,'items-center')][.//span]");

                _log.LogInformation("Found {Count} possible rows in history container", rows.Count);

                if (rows.Count == 0)
                {
                    _log.LogInformation("⏭️ No history rows found.");
                    await popupPage.CloseAsync();
                    return Array.Empty<ScrapedPriceHistory>();
                }

                var history = new List<ScrapedPriceHistory>();

                foreach (var row in rows)
                {
                    var spans = await row.QuerySelectorAllAsync("span");

                    // Skip header row and anything that does not look like a real data row
                    if (spans.Count < 4)
                        continue;

                    var dateText = (await spans[0].InnerTextAsync()).Trim();
                    var expText = (await spans[1].InnerTextAsync()).Trim();   // not used now, but available
                    var savingsText = (await spans[2].InnerTextAsync()).Trim();
                    var priceText = (await spans[3].InnerTextAsync()).Trim();

                    // Skip the header labels
                    if (dateText.Equals("Updated", StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Parse date
                    if (!DateTime.TryParse(dateText, CultureInfo.InvariantCulture, out var date))
                    {
                        _log.LogInformation("Skipping row because date could not be parsed: {DateText}", dateText);
                        continue;
                    }

                    decimal? savings = (savingsText == "-" || savingsText == "–")
                        ? null
                        : PriceParsingHelper.ParsePrice(savingsText, _log);

                    decimal? price = (priceText == "-" || priceText == "–")
                        ? null
                        : PriceParsingHelper.ParsePrice(priceText, _log);

                    decimal? fullPrice = (price.HasValue && savings.HasValue)
                        ? price.Value + savings.Value
                        : null;

                    history.Add(new ScrapedPriceHistory(date, price, savings, fullPrice));

                    _log.LogInformation(
                        "Parsed history row: {Date} exp={Exp} price={Price} savings={Savings}",
                        date,
                        expText,
                        price,
                        savings);
                }

                await popupPage.CloseAsync();
                return history;
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Failed scraping popup price history for CostcoId {CostcoId}", costcoId);
                try { await popupPage.CloseAsync(); } catch { }
                return Array.Empty<ScrapedPriceHistory>();
            }
        }

        public async Task CloseAsync()
        {
            try
            {
                if (_page != null)
                {
                    await _page.CloseAsync();
                    _page = null;
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Error while closing page");
            }

            try
            {
                if (_browser != null)
                {
                    await _browser.CloseAsync();
                    _browser = null;
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Error while closing browser");
            }

            try
            {
                _playwright?.Dispose();
                _playwright = null;
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Error while disposing Playwright");
            }
        }

        private static string ExtractCostcoId(string titleRaw, string href)
        {
            if (!string.IsNullOrWhiteSpace(titleRaw))
            {
                var itmMatch = Regex.Match(titleRaw, @"ITM\s*(\d+)", RegexOptions.IgnoreCase);
                if (itmMatch.Success)
                    return itmMatch.Groups[1].Value;
            }

            if (!string.IsNullOrWhiteSpace(href))
            {
                var hrefMatch = Regex.Match(href, @"-(\d+)(?:\?|$)");
                if (hrefMatch.Success)
                    return hrefMatch.Groups[1].Value;
            }

            return "";
        }

        private static string ExtractMoneyValue(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return "";

            var cleaned = raw.Replace(",", "");
            var match = Regex.Match(cleaned, @"(\d+(?:\.\d{1,2})?)");
            return match.Success ? match.Groups[1].Value : "";
        }
    }
}
