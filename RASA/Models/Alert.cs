using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RasaApi.Models
{
    [Table("alerts")]
    public class Alert
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("elderly_id")]
        public Guid ElderlyId { get; set; }

        [Required]
        [Column("family_id")]
        public Guid FamilyId { get; set; }

        [Required]
        [Column("alert_type")]
        public string AlertType { get; set; } = string.Empty;

        [Required]
        [Column("message")]
        public string Message { get; set; } = string.Empty;

        [Required]
        [Column("risk_level")]
        public string RiskLevel { get; set; } = "waspada";

        //[Column("latitude")]
        //public double? Latitude { get; set; }

        //[Column("longitude")]
        //public double? Longitude { get; set; }

        //[Required]
        //[Column("status")]
        //public string Status { get; set; } = "unread";

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("is_deleted")]
        public bool IsDeleted { get; set; } = false;

        [Column("deleted_at")]
        public DateTime? DeletedAt { get; set; }
    }
}