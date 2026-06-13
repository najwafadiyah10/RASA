namespace RASA.DTOs
{
    public class FirebaseRegisterRequest
    {
        public string IdToken { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Phone { get; set; } = string.Empty;

        public string Role { get; set; } = string.Empty;
    }
}
