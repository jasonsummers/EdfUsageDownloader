using System.Net.Http.Headers;
using System.Text;
using Microsoft.Playwright;

namespace EdfUsageDownloader;

public class EdfCsvDownloader : IEdfDataProducer
{
    private string _email;
    private string _password;
    private string _cookieString;
    private List<Cookie> _cookies;
    public EdfCsvDownloader(string email, string password)
    {
        this._email = email;
        this._password = password;
    }

    public async Task<List<EdfDailyUsageRecord>> GetDailyUsageAsync(DateTime? fromDate)
    {
        var usageRecords = new List<EdfDailyUsageRecord>();

        fromDate = fromDate.HasValue
            ? new DateTime(fromDate.Value.Year, fromDate.Value.Month, 1)
            : new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        
        var currentDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);

        while (currentDate.Date >= (fromDate?.Date ?? DateTime.Now.Date))
        {
            Console.WriteLine($"Retrieving Daily Usage information for {currentDate.ToString("MMMM yyyy")}");
            var defaultMode = currentDate.Year == fromDate.Value.Year && currentDate.Month == fromDate.Value.Month;

            try
            {
                var csv = await this.GetCsvData(currentDate, true, defaultMode);
                var edfUsageRecords = await csv.ToEdfDailyUsageRecordsAsync();

                usageRecords.AddRange(edfUsageRecords);
                currentDate = currentDate.AddMonths(-1);
            }
            catch (Exception)
            {
                // If we hit an exception it's probably because we have no data, so just go to the next month
                currentDate = currentDate.AddMonths(-1);    
            }
        }

        return usageRecords;
    }

    public async Task<List<EdfTimeUsageRecord>> GetTimeUsageAsync(DateTime? fromDate)
    {
        var usageRecords = new List<EdfTimeUsageRecord>();

        var currentDate = DateTime.Now;
        var isFirstPass = true;

        while (currentDate.Date >= (fromDate?.Date ?? DateTime.Now.Date))
        {
            Console.WriteLine($"Retrieving Time Usage information for {currentDate.ToString("dd/MM/yyyy")}");
            
            try
            {
                var csv = await this.GetCsvData(currentDate, false, isFirstPass);
                var edfUsageRecords = await csv.ToEdfTimeUsageRecordsAsync();

                usageRecords.AddRange(edfUsageRecords);

                // if the data for today isn't available, the query to edf will default to the last available day
                // so we reset the currentDate to the date returned from EDF so we don't get multiple resultsets for
                // the same day (i.e. the last day that EDF had available)
                currentDate = edfUsageRecords.First().ReadTime.AddDays(-1);
                isFirstPass = false;
            }
            catch (Exception)
            {
                // If we hit an exception it's probably because we have no data, so just go to the next day
                currentDate = currentDate.AddDays(-1);
                isFirstPass = false;
            }
        }

        return usageRecords;
    }

    public async Task Authenticate()
    {
        Console.WriteLine("EdfCsvDownloader: Beginning Authentication...");
        
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            SlowMo = 5000
        });
        var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            AcceptDownloads = true,
        });

        // Open new page
        //return await context.NewPageAsync();
        // Open new page
        //var page = await GetNewPlaywrightPage();
        var page = await context.NewPageAsync();
        // Go to https://my.edfenergy.com/
        await page.GotoAsync("https://my.edfenergy.com/user/login", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded});

        await page.WaitForSelectorAsync("[placeholder=\"e.g. an.other@email.com\"]");
        
        await page.ClickAsync("[aria-label=\"Let us know if we can use cookies\"] [aria-label=\"Close\"]");

        // Fill [placeholder="e.g. an.other@email.com"]
        await page.FillAsync("[placeholder=\"e.g. an.other@email.com\"]", this._email);

        // Click text=Next
        await page.ClickAsync("input:has-text('Log In')");
        // Assert.AreEqual("https://my.edfenergy.com/login/pwdorotp", page.Url);

        // Fill [placeholder="Password"]
        await page.FillAsync("[placeholder=\"Password\"]", this._password);

        // Click text=Log in with password
        await page.ClickAsync("text=Log in with password");
        
        this._cookieString = string.Empty;
        this._cookies = new List<Cookie>();
        
        foreach (BrowserContextCookiesResult cookie in await page.Context.CookiesAsync())
        {
            this._cookieString += $"{cookie.Name}={cookie.Value};";
            this._cookies.Add(new Cookie
            {
                Domain = cookie.Domain,
                Name = cookie.Name,
                Path = cookie.Path,
                Value = cookie.Value,
                SameSite = cookie.SameSite
            });
        }
        
        Console.WriteLine("EdfCsvDownloader: Authentication completed.");
    }

    private async Task<Stream?> GetCsvData(DateTime? forDate, bool isDailyUsage, bool defaultMode)
    {
        var dataType = isDailyUsage ? "day" : "hour";
        var fromDate = !forDate.HasValue ? string.Empty : forDate.Value.ToString("dd+MMMM+yyyy");
        var eventType = defaultMode ? "default" : "dateEvent";
        var data = $"tabVal={dataType}&fuelType=electricity&consumptionType=true&fromDate={fromDate}&eventType={eventType}";

        var baseAddress = new Uri("https://my.edfenergy.com");

        using (var handler = new HttpClientHandler { UseCookies = false })
        using (var client = new HttpClient(handler) { BaseAddress = baseAddress })
        {
            var message = new HttpRequestMessage(HttpMethod.Post, "/smartplus/graph/generate");
            message.Headers.Add("Cookie", this._cookieString);
            message.Content = new StringContent(data, Encoding.UTF8);
            message.Content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");
            var result = await client.SendAsync(message);
            result.EnsureSuccessStatusCode();
        }

        Stream? resultStream;
        
        using (var handler = new HttpClientHandler { UseCookies = false })
        using (var client = new HttpClient(handler) { BaseAddress = baseAddress })
        {
            var message = new HttpRequestMessage(HttpMethod.Get, "/smartplus-csv");
            message.Headers.Add("Cookie", this._cookieString);
            var result = await client.SendAsync(message);
            result.EnsureSuccessStatusCode();
            resultStream = await result.Content.ReadAsStreamAsync();
        }

        return resultStream;
    }
}