namespace Bizden.Infrastructure.Email;

public interface IEmailSender
{
    Task<bool> SendVerificationCodeAsync(string recipient, string code, CancellationToken cancellationToken);
}
