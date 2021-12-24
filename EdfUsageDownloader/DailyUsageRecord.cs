namespace EdfUsageDownloader;

public class DailyUsageRecord
{
    public int Id { get; set; }
    
    public DateTime EntryTime { get; set; }
    
    public DateTime ReadDate { get; set; }
    
    public double ElectricityUnits { get; set; }
    
    public double ElectricityCost { get; set; }
    
    public bool ElectricityEstimated { get; set; }
    
    public double GasUnits { get; set; }
    
    public double GasCost { get; set; }
    
    public bool GasEstimated { get; set; }
}