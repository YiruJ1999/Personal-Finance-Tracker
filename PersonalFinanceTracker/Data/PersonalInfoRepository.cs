// Personal Finance Tracker
// File: PersonalFinanceTracker/Data/PersonalInfoRepository.cs
// Purpose: Encapsulates persistence and data-access behavior for the finance domain.

using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.Services;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace PersonalFinanceTracker.Data
{
    public class PersonalInfoRepository
    {
        private readonly DatabaseService _database;

        public PersonalInfoRepository(DatabaseService database)
        {
            _database = database;
        }

        public Task EnsureDatabaseInitializedAsync()
            => _database.InitAsync(); 

        // Get personal information, create default if none exists
        public async Task<PersonalInfo> GetPersonalInfoAsync()
        {
            var db = await _database.GetConnectionAsync(); 
            var list = await db.Table<PersonalInfo>().ToListAsync();

            if (list.Count == 0)
            {
                var defaultInfo = new PersonalInfo
                {
                    Name = GenerateRandomName(),
                    AvatarPath = "default_avatar.png"
                };

                await db.InsertAsync(defaultInfo);
                return defaultInfo;
            }

            return list.First();
        }

        // Update or insert personal information
        public async Task SaveAsync(PersonalInfo personalInfo)
        {
            var db = await _database.GetConnectionAsync();

            if (personalInfo.id == 0)
                await db.InsertAsync(personalInfo);
            else
                await db.UpdateAsync(personalInfo);
        }

        // Get all personal information
        public async Task<List<PersonalInfo>> ListAsync()
        {
            var db = await _database.GetConnectionAsync();
            return await db.Table<PersonalInfo>().ToListAsync();
        }

        // Generate a random name for the default user
        private static string GenerateRandomName()
        {
            var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, 5)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }
    }
}

