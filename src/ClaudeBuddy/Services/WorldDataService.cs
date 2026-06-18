using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using ClaudeBuddy.Settings;

namespace ClaudeBuddy.Services;

/// <summary>The kind of weather the mascot should react to.</summary>
public enum WeatherMood
{
    Unknown,
    Cold,    // bundle up, shiver, icy thermometer
    Cool,
    Mild,
    Warm,
    Hot,     // sweat, fan
    Rain,    // umbrella
    Snow,    // catch snowflakes
}

/// <summary>An immutable read of the "world data" the buddy can chat about.</summary>
public readonly struct WorldDataSnapshot
{
    public bool HasWeather { get; init; }
    public double TemperatureC { get; init; }
    public WeatherMood Weather { get; init; }
    public string City { get; init; }

    public bool HasDollar { get; init; }
    public int DollarBlue { get; init; }     // ARS per USD (informal "blue" rate)

    public bool HasCrypto { get; init; }
    public long BtcUsd { get; init; }

    /// <summary>Name of today's public holiday in the user's country, or empty.</summary>
    public string TodayHoliday { get; init; }

    public WorldDataSnapshot()
    {
        City = string.Empty;
        TodayHoliday = string.Empty;
    }
}

/// <summary>
/// Pulls fun, real-world data from free, key-less public APIs so the mascot can react to
/// it — the headline feature being the weather (it shivers with an icy thermometer when
/// it's cold, fans itself when it's hot, etc.). Also fetches the ARS "blue" dollar and the
/// BTC price for the occasional speech bubble.
///
/// Sources (all free, no API key, all read-only GETs):
///   • Location  — ip-api.com  (approximate city/lat/lon from the public IP)
///   • Weather   — open-meteo.com
///   • Dollar    — dolarapi.com (Argentina)
///   • Crypto    — coingecko.com
///
/// Runs on a background thread, polls slowly (weather every 30 min, rates every 15 min),
/// and is entirely best-effort: no internet or any error just leaves that datum
/// "unavailable" and the buddy carries on. PRIVACY: only an approximate city is derived
/// from the IP, nothing is sent anywhere, and the whole thing is gated behind
/// <see cref="AppSettings.WorldData"/> (off → no requests are ever made).
/// </summary>
public sealed class WorldDataService
{
    private static readonly TimeSpan WeatherEvery = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan RatesEvery = TimeSpan.FromMinutes(15);
    private const double TickSeconds = 60.0;

    private readonly ISettingsService _settings;
    private readonly object _gate = new();

    private Thread? _thread;
    private volatile bool _running;

    private double? _lat, _lon;
    private string _country = string.Empty;   // ISO-2 country code from IP
    private DateTimeOffset _weatherAt = DateTimeOffset.MinValue;
    private DateTimeOffset _ratesAt = DateTimeOffset.MinValue;
    private DateTime _holidaysFor = DateTime.MinValue; // date the holiday set was loaded for

    // Cached snapshot fields (guarded by _gate).
    private WorldDataSnapshot _snapshot;

    public WorldDataService(ISettingsService settings) => _settings = settings;

    public WorldDataSnapshot Current
    {
        get { lock (_gate) { return _snapshot; } }
    }

    public void Start()
    {
        if (_running)
        {
            return;
        }

        _running = true;
        _thread = new Thread(PollLoop) { IsBackground = true, Name = "ClaudeBuddy.WorldData" };
        _thread.Start();
    }

    public void Stop()
    {
        _running = false;
        _thread?.Join(TimeSpan.FromSeconds(1));
        _thread = null;
    }

    private void PollLoop()
    {
        // A single shared client for the life of the poller.
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("ClaudeBuddy/1.0");

        while (_running)
        {
            try
            {
                if (_settings.Current.WorldData)
                {
                    RefreshDue(http);
                }
            }
            catch
            {
                // Best-effort: any failure just leaves the data unavailable this round.
            }

            for (int i = 0; i < TickSeconds * 10 && _running; i++)
            {
                Thread.Sleep(100);
            }
        }
    }

    private void RefreshDue(HttpClient http)
    {
        DateTimeOffset now = DateTimeOffset.Now;

        if (now - _weatherAt >= WeatherEvery)
        {
            _weatherAt = now;
            TryUpdateWeather(http);
        }

        if (now - _ratesAt >= RatesEvery)
        {
            _ratesAt = now;
            TryUpdateDollar(http);
            TryUpdateCrypto(http);
        }

        // Holidays: refresh once per calendar day (after location is known).
        if (_holidaysFor.Date != now.Date && !string.IsNullOrEmpty(_country))
        {
            _holidaysFor = now.Date;
            TryUpdateHolidayToday(http, now.Date);
        }
    }

    // ---- Public holidays (Nager.Date) ------------------------------------

    private void TryUpdateHolidayToday(HttpClient http, DateTime today)
    {
        try
        {
            // date.nager.at: free, no key. Returns the year's public holidays for a country.
            string url = $"https://date.nager.at/api/v3/PublicHolidays/{today.Year}/{_country}";
            string json = http.GetStringAsync(url).GetAwaiter().GetResult();

            string isoToday = today.ToString("yyyy-MM-dd");
            string holiday = string.Empty;

            using JsonDocument doc = JsonDocument.Parse(json);
            foreach (JsonElement h in doc.RootElement.EnumerateArray())
            {
                if (h.TryGetProperty("date", out JsonElement d) && d.GetString() == isoToday)
                {
                    // Prefer the localised name; fall back to English.
                    holiday = h.TryGetProperty("localName", out JsonElement ln) && ln.GetString() is { Length: > 0 } loc
                        ? loc
                        : (h.TryGetProperty("name", out JsonElement n) ? (n.GetString() ?? string.Empty) : string.Empty);
                    break;
                }
            }

            lock (_gate)
            {
                _snapshot = _snapshot with { TodayHoliday = holiday };
            }
        }
        catch
        {
            // No holiday data this round; tried again tomorrow.
        }
    }

    // ---- Weather (+ IP geolocation) --------------------------------------

    private void TryUpdateWeather(HttpClient http)
    {
        try
        {
            if (_lat is null || _lon is null)
            {
                ResolveLocation(http);
            }

            if (_lat is null || _lon is null)
            {
                return;
            }

            string lat = _lat.Value.ToString(CultureInfo.InvariantCulture);
            string lon = _lon.Value.ToString(CultureInfo.InvariantCulture);
            string url = $"https://api.open-meteo.com/v1/forecast?latitude={lat}&longitude={lon}&current=temperature_2m,weather_code";
            string json = http.GetStringAsync(url).GetAwaiter().GetResult();

            using JsonDocument doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("current", out JsonElement cur))
            {
                return;
            }

            double temp = cur.TryGetProperty("temperature_2m", out JsonElement t) ? t.GetDouble() : double.NaN;
            int code = cur.TryGetProperty("weather_code", out JsonElement c) ? c.GetInt32() : -1;
            if (double.IsNaN(temp))
            {
                return;
            }

            WeatherMood mood = ClassifyWeather(temp, code);
            lock (_gate)
            {
                _snapshot = _snapshot with { HasWeather = true, TemperatureC = temp, Weather = mood };
            }
        }
        catch
        {
            // No weather this round; tried again on the next refresh.
        }
    }

    private void ResolveLocation(HttpClient http)
    {
        try
        {
            // ip-api.com: free, no key, returns approximate city + lat/lon + country code.
            string json = http.GetStringAsync("http://ip-api.com/json/?fields=status,city,lat,lon,countryCode").GetAwaiter().GetResult();
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;
            if (root.TryGetProperty("status", out JsonElement st) && st.GetString() == "success")
            {
                _lat = root.GetProperty("lat").GetDouble();
                _lon = root.GetProperty("lon").GetDouble();
                _country = root.TryGetProperty("countryCode", out JsonElement cc) ? (cc.GetString() ?? string.Empty) : string.Empty;
                string city = root.TryGetProperty("city", out JsonElement ci) ? (ci.GetString() ?? string.Empty) : string.Empty;
                lock (_gate)
                {
                    _snapshot = _snapshot with { City = city };
                }
            }
        }
        catch
        {
            // No location → no weather this round; tried again next time.
        }
    }

    // Open-Meteo WMO weather codes: 0 clear … 45/48 fog … 51-67 rain … 71-77 snow …
    private static WeatherMood ClassifyWeather(double temp, int code)
    {
        if (code is >= 71 and <= 77 or 85 or 86)
        {
            return WeatherMood.Snow;
        }

        if (code is (>= 51 and <= 67) or (>= 80 and <= 82) or (>= 95 and <= 99))
        {
            return WeatherMood.Rain;
        }

        return temp switch
        {
            <= 8 => WeatherMood.Cold,
            <= 15 => WeatherMood.Cool,
            <= 24 => WeatherMood.Mild,
            <= 30 => WeatherMood.Warm,
            _ => WeatherMood.Hot,
        };
    }

    // ---- Dollar (Argentina "blue") ---------------------------------------

    private void TryUpdateDollar(HttpClient http)
    {
        try
        {
            string json = http.GetStringAsync("https://dolarapi.com/v1/dolares/blue").GetAwaiter().GetResult();
            using JsonDocument doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("venta", out JsonElement v) && v.TryGetDouble(out double venta))
            {
                lock (_gate)
                {
                    _snapshot = _snapshot with { HasDollar = true, DollarBlue = (int)Math.Round(venta) };
                }
            }
        }
        catch
        {
            // leave dollar as-is
        }
    }

    // ---- Crypto (BTC/USD) ------------------------------------------------

    private void TryUpdateCrypto(HttpClient http)
    {
        try
        {
            string json = http.GetStringAsync("https://api.coingecko.com/api/v3/simple/price?ids=bitcoin&vs_currencies=usd").GetAwaiter().GetResult();
            using JsonDocument doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("bitcoin", out JsonElement btc) &&
                btc.TryGetProperty("usd", out JsonElement usd) && usd.TryGetDouble(out double price))
            {
                lock (_gate)
                {
                    _snapshot = _snapshot with { HasCrypto = true, BtcUsd = (long)Math.Round(price) };
                }
            }
        }
        catch
        {
            // leave crypto as-is
        }
    }
}
