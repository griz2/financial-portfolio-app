namespace Portfolio.Core.Management
{
    public class ResourceNotFoundException : Exception
    {
        public ResourceNotFoundException() : base("The requested record was not found.")
        {
        }
    }
}
