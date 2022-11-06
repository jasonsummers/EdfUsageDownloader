namespace EdfUsageDownloader;

public class EdfCsvFileReader : IEdfDataProducer
{
    private readonly string _dailyUsageFilePath;
    private readonly string _timeUsageFilePath;

    public EdfCsvFileReader(string dailyUsageFilePath, string timeUsageFilePath)
    {
        this._dailyUsageFilePath = dailyUsageFilePath;
        this._timeUsageFilePath = timeUsageFilePath;
    }
    
    public async Task<List<EdfDailyUsageRecord>> GetDailyUsageAsync(DateTime? fromDate)
    {
        if (string.IsNullOrEmpty(this._dailyUsageFilePath))
        {
            throw new NullReferenceException("Daily Usage CSV File Not Specified.");
        }
        
        Stream fileStream = new FileStream(this._dailyUsageFilePath, FileMode.Open);
        return await fileStream.ToEdfDailyUsageRecordsAsync();
    }

    public async Task<List<EdfTimeUsageRecord>> GetTimeUsageAsync(DateTime? fromDate)
    {
        if (string.IsNullOrEmpty(this._timeUsageFilePath))
        {
            throw new NullReferenceException("Time Usage CSV File Not Specified.");
        }
        
        Stream fileStream = new FileStream(this._timeUsageFilePath, FileMode.Open);
        return await fileStream.ToEdfTimeUsageRecordsAsync();
    }
}