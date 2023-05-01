using System.Data;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Playwright;

namespace EdfUsageDownloader;

public class EdfCsvDownloader : IEdfDataProducer, IDisposable
{
    private readonly Uri _baseAddress = new Uri("https://my.edfenergy.com");
    
    private readonly string _email;
    private readonly string _password;
    private readonly EdfDownloadMode _downloadMode;
    private string _cookieString;
    private List<Cookie> _cookies;

    private IPlaywright _playwright;
    private IBrowser _browser;
    private IBrowserContext _browserContext;
    private IPage _page;
    
    public EdfCsvDownloader(string email, string password, EdfDownloadMode downloadMode)
    {
        this._email = email;
        this._password = password;
        this._downloadMode = downloadMode;
        this._cookieString = string.Empty;
        this._cookies = new List<Cookie>();
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
            catch (Exception e)
            {
                Console.WriteLine($"Exception: {e.Message}");
                if (e.InnerException != null)
                    Console.WriteLine($"InnerException: {e.InnerException.Message}");

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
            catch (Exception e)
            {
                Console.WriteLine($"Exception: {e.Message}");
                if (e.InnerException != null)
                    Console.WriteLine($"InnerException: {e.InnerException.Message}");
                
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
        
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = !System.Diagnostics.Debugger.IsAttached,
            SlowMo = 5000
        });
        _browserContext = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            AcceptDownloads = true,
        });

        // Open new page
        this._page = await _browserContext.NewPageAsync();
        // Go to https://my.edfenergy.com/
        await this._page.GotoAsync("https://my.edfenergy.com/user/login",
            new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });

        await this._page.WaitForSelectorAsync("input[name=\"email\"]");
        
        //await this._page.ClickAsync("[aria-label=\"Let us know if we can use cookies\"] [aria-label=\"Close\"]");

        // Fill "input[name=\"email\"]"
        await this._page.FillAsync("input[name=\"email\"]", this._email);

        // Click text=Next
        await this._page.ClickAsync("button[aria-label=\"Log in\"]");

        // Fill [placeholder="Password"]
        await this._page.FillAsync("[placeholder=\"Password\"]", this._password);

        // Click text=Log in with password
        await this._page.ClickAsync("text=Log in with password");

        await this._page.GotoAsync("https://my.edfenergy.com/myaccount/energyhub/home",
            new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
        
        foreach (var cookie in await this._page.Context.CookiesAsync())
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

    private async Task<string> SetGraphData(DateTime? forDate, bool isDailyUsage, bool defaultMode)
    {
        var dataType = isDailyUsage ? "day" : "hour";
        var fromDate = !forDate.HasValue ? string.Empty : forDate.Value.ToString("dd+MMMM+yyyy");
        var eventType = defaultMode ? "default" : "dateEvent";
        var data =
            $"tabVal={dataType}&fuelType=electricity&consumptionType=true&fromDate={fromDate}&eventType={eventType}";

        using var handler = new HttpClientHandler { UseCookies = false };
        using var client = new HttpClient(handler) { BaseAddress = _baseAddress };
        
        var message = new HttpRequestMessage(HttpMethod.Post, "/smartplus/graph/generate");
        message.Headers.Add("Cookie", this._cookieString);
        message.Content = new StringContent(data, Encoding.UTF8);
        message.Content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");
        var result = await client.SendAsync(message);
        result.EnsureSuccessStatusCode();
        
        return await result.Content.ReadAsStringAsync();
    }

    private async Task<Stream?> GetCsvData(DateTime? forDate, bool isDailyUsage, bool defaultMode)
    {
        if (_downloadMode == EdfDownloadMode.Direct)
        {
            await this.SetGraphData(forDate, isDailyUsage, defaultMode);
            using var handler = new HttpClientHandler { UseCookies = false };
            using var client = new HttpClient(handler) { BaseAddress = _baseAddress };
            var message = new HttpRequestMessage(HttpMethod.Get, "/smartplus-csv");
            message.Headers.Add("Cookie", this._cookieString);
            var result = await client.SendAsync(message);
            result.EnsureSuccessStatusCode();
            return await result.Content.ReadAsStreamAsync();
        }

        switch (isDailyUsage)
        {
            case true:
                await this._page.ClickAsync("#smartplus-day");
                break;
            case false:
                await this._page.ClickAsync("#smartplus-hour");
                break;
        }

        var dailyDateFormat = "dd MMMM yyyy";
        var monthlyDateFormat = "MMMM yyyy";

        var requiredDatepickerValue = isDailyUsage 
            ? forDate.HasValue ? forDate.Value.ToString(monthlyDateFormat) : DateTime.Now.ToString(monthlyDateFormat)
            : forDate.HasValue ? forDate.Value.ToString(dailyDateFormat) : DateTime.Now.ToString(dailyDateFormat);
        
        var datePicker = this._page.Locator("#smart_plus_date_picker").First;
        var datePickerValue = await datePicker.InputValueAsync();

        // if we're getting hourly usage, and the required date month is the same as the datepicker month
        // and the required date day is not equal to the datepicker day
        // and the required date day is greater than the datepicker day
        // then we're asking for data which doesn't exist yet
        if (!isDailyUsage && datePickerValue.Substring(3, 3) == requiredDatepickerValue.Substring(3, 3) &&
            datePickerValue.Substring(0, 2) != requiredDatepickerValue.Substring(0, 2) &&
            Convert.ToInt32(datePickerValue.Substring(0, 2)) < Convert.ToInt32(requiredDatepickerValue.Substring(0, 2)))
            throw new DataException($"Information for {requiredDatepickerValue} is not currently available.");

        while (requiredDatepickerValue != datePickerValue)
        {
            await this._page.ClickAsync("a.ptrn-cal-Prev");
            datePickerValue = await this._page.Locator("#smart_plus_date_picker").First.InputValueAsync();
        }

        var download = await this._page.RunAndWaitForDownloadAsync(async () =>
        {
            await this._page.ClickAsync("a.csv_download");
        });
        
        return await download.CreateReadStreamAsync();
    }

    public void Dispose()
    {
        _playwright.Dispose();
        _browser.DisposeAsync();
        _browserContext.DisposeAsync();
    }
}