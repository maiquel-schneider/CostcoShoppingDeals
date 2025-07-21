namespace CostcoDeals.Models
{
    /// <summary>
    /// Raw values extracted from the Costco deals page, ready for persistence.
    /// </summary>
    public class ScrapedProduct
    {
        public string CostcoId { get; set; } = "";
        public string Name { get; set; } = "";
        public string? FullPrice { get; set; }
        public string? Discount { get; set; }
        public string? FinalPrice { get; set; }
        public string? ExpirationDate { get; set; }
        public string? ImageUrl { get; set; }
        public string? LastUpdatedText { get; set; }
    }
}
