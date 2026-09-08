using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Bizden.Infrastructure.Email;

public sealed class SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task<bool> SendVerificationCodeAsync(string recipient, string code, CancellationToken cancellationToken)
    {
        var host = configuration["Smtp:Host"];
        var from = configuration["Smtp:From"];
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(from))
        {
            logger.LogError("SMTP is not configured; verification email for {Recipient} was not sent", recipient);
            return false;
        }

        try
        {
            var port = int.TryParse(configuration["Smtp:Port"], out var configuredPort) ? configuredPort : 587;
            using var client = new SmtpClient(host, port)
            {
                EnableSsl = !string.Equals(configuration["Smtp:EnableSsl"], "false", StringComparison.OrdinalIgnoreCase)
            };
            var username = configuration["Smtp:UserName"];
            var password = configuration["Smtp:Password"];
            if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password)) client.Credentials = new NetworkCredential(username, password);
            using var message = new MailMessage(from, recipient)
            {
                Subject = "Bizdən email təsdiqi",
                Body = $"Bizdən hesabınızı təsdiqləmək üçün kodunuz: {code}\n\nKod 15 dəqiqə etibarlıdır.",
                IsBodyHtml = false
            };
            var fromName = configuration["Smtp:FromName"];
            if (!string.IsNullOrWhiteSpace(fromName)) message.From = new MailAddress(from, fromName);
            await client.SendMailAsync(message, cancellationToken);
            return true;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Verification email could not be sent to {Recipient}", recipient);
            return false;
        }
    }
}
