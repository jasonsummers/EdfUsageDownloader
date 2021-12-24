using System.Globalization;
using CsvHelper;

namespace EdfUsageDownloader;

public static class StaticMethods
{
    public static List<EdfDailyUsageRecord> ToEdfDailyUsageRecords(this Stream? usageStream)
    {
        List<EdfDailyUsageRecord> usageRecords = new List<EdfDailyUsageRecord>();
        
        using (var reader = new StreamReader(usageStream))
        using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
        {
            csv.Read();
            csv.ReadHeader();
            while (csv.Read())
            {
                var record = new EdfDailyUsageRecord
                {
                    ReadDate = csv.GetField<DateTime>("Read Date"),
                    ElectricityUnits = csv.GetField<double>("Electricity Consumption"),
                    ElectricityCost = csv.GetField<double>("Electricity Cost"),
                    ElectricityEstimated = csv.GetField("Electricity Estimated") == "Yes",
                    GasUnits = csv.GetField<double>("Gas Consumption"),
                    GasCost = csv.GetField<double>("Gas Cost"),
                    GasEstimated = csv.GetField("Gas Estimated") == "Yes"
                };
                    
                usageRecords.Add(record);
            }
        }

        return usageRecords;
    }
    
    public static List<EdfTimeUsageRecord> ToEdfTimeUsageRecords(this Stream? usageStream)
    {
        List<EdfTimeUsageRecord> usageRecords = new List<EdfTimeUsageRecord>();
        
        using (var reader = new StreamReader(usageStream))
        using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
        {
            csv.Read();
            csv.ReadHeader();
            while (csv.Read())
            {
                var record = new EdfTimeUsageRecord
                {
                    ReadTime = csv.GetField<DateTime>("Read Date"),
                    ElectricityUsage = csv.GetField<double>("Electricity Consumption"),
                    GasUsage = csv.GetField<double>("Gas Consumption")
                };
                    
                usageRecords.Add(record);
            }
        }

        return usageRecords;
    }
}