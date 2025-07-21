using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using CostcoDeals.Models;
using CostcoDeals.Shared.Enums;
using CostcoDeals.Services;
using Microsoft.VisualBasic;
using static System.Runtime.InteropServices.JavaScript.JSType;


namespace CostcoDeals.Scraper
{
    /// <summary>
    /// Uses Playwright to scrape Costco deals for a given warehouse,
    /// reporting progress (0–100%) back to the caller.
    /// </summary>
    public class CostcoScraperService : IScraperService
    {
        private readonly ILogger<CostcoScraperService> _log;
        private readonly ProductParser _parser;
        private IBrowser _browser;
        private IPage _page;
        private IReadOnlyList<IElementHandle> _blocks;

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

            // 1) Launch browser and navigate
            var url = WarehouseInfo.GetUrl(warehouse);
            var playwright = await Playwright.CreateAsync();
            _browser = await playwright.Chromium.LaunchAsync(new() { Headless = false });
            _page = await _browser.NewPageAsync();
            await _page.GotoAsync(url, new() { WaitUntil = WaitUntilState.DOMContentLoaded });

            // 2) Capture “last updated” info
            try
            {
                var upd = await _page.QuerySelectorAsync("//div[contains(@class,'flex items-center')][contains(.,'Updates')]");
                LastUpdateText = upd is null
                    ? null
                    : (await upd.InnerTextAsync()).Trim();
                _log.LogInformation("LastUpdateText: {Text}", LastUpdateText);
            }
            catch
            {
                _log.LogWarning("Could not read last-update text");
            }

            // 3) Scroll to load all items
            await ScrollToBottomAsync(_page, progress);

            // 4) Dismiss any overlay
            await TryCloseBannerAsync(_page);

            // 5) Extract each product block
            _blocks = await _page.QuerySelectorAllAsync("//div[contains(@id,'productDesc')]");
            int total = _blocks.Count;
            var results = new List<ScrapedProduct>(total);

            for (int i = 0; i < total; i++)
            {
                var block = _blocks[i];

                // 5a) Raw text (price + title)
                var txtEl = await block.QuerySelectorAsync("div.absolute.left-0.top-0.min-w-fit");
                var raw = txtEl is null
                    ? ""
                    : (await txtEl.InnerTextAsync()).Trim();

                // 5b) Parse into DTO
                var dto = _parser.Parse(raw);
                if (dto is null) continue;

                // 5c) Image URL
                try
                {
                    var img = await block.QuerySelectorAsync("img");
                    var src = img is null
                        ? ""
                        : await img.GetAttributeAsync("src") ?? "";
                    dto.ImageUrl = src.StartsWith("http")
                        ? src
                        : $"https://yepsavings.com{src}";
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "Image extraction failed");
                }

                results.Add(dto);
            }

            _log.LogInformation("✔️ Scraped {Count} items", results.Count);
            return results;
        }

        private async Task ScrollToBottomAsync(IPage page, IProgress<int>? progress)
        {
            _log.LogInformation("🔃 Scrolling to load all deals…");
            int previousHeight = 0, sameCount = 0, tries = 0;

            while (tries < 60)
            {
                await _page.EvaluateAsync("window.scrollBy(0, document.body.scrollHeight)");
                await _page.WaitForTimeoutAsync(500);

                var height = await _page.EvaluateAsync<int>("() => document.body.scrollHeight");
                if (height == previousHeight) sameCount++; else sameCount = 0;
                previousHeight = height;

                if (sameCount >= 3) break;
                tries++;

                // approximate progress by tries (not ideal, but better than nothing)
                progress?.Report(tries * 100 / 60);
            }
            progress?.Report(100);
            _log.LogInformation("→ Finished scrolling after {Tries} loops", tries);
        }

        private async Task TryCloseBannerAsync(IPage page)
        {
            try
            {
                var btn = await _page.QuerySelectorAsync("div.subscribe svg");
                if (btn != null)
                {
                    await btn.ClickAsync();
                    await _page.WaitForTimeoutAsync(300);
                    _log.LogInformation("📭 Dismissed subscription banner");
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Failed to close banner overlay");
            }
        }

        /// <summary>
        /// Call after ScrapeAsync, passing in the CostcoId of a product you know is new.
        /// This will click its tile, grab the history table, then close the new tab.
        /// </summary>
        public async Task<IReadOnlyList<ScrapedPriceHistory>> ScrapePriceHistoryFor(string costcoId)
        {
            _log.LogInformation("Started ScrapePriceHistoryFor");

            // 1) Find the block for this CostcoId
            var index = _blocks.ToList()
                .FindIndex(b => b.InnerTextAsync().Result.Contains(costcoId));
            _log.LogInformation("Found on block {Index} with ID {Id}", index, costcoId);

            if (index < 0)
                return Array.Empty<ScrapedPriceHistory>();

            var block = _blocks[index];

            // 2–4) Click + wait for popup
            _log.LogInformation("Waiting for new tab (popup)...");
            var popupPage = await _page.RunAndWaitForPopupAsync(() => block.ClickAsync());
            _log.LogInformation("Popup opened: {Url}", popupPage.Url);

            // Prepare the list up front
            //var history = new List<ScrapedPriceHistory>();

            // 5) Scrape its history table
            _log.LogInformation("Looking for history table via XPath…");
            var tableHandle = await popupPage.QuerySelectorAsync("table");
            _log.LogInformation("Found table via CSS: {Found}", tableHandle != null);
            if (tableHandle == null)
            {
                _log.LogInformation("❌ No <table> found on detail page.");
                await popupPage.CloseAsync();
                return Array.Empty<ScrapedPriceHistory>();
            }

            // Now grab the rows under <tbody>
            _log.LogInformation("Table found but breaking on the code below");
            var rows = await tableHandle.QuerySelectorAllAsync("tbody > tr");
            _log.LogInformation("Found {Count} rows under <tbody>", rows.Count);

            if (rows.Count <= 1)
            {
                _log.LogInformation("⏭️ Only header + current deal rows; no history.");
                await popupPage.CloseAsync();
                return Array.Empty<ScrapedPriceHistory>();
            }

            // Parse from the second row onward (skip just header row)
            var history = new List<ScrapedPriceHistory>();
            foreach (var row in rows.Skip(1))
            {
                var cells = await row.QuerySelectorAllAsync("td");
                var dateText = (await cells[0].InnerTextAsync()).Trim();
                var savingsText = (await cells[2].InnerTextAsync()).Trim();
                var priceText = (await cells[3].InnerTextAsync()).Trim();

                var date = DateTime.Parse(dateText);
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
                _log.LogInformation("Parsed history row: {Date} price={Price} savings={Savings}",
                                     date, price, savings);
            }

            // 8) Close the popup tab
            _log.LogInformation("Closing popup tab");
            await popupPage.CloseAsync();

            return history;
        }
        public async Task CloseAsync()
        {
            if (_page != null) await _page.CloseAsync();
            if (_browser != null) await _browser.CloseAsync();
        }
    }
}
