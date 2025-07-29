using Blazored.LocalStorage;
using Blazored.SessionStorage;
using Microsoft.AspNetCore.Components.Authorization;
using Newtonsoft.Json;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using WebApp.Application.Services.Interfaces;
using WebApp.Domain.Models.User;
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

        public async Task<bool> Login(string username, string password, bool rememberMe = false)
        {
            var loginModel = new LoginRequestModel
            {
                Username = username,
                Password = password,
                RefreshToken = ""
            };

            var response = await httpClient.PostAsJsonAsync("auth/login", loginModel);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                Console.WriteLine("Login Hatası: " + error);
                return false;
            }

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<LoginResponseModel>(json);

            if (result == null)
                return false;

            // Tarayıcı kapatıldığında silinecek oturum verisi
            await sessionStorage.SetItemAsync("username", username);
            await sessionStorage.SetItemAsync("token", result.Token);
            await sessionStorage.SetItemAsync("refresh_token", result.RefreshToken);

            // Eğer kullanıcı beni hatırla dediyse sadece input için kaydet (login otomatik yapılmaz)
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

            ((AuthStateProvider)authStateProvider).NotifyUserLogin(username);
            return true;
        }

        public async Task<bool> Register(string userName, string email, string password)
        {
            var json = JsonConvert.SerializeObject(new { userName, email, password });
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync("auth/register", content);

            if (!response.IsSuccessStatusCode)
                return false;

            var responseJson = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<RegisterResponseModel>(responseJson);

            return result?.Success == true;
        }

        public async void Logout()
        {
            await sessionStorage.RemoveItemAsync("username");
            await sessionStorage.RemoveItemAsync("token");
            await sessionStorage.RemoveItemAsync("refresh_token");

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

        // Bu kısım Login sayfasında inputlara otomatik değer doldurmak için kullanılabilir
        public async Task<(string Username, string Password)> GetRememberedCredentials()
        {
            var username = await localStorage.GetItemAsync<string>("saved_username") ?? "";
            var password = await localStorage.GetItemAsync<string>("saved_password") ?? "";
            return (username, password);
        }
    }
}
