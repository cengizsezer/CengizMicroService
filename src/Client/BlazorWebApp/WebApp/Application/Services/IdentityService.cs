using Blazored.LocalStorage;
using Blazored.SessionStorage;
using Microsoft.AspNetCore.Components.Authorization;
using System.Net.Http;
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

        public async Task<LoginResponseModel?> Login(string username, string password, bool rememberMe = false)
        {
            var loginModel = new LoginRequestModel
            {
                Username = username,
                Password = password,
                RefreshToken = ""
            };

            var result = await httpClient.PostGetResponseAsync<LoginResponseModel, LoginRequestModel>("auth/login", loginModel);

            if (result == null)
            {
                Console.WriteLine("Login Hatası: Sunucudan geçerli yanıt alınamadı.");
                return null;
            }

            // Oturum bilgilerini SessionStorage’a yaz
            await sessionStorage.SetItemAsync("username", result.Username);
            await sessionStorage.SetItemAsync("token", result.Token);
            await sessionStorage.SetItemAsync("refresh_token", result.RefreshToken);
            await sessionStorage.SetItemAsync("role", result.Role);
            await sessionStorage.SetItemAsync("firmalar", result.Firmalar);

            // Eğer beni hatırla seçiliyse LocalStorage’a sadece kullanıcı adı ve şifreyi kaydet
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

            ((AuthStateProvider)authStateProvider).NotifyUserLogin(result.Username);
            return result;
        }

        public async Task<bool> Register(string userName, string email, string password)
        {
            var model = new { userName, email, password };
            var result = await httpClient.PostGetResponseAsync<RegisterResponseModel, object>("auth/register", model);
            return result?.Success == true;
        }

        public async void Logout()
        {
            await sessionStorage.RemoveItemAsync("username");
            await sessionStorage.RemoveItemAsync("token");
            await sessionStorage.RemoveItemAsync("refresh_token");
            await sessionStorage.RemoveItemAsync("role");
            await sessionStorage.RemoveItemAsync("firmalar");

            ((AuthStateProvider)authStateProvider).NotifyUserLogout();
            httpClient.DefaultRequestHeaders.Authorization = null;
        }

        public async Task<string> GetUserName() => await sessionStorage.GetItemAsync<string>("username");
        public async Task<string> GetUserToken() => await sessionStorage.GetItemAsync<string>("token");

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
    }
}
