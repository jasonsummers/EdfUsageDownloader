namespace EdfUsageDownloader;

public interface IEdfDataProducer
{
    public List<EdfDailyUsageRecord> GetDailyUsage(DateTime? fromDate);

    public List<EdfTimeUsageRecord> GetTimeUsage(DateTime? fromDate);
}