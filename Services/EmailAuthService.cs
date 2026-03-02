using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NutritionTracker.Services;

public class EmailAuthService
{
    private const string UsersKey = "local_auth_users_v1";

    public Task<(bool ok, string message)> RegisterAsync(string email, string password, string? displayName = null)
    {
        var normalizedEmail = NormalizeEmail(email);
        if (string.IsNullOrWhiteSpace(normalizedEmail) || !normalizedEmail.Contains('@'))
            return Task.FromResult((false, LocalizationService.T("login_email_invalid")));

        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
            return Task.FromResult((false, LocalizationService.T("login_password_short")));

        var users = LoadUsers();
        if (users.Any(u => string.Equals(u.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase)))
            return Task.FromResult((false, LocalizationService.T("login_email_exists")));

        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = HashPassword(password, salt);

        users.Add(new LocalAuthUser
        {
            Email = normalizedEmail,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? normalizedEmail.Split('@')[0] : displayName.Trim(),
            PasswordHash = Convert.ToBase64String(hash),
            Salt = Convert.ToBase64String(salt),
            CreatedAtUtc = DateTime.UtcNow,
        });

        SaveUsers(users);
        return Task.FromResult((true, LocalizationService.T("login_register_success")));
    }

    public Task<(bool ok, string message, string name)> LoginAsync(string email, string password)
    {
        var normalizedEmail = NormalizeEmail(email);
        if (string.IsNullOrWhiteSpace(normalizedEmail) || string.IsNullOrWhiteSpace(password))
            return Task.FromResult((false, LocalizationService.T("login_email_password_required"), ""));

        var users = LoadUsers();
        var user = users.FirstOrDefault(u => string.Equals(u.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase));
        if (user == null)
            return Task.FromResult((false, LocalizationService.T("login_email_not_found"), ""));

        byte[] salt;
        byte[] expectedHash;
        try
        {
            salt = Convert.FromBase64String(user.Salt);
            expectedHash = Convert.FromBase64String(user.PasswordHash);
        }
        catch
        {
            return Task.FromResult((false, LocalizationService.T("login_failed"), ""));
        }

        var actualHash = HashPassword(password, salt);
        var ok = CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        if (!ok)
            return Task.FromResult((false, LocalizationService.T("login_wrong_password"), ""));

        var name = string.IsNullOrWhiteSpace(user.DisplayName) ? normalizedEmail.Split('@')[0] : user.DisplayName;
        return Task.FromResult((true, LocalizationService.T("login_success"), name));
    }

    private static string NormalizeEmail(string email) => (email ?? "").Trim().ToLowerInvariant();

    private static byte[] HashPassword(string password, byte[] salt)
        => Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, 120_000, HashAlgorithmName.SHA256, 32);

    private static List<LocalAuthUser> LoadUsers()
    {
        var raw = Preferences.Default.Get(UsersKey, "");
        if (string.IsNullOrWhiteSpace(raw))
            return new List<LocalAuthUser>();

        try
        {
            return JsonSerializer.Deserialize<List<LocalAuthUser>>(raw) ?? new List<LocalAuthUser>();
        }
        catch
        {
            return new List<LocalAuthUser>();
        }
    }

    private static void SaveUsers(List<LocalAuthUser> users)
    {
        var raw = JsonSerializer.Serialize(users);
        Preferences.Default.Set(UsersKey, raw);
    }

    private sealed class LocalAuthUser
    {
        public string Email { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string PasswordHash { get; set; } = "";
        public string Salt { get; set; } = "";
        public DateTime CreatedAtUtc { get; set; }
    }
}
