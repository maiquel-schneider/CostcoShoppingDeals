using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CostcoDeals.Shared.Enums;

namespace CostcoDeals.Data
{
    public class Product
    {
        /// <summary>
        /// Primary key for the Product entity.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Unique identifier of the product on the Costco website.
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string CostcoId { get; set; } = null!;

        /// <summary>
        /// Display name of the product.
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = null!;

        /// <summary>
        /// Category classification of the product.
        /// </summary>
        public ProductCategory Category { get; set; } = ProductCategory.Unknown;

        /// <summary>
        /// Color-coded user preference for purchasing the product.
        /// </summary>
        public Preference Preference { get; set; } = Preference.None;

        /// <summary>
        /// FK to the warehouse where this product lives.
        /// </summary>
        public int WarehouseLocationId { get; set; }

        /// <summary>
        /// All historical price records for this product.
        /// </summary>
        public ICollection<PriceHistory> PriceHistories { get; set; } = new List<PriceHistory>();
    }
}
