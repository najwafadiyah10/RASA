namespace RasaApi.DTOs
{
    public class UpdateProfilePhotoRequest
    {
        public IFormFile Photo { get; set; } = null!;
    }
}