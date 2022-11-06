namespace EdfUsageDownloader;

public interface IEdfDataProducer
{
    public Task<List<EdfDailyUsageRecord>> GetDailyUsageAsync(DateTime? fromDate);

    public Task<List<EdfTimeUsageRecord>> GetTimeUsageAsync(DateTime? fromDate);
}