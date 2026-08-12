using System.ComponentModel.DataAnnotations.Schema;

namespace DigiPOSE.Models
{
    [NotMapped]
    public class MailInfo
    {
        public string ToEmail { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public List<IFormFile>? Attachments { get; set; }
    }
    [NotMapped]
    public class MailSettings
    {
        public string Address { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; }
    }
}