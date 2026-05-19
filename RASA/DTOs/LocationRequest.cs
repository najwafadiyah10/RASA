namespace RasaApi.DTOs
{
    public class LocationRequest
    {
        public double Latitude { get; set; }

        public double Longitude { get; set; }

        public double? Accuracy { get; set; }
    }
}