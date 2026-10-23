namespace Portfolio.Core.Authentication
{
    public class RegistrationResult
    {
        public bool Succeeded { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
    }
}
