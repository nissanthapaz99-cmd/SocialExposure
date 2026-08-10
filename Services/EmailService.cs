using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace SocialExposure.Services
{
    public class EmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendOTPAsync(string email, string otp)
        {
            var message = new MimeMessage();

            message.From.Add(new MailboxAddress(
                _configuration["EmailSettings:SenderName"],
                _configuration["EmailSettings:SenderEmail"]
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

            // Local development fix for the certificate
            smtp.ServerCertificateValidationCallback =
                (sender, certificate, chain, sslPolicyErrors) => true;

            await smtp.ConnectAsync(
                _configuration["EmailSettings:SmtpServer"],
                int.Parse(_configuration["EmailSettings:Port"] ?? "587"),
                SecureSocketOptions.StartTls
            );

            await smtp.AuthenticateAsync(
                _configuration["EmailSettings:Username"],
                _configuration["EmailSettings:Password"]
            );

            await smtp.SendAsync(message);

            await smtp.DisconnectAsync(true);
        }
    }
}