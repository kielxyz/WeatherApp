using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace WeatherApp
{
    public partial class MainWindow : Window
    {
        private const string ApiKey = "183844d4fe3ef7a8f4d8b008160807cb";
        private const string BaseUrl = "https://api.openweathermap.org/data/2.5";

        private readonly HttpClient _httpClient = new();

        public MainWindow()
        {
            InitializeComponent();
            // Załaduj pogodę dla domyślnego miasta po uruchomieniu
            Loaded += async (_, _) => await LoadWeatherAsync("Biesko-Biała");
        }

        //  Obsługa zdarzeń UI
        private async void SearchButton_Click(object sender, RoutedEventArgs e)
            => await LoadWeatherAsync(CityTextBox.Text.Trim()); // Obsługa kliknięcia przycisku "Szukaj"

        private async void CityTextBox_KeyDown(object sender, KeyEventArgs e) // Obsługa naciśnięcia klawisza Enter w wyszukiwarce
        {
            if (e.Key == Key.Enter)
                await LoadWeatherAsync(CityTextBox.Text.Trim());
        }

        //  Główna metoda ładowania pogody
        private async Task LoadWeatherAsync(string city)
        {
            if (string.IsNullOrWhiteSpace(city)) return;

            ShowStatus("⏳ Wczytywanie...");

            try
            {
                // Aktualna pogoda 
                var currentJson = await FetchAsync(
                    $"{BaseUrl}/weather?q={Uri.EscapeDataString(city)}&appid={ApiKey}&units=metric&lang=pl");

                using var currentDoc = JsonDocument.Parse(currentJson);
                var root = currentDoc.RootElement;

                // Obsługa błędów zwróconych przez API
                if (root.TryGetProperty("cod", out var codEl) && codEl.ToString() != "200")
                {
                    var msg = root.TryGetProperty("message", out var msgEl) ? msgEl.GetString() : "Nieznany błąd";
                    ShowStatus($"❌ Błąd: {msg}");
                    return;
                }

                var weather      = root.GetProperty("weather")[0];
                var main         = root.GetProperty("main");
                var wind         = root.GetProperty("wind");
                var sys          = root.GetProperty("sys");
                var country      = sys.GetProperty("country").GetString();
                var cityName     = root.GetProperty("name").GetString();
                var temp         = (int)Math.Round(main.GetProperty("temp").GetDouble());
                var humidity     = main.GetProperty("humidity").GetInt32();
                var windSpeed    = wind.GetProperty("speed").GetDouble();
                var description  = weather.GetProperty("description").GetString() ?? "";
                var weatherId    = weather.GetProperty("id").GetInt32();

                CityNameText.Text      = $"{cityName}, {country}";
                CurrentDateText.Text   = DateTime.Now.ToString("dddd, d MMMM yyyy", new CultureInfo("pl-PL"));
                CurrentTempText.Text   = $"{temp}°C";
                CurrentDescText.Text   = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(description);
                CurrentHumidityText.Text = $"Wilgotność: {humidity}%";
                CurrentWindText.Text   = $"Wiatr: {windSpeed:F1} m/s";
                CurrentIconText.Text   = GetWeatherEmoji(weatherId);

                // Prognoza 5-dniowa
                var forecastJson = await FetchAsync(
                    $"{BaseUrl}/forecast?q={Uri.EscapeDataString(city)}&appid={ApiKey}&units=metric&lang=pl");

                using var forecastDoc = JsonDocument.Parse(forecastJson);
                var forecastRoot = forecastDoc.RootElement;

                var forecasts = ParseForecast(forecastRoot);
                ForecastItemsControl.ItemsSource = forecasts;

                // Pokaż UI
                CurrentWeatherPanel.Visibility = Visibility.Visible;
                ForecastGrid.Visibility        = Visibility.Visible;
                StatusPanel.Visibility         = Visibility.Collapsed;
            }
            catch (HttpRequestException ex)
            {
                ShowStatus($"❌ Błąd połączenia:\n{ex.Message}");
            }
            catch (Exception ex)
            {
                ShowStatus($"❌ Nieoczekiwany błąd:\n{ex.Message}");
            }
        }

        //  Parsowanie prognozy
        private static List<DayForecast> ParseForecast(JsonElement root)
        {
            var list = root.GetProperty("list");

            // Grupuj wpisy po dacie
            var grouped = new Dictionary<DateTime, List<JsonElement>>();
            foreach (var item in list.EnumerateArray())
            {
                var dt = DateTimeOffset.FromUnixTimeSeconds(item.GetProperty("dt").GetInt64()).LocalDateTime.Date;
                if (!grouped.ContainsKey(dt)) grouped[dt] = new List<JsonElement>();
                grouped[dt].Add(item);
            }

            var result = new List<DayForecast>();
            var culture = new CultureInfo("pl-PL");

            foreach (var (date, items) in grouped.OrderBy(k => k.Key).Take(7))
            {
                var temps = items.Select(i => i.GetProperty("main").GetProperty("temp").GetDouble()).ToList();
                var humidities = items.Select(i => i.GetProperty("main").GetProperty("humidity").GetInt32()).ToList();

                // Bierz pogodę z wpisu środka dnia (lub pierwszego dostępnego)
                var midItem = items.FirstOrDefault(i =>
                {
                    var h = DateTimeOffset.FromUnixTimeSeconds(i.GetProperty("dt").GetInt64()).LocalDateTime.Hour;
                    return h is >= 11 and <= 14;
                });
                if (midItem.ValueKind == JsonValueKind.Undefined) midItem = items[0];

                var weatherArr = midItem.GetProperty("weather")[0];
                var desc       = weatherArr.GetProperty("description").GetString() ?? "";
                var wid        = weatherArr.GetProperty("id").GetInt32();

                var isToday = date == DateTime.Today;
                var dayName = isToday ? "Dziś" : date.ToString("ddd", culture).ToUpper();

                result.Add(new DayForecast
                {
                    DayName     = dayName,
                    Date        = date,
                    TempMax     = (int)Math.Round(temps.Max()),
                    TempMin     = (int)Math.Round(temps.Min()),
                    Description = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(desc),
                    Icon        = GetWeatherEmoji(wid),
                    Humidity    = (int)Math.Round(humidities.Average()),
                });
            }

            return result;
        }

        //  Pomocnicze
        private async Task<string> FetchAsync(string url)
        {
            var response = await _httpClient.GetAsync(url);
            return await response.Content.ReadAsStringAsync();
        }

        private void ShowStatus(string message)
        {
            CurrentWeatherPanel.Visibility = Visibility.Collapsed;
            ForecastGrid.Visibility        = Visibility.Collapsed;
            StatusPanel.Visibility         = Visibility.Visible;
            StatusText.Text                = message;
        }

        private static string GetWeatherEmoji(int weatherId) => weatherId switch
        {
            >= 200 and < 300 => "⛈",   // burza
            >= 300 and < 400 => "🌦",   // mżawka
            >= 500 and < 504 => "🌧",   // deszcz
            511              => "🌨",   // deszcz ze śniegiem
            >= 520 and < 600 => "🌧",   // przelotny deszcz
            >= 600 and < 700 => "❄️",   // śnieg
            >= 700 and < 800 => "🌫",   // mgła/zamglenie
            800              => "☀️",   // bezchmurnie
            801              => "🌤",   // lekkie zachmurzenie
            802              => "⛅",   // częściowe zachmurzenie
            803 or 804       => "☁️",   // zachmurzenie
            _                => "🌡"
        };
    }
}
