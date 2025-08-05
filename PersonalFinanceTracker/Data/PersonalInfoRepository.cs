using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.Services;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PersonalFinanceTracker.Data
{
    public class PersonalInfoRepository
    {
        private readonly DatabaseService _database;

        public PersonalInfoRepository(DatabaseService database)
        {
            _database = database;
        }

        // Get personal information, create default if none exists
        public async Task<PersonalInfo> GetPersonalInfoAsync()
        {
            var list = await _database.Database.Table<PersonalInfo>().ToListAsync();

            if (list.Count == 0)
            {
                var defaultInfo = new PersonalInfo
                {
                    Name = GenerateRandomName(),
                    AvatarPath = "default_avatar.png"
                };

                await _database.Database.InsertAsync(defaultInfo);
                return defaultInfo;
            }

            return list.First();
        }

        // update or insert personal information
        public Task SaveAsync(PersonalInfo personalInfo)
        {
            if (personalInfo.id == 0)
                return _database.Database.InsertAsync(personalInfo);
            else
                return _database.Database.UpdateAsync(personalInfo);
        }

        // get all personal information
        public Task<List<PersonalInfo>> ListAsync()
        {
            return _database.Database.Table<PersonalInfo>().ToListAsync();
        }

        // generate a random name for the default user
        private static string GenerateRandomName()
        {
            var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, 5)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }
    }
}
