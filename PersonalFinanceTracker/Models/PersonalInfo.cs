// Personal Finance Tracker
// File: PersonalFinanceTracker/Models/PersonalInfo.cs
// Purpose: Defines a finance domain model persisted or displayed by the app.

using SQLite;
using System;

namespace PersonalFinanceTracker.Models
{
    [Table("PersonalInfos")]
    public class PersonalInfo
    {
        [PrimaryKey, AutoIncrement]
        public int id { get; set; }

        // Account name
        public string Name { get; set; } = string.Empty;

        // Account avatar
        public string AvatarPath { get; set; } = string.Empty;

        public string CurrencyCode { get; set; } = "EUR";  // default to EUR

        public string LanguageCode { get; set; } = Services.LanguageManager.ZhHans;

    }

}

