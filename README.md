# Costco Deals App

**WPF desktop app** that scrapes Costco warehouse deals, stores price history in SQLite and JSON cache, and presents a rich UI with filtering, shopping‑list and price‑alert badges.
The YepSavings is an independent website. This app only facilitates its use.
---

## 🚀 Features

- **Live Scraping**  
  - Click **Get Warehouse Deals** to fetch & parse deals via Playwright headless  
- **Instant Startup**  
  - Load last scrape from `lastScrape.json` into grid—no blank UI  
- **Filtering**  
  - Real‑time filter by Name / Category / Preference (“All” = no filter)  
- **Category Inheritance**  
  - New items inherit last saved category for same Costco ID  
- **Editing & Persistence**  
  - In‑grid dropdowns update SQLite immediately  
- **Shopping List**  
  - “Always Buy” items auto‑populate a second tab  
- **Price‑Alert Badges**  
  - ⚠ Price ↑  •  ✔ Price ↓  •  🔥 Hot deal (≥ 20% off)  •  🏆 All‑time low (≥ 4 data points)  
- **Responsive UI**  
  - Async scrape, progress bar, controls disabled while busy  
- **Price History**  
  - Scrape & persist historical prices for each product  

---

## 🏗 Tech Stack & Architecture

- **UI**: WPF (.NET 9) MVVM (`ViewModels`, `RelayCommand`, `INotifyPropertyChanged`)  
- **Scraper**: Playwright headless (`CostcoScraperService`)  
- **Data**: EF Core 8 Code‑First → SQLite (`MainDatabase`, `Product`, `PriceHistory`)  
- **Caching**: JSON (`lastScrape.json`) for instant grid load  
- **DI & Host**: `HostBuilder` + `IServiceCollection`  
- **Logging**: `ILogger<T>`  
- **Tests** (planned): xUnit + EF Core InMemory  
- **CI** (planned): GitHub Actions + coverage badges  

---

## 📦 Getting Started

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)  
- Windows 10+  
- Visual Studio 2022 (WPF workload) or VS Code  

---

### ▶️ Usage

1. Select Warehouse from dropdown
2. Click Get Warehouse Deals
3. Watch progress bar & summary log
4. On Scraped Products tab:
	Click image to enlarge
	Edit Category & Preference via dropdown
	Check Add to List for shopping‑list items
	View price‑alert badges
5. Switch to Shopping List tab to review picks

### Clone & Build

```bash
git clone https://github.com/your‑username/CostcoDealsApp.git
cd CostcoDealsApp
dotnet build


