namespace RasaApi.DTOs
{
    public class AlertRequest
    {
        public string AlertType { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public string RiskLevel { get; set; } = "waspada";

        //public double? Latitude { get; set; }

        //public double? Longitude { get; set; }
    }
}