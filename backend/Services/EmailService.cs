using backend.Config;
using backend.Infrastructure;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;

namespace backend.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string subject, string toEmail, string body);
    }

    public class EmailService : IEmailService
    {
        private readonly EmailOptions _opts;

        public EmailService(IOptions<EmailOptions> opts)
        {
            _opts = opts.Value;
        }
        public async Task SendEmailAsync(string subject, string toEmail, string body)
        {
            var client = new SmtpClient("smtp.gmail.com")
            {
                Port = 587,
                EnableSsl = true,
                Credentials = new NetworkCredential(_opts.SmtpEmail, _opts.SmtpAppPassword),
            };

            var mail = new MailMessage("noreply@hamari-company.com", toEmail)
            {
                Subject = subject,
                Body = body
            };

            await client.SendMailAsync(mail);
        }
    }
}
