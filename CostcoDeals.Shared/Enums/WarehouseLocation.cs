using System.ComponentModel;

namespace CostcoDeals.Shared.Enums
{
    /// <summary>
    /// Costco warehouse locations that can be scraped.
    /// </summary>
    public enum WarehouseLocation
    {
        [Description("Calgary, AB (Beacon Hill)")]
        AB_Calgary_BeaconHill,

        [Description("Edmonton, AB (91st St NW)")]
        AB_Edmonton_91StNW,

        [Description("Langley, BC (64th Ave)")]
        BC_Langley_64Ave,

        [Description("Vancouver, BC (Downtown)")]
        BC_Vancouver_Downtown,

        [Description("Winnipeg, MB (McGillivray)")]
        MB_Winnipeg_McGillivray,

        [Description("Burlington, ON (Brant St)")]
        ON_Burlington_BrantSt,

        [Description("London, ON (Wonderland Rd)")]
        ON_London_WonderlandRd,

        [Description("Mississauga, ON (3180 Laird Rd)")]
        ON_Mississauga_3180LairdRd,

        [Description("Ottawa, ON (Gloucester)")]
        ON_Ottawa_Gloucester,

        [Description("Toronto, ON (Warden Ave)")]
        ON_Toronto_WardenAve,

        [Description("Saskatoon, SK (South)")]
        SK_Saskatoon_South
    }
}
