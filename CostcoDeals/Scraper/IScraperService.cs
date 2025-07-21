using System.Collections.Generic;
using System.Threading.Tasks;
using CostcoDeals.Models;
using CostcoDeals.Shared.Enums;

namespace CostcoDeals.Scraper
{
    /// <summary>
    /// Defines a contract for scraping Costco deals from a given warehouse.
    /// </summary>
    public interface IScraperService
    {
        /// <summary>
        /// Scrapes the Costco deals page for the specified warehouse.
        /// Reports progress (0–100%).
        /// </summary>
        /// <param name="warehouse">Which warehouse to scrape.</param>
        /// <param name="progress">Optional progress reporter.</param>
        Task<IEnumerable<ScrapedProduct>> ScrapeAsync(WarehouseLocation warehouse, IProgress<int>? progress = null);
        /// <summary>
        /// After a scrape, this holds the raw “last updated” text from the page.
        /// </summary>
        string? LastUpdateText { get; }
        Task<IReadOnlyList<ScrapedPriceHistory>> ScrapePriceHistoryFor(string costcoId);

        Task CloseAsync();

    }
}
