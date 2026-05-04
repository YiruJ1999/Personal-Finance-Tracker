# Personal Finance Tracker

A cross-platform personal finance app built with **.NET MAUI**, designed to help users manage books, accounts, and daily transactions with clear local-first data ownership.

The app combines practical bookkeeping workflows with a warm hand-drawn visual style, multilingual UI support, currency selection, and chart-based financial insight.

## Highlights

- **Cross-platform .NET MAUI app** targeting Android, iOS, macOS Catalyst, and Windows.
- **Local-first SQLite storage** so finance data stays on the device.
- **Multiple finance books** for separating personal budgets, family expenses, travel funds, or other accounting contexts.
- **Account management** with balances, account detail views, and account-level history.
- **Transaction workflow** for creating, editing, deleting, and browsing income and expense records.
- **Visual analytics** powered by Syncfusion charts and toolkit components.
- **Multilingual interface** with Chinese, English, and German resource files.
- **Currency support** backed by CLDR currency symbol data.
- **Custom illustrated UI assets** for a more personal, approachable experience.

## What The App Does

Personal Finance Tracker helps users record everyday financial activity and understand their money flow over time. Users can create separate books, add accounts, enter transactions, review monthly records, inspect account balances, and switch language or currency settings from inside the app.

The project is built as a single .NET MAUI application with an MVVM-oriented structure. SQLite is used for local persistence, repositories encapsulate data access, and page models keep UI state and interaction logic separated from XAML pages.

## Core Features

### Book Management

- Create and manage multiple books.
- Store each book in its own physical SQLite transaction table.
- Switch the active book through persisted preferences.
- Import or initialize existing physical book tables when needed.

### Transaction Management

- Add income and expense records with amount, category, account, note, and timestamp.
- Edit existing records and delete records through confirmation popups.
- Browse records with paging support for smoother monthly views.
- Link every transaction to an account for balance aggregation.

### Account Management

- Create and delete accounts.
- Maintain account balances.
- Review account detail pages with related transaction history.
- Aggregate balances across all books.
- Recalculate account totals from stored book records.

### Dashboard And Insights

- Display high-level financial information on the main page.
- Show recent records and monthly context.
- Use Syncfusion chart components for account and finance trend views.
- Support pull-to-refresh and responsive UI behavior.

### Localization And Currency

- Resource-based localization for Simplified Chinese, English, and German.
- Runtime language switching through a dedicated language manager.
- Currency code and symbol selection through a dedicated currency manager.
- CLDR-based currency symbol loading from packaged raw assets.

### Data Privacy

- Data is stored locally in SQLite.
- The app does not require an internet connection for the core finance workflow.
- No cloud account is required for normal use.

## Tech Stack

| Area | Technology |
| --- | --- |
| Framework | .NET 9, .NET MAUI |
| Language | C# |
| UI | XAML, MAUI Shell |
| Architecture | MVVM-style page models, dependency injection |
| Local Database | SQLite, sqlite-net-pcl, Microsoft.Data.Sqlite.Core |
| UI Toolkit | CommunityToolkit.Maui, Syncfusion.Maui.Toolkit |
| Charts | Syncfusion.Maui.Charts |
| MVVM Helpers | CommunityToolkit.Mvvm |
| Localization | RESX resource files |
| Assets | MAUI images, raw assets, custom fonts |

## Project Structure

```text
PersonalFinanceTracker/
  Behaviors/             Responsive font and icon sizing behaviors
  Controls/              Reusable custom UI controls
  Converters/            XAML value converters
  Data/                  Repositories, constants, and seed-data loading
  Localization/          XAML translation extension
  Messages/              Weak-reference messenger event payloads
  Models/                SQLite-backed domain models and category data
  PageModels/            View models / page state and commands
  Pages/                 XAML pages and popups
  Platforms/             Platform-specific MAUI bootstrap code
  Resources/
    AppIcon/             Application icons
    Fonts/               Custom fonts and icon font metadata
    Images/              Illustrated app images and category icons
    Raw/                 Seed data and CLDR currency data
    Splash/              Splash screen assets
    Strings/             Localized RESX resources
    Styles/              Shared colors and UI styles
  Services/              Database, language, currency, and error services
  Utilities/             Shared utility helpers
```

## Data Model Overview

### Book

Represents a bookkeeping space. Each book has a display name and a physical SQLite table name used to store that book's records.

### Record

Represents one transaction. A record stores its account, type, amount, category, note, and timestamp.

### Account

Represents a financial account. Account balances can be updated directly and also synchronized from transactions stored across books.

### PersonalInfo

Stores basic profile information such as display name and avatar path.

## Getting Started

### Prerequisites

- Visual Studio 2022 with the .NET MAUI workload, or a compatible .NET SDK setup.
- .NET 9 SDK.
- Android SDK / emulator for Android builds, or the required Apple tooling for iOS and Mac Catalyst builds.

### Restore Dependencies

```bash
cd PersonalFinanceTracker
dotnet restore
```

### Build

```bash
dotnet build PersonalFinanceTracker.sln
```

### Run

Open `PersonalFinanceTracker/PersonalFinanceTracker.sln` in Visual Studio and run the target platform you want to test.

For command-line Android development, use a MAUI-compatible target and connected emulator/device, for example:

```bash
dotnet build -f net9.0-android
```

## Notes For Reviewers

This repository demonstrates:

- Practical MAUI application structure.
- Local SQLite persistence with repository abstractions.
- Runtime localization and currency configuration.
- Multi-page finance workflows with modal popups.
- Chart-driven account and finance insights.
- A user-friendly visual direction beyond a default template app.

<<<<<<< Updated upstream
## 👨‍💻 Author
=======
## Roadmap Ideas

- Export monthly or yearly reports as PDF or Excel files.
- Add budget goals and spending warnings.
- Add optional encrypted backups.
- Add richer category customization.
- Add automated tests for repositories and page models.

## Author
>>>>>>> Stashed changes

**YiruJ**

Developer of Personal Finance Tracker.

## License

This project is not open source. Redistribution or modification is not permitted without explicit permission.
