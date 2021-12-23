using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace EdfUsageDownloader;

public class UsageDbContext : DbContext
{
    public DbSet<DailyUsageRecord> DailyUsage { get; set; }
    
    public DbSet<TimeUsageRecord> TimeUsage { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false).Build();

        string connectionString = configuration["connectionString"];
        ServerVersion serverVersion = ServerVersion.AutoDetect(connectionString);
        
        options.UseMySql(connectionString, serverVersion);
    }
}