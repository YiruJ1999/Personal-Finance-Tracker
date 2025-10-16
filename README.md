# 💰 Personal Finance Tracker

A cross-platform **.NET MAUI** personal finance app that helps users manage multiple books, accounts, and transactions with visual insights.  
It features a **hand-drawn guinea pig theme**, supports **multiple languages (Chinese / English / German)**, and allows **currency selection** for global users.

---

## 🧩 Overview

**Personal Finance Tracker** enables users to create and manage multiple books, track account balances, record daily transactions, and visualize their financial trends.  
All data is securely stored locally in SQLite — no internet connection required.

---

## ✨ Features

### 📘 Book Management
- Manage multiple books (create, edit, delete)
- Each book has its own transaction table (`book_*`)
- Switch between different books easily

### 📄 Transaction Records
- View monthly transaction lists
- Edit or delete any record
- Pagination for smooth scrolling performance

### 🏠 Home Dashboard
- Display monthly trends, income, expenses, and balance
- Show today's transactions
- Bottom navigation bar for quick access to all features

### ➕ Add Record
- Enter amount, category, account, date, and notes
- Icon-based category selection (e.g., 🍔 Food, 🚌 Transport, 🛍️ Shopping)
- Automatically detects income/expense type

### 💳 Account Management
- View all accounts and balances
- Add or delete accounts
- Account details page includes:
  - Editable balance
  - Transaction trend chart
  - Monthly account history view
- Aggregate data across all books

### 🎨 Hand-Drawn Guinea Pig Theme
- Unique hand-drawn icons and illustrations
- Warm, cartoon-style UI for an engaging experience

### 🌍 Multilingual & Currency Support
- Supports **Chinese, English, and German**
- Users can switch languages in-app
- Choose preferred currency (€, $, ¥, etc.)

---

## 🧮 Data Structure

### Book
- Each book corresponds to a physical SQLite table (`book_xxx`)
- Dynamic creation and deletion supported

### Record
- Stores each transaction (income/expense)
- Includes account, type, amount, category, note, and timestamp

### Account
- Aggregates balance data from all books
- Provides total assets and monthly trend calculation

### PersonalInfo
- Stores user name and avatar info
- Generates a default user on first run

---

## 🧱 Project Structure

```
PersonalFinanceTracker/
├── Pages/                     # UI Pages (XAML)
│   ├── HomePage.xaml
│   ├── BookPage.xaml
│   ├── RecordPage.xaml
│   ├── AddRecordPage.xaml
│   ├── AccountPage.xaml
│   ├── AccountDetailPage.xaml
│   └── StatsPage.xaml
│
├── PageModels/                # MVVM ViewModels
│   ├── HomePageModel.cs
│   ├── BookPageModel.cs
│   ├── RecordPageModel.cs
│   ├── AddRecordPageModel.cs
│   ├── AccountPageModel.cs
│   ├── AccountDetailPageModel.cs
│   └── StatsPageModel.cs
│
├── Data/                      # Database Repositories
│   ├── DatabaseService.cs
│   ├── BookRepository.cs
│   ├── RecordRepository.cs
│   └── AccountRepository.cs
│
├── Models/                    # Data Models
│   ├── Book.cs
│   ├── Record.cs
│   ├── Account.cs
│   └── PersonalInfo.cs
│
├── Resources/
│   └── Images/                # Hand-drawn icons and illustrations
│
├── Utilities/                 # Utility Classes
│
├── App.xaml / App.xaml.cs     # App Entry Point
├── GlobalUsings.cs            # Global using declarations
└── MauiProgram.cs             # Dependency Injection setup
```

---

## ⚙️ Tech Stack

- **Framework**: .NET MAUI (C# + XAML)
- **Architecture**: MVVM (via CommunityToolkit.Mvvm)
- **Database**: SQLite (sqlite-net-pcl)
- **Charts**: Microcharts or Syncfusion.Maui.Charts
- **Dependency Injection**: Microsoft.Extensions.DependencyInjection
- **Async Programming**: `async/await`
- **UI Style**: Hand-drawn, guinea pig theme with cross-platform support

---

## 🚀 Getting Started

1. Clone the repository:
   ```bash
   git clone https://github.com/<your-username>/PersonalFinanceTracker.git
   cd PersonalFinanceTracker
   ```

2. Restore dependencies:
   ```bash
   dotnet restore
   ```

3. Run the app:
   ```bash
   dotnet build
   dotnet maui run -t Android
   ```
   *(Or open the `.sln` file in Visual Studio and run directly.)*

---

## 🧩 Optional Features
- Export monthly/annual reports (PDF / Excel)
- Cloud synchronization (Firebase / Supabase)
- More theme customization options

---

## 👨‍💻 Author

**YiruJ**  
💡 Developer of Personal Finance Tracker  

---

## 🪪 License

This project is not open source and may not be redistributed or modified without permission.
