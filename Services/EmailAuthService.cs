namespace NutritionTracker.Services;

public class EmailAuthService
{
    private readonly BackendSyncService _sync;

    public EmailAuthService(BackendSyncService sync)
    {
        _sync = sync;
    }

    public Task<(bool ok, string message)> RegisterAsync(string email, string password, string? displayName = null)
    {
        var normalizedEmail = NormalizeEmail(email);
        if (string.IsNullOrWhiteSpace(normalizedEmail) || !normalizedEmail.Contains('@'))
            return Task.FromResult((false, LocalizationService.T("login_email_invalid")));

        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
            return Task.FromResult((false, LocalizationService.T("login_password_short")));

        return _sync.RegisterEmailAsync(normalizedEmail, password, displayName);
    }

    public async Task<(bool ok, string message, string name)> LoginAsync(string email, string password)
    {
        var normalizedEmail = NormalizeEmail(email);
        if (string.IsNullOrWhiteSpace(normalizedEmail) || string.IsNullOrWhiteSpace(password))
            return (false, LocalizationService.T("login_email_password_required"), "");

        var (ok, message, _, name) = await _sync.LoginEmailAsync(normalizedEmail, password);
        return (ok, message, name);
    }

    public Task<(bool ok, string message)> VerifyEmailCodeAsync(string email, string code)
    {
        var normalizedEmail = NormalizeEmail(email);
        if (string.IsNullOrWhiteSpace(normalizedEmail) || string.IsNullOrWhiteSpace(code))
            return Task.FromResult((false, LocalizationService.T("verification_code_required")));

        return _sync.VerifyEmailCodeAsync(normalizedEmail, code);
    }

    public Task<(bool ok, string message)> ResendVerificationCodeAsync(string email)
    {
        var normalizedEmail = NormalizeEmail(email);
        if (string.IsNullOrWhiteSpace(normalizedEmail) || !normalizedEmail.Contains('@'))
            return Task.FromResult((false, LocalizationService.T("login_email_invalid")));

        return _sync.ResendVerificationCodeAsync(normalizedEmail);
    }

    public Task<(bool ok, string message)> RequestPasswordResetAsync(string email)
    {
        var normalizedEmail = NormalizeEmail(email);
        if (string.IsNullOrWhiteSpace(normalizedEmail) || !normalizedEmail.Contains('@'))
            return Task.FromResult((false, LocalizationService.T("login_email_invalid")));

        return _sync.RequestPasswordResetAsync(normalizedEmail);
    }

    public Task<(bool ok, string message)> ResetPasswordAsync(string email, string code, string newPassword)
    {
        var normalizedEmail = NormalizeEmail(email);
        if (string.IsNullOrWhiteSpace(normalizedEmail) || string.IsNullOrWhiteSpace(code))
            return Task.FromResult((false, LocalizationService.T("verification_code_required")));
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
            return Task.FromResult((false, LocalizationService.T("login_password_short")));

        return _sync.ResetPasswordAsync(normalizedEmail, code, newPassword);
    }

    public Task<(bool ok, string message)> ChangePasswordAsync(string currentPassword, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(currentPassword) || string.IsNullOrWhiteSpace(newPassword))
            return Task.FromResult((false, LocalizationService.T("login_email_password_required")));
        if (newPassword.Length < 6)
            return Task.FromResult((false, LocalizationService.T("login_password_short")));

        return _sync.ChangePasswordAsync(currentPassword, newPassword);
    }

    public Task<(bool ok, string message)> DeleteAccountAsync(string? password)
    {
        return _sync.DeleteAccountAsync(password);
    }

    private static string NormalizeEmail(string email) => (email ?? "").Trim().ToLowerInvariant();
}
