using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RasaApi.Models
{
    [Table("connections")]
    public class Connection
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; }

        [Column("elderly_id")]
        public Guid ElderlyId { get; set; }

        [Column("family_id")]
        public Guid FamilyId { get; set; }

        [Column("status")]
        public string Status { get; set; } = "pending";

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }
}