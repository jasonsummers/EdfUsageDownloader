using System.Globalization;
using System.Runtime.CompilerServices;
using AutoMapper;
using CsvHelper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace EdfUsageDownloader
{
    class Program
    {
        static async Task Main(string[] args)
        {
            using IHost host = Host.CreateDefaultBuilder(args).Build();
            IConfiguration config = host.Services.GetRequiredService<IConfiguration>();

            string email = config.GetValue<string>("edf_account_email");
            string password = config.GetValue<string>("edf_account_password");
            string dailyUsageCsvFile = config.GetValue<string>("dailyUsageCsvFile");
            string timeUsageCsvFile = config.GetValue<string>("timeUsageCsvFile");

            IEdfDataProducer edfDataProducer;

            if (string.IsNullOrWhiteSpace(dailyUsageCsvFile) || string.IsNullOrWhiteSpace(timeUsageCsvFile))
            {
                EdfCsvDownloader csvDownloader = new EdfCsvDownloader(email, password);
                await csvDownloader.Authenticate();
                edfDataProducer = csvDownloader;
            }
            else
            {
                edfDataProducer = new EdfCsvFileReader(dailyUsageCsvFile, timeUsageCsvFile);
            }

            var configuration = new MapperConfiguration(cfg => 
            {
                cfg.CreateMap<EdfDailyUsageRecord, DailyUsageRecord>();
                cfg.CreateMap<EdfTimeUsageRecord, TimeUsageRecord>();
            });
            
            var mapper = configuration.CreateMapper();
            UsageDbContext dbContext = new UsageDbContext();

            await ProcessDailyUsage(dbContext, edfDataProducer, mapper);
            await ProcessTimeUsage(dbContext, edfDataProducer, mapper);
        }

        private static async Task ProcessDailyUsage(UsageDbContext dbContext, IEdfDataProducer edfDataProducer,
            IMapper mapper)
        {
            // start by getting the latest date of complete information
            DailyUsageRecord? oldestIncompleteInfo = await dbContext.DailyUsage.Where(x =>
                x.ElectricityEstimated ||
                x.GasEstimated ||
                x.ElectricityCost == 0 ||
                x.ElectricityUnits == 0 ||
                x.GasCost == 0 ||
                x.GasUnits == 0).OrderBy(x => x.Date).FirstOrDefaultAsync();

            // then go and get the data
            DateTime fromDate = oldestIncompleteInfo is null
                ? DateTime.Today.AddDays(-1)
                : oldestIncompleteInfo.Date.ToDateTime(TimeOnly.MinValue);

            List<EdfDailyUsageRecord> edfUsageRecords = edfDataProducer.GetDailyUsage(fromDate);
            List<DailyUsageRecord> usageRecords =
                mapper.Map<List<EdfDailyUsageRecord>, List<DailyUsageRecord>>(edfUsageRecords);

            foreach (DailyUsageRecord usageRecord in usageRecords.OrderBy(x => x.Date))
            {
                DailyUsageRecord? existingRecord =
                    await dbContext.DailyUsage.FirstOrDefaultAsync(x => x.Date == usageRecord.Date);

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
        }

        private static async Task ProcessTimeUsage(UsageDbContext dbContext, IEdfDataProducer edfDataProducer,
            IMapper mapper)
        {
            TimeUsageRecord oldestData = await dbContext.TimeUsage.OrderByDescending(x => x.ReadTime).FirstAsync();

            List<EdfTimeUsageRecord> edfUsageRecords = edfDataProducer.GetTimeUsage(oldestData.ReadTime);
            List<TimeUsageRecord> usageRecords = mapper.Map<List<EdfTimeUsageRecord>, List<TimeUsageRecord>>(edfUsageRecords);

            foreach (TimeUsageRecord usageRecord in usageRecords)
            {
                TimeUsageRecord existingRecord =
                    await dbContext.TimeUsage.FirstOrDefaultAsync(x => x.ReadTime == usageRecord.ReadTime);

                if (existingRecord is not null)
                {
                    continue;
                }
                
                usageRecord.EntryTime = DateTime.Now;
                await dbContext.TimeUsage.AddAsync(usageRecord);
            }

            await dbContext.SaveChangesAsync();
        }
    }
}