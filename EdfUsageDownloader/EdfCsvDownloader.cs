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
    
    public List<EdfDailyUsageRecord> GetDailyUsage(DateTime? fromDate)
    {
        //TODO: Handle fromdate previous months
        Stream? csvStream = this.GetCsvData(fromDate.Value, true, true).Result;
        return csvStream.ToEdfDailyUsageRecords();
    }

    public List<EdfTimeUsageRecord> GetTimeUsage(DateTime? fromDate)
    {
        return this.GetTimeUsageAsync(fromDate).Result;
    }

    private async Task<List<EdfTimeUsageRecord>> GetTimeUsageAsync(DateTime? fromDate)
    {
        List<EdfTimeUsageRecord> usageRecords = new List<EdfTimeUsageRecord>();

        DateTime currentDate = DateTime.Now;
        bool isFirstPass = true;

        while (currentDate.Date >= fromDate.Value.Date)
        {
            Stream? csv = await this.GetCsvData(currentDate, false, isFirstPass);
            List<EdfTimeUsageRecord> edfUsageRecords = csv.ToEdfTimeUsageRecords();

            usageRecords.AddRange(edfUsageRecords);

            // if the data for today isn't available, the query to edf will default to the last available day
            // so we reset the currentDate to the date returned from EDF so we don't get multiple resultsets for
            // the same day (i.e. the last day that EDF had available)
            currentDate = edfUsageRecords.First().ReadTime.AddDays(-1);
            isFirstPass = false;
        }

        return usageRecords;
    }

    public async Task Authenticate()
    {
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

        // Fill [placeholder="e.g. an.other@email.com"]
        await page.FillAsync("[placeholder=\"e.g. an.other@email.com\"]", this._email);

        // Click text=Next
        await page.ClickAsync("text=Next");
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
    }

    private async Task<Stream?> GetCsvData(DateTime? forDate, bool isDailyUsage, bool defaultMode)
    {
        string dataType = isDailyUsage ? "day" : "hour";
        string fromDate = !forDate.HasValue ? string.Empty : forDate.Value.ToString("dd+MMMM+yyyy");
        string eventType = defaultMode ? "default" : "dateEvent";
        string data = $"tabVal={dataType}&fuelType=electricity&consumptionType=true&fromDate={fromDate}&eventType={eventType}";

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
            string test = await result.Content.ReadAsStringAsync();
            resultStream = await result.Content.ReadAsStreamAsync();
        }

        return resultStream;
    }
}