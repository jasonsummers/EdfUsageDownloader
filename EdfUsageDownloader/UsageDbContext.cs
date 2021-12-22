using Microsoft.EntityFrameworkCore;

namespace EdfUsageDownloader;

public class UsageDbContext : DbContext
{
    private string _connectionString;
    
    public DbSet<DailyUsageRecord> DailyUsage { get; set; }
    
    public DbSet<TimeUsageRecord> TimeUsage { get; set; }

    public UsageDbContext(string connectionString)
    {
        this._connectionString = connectionString;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        ServerVersion serverVersion = ServerVersion.AutoDetect(this._connectionString);
        
        options.UseMySql(this._connectionString, serverVersion);
    }
}