// File: Scraper/WarehouseInfo.cs
using System;
using System.Collections.Generic;
using CostcoDeals.Shared.Enums;

namespace CostcoDeals.Scraper
{
    /// <summary>
    /// Maps each WarehouseLocation enum to its Costco deals URL.
    /// </summary>
    public static class WarehouseInfo
    {
        private static readonly Dictionary<WarehouseLocation, string> _warehouseUrls =
            new()
            {
                [WarehouseLocation.AB_Calgary_BeaconHill] = "https://yepsavings.com/ca-ab-calgary-nw-beacon-hill-25",
                [WarehouseLocation.AB_Edmonton_91StNW] = "https://yepsavings.com/ca-ab-edmonton-s-91-st-nw-28",
                [WarehouseLocation.BC_Langley_64Ave] = "https://yepsavings.com/ca-bc-langley--20499-64-ave-34",
                [WarehouseLocation.BC_Vancouver_Downtown] = "https://yepsavings.com/ca-bc-vancouver--vancouver-downtown-33",
                [WarehouseLocation.MB_Winnipeg_McGillivray] = "https://yepsavings.com/ca-mb-winnipeg-sw-mcgillivray-27",
                [WarehouseLocation.ON_Burlington_BrantSt] = "https://yepsavings.com/ca-on-burlington--brant-st-30",
                [WarehouseLocation.ON_London_WonderlandRd] = "https://yepsavings.com/ca-on-london-n-693-wonderland-rd-29",
                [WarehouseLocation.ON_Mississauga_3180LairdRd] = "https://yepsavings.com/ca-on-mississauga-&-oakville-s-3180-laird-rd-23",
                [WarehouseLocation.ON_Ottawa_Gloucester] = "https://yepsavings.com/ca-on-ottawa-ne-gloucester-35",
                [WarehouseLocation.ON_Toronto_WardenAve] = "https://yepsavings.com/ca-on-toronto--warden-ave-31",
                [WarehouseLocation.SK_Saskatoon_South] = "https://yepsavings.com/ca-sk-saskatoon-s-south-19",
            };

        /// <summary>
        /// Returns the deals URL for the given warehouse.
        /// </summary>
        public static string GetUrl(this WarehouseLocation warehouse)
        {
            if (_warehouseUrls.TryGetValue(warehouse, out var url))
                return url;

            throw new ArgumentException($"No URL configured for warehouse {warehouse}", nameof(warehouse));
        }
    }
}
