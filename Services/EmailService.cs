using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace SmartClinic.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            var smtpHost = _configuration["Email:SmtpHost"];
            var smtpPortRaw = _configuration["Email:SmtpPort"];
            var senderEmail = _configuration["Email:SenderEmail"];
            var senderName = _configuration["Email:SenderName"];
            var username = _configuration["Email:Username"]?.Trim();
            var password = _configuration["Email:Password"]?.Replace(" ", string.Empty).Trim();
            var enableSslRaw = _configuration["Email:EnableSsl"];

            if (string.IsNullOrWhiteSpace(smtpHost) ||
                string.IsNullOrWhiteSpace(smtpPortRaw) ||
                string.IsNullOrWhiteSpace(senderEmail) ||
                string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrWhiteSpace(password) ||
                !int.TryParse(smtpPortRaw, out var smtpPort))
            {
                throw new InvalidOperationException("Thiếu cấu hình Email SMTP trong appsettings.");
            }

            var enableSsl = true;
            if (!string.IsNullOrWhiteSpace(enableSslRaw) && bool.TryParse(enableSslRaw, out var parsedSsl))
            {
                enableSsl = parsedSsl;
            }

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(
                string.IsNullOrWhiteSpace(senderName) ? "SmartClinic" : senderName,
                senderEmail));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = subject;
            message.Body = new TextPart("plain")
            {
                Text = body
            };

            using var client = new SmtpClient();
            var socketOptions = enableSsl
                ? SecureSocketOptions.StartTls
                : SecureSocketOptions.None;

            _logger.LogInformation("Sending email to {ToEmail} via {SmtpHost}:{SmtpPort}", toEmail, smtpHost, smtpPort);

            await client.ConnectAsync(smtpHost, smtpPort, socketOptions);
            await client.AuthenticateAsync(username, password);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
    }
}
