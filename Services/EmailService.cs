using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace SocialExposure.Services
{
    public class EmailService
    {
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<EmailService> _logger;

        public EmailService(
            IConfiguration configuration,
            IWebHostEnvironment environment,
            ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _environment = environment;
            _logger = logger;
        }

        public async Task<bool> SendOTPAsync(string email, string otp)
        {
            var senderName = _configuration["EmailSettings:SenderName"];
            var senderEmail = _configuration["EmailSettings:SenderEmail"];
            var smtpServer = _configuration["EmailSettings:SmtpServer"];
            var username = _configuration["EmailSettings:Username"];
            var password = _configuration["EmailSettings:Password"];

            if (string.IsNullOrWhiteSpace(senderEmail) ||
                string.IsNullOrWhiteSpace(smtpServer) ||
                string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrWhiteSpace(password))
            {
                if (_environment.IsDevelopment())
                {
                    _logger.LogWarning(
                        "Email is not configured. Using the development OTP display instead.");
                    return false;
                }

                throw new InvalidOperationException(
                    "EmailSettings is incomplete. Configure SMTP before using OTP login.");
            }

            var message = new MimeMessage();

            message.From.Add(new MailboxAddress(
                senderName ?? "Social Exposure",
                senderEmail
            ));

            message.To.Add(MailboxAddress.Parse(email));

            message.Subject = "Social Exposure - Your OTP";

            message.Body = new TextPart("plain")
            {
                Text = $"""
                Hello,

                Your Social Exposure verification code is:

                {otp}

                This code will expire in 10 minutes.

                If you did not request this code, please ignore this email.

                Regards,
                Social Exposure
                """
            };

            using var smtp = new SmtpClient();

            if (_environment.IsDevelopment())
            {
                smtp.ServerCertificateValidationCallback =
                    (sender, certificate, chain, sslPolicyErrors) => true;
            }

            await smtp.ConnectAsync(
                smtpServer,
                int.Parse(_configuration["EmailSettings:Port"] ?? "587"),
                SecureSocketOptions.StartTls
            );

            await smtp.AuthenticateAsync(
                username,
                password
            );

            await smtp.SendAsync(message);

            await smtp.DisconnectAsync(true);
            return true;
        }
    }
}
