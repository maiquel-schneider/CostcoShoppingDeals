using System.ComponentModel;

namespace CostcoDeals.Shared.Enums
{
    /// <summary>
    /// Color-coded user preference categories.
    /// </summary>
    public enum Preference
    {
        [Description("None")]
        None = 0,
        [Description("Always Buy")]
        AlwaysBuy,
        [Description("Usually Buy")]
        UsuallyBuy,
        [Description("Sometimes Buy")]
        SometimesBuy,
        [Description("Only If Huge Sale")]
        OnlyIfHugeSale,
        [Description("Never Buy")]
        NeverBuy
    }
}
