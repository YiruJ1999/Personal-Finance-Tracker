// Personal Finance Tracker
// File: PersonalFinanceTracker/Data/Constants.cs
// Purpose: Encapsulates persistence and data-access behavior for the finance domain.

namespace PersonalFinanceTracker.Data
{
    public static class Constants
    {
        public const string DatabaseFilename = "AppSQLite.db3";

        public static string DatabasePath =>
            $"Data Source={Path.Combine(FileSystem.AppDataDirectory, DatabaseFilename)}";
    }
}
