using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace EdfUsageDownloader
{
    internal static class Program
    {
        private static async Task Main(string[] args)
        {
            Console.WriteLine("Initialising and reading config...");
            using var host = Host.CreateDefaultBuilder(args).Build();
            var config = host.Services.GetRequiredService<IConfiguration>();

            var email = config.GetValue<string>("edf_account_email");
            var password = config.GetValue<string>("edf_account_password");
            var dailyUsageCsvFile = config.GetValue<string>("dailyUsageCsvFile");
            var timeUsageCsvFile = config.GetValue<string>("timeUsageCsvFile");

            IEdfDataProducer edfDataProducer;

            if (string.IsNullOrWhiteSpace(dailyUsageCsvFile) || string.IsNullOrWhiteSpace(timeUsageCsvFile))
            {
                Console.WriteLine("Either dailyUsageCsvFile or timeUsageCsvFile not set, using CSV Downloader.");
                
                var csvDownloader = new EdfCsvDownloader(email, password);
                await csvDownloader.Authenticate();
                edfDataProducer = csvDownloader;
            }
            else
            {
                Console.WriteLine($"Initialising CSV File Reader with dailyUsageCsvFile = {dailyUsageCsvFile} and timeUsageCsvFile = {timeUsageCsvFile}");
                edfDataProducer = new EdfCsvFileReader(dailyUsageCsvFile, timeUsageCsvFile);
            }

            var configuration = new MapperConfiguration(cfg => 
            {
                cfg.CreateMap<EdfDailyUsageRecord, DailyUsageRecord>();
                cfg.CreateMap<EdfTimeUsageRecord, TimeUsageRecord>();
            });
            
            var mapper = configuration.CreateMapper();
            var dbContext = new UsageDbContext();

            await ProcessDailyUsage(dbContext, edfDataProducer, mapper);
            await ProcessTimeUsage(dbContext, edfDataProducer, mapper);
        }

        private static async Task ProcessDailyUsage(UsageDbContext dbContext, IEdfDataProducer edfDataProducer,
            IMapper mapper)
        {
            // start by getting the latest date of complete information
            var oldestIncompleteInfo = await dbContext.DailyUsage.Where(x =>
                x.ElectricityEstimated ||
                x.GasEstimated ||
                x.ElectricityCost == 0 ||
                x.ElectricityUnits == 0 ||
                x.GasCost == 0 ||
                x.GasUnits == 0).OrderBy(x => x.ReadDate).FirstOrDefaultAsync();

            // then go and get the data
            var fromDate = oldestIncompleteInfo?.ReadDate ?? DateTime.Today.AddDays(-1);
            
            Console.WriteLine($"ProcessDailyUsage: OldestIncompleteInfo is {fromDate.ToString("dd/MM/yyyy")}");

            var edfUsageRecords = await edfDataProducer.GetDailyUsageAsync(fromDate);
            var usageRecords = mapper.Map<List<EdfDailyUsageRecord>, List<DailyUsageRecord>>(edfUsageRecords);
            
            Console.WriteLine($"Processing {usageRecords.Count} Daily Usage Records...");

            foreach (var usageRecord in usageRecords.OrderBy(x => x.ReadDate))
            {
                var existingRecord =
                    await dbContext.DailyUsage.FirstOrDefaultAsync(x => x.ReadDate == usageRecord.ReadDate);

                if (existingRecord is null)
                {
                    usageRecord.EntryTime = DateTime.Now;
                    await dbContext.DailyUsage.AddAsync(usageRecord);
                    continue;
                }

                existingRecord.ElectricityCost = usageRecord.ElectricityCost;
                existingRecord.ElectricityEstimated = usageRecord.ElectricityEstimated;
                existingRecord.ElectricityUnits = usageRecord.ElectricityUnits;

                existingRecord.GasCost = usageRecord.GasCost;
                existingRecord.GasEstimated = usageRecord.GasEstimated;
                existingRecord.GasUnits = usageRecord.GasUnits;
            }

            await dbContext.SaveChangesAsync();
            
            Console.WriteLine("Daily Usage processing completed");
        }

        private static async Task ProcessTimeUsage(UsageDbContext dbContext, IEdfDataProducer edfDataProducer,
            IMapper mapper)
        {
            var oldestData = await dbContext.TimeUsage.OrderByDescending(x => x.ReadTime).FirstAsync();
            
            Console.WriteLine($"ProcessTimeUsage: OldestData is {oldestData.ReadTime.ToString("dd/MM/yyyy")}");

            var edfUsageRecords = await edfDataProducer.GetTimeUsageAsync(oldestData.ReadTime);
            var usageRecords = mapper.Map<List<EdfTimeUsageRecord>, List<TimeUsageRecord>>(edfUsageRecords);
            
            Console.WriteLine($"Processing {usageRecords.Count} Time Usage Records...");

            foreach (var usageRecord in usageRecords.OrderBy(x => x.ReadTime))
            {
                var existingRecord =
                    await dbContext.TimeUsage.FirstOrDefaultAsync(x => x.ReadTime == usageRecord.ReadTime);

                if (existingRecord is not null)
                {
                    continue;
                }
                
                usageRecord.EntryTime = DateTime.Now;
                await dbContext.TimeUsage.AddAsync(usageRecord);
            }

            await dbContext.SaveChangesAsync();
            
            Console.WriteLine("Time Usage processing completed");
        }
    }
}