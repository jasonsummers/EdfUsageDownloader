namespace EdfUsageDownloader;

public struct EdfTimeUsageRecord
{
    public DateTime ReadTime { get; set; }
    
    public double ElectricityUsage { get; set; }
    
    public double GasUsage { get; set; }
}