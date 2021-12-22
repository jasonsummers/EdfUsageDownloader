namespace EdfUsageDownloader;

public class EdfCsvFileReader : IEdfDataProducer
{
    private string _dailyUsageFilePath;
    private string _timeUsageFilePath;

    public EdfCsvFileReader(string dailyUsageFilePath, string timeUsageFilePath)
    {
        this._dailyUsageFilePath = dailyUsageFilePath;
        this._timeUsageFilePath = timeUsageFilePath;
    }

    public EdfCsvFileReader(string filePath, bool isDailyUsageFile)
    {
        if (isDailyUsageFile)
        {
            this._dailyUsageFilePath = filePath;
            return;
        }

        this._timeUsageFilePath = filePath;
    }
    
    public List<EdfDailyUsageRecord> GetDailyUsage(DateTime? fromDate)
    {
        if (string.IsNullOrEmpty(this._dailyUsageFilePath))
        {
            throw new NullReferenceException("Daily Usage CSV File Not Specified.");
        }
        
        Stream fileStream = new FileStream(this._dailyUsageFilePath, FileMode.Open);
        return fileStream.ToEdfDailyUsageRecords();
    }

    public List<EdfTimeUsageRecord> GetTimeUsage(DateTime? fromDate)
    {
        if (string.IsNullOrEmpty(this._timeUsageFilePath))
        {
            throw new NullReferenceException("Time Usage CSV File Not Specified.");
        }
        
        Stream fileStream = new FileStream(this._timeUsageFilePath, FileMode.Open);
        return fileStream.ToEdfTimeUsageRecords();
    }
}