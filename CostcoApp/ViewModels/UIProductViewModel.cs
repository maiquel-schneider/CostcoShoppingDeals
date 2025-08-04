using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using CostcoDeals.Shared.Enums;
using CostcoDeals.Shared.Utilities;
using CostcoDeals.Data;

namespace CostcoApp.ViewModels
{
    /// <summary>
    /// ViewModel representing one Product row in the DataGrid.
    /// </summary>
    public class UIProductViewModel : ViewModelBase
    {
        // Immutable core fields
        public int Id { get; }
        public string CostcoId { get; }
        private string _name;
        public string Name
        {
            get => _name;
            set => SetField(ref _name, value);
        }
        public string ImageUrl { get; }

        // Latest‐scrape values (writable if needed)
        public string FullPrice { get; set; }
        public string Discount { get; set; }
        private string _finalPrice;
        public string FinalPrice
        {
            get => _finalPrice;
            set => SetField(ref _finalPrice, value);
        }
        public string LastPriceFound { get; set; }

        // Persistent settings
        private ProductCategory _category;
        public ProductCategory Category
        {
            get => _category;
            set => SetAndRaise(ref _category, value, CategoryChanged);
        }

        private Preference _preference;
        public Preference Preference
        {
            get => _preference;
            set => SetAndRaise(ref _preference, value, PreferenceChanged);
        }

        private bool _isInShoppingList;
        public bool IsInShoppingList
        {
            get => _isInShoppingList;
            set => SetAndRaise(ref _isInShoppingList, value, ShoppingListToggled);
        }

        // Price‐alert state
        public PriceAlertType AlertType { get; private set; }
        public string AlertText { get; private set; }

        // Static lookup for ComboBoxes
        public static readonly List<KeyValuePair<ProductCategory, string>> SortedCategories =
            Enum.GetValues<ProductCategory>()
                .Select(c => new KeyValuePair<ProductCategory, string>(c, EnumHelper.GetDescription(c)))
                .OrderBy(kv => kv.Value)
                .ToList();

        public static readonly List<KeyValuePair<Preference, string>> SortedPreferences =
            Enum.GetValues<Preference>()
                .Select(p => new KeyValuePair<Preference, string>(p, EnumHelper.GetDescription(p)))
                .ToList();

        // Events for upstream persistence
        public event PropertyChangedEventHandler? PropertyChanged;
        public event Action<UIProductViewModel, ProductCategory>? CategoryChanged;
        public event Action<UIProductViewModel, Preference>? PreferenceChanged;
        public event Action<UIProductViewModel, bool>? ShoppingListToggled;

        /// <summary>
        /// Constructor For our UI view and data
        /// </summary>
        public UIProductViewModel(
            int id,
            string costcoId,
            string name,
            string fullPrice,
            string discount,
            string finalPrice,
            string lastPriceFound,
            string imageUrl,
            ProductCategory category,
            Preference preference,
            bool isInShoppingList = false)
        {
            Id = id;
            CostcoId = costcoId;
            Name = name;
            FullPrice = fullPrice;
            Discount = discount;
            FinalPrice = finalPrice;
            LastPriceFound = lastPriceFound;
            ImageUrl = imageUrl;
            _category = category;
            _preference = preference;
            _isInShoppingList = isInShoppingList;
        }

        // Method to computed Price Alerts
        public void ComputePriceAlert(IEnumerable<PriceHistory> histories)
        {
            var prices = histories
                .Where(h => h.FinalPrice.HasValue)
                .OrderBy(h => h.ScrapedAt)
                .Select(h => h.FinalPrice!.Value)
                .ToList();
            // 1) If there are no history of prices set it to Empty
            if (prices.Count < 2)
            {
                SetAlert(PriceAlertType.None, string.Empty);
                return;
            }

            var previous = prices[^2];
            var current = prices[^1];

            // 2) If price has raised from previous time
            if (current > previous)
                SetAlert(PriceAlertType.Increased, "⚠ Price has gone up");
            // 3) If price has dropped or dropped more than 20%
            else
            {
                var drop = (previous - current) / previous;
                if (drop >= 0.20m)
                    SetAlert(PriceAlertType.HotDeal, $"🔥 Hot deal!{Environment.NewLine}{(previous - current) / previous:P2} Cheaper!");  
                else if (drop > 0)
                    SetAlert(PriceAlertType.Decreased, "✔ Price has gone down");
                else
                {
                    SetAlert(PriceAlertType.None, string.Empty);
                }
            }
            // 4) If price is an All-Time-Low considering at least 4 entries
            if (prices.Count >= 4 && current < prices.Min())
                SetAlert(PriceAlertType.AllTimeLow, $"🏆 All-time low!{Environment.NewLine}Based on {prices.Count} deals!");
        }
        private void SetAlert(PriceAlertType type, string text)
        {
            AlertType = type;
            AlertText = text;
            OnPropertyChanged(nameof(AlertType));
            OnPropertyChanged(nameof(AlertText));
        }
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        private void SetAndRaise<T>(
            ref T field,
            T newValue,
            Action<UIProductViewModel, T>? evt,
            [CallerMemberName] string? name = null)
        {
            if (!EqualityComparer<T>.Default.Equals(field, newValue))
            {
                field = newValue;
                OnPropertyChanged(name);
                evt?.Invoke(this, newValue);
            }
        }
    }
}
