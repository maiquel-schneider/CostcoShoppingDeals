using System.ComponentModel;

namespace CostcoDeals.Shared.Enums
{
    /// <summary>
    /// Categories of Costco products.
    /// </summary>
    public enum ProductCategory
    {
        [Description("Unknown")]
        Unknown = 0,
        [Description("Produce")]
        Produce,
        [Description("Bakery")]
        Bakery,
        [Description("Bread")]
        Bread,
        [Description("Meat, Poultry & Seafood")]
        MeatPoultryAndSeafood,
        [Description("Prepared Meals")]
        PreparedMeals,
        [Description("Bacon")]
        Bacon,
        [Description("Sausage")]
        Sausage,
        [Description("Dairy & Eggs (non-cheese)")]
        NonCheeseDairyAndEggs,
        [Description("Cheese")]
        Cheese,
        [Description("Sliced Cheese")]
        SlicedCheese,
        [Description("Fridge Items")]
        FridgeItems,
        [Description("Pizza")]
        Pizza,
        [Description("Frozen Bites")]
        FrozenBites,
        [Description("Burgers")]
        Burger,
        [Description("Pre-Cooked Meat")]
        PreCookedMeat,
        [Description("Pasta, Rice & Grains")]
        PastaRiceAndGrains,
        [Description("Canned & Jarred Goods")]
        CannedAndJarred,
        [Description("Baking Supplies")]
        BakingSupplies,
        [Description("Sweet Snacks & Candies")]
        SweetSnacksAndCandies,
        [Description("Breakfast Items")]
        BreakfastItems,
        [Description("Coffee")]
        Coffee,
        [Description("Condiments & Sauces")]
        CondimentsAndSauce,
        [Description("Spices & Seasonings")]
        SpicesAndSeasoning,
        [Description("Oils & Vinegars")]
        OilsAndVinegars,
        [Description("Cereal & Protein Bars")]
        CerealAndProteinBars,
        [Description("Beverages")]
        Beverages,
        [Description("Soups & Broth")]
        SoupsAndBroth,
        [Description("Ice Cream")]
        IceCream,
        [Description("Lights & Lanterns")]
        LightsAndLanterns,
        [Description("Plants")]
        Plants,
        [Description("Cleaning Products")]
        CleaningProducts,
        [Description("Laundry Supplies")]
        LaundrySupplies,
        [Description("Paper Goods")]
        PaperGoods,
        [Description("Trash & Storage")]
        TrashAndStorage,
        [Description("Personal Care")]
        PersonalCare,
        [Description("Baby & Kids Supplies")]
        BabyAndKidsSupplies,
        [Description("Pet Supplies")]
        PetSupplies,
        [Description("Medicine")]
        Medicine,
        [Description("Vitamins")]
        Vitamins,
        [Description("Sports & Gym Equipment")]
        SportsAndGym,
        [Description("Games")]
        Games,
        [Description("Stuffed Animals")]
        StuffedAnimals,
        [Description("Diapers")]
        Diapers,
        [Description("Light Bulbs, Batteries & Misc.")]
        LightBulbsBatteriesAndOthers,
        [Description("Hardware & Misc.")]
        HardwareAndOthers,
        [Description("Bed, Bath & Beyond")]
        BedBathAndBeyond,
        [Description("Kitchen Utensils")]
        KitchenUtensils,
        [Description("Appliances")]
        Appliances,
        [Description("Hiking & Camping")]
        HikingCamping,
        [Description("Phones & Tablets")]
        PhonesTablets,
        [Description("Notebooks, PCs & Monitors")]
        NotebooksPcsMonitor,
        [Description("Sound Bars")]
        SoundBars,
        [Description("TVs")]
        TVs,
        [Description("Jewelry")]
        Jewelry,
        [Description("Mattresses")]
        Mattresses,
        [Description("Massage Equipment")]
        MassageEquipment,
        [Description("Sunglasses")]
        Sunglasses,
        [Description("Women's Clothes")]
        WomensClothes,
        [Description("Kids' Clothes")]
        KidsClothes,
        [Description("Men's Clothes")]
        MensClothes,
        [Description("Carpets & Rugs")]
        CarpetsRugs,
        [Description("Shoes & Boots")]
        ShoesBoots,
        [Description("Furniture")]
        Furniture,
        [Description("Car Supplies & Others")]
        CarsSupplies,
        [Description("Books")]
        Books,
        [Description("Outdoor & Garden")]
        OutdoorAndGarden,
        [Description("Smart Home & Electronics")]
        SmartHomeAndElectronics,
        [Description("Home Decor, Acessories & Others")]
        HomeDecorAndAccessories,
        [Description("Travel Accessories")]
        TravelAndAccessories,
        [Description("Salty Snacks")]
        SaltySnack
    }
}