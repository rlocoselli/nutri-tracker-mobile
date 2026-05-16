using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Maui.Authentication;

namespace NutritionTracker.Services;

public class GoogleAuthResult
{
    public string IdToken { get; set; } = "";
    public string AccessToken { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public string Email { get; set; } = "";
    public string Name { get; set; } = "";
    public string PictureUrl { get; set; } = "";
}

public class AuthService
{
    private readonly HttpClient _http = new();

    private const string AndroidClientId =
        "199402303503-uar38edri7qu3pd3p14ngmcditvvbckq.apps.googleusercontent.com";

    private const string RedirectScheme =
        "com.googleusercontent.apps.199402303503-uar38edri7qu3pd3p14ngmcditvvbckq";

    private static readonly Uri RedirectUri = new($"{RedirectScheme}:/oauth2redirect");

    private const string BaseScope = "openid email profile";

    // OAuth2 "Authorization Code + PKCE" (robuste et recommandé pour mobile)
    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static (string verifier, string challenge) CreatePkce()
    {
        // 32 bytes => verifier ~ 43 chars (ok PKCE)
        var verifierBytes = RandomNumberGenerator.GetBytes(32);
        var verifier = Base64Url(verifierBytes);

        var challengeBytes = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        var challenge = Base64Url(challengeBytes);

        return (verifier, challenge);
    }

    public static bool IsIdTokenStillValid(string idToken, int skewSeconds = 60)
    {
        if (string.IsNullOrWhiteSpace(idToken))
            return false;

        try
        {
            var parts = idToken.Split('.');
            if (parts.Length < 2)
                return false;

            var payload = parts[1]
                .Replace('-', '+')
                .Replace('_', '/');

            var pad = payload.Length % 4;
            if (pad > 0)
                payload = payload.PadRight(payload.Length + (4 - pad), '=');

            var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("exp", out var expProp))
                return false;

            var expUnix = expProp.GetInt64();
            var expiry = DateTimeOffset.FromUnixTimeSeconds(expUnix);
            return expiry > DateTimeOffset.UtcNow.AddSeconds(skewSeconds);
        }
        catch
        {
            return false;
        }
    }


    public async Task<GoogleAuthResult> LoginAsync()
    {
        return await LoginWithScopeAsync(BaseScope);
    }

    public async Task<GoogleAuthResult?> TryRefreshAsync(string refreshToken)
    {
        var token = (refreshToken ?? "").Trim();
        if (string.IsNullOrWhiteSpace(token))
            return null;

        using var req = new HttpRequestMessage(HttpMethod.Post, "https://oauth2.googleapis.com/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = AndroidClientId,
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = token,
            })
        };

        using var resp = await _http.SendAsync(req);
        var json = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
            return null;

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var access = root.TryGetProperty("access_token", out var at) ? at.GetString() ?? "" : "";
        var idt = root.TryGetProperty("id_token", out var it) ? it.GetString() ?? "" : "";
        var returnedRefresh = root.TryGetProperty("refresh_token", out var rt) ? rt.GetString() ?? "" : "";
        var effectiveRefresh = string.IsNullOrWhiteSpace(returnedRefresh) ? token : returnedRefresh;

        if (string.IsNullOrWhiteSpace(access) || string.IsNullOrWhiteSpace(idt))
            return null;

        var profile = new GoogleAuthResult
        {
            IdToken = idt,
            AccessToken = access,
            RefreshToken = effectiveRefresh,
        };

        try
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", profile.AccessToken);
            var userInfoJson = await _http.GetStringAsync("https://www.googleapis.com/oauth2/v3/userinfo");
            using var userDoc = JsonDocument.Parse(userInfoJson);
            var user = userDoc.RootElement;
            profile.Email = user.TryGetProperty("email", out var em) ? em.GetString() ?? "" : "";
            profile.Name = user.TryGetProperty("name", out var nm) ? nm.GetString() ?? "" : "";
            profile.PictureUrl = user.TryGetProperty("picture", out var pc) ? pc.GetString() ?? "" : "";
        }
        catch
        {
            // Keep refreshed tokens even if userinfo fails temporarily.
        }

        return profile;
    }

    private async Task<GoogleAuthResult> LoginWithScopeAsync(string scope)
    {
        var (verifier, challenge) = CreatePkce();

        var authUrl =
            "https://accounts.google.com/o/oauth2/v2/auth" +
            $"?client_id={Uri.EscapeDataString(AndroidClientId)}" +
            $"&redirect_uri={Uri.EscapeDataString(RedirectUri.ToString())}" +
            "&response_type=code" +
            $"&scope={Uri.EscapeDataString(scope)}" +
            $"&code_challenge={Uri.EscapeDataString(challenge)}" +
            "&code_challenge_method=S256" +
            "&access_type=offline" +
            "&prompt=select_account";

        var result = await WebAuthenticator.Default.AuthenticateAsync(new Uri(authUrl), RedirectUri);

        if (!result.Properties.TryGetValue("code", out var code) || string.IsNullOrWhiteSpace(code))
            throw new Exception("Google login n'a pas renvoyé de code. Vérifie le client OAuth et le Redirect URI.");

        // Échange code -> tokens
        var token = await ExchangeCodeForTokensAsync(code, verifier);

        var profile = new GoogleAuthResult
        {
            IdToken = token.IdToken,
            AccessToken = token.AccessToken,
            RefreshToken = token.RefreshToken
        };

        if (!string.IsNullOrWhiteSpace(profile.AccessToken))
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", profile.AccessToken);
            var json = await _http.GetStringAsync("https://www.googleapis.com/oauth2/v3/userinfo");
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            profile.Email = root.TryGetProperty("email", out var em) ? em.GetString() ?? "" : "";
            profile.Name = root.TryGetProperty("name", out var nm) ? nm.GetString() ?? "" : "";
            profile.PictureUrl = root.TryGetProperty("picture", out var pc) ? pc.GetString() ?? "" : "";
        }

        if (string.IsNullOrWhiteSpace(profile.IdToken))
            throw new Exception("Google login n'a pas renvoyé d'id_token. Vérifie le client OAuth + Redirect URI.");

        return profile;
    }

    private async Task<(string AccessToken, string IdToken, string RefreshToken)> ExchangeCodeForTokensAsync(string code, string codeVerifier)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://oauth2.googleapis.com/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = AndroidClientId,
                ["code"] = code,
                ["redirect_uri"] = RedirectUri.ToString(),
                ["grant_type"] = "authorization_code",
                ["code_verifier"] = codeVerifier,
            })
        };

        using var resp = await _http.SendAsync(req);
        var json = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode)
            throw new Exception($"Token exchange failed: {(int)resp.StatusCode} {resp.ReasonPhrase}\n{json}");

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var access = root.TryGetProperty("access_token", out var at) ? at.GetString() ?? "" : "";
        var idt = root.TryGetProperty("id_token", out var it) ? it.GetString() ?? "" : "";
        var refresh = root.TryGetProperty("refresh_token", out var rt) ? rt.GetString() ?? "" : "";

        return (access, idt, refresh);
    }
}
