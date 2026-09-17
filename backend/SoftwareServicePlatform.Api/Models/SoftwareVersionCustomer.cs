namespace SoftwareServicePlatform.Api.Models
{
    public class SoftwareVersionCustomer
    {
        public int Id { get; set; }

        public int SoftwareVersionId { get; set; }

        public int CustomerId { get; set; }

        public DateTime CreatedAt { get; set; }
            = DateTime.UtcNow;

        public SoftwareVersion? SoftwareVersion { get; set; }

        public Customer? Customer { get; set; }
    }
}