using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using CostcoApp.Helpers;
using CostcoApp.ViewModels;
using CostcoDeals.Data;
using CostcoDeals.Models;
using CostcoDeals.Scraper;
using CostcoDeals.Services;
using CostcoDeals.Shared.Enums;
using CostcoDeals.Shared.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CostcoApp.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly ProductManager _manager;
        private readonly MainDatabase _db;
        private readonly IScraperService _scraper;


        public ObservableCollection<UIProductViewModel> Products { get; }
            = new ObservableCollection<UIProductViewModel>();

        private double _progress;
        public double Progress
        {
            get => _progress;
            private set => SetField(ref _progress, value);
        }

        public ICommand ScrapeCommand { get; }

        // 1) Warehouse picker
        public IEnumerable<WarehouseLocation> WarehouseList { get; }
        private WarehouseLocation _selectedWarehouse;
        public WarehouseLocation SelectedWarehouse
        {
            get => _selectedWarehouse;
            set
            {
                SetField(ref _selectedWarehouse, value);
            }
        }

        // 2) Filters
        private string _nameFilter = "";
        public string NameFilter
        {
            get => _nameFilter;
            set
            {
                SetField(ref _nameFilter, value);
                ProductsView.Refresh();
            }
        }
        public IList<KeyValuePair<ProductCategory?, string>> CategoryFilterList { get; }
        private ProductCategory? _selectedCategoryFilter = null;
        public ProductCategory? SelectedCategoryFilter
        {
            get => _selectedCategoryFilter;
            set
            {
                SetField(ref _selectedCategoryFilter, value);
                ProductsView.Refresh();
            }
        }
        public IList<KeyValuePair<Preference?, string>> PreferenceFilterList { get; }
        private Preference? _selectedPreferenceFilter = null;
        public Preference? SelectedPreferenceFilter
        {
            get => _selectedPreferenceFilter;
            set
            {
                SetField(ref _selectedPreferenceFilter, value);
                ProductsView.Refresh();
            }
        }

        // 3) Your CollectionView for filtering
        public ICollectionView ProductsView { get; }

        // 4) Shopping list tab
        public ObservableCollection<UIProductViewModel> ShoppingList { get; }
            = new ObservableCollection<UIProductViewModel>();

        // 5) Last‐update text
        private string _lastUpdateText = "";
        public string LastUpdateText
        {
            get => _lastUpdateText;
            set => SetField(ref _lastUpdateText, value);
        }

        // 6) Live progress label for Deals and History Prices
        private string _progressText = "";
        public string ProgressText
        {
            get => _progressText;
            private set => SetField(ref _progressText, value);
        }

        private double _historyProgress;
        public double HistoryProgress
        {
            get => _historyProgress;
            private set => SetField(ref _historyProgress, value);
        }

        private string _historyText = "";
        public string HistoryText
        {
            get => _historyText;
            private set => SetField(ref _historyText, value);
        }

        // 7) Final summary label
        private string _summaryText = "";
        public string SummaryText
        {
            get => _summaryText;
            private set => SetField(ref _summaryText, value);
        }

        public MainViewModel(ProductManager manager, MainDatabase db, IScraperService scraper)
        {
            _manager = manager;
            _db = db;
            _scraper = scraper;

            // Populate the warehouse list from the enum
            WarehouseList = Enum.GetValues<WarehouseLocation>();
            _selectedWarehouse = WarehouseList.First();

            // Initialize filter lists (with an “All” at the front)
            CategoryFilterList = new[] {
                new KeyValuePair<ProductCategory?,string>(null, "All")
            }
            .Concat(Enum.GetValues<ProductCategory>()
                       .Select(c => new KeyValuePair<ProductCategory?, string>(
                           c, EnumHelper.GetDescription(c))))
            .ToList();

            PreferenceFilterList = new[] {
                new KeyValuePair<Preference?,string>(null, "All")
            }
            .Concat(Enum.GetValues<Preference>()
                       .Select(p => new KeyValuePair<Preference?, string>(
                           p, EnumHelper.GetDescription(p))))
            .ToList();


            // Attempt to load last scrape from disk cache
            if (File.Exists(CacheFilePath))
            {
                try
                {
                    var json = File.ReadAllText(CacheFilePath);
                    _lastScraped = JsonSerializer.Deserialize<List<ScrapedProduct>>(json) ?? new List<ScrapedProduct>();
                }
                catch
                {
                    _lastScraped = Array.Empty<ScrapedProduct>();
                }
            }
            // Load the raw product VMs
            LoadProductsFromLastScrape();

            // Wrap Products in a CollectionView so we can filter
            ProductsView = CollectionViewSource.GetDefaultView(Products);
            ProductsView.Filter = o =>
            {
                var vm = (UIProductViewModel)o;
                if (!string.IsNullOrWhiteSpace(NameFilter) &&
                    !vm.Name.Contains(NameFilter, StringComparison.OrdinalIgnoreCase))
                    return false;
                if (SelectedCategoryFilter.HasValue
                    && vm.Category != SelectedCategoryFilter.Value)
                    return false;
                if (SelectedPreferenceFilter.HasValue
                    && vm.Preference != SelectedPreferenceFilter.Value)
                    return false;
                return true;
            };

            ScrapeCommand = new RelayCommand(async _ => await RunScrapeAsync(),
                                             _ => !IsBusy);
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                SetField(ref _isBusy, value);
                (ScrapeCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        /// <summary>
        /// Merges the raw scrape (_lastScraped) with DB metadata into the Products collection.
        /// </summary>
        private void LoadProductsFromLastScrape()
        {
            Products.Clear();
            ShoppingList.Clear();

            // Pull in all products for DB lookup (history + category + pref)
            var dbProducts = _db.Products
                .Include(p => p.PriceHistories)
                .ToList();

            foreach (var scraped in _lastScraped)
            {
                // 1) Find the matching saved product, if any
                var saved = dbProducts
                    .SingleOrDefault(p =>
                        p.CostcoId == scraped.CostcoId &&
                        p.WarehouseLocationId == (int)SelectedWarehouse);
                
                // 2) Build List of price history
                var histories = (saved?.PriceHistories ?? new List<PriceHistory>())
                    .Where(h => h.FinalPrice.HasValue)
                    .OrderByDescending(h => h.ScrapedAt)
                    .ToList();

                // 3) Compute lastPrice from the second‑most recent history entry that has a non‑null price
                var previousEntry = histories.Skip(1)
                        .Where(h => h.FinalPrice.HasValue)                                  
                        .FirstOrDefault();                       
                
                string lastPrice = previousEntry?.FinalPrice
                        .Value
                        .ToString("F2")                        
                        ?? "Not Found";                        

                // 4) Build the VM:
                var vm = new UIProductViewModel(
                    id: saved?.Id ?? 0,
                    costcoId: scraped.CostcoId,
                    name: scraped.Name,
                    fullPrice: scraped.FullPrice ?? "",
                    discount: scraped.Discount ?? "",
                    finalPrice: scraped.FinalPrice ?? "",
                    lastPriceFound: lastPrice,
                    imageUrl: scraped.ImageUrl ?? "",
                    category: saved?.Category ?? ProductCategory.Unknown,
                    preference: saved?.Preference ?? Preference.None,
                    isInShoppingList: (saved?.Preference ?? Preference.None) == Preference.AlwaysBuy
                );

                // 5) Compute alert badge
                vm.ComputePriceAlert(histories);

                // 6) Wire up its checkbox changes
                SubscribeRowVm(vm);

                // 7) Add to the grid
                Products.Add(vm);

                // 8) Seed the shopping-list tab
                if (vm.IsInShoppingList)
                    ShoppingList.Add(vm);
            }
        }

        private void SubscribeRowVm(UIProductViewModel vm)
        {
            vm.CategoryChanged += async (_, newCat) =>
            {
                var prod = _db.Products.Find(vm.Id);
                if (prod != null)
                {
                    prod.Category = newCat;
                    await _db.SaveChangesAsync();
                }
            };
            vm.PreferenceChanged += async (_, newPref) =>
            {
                var prod = _db.Products.Find(vm.Id);
                if (prod != null)
                {
                    prod.Preference = newPref;
                    await _db.SaveChangesAsync();
                }
            };
            vm.ShoppingListToggled += (sender, isChecked) =>
            {
                if (isChecked)
                    ShoppingList.Add(vm);
                else
                    ShoppingList.Remove(vm);
            };
        }

        private IReadOnlyList<ScrapedProduct> _lastScraped = Array.Empty<ScrapedProduct>();
        private async Task RunScrapeAsync()
        {
            IsBusy = true;
            Progress = 0;
            ProgressText = "Loading Deals... 0%";
            SummaryText = "";
            HistoryProgress = 0;
            HistoryText = "Loading Price History... 0%";

            var progressReporter = new Progress<int>(p =>
            {
                Progress = p;
                ProgressText = $"Loading Deals... {p}%";
            });

            var historyReporter = new Progress<int>(p =>
            {
                HistoryProgress = p;
                HistoryText = $"Loading Price History... {p}%";
            });

            try
            {
                var pageUpdate = await _manager.RunAsync(SelectedWarehouse, progressReporter,historyReporter);
                LastUpdateText = pageUpdate ?? "Last update not available";
                _lastScraped = _manager.LastScrapedProducts;
                LoadProductsFromLastScrape();
                try
                {
                    var json = JsonSerializer.Serialize(_lastScraped);
                    File.WriteAllText(CacheFilePath, json);
                }
                catch
                {
                    // ignore any file‐I/O errors
                }
                SummaryText = $"✅ {Products.Count} Deals Found! {LastUpdateText}";
                foreach (var vm in Products.Where(pvm => pvm.Preference == Preference.AlwaysBuy))
                    vm.IsInShoppingList = true;
            }
            catch (Exception ex)
            {
                SummaryText = "❌ Error scraping deals";
            }
            finally
            {
                Progress = 0;
                ProgressText = "";
                IsBusy = false;
            }
        }
        private const string CacheFilePath = "lastScrape.json";
    }
}
