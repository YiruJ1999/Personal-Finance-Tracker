using System.Text.Json;
using Microsoft.Extensions.Logging;
using PersonalFinanceTracker.Models;
using PersonalFinanceTracker.Data;

namespace PersonalFinanceTracker.Data
{
    public class SeedDataService
    {
        private readonly RecordRepository _recordRepository;
        private readonly ILogger<SeedDataService> _logger;

        private readonly string _seedDataFilePath = "SeedData.json";

        public SeedDataService(
            RecordRepository recordRepository,
            ILogger<SeedDataService> logger)
        {
            _recordRepository = recordRepository;
            _logger = logger;
        }

        public async Task LoadSeedDataAsync()
        {
            try
            {
                await using Stream recordStream = await FileSystem.OpenAppPackageFileAsync(_seedDataFilePath);
                var records = await JsonSerializer.DeserializeAsync<List<Record>>(recordStream);

                if (records != null)
                {
                    foreach (var record in records)
                    {
                        record.Account ??= "ƒ¨»œ’Àªß";
                        await _recordRepository.SaveAsync(record);
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error loading Record seed data");
            }
        }
    }
}
