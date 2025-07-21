using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CostcoDeals.Models
{
    /// <summary>
    /// A single row of historical pricing scraped from the product page.
    /// </summary>
    public record ScrapedPriceHistory(
        DateTime Date,
        decimal? FinalPrice,
        decimal? Savings,
        decimal? FullPrice
    );
}
