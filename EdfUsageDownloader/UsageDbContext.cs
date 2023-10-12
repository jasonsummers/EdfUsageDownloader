using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace EdfUsageDownloader;

public class UsageDbContext : DbContext
{
    private readonly IConfiguration _configuration;

    public UsageDbContext()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false).Build();

        _configuration = configuration;
    }
    
    public UsageDbContext(IConfiguration configuration)
    {
        _configuration = configuration;
    }
    
    public DbSet<DailyUsageRecord> DailyUsage { get; set; }
    
    public DbSet<TimeUsageRecord> TimeUsage { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        string connectionString = _configuration["connectionString"];
        ServerVersion serverVersion = ServerVersion.AutoDetect(connectionString);
        
        options.UseMySql(connectionString, serverVersion);
    }
}