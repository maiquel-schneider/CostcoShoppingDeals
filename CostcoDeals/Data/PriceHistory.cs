using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CostcoDeals.Data
{
    public class PriceHistory
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// FK to the <see cref="Product"/> this history belongs to.
        /// </summary>
        [Required]
        [ForeignKey(nameof(Product))]
        public int ProductId { get; set; }
        public Product Product { get; set; } = null!;

        /// <summary>
        /// Timestamp (UTC) when this price was scraped.
        /// </summary>
        public DateTime ScrapedAt { get; set; }

        /// <summary>
        /// The regular (full) price at scrape time, if available.
        /// </summary>
        public decimal? FullPrice { get; set; }

        /// <summary>
        /// Discount amount found at scrape time, if any.
        /// </summary>
        public decimal? Discount { get; set; }

        /// <summary>
        /// Final price after applying discount, if available.
        /// </summary>
        public decimal? FinalPrice { get; set; }

        /// <summary>
        /// Default constructor sets the scrape timestamp to now (UTC).
        /// </summary>
        public PriceHistory()
        {
            ScrapedAt = DateTime.UtcNow;
        }
    }
}
