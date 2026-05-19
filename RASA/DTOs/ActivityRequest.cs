namespace RasaApi.DTOs
{
    public class ActivityRequest
    {
        public double? XAxis { get; set; }

        public double? YAxis { get; set; }

        public double? ZAxis { get; set; }

        public double? AccelerationValue { get; set; }

        public string ActivityStatus { get; set; } = string.Empty;

        public string RiskLevel { get; set; } = "normal";
    }
}