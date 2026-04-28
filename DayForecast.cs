using System;

namespace WeatherApp
{
    /// Model jednego dnia prognozy pogody.
    public class DayForecast
    {
        public string DayName     { get; set; } = "";
        public DateTime Date      { get; set; }
        public int TempMax        { get; set; }
        public int TempMin        { get; set; }
        public string Description { get; set; } = "";
        public string Icon        { get; set; } = "";
        public int Humidity       { get; set; }
    }
}
