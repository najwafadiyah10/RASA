using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RasaApi.Models
{
    [Table("environment_records")]
    public class EnvironmentRecord
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; }

        [Column("elderly_id")]
        public Guid ElderlyId { get; set; }

        [Column("temperature")]
        public double? Temperature { get; set; }

        [Column("humidity")]
        public double? Humidity { get; set; }

        [Column("air_quality")]
        public string AirQuality { get; set; } = string.Empty;

        [Column("risk_level")]
        public string RiskLevel { get; set; } = string.Empty;

        [Column("latitude")]
        public double? Latitude { get; set; }

        [Column("longitude")]
        public double? Longitude { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }
}