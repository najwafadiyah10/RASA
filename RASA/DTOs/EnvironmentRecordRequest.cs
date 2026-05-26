namespace RasaApi.DTOs
{
    public class EnvironmentRecordRequest
    {
        public double? Temperature { get; set; }

        public double? Humidity { get; set; }

        public string AirQuality { get; set; } = string.Empty;

        public string RiskLevel { get; set; } = string.Empty;

        public double? Latitude { get; set; }

        public double? Longitude { get; set; }
    }
}