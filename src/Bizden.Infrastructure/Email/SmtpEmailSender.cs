using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Bizden.Infrastructure.Email;

public sealed class SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public Task<bool> SendOneTimeCodeAsync(string recipient, EmailCodePurpose purpose, string code, CancellationToken cancellationToken)
    {
        var (subject, body) = purpose switch
        {
            EmailCodePurpose.PasswordReset => ("Bizdən şifrə sıfırlama", $"Bizdən hesabınızın şifrəsini sıfırlamaq üçün kodunuz: {code}\n\nKod 15 dəqiqə etibarlıdır. Bu sorğunu siz etməmisinizsə, bu emaili nəzərə almayın."),
            EmailCodePurpose.EmailChange => ("Bizdən email dəyişmə", $"Bizdən hesabınızın email ünvanını dəyişmək üçün kodunuz: {code}\n\nKod 15 dəqiqə etibarlıdır."),
            _ => ("Bizdən email təsdiqi", $"Bizdən hesabınızı təsdiqləmək üçün kodunuz: {code}\n\nKod 15 dəqiqə etibarlıdır.")
        };
        return SendAsync(recipient, subject, body, cancellationToken);
    }

    public Task<bool> SendExportReadyAsync(string recipient, string eventName, CancellationToken cancellationToken) =>
        SendAsync(recipient, "Bizdən export hazırdır", $"\"{eventName}\" tədbirinizin ZIP exportu hazırdır. Hesabınıza daxil olaraq yükləyə bilərsiniz.", cancellationToken);

    private async Task<bool> SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken)
    {
        var host = configuration["Smtp:Host"];
        var from = configuration["Smtp:From"];
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(from))
        {
            logger.LogError("SMTP is not configured; email for {Recipient} was not sent", recipient);
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
                Subject = subject,
                Body = body,
                IsBodyHtml = false
            };
            var fromName = configuration["Smtp:FromName"];
            if (!string.IsNullOrWhiteSpace(fromName)) message.From = new MailAddress(from, fromName);
            await client.SendMailAsync(message, cancellationToken);
            return true;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Email could not be sent to {Recipient}", recipient);
            return false;
        }
    }
}
