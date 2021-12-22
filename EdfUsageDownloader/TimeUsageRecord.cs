namespace EdfUsageDownloader;

public class TimeUsageRecord
{
    public int Id { get; set; }
    
    public DateTime EntryTime { get; set; }
    
    public DateTime ReadTime { get; set; }
    
    public double ElectricityUsage { get; set; }
    
    public double GasUsage { get; set; }
}