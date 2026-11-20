using System;
using System.Collections;

namespace PortalDesktop.Data {
    /// <summary>What a weather icon shows.</summary>
    public enum WeatherKind { Clear, PartlyCloudy, Cloudy, Fog, Drizzle, Rain, Snow, Storm }

    /// <summary>Current conditions and a 5-day forecast from open-meteo.com.</summary>
    public class WeatherData {
        public const int Days = 5;

        public double Temperature;
        public double FeelsLike;
        public int Humidity;
        public double Wind;
        public int Code;
        public bool IsDay = true;
        public int UtcOffsetSeconds;
        public string TimeZone = "";
        public DateTime Updated;         // local time of the reading

        public DateTime[] DayDates = new DateTime[0];
        public int[] DayCodes = new int[0];
        public double[] DayHigh = new double[0];
        public double[] DayLow = new double[0];
        public DateTime Sunrise = DateTime.MinValue;
        public DateTime Sunset = DateTime.MinValue;

        public WeatherKind Kind => KindOf(this.Code);
        public string Description => Describe(this.Code);

        /// <summary>The open-meteo /v1/forecast URL with everything this class reads.</summary>
        public static string Url(double latitude, double longitude, bool metric) =>
            "http://api.open-meteo.com/v1/forecast?latitude=" + Coordinate(latitude) + "&longitude=" + Coordinate(longitude) +
            "&current=temperature_2m,apparent_temperature,relative_humidity_2m,weather_code,wind_speed_10m,is_day" +
            "&daily=weather_code,temperature_2m_max,temperature_2m_min,sunrise,sunset" +
            "&timezone=auto&forecast_days=" + Days +
            (metric ? "" : "&temperature_unit=fahrenheit&wind_speed_unit=mph");

        public static WeatherData Parse(string json) {
            var root = Json.Parse(json);
            if (Json.Str(root, "error") != null || Json.Bool(root, "error")) throw new Exception(Json.Str(root, "reason") ?? "Weather service error");
            var current = Json.Obj(root, "current");
            if (current == null) throw new Exception("No current weather in the answer");

            var w = new WeatherData {
                Temperature = Json.Num(current, "temperature_2m", 0),
                FeelsLike = Json.Num(current, "apparent_temperature", Json.Num(current, "temperature_2m", 0)),
                Humidity = (int)Json.Num(current, "relative_humidity_2m", 0),
                Wind = Json.Num(current, "wind_speed_10m", 0),
                Code = (int)Json.Num(current, "weather_code", 0),
                IsDay = Json.Num(current, "is_day", 1) != 0,
                UtcOffsetSeconds = (int)Json.Num(root, "utc_offset_seconds", 0),
                TimeZone = Json.Str(root, "timezone") ?? "",
            };
            Clock.TryParseIso(Json.Str(current, "time"), out w.Updated);

            var daily = Json.Obj(root, "daily");
            var times = Json.Arr(daily, "time");
            var count = times == null ? 0 : times.Count;
            w.DayDates = new DateTime[count];
            w.DayCodes = new int[count];
            w.DayHigh = new double[count];
            w.DayLow = new double[count];
            var codes = Json.Arr(daily, "weather_code");
            var highs = Json.Arr(daily, "temperature_2m_max");
            var lows = Json.Arr(daily, "temperature_2m_min");
            for (var i = 0; i < count; i++) {
                Clock.TryParseIso(Json.StrAt(times, i), out w.DayDates[i]);
                w.DayCodes[i] = (int)Json.NumAt(codes, i, 0);
                w.DayHigh[i] = Json.NumAt(highs, i, 0);
                w.DayLow[i] = Json.NumAt(lows, i, 0);
            }
            Clock.TryParseIso(Json.StrAt(Json.Arr(daily, "sunrise"), 0), out w.Sunrise);
            Clock.TryParseIso(Json.StrAt(Json.Arr(daily, "sunset"), 0), out w.Sunset);
            return w;
        }

        /// <summary>WMO weather code (used by open-meteo) to icon.</summary>
        public static WeatherKind KindOf(int code) {
            if (code == 0) return WeatherKind.Clear;
            if (code <= 2) return WeatherKind.PartlyCloudy;
            if (code == 3) return WeatherKind.Cloudy;
            if (code == 45 || code == 48) return WeatherKind.Fog;
            if (code >= 51 && code <= 57) return WeatherKind.Drizzle;
            if ((code >= 61 && code <= 67) || (code >= 80 && code <= 82)) return WeatherKind.Rain;
            if ((code >= 71 && code <= 77) || code == 85 || code == 86) return WeatherKind.Snow;
            if (code >= 95) return WeatherKind.Storm;
            return WeatherKind.Cloudy;
        }

        public static string Describe(int code) {
            switch (code) {
                case 0: return "Clear";
                case 1: return "Mostly clear";
                case 2: return "Partly cloudy";
                case 3: return "Overcast";
                case 45: case 48: return "Fog";
                case 51: case 53: case 55: return "Drizzle";
                case 56: case 57: return "Freezing drizzle";
                case 61: return "Light rain";
                case 63: return "Rain";
                case 65: return "Heavy rain";
                case 66: case 67: return "Freezing rain";
                case 71: return "Light snow";
                case 73: return "Snow";
                case 75: return "Heavy snow";
                case 77: return "Snow grains";
                case 80: case 81: return "Rain showers";
                case 82: return "Heavy showers";
                case 85: case 86: return "Snow showers";
                case 95: return "Thunderstorm";
                case 96: case 99: return "Storm with hail";
                default: return "Cloudy";
            }
        }

        static string Coordinate(double v) => Ui.Format.Plain(v, 4);
    }

    /// <summary>Where the Portal is, from weather.txt or looked up from the internet address.</summary>
    public class Location {
        public string Place = "";
        public double Latitude;
        public double Longitude;
        public string TimeZone = "";
        public int UtcOffsetSeconds;
        public bool HasOffset;

        /// <summary>ip-api.com: free, no key, plain HTTP. Gives the area of the public IP address.</summary>
        public const string LookupUrl = "http://ip-api.com/json/?fields=status,message,city,region,lat,lon,timezone,offset";

        public static Location ParseLookup(string json) {
            var root = Json.Parse(json);
            if (Json.Str(root, "status") != "success") throw new Exception(Json.Str(root, "message") ?? "Location lookup failed");
            var city = Json.Str(root, "city") ?? "";
            var region = Json.Str(root, "region") ?? "";
            return new Location {
                Place = region.Length > 0 && city.Length > 0 ? city + ", " + region : city,
                Latitude = Json.Num(root, "lat", 0),
                Longitude = Json.Num(root, "lon", 0),
                TimeZone = Json.Str(root, "timezone") ?? "",
                UtcOffsetSeconds = (int)Json.Num(root, "offset", 0),
                HasOffset = Json.Has(root, "offset"),
            };
        }

        /// <summary>Saved to Portal\cache\location.txt so the clock and weather are right before the network is up.</summary>
        public string ToText() {
            var f = new SettingsFile();
            f.Set("place", this.Place);
            f.Set("latitude", Ui.Format.Plain(this.Latitude, 4));
            f.Set("longitude", Ui.Format.Plain(this.Longitude, 4));
            f.Set("timezone", this.TimeZone);
            f.Set("offset", this.UtcOffsetSeconds.ToString());
            return f.ToText();
        }

        public static Location FromText(string text) {
            var f = new SettingsFile(text);
            var l = new Location {
                Place = f.Get("place", ""),
                Latitude = f.GetNumber("latitude", 0),
                Longitude = f.GetNumber("longitude", 0),
                TimeZone = f.Get("timezone", ""),
                UtcOffsetSeconds = (int)f.GetNumber("offset", 0),
                HasOffset = f.Get("offset", "").Length > 0,
            };
            return l.Latitude == 0 && l.Longitude == 0 ? null : l;
        }
    }
}
