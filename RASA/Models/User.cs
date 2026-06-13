using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RasaApi.Models
{
    [Table("users")]
    public class User
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Column("email")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [Column("phone")]
        public string Phone { get; set; } = string.Empty;

        //[Required]
        [Column("password_hash")]
        public string? PasswordHash { get; set; } 

        [Required]
        [Column("role")]
        public string Role { get; set; } = string.Empty;

        [Column("photo_url")]
        public string? PhotoUrl { get; set; }

        [Column("is_photo_deleted")]
        public bool IsPhotoDeleted { get; set; } = false;

        [Column("photo_deleted_at")]
        public DateTime? PhotoDeletedAt { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column("auth_provider")]
        public string AuthProvider { get; set; } = "local";

        [Column("firebase_uid")]
        public string? FirebaseUid { get; set; }
    }
}