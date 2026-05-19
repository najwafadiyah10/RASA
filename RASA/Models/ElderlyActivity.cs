using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RasaApi.Models
{
    [Table("activities")]
    public class ElderlyActivity
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("elderly_id")]
        public Guid ElderlyId { get; set; }

        [Column("x_axis")]
        public double? XAxis { get; set; }

        [Column("y_axis")]
        public double? YAxis { get; set; }

        [Column("z_axis")]
        public double? ZAxis { get; set; }

        [Column("acceleration_value")]
        public double? AccelerationValue { get; set; }

        [Required]
        [Column("activity_status")]
        public string ActivityStatus { get; set; } = string.Empty;

        [Required]
        [Column("risk_level")]
        public string RiskLevel { get; set; } = "normal";

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}