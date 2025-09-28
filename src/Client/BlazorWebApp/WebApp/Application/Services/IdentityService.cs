using Blazored.LocalStorage;
using Blazored.SessionStorage;
using Microsoft.AspNetCore.Components.Authorization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using WebApp.Application.Services.Interfaces;
using WebApp.Domain.Models.User;
using WebApp.Extensions;
using WebApp.Utils;

namespace WebApp.Application.Services
{
    public class IdentityService : IIdentityService
    {
        private readonly HttpClient httpClient;
        private readonly ISessionStorageService sessionStorage;
        private readonly ILocalStorageService localStorage;
        private readonly AuthenticationStateProvider authStateProvider;

        public IdentityService(
            HttpClient httpClient,
            ISessionStorageService sessionStorage,
            ILocalStorageService localStorage,
            AuthenticationStateProvider authStateProvider)
        {
            this.httpClient = httpClient;
            this.sessionStorage = sessionStorage;
            this.localStorage = localStorage;
            this.authStateProvider = authStateProvider;
        }

        // --- REGISTER --------------------------------------------------------

        public async Task<RegisterResponseModel?> Register(string userName, string email, string password)
        {
            var payload = new RegisterRequestModel { UserName = userName, Email = email, Password = password };
            return await httpClient.PostGetResponseAsync<RegisterResponseModel, RegisterRequestModel>("auth/register", payload);
        }

        // --- LOGIN (ACCESS YOK; sadece refresh + firmalar) -------------------

        public async Task<LoginResponseModel?> Login(string username, string password, bool rememberMe = false)
        {
            var payload = new LoginRequestModel { Username = username, Password = password, RefreshToken = "" };

            var result = await httpClient.PostGetResponseAsync<LoginResponseModel, LoginRequestModel>("auth/login", payload);
            if (result is null) return null;

            // Access token bu aşamada YOK → header’ı temizle
            httpClient.DefaultRequestHeaders.Authorization = null;

            // Oturum bilgileri
            await sessionStorage.SetItemAsync("username", result.Username);
            await sessionStorage.SetItemAsync("refresh_token", result.RefreshToken);
            await sessionStorage.SetItemAsync("firmalar", result.Firmalar);

            // Beni hatırla
            if (rememberMe)
            {
                await localStorage.SetItemAsync("saved_username", username);
                await localStorage.SetItemAsync("saved_password", password);
            }
            else
            {
                await localStorage.RemoveItemAsync("saved_username");
                await localStorage.RemoveItemAsync("saved_password");
            }

            if (authStateProvider is AuthStateProvider asp)
                asp.NotifyUserLogin(result.Username);

            return result;
        }

        // --- SELECT TENANT (ACCESS üretir) ----------------------------------

        public async Task<LoginResponseModel> SelectTenant(string firmaNo)
        {
            var username = await sessionStorage.GetItemAsync<string>("username") ?? "";
            var refresh = await sessionStorage.GetItemAsync<string>("refresh_token") ?? "";

            var payload = new { UserName = username, TenantNo = firmaNo, RefreshToken = refresh };

            var res = await httpClient.PostAsJsonAsync("auth/select-tenant", payload);
            res.EnsureSuccessStatusCode();

            var result = (await res.Content.ReadFromJsonAsync<LoginResponseModel>())!;

            // 1) Token’ları yaz
            await StoreTokens(result.Token, result.RefreshToken);

            // 2) Tenant’ı yaz (handler buradan okuyacak)
            await sessionStorage.SetItemAsync("tenantNo", firmaNo);

            return result;
        }


        // --- TOKEN SAKLAMA + HEADER AYARLAMA --------------------------------

        public async Task StoreTokens(string accessToken, string? refreshToken = null)
        {
            if (string.IsNullOrWhiteSpace(accessToken))
                return;

            await sessionStorage.SetItemAsync("token", accessToken);
            httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);

            if (!string.IsNullOrWhiteSpace(refreshToken))
                await sessionStorage.SetItemAsync("refresh_token", refreshToken);
        }

        // Uygulama yeniden yüklendiğinde header’ı geri kurmak için (App.razor OnAfterRender’ında çağır)
        public async Task InitializeAuthHeaderFromSessionAsync()
        {
            var token = await sessionStorage.GetItemAsync<string>("token");
            if (!string.IsNullOrWhiteSpace(token))
            {
                httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);
            }
        }

        // --- YARDIMCI OKUMALAR ---------------------------------------------

        public async Task<string> GetUserName() => await sessionStorage.GetItemAsync<string>("username") ?? "";
        public async Task<string> GetAccessToken() => await sessionStorage.GetItemAsync<string>("token") ?? "";
        public async Task<string> GetRefreshToken() => await sessionStorage.GetItemAsync<string>("refresh_token") ?? "";

        public async Task<bool> IsLoggedIn()
        {
            var token = await sessionStorage.GetItemAsync<string>("token");
            return !string.IsNullOrWhiteSpace(token);
        }

        public async Task<(string Username, string Password)> GetRememberedCredentials()
        {
            var username = await localStorage.GetItemAsync<string>("saved_username") ?? "";
            var password = await localStorage.GetItemAsync<string>("saved_password") ?? "";
            return (username, password);
        }

        // --- LOGOUT ---------------------------------------------------------

        public async void Logout()
        {
            await sessionStorage.RemoveItemAsync("username");
            await sessionStorage.RemoveItemAsync("token");
            await sessionStorage.RemoveItemAsync("refresh_token");
            await sessionStorage.RemoveItemAsync("firmalar");

            httpClient.DefaultRequestHeaders.Authorization = null;

            if (authStateProvider is AuthStateProvider asp)
                asp.NotifyUserLogout();
        }
    }
}
