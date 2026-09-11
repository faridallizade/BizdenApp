namespace Bizden.Infrastructure.Email;

public interface IEmailSender
{
    Task<bool> SendOneTimeCodeAsync(string recipient, EmailCodePurpose purpose, string code, CancellationToken cancellationToken);
    Task<bool> SendExportReadyAsync(string recipient, string eventName, CancellationToken cancellationToken);
}

public enum EmailCodePurpose { Verification, PasswordReset, EmailChange }
