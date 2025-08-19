using Microsoft.Extensions.Configuration;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace weblamchoi.Services
{
    public class EmailService
    {
        private readonly IConfiguration _config;
        public EmailService(IConfiguration config)
        {
            _config = config;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            var smtpSection = _config.GetSection("Smtp");
            var fromEmail = smtpSection["Email"];
            var password = smtpSection["Password"];
            var host = smtpSection["Host"];
            var port = int.Parse(smtpSection["Port"]);
            var enableSsl = bool.Parse(smtpSection["EnableSsl"]);

            var message = new MailMessage(fromEmail, toEmail, subject, body)
            {
                IsBodyHtml = true
            };

            using var client = new SmtpClient(host, port)
            {
                Credentials = new NetworkCredential(fromEmail, password),
                EnableSsl = enableSsl
            };
            await client.SendMailAsync(message);
        }
    }
}
