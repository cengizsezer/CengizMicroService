using Blazored.SessionStorage;
using Microsoft.AspNetCore.Components.Authorization;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using WebApp.Infrastructure;

public class AuthStateProvider : AuthenticationStateProvider
{
    private readonly ISessionStorageService session;
    private readonly HttpClient client;
    private readonly AuthenticationState anonymous;
    private readonly AppStateManager appState;

    public AuthStateProvider(
        ISessionStorageService sessionStorageService,
        HttpClient Client,
        AppStateManager appState)
    {
        session = sessionStorageService;
        client = Client;
        this.appState = appState;
        anonymous = new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await session.GetItemAsync<string>("token");
        if (string.IsNullOrWhiteSpace(token))
            return anonymous;

        try
        {
            // Authorization header'ı kur
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);

            // JWT'yi çöz
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

            // Tüm claim'leri temel al
            var claims = jwt.Claims.ToList();

            // İsim claim'i yoksa, unique_name / sub'tan üret
            if (!claims.Any(c => c.Type == ClaimTypes.Name))
            {
                var uname = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.UniqueName)?.Value
                            ?? jwt.Claims.FirstOrDefault(c => c.Type == "unique_name")?.Value
                            ?? jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value
                            ?? "";
                if (!string.IsNullOrWhiteSpace(uname))
                    claims.Add(new Claim(ClaimTypes.Name, uname));
            }

            // Roller: "role" VEYA ClaimTypes.Role -> ClaimTypes.Role olarak tekille
            var roleValues = jwt.Claims
                .Where(c => c.Type == "role" || c.Type == ClaimTypes.Role)
                .Select(c => c.Value)
                .Distinct()
                .ToList();

            foreach (var r in roleValues)
            {
                var hasRole = claims.Any(c => c.Type == ClaimTypes.Role && c.Value == r);
                if (!hasRole)
                    claims.Add(new Claim(ClaimTypes.Role, r));
            }

            // Tenant no ("tn") varsa aynen ekli
            var tn = jwt.Claims.FirstOrDefault(c => c.Type == "tn")?.Value;
            if (!string.IsNullOrWhiteSpace(tn) &&
                !claims.Any(c => c.Type == "tn"))
            {
                claims.Add(new Claim("tn", tn));
            }

            var identity = new ClaimsIdentity(claims, authenticationType: "jwt");
            var principal = new ClaimsPrincipal(identity);
            return new AuthenticationState(principal);
        }
        catch
        {
            // Token bozuk ise güvenli şekilde anonim dön
            client.DefaultRequestHeaders.Authorization = null;
            return anonymous;
        }
    }


    // Token/tenant değiştiğinde UI’yi yeniletmek için tek bir helper
    public void NotifyAuthChanged()
    {
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
        appState.LoginChanged();
    }

    // (İstersen bunları da NotifyAuthChanged'e yönlendirebilirsin)
    public void NotifyUserLogin(string _)
        => NotifyAuthChanged();

    public void NotifyUserLogout()
    {
        NotifyAuthenticationStateChanged(Task.FromResult(anonymous));
        appState.LoginChanged();
    }
}
