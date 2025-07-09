using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
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
        private readonly ISyncLocalStorageService syncLocalStorageService;
        private readonly AuthenticationStateProvider authStateProvider;


        public IdentityService(HttpClient httpClient, ISyncLocalStorageService syncLocalStorageService, AuthenticationStateProvider authStateProvider)
        {
            this.httpClient = httpClient;
            this.syncLocalStorageService = syncLocalStorageService;
            this.authStateProvider = authStateProvider;
        }


        public bool IsLoggedIn => !string.IsNullOrEmpty(GetUserToken());

        public string GetUserName()
        {
            return syncLocalStorageService.GetItem<string>("username");
        }

        public string GetUserToken()
        {
            return syncLocalStorageService.GetItem<string>("token");
        }
        public async Task<bool> Login(string username, string password, bool rememberMe = false)

        {
            var loginModel = new UserLoginRequest
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

            var result = await response.Content.ReadFromJsonAsync<UserLoginResponse>();

            if (rememberMe)
            {
                syncLocalStorageService.SetItem("username", username);
                syncLocalStorageService.SetItem("token", result.Token);
                syncLocalStorageService.SetItem("refresh_token", result.RefreshToken);
            }
            else
            {
                // Kalıcı saklama yerine sadece bellekte tutabilirsin veya saklamayabilirsin
                // Bu örnekte hiçbir yere kaydetmiyoruz
            }

            ((AuthStateProvider)authStateProvider).NotifyUserLogin(username);

            return true;
        }

        //public async Task<bool> Login(string username, string password)
        //{
        //    var loginModel = new UserLoginRequest
        //    {
        //        Username = username,
        //        Password = password,
        //        RefreshToken = ""
        //    };

        //    var response = await httpClient.PostAsJsonAsync("auth/login", loginModel);

        //    if (!response.IsSuccessStatusCode)
        //    {
        //        var error = await response.Content.ReadAsStringAsync();
        //        Console.WriteLine("Login Hatası: " + error);
        //        return false;
        //    }

        //    var result = await response.Content.ReadFromJsonAsync<UserLoginResponse>();

        //    // Token'ları kaydet
        //    syncLocalStorageService.SetItem("username", username);  // Bunu da ekle
        //    syncLocalStorageService.SetItem("access_token", result.Token);
        //    syncLocalStorageService.SetItem("refresh_token", result.RefreshToken);

        //    return true;
        //}



        public async Task<bool> Register(string userName, string email, string password)
        {
            var json = JsonSerializer.Serialize(new
            {
                userName,
                email,
                password
            });

            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync("auth/register", content);

            if (!response.IsSuccessStatusCode)
                return false;

            // 🔥 BURADA ReadFromJsonAsync KULLANIYORSUN
            var result = await response.Content.ReadFromJsonAsync<RegisterResponseModel>();

            return result?.Success == true;
        }
        public void Logout()
        {
            syncLocalStorageService.RemoveItem("username");
            syncLocalStorageService.RemoveItem("token");        // "access_token" değil
            syncLocalStorageService.RemoveItem("refresh_token");

            ((AuthStateProvider)authStateProvider).NotifyUserLogout();
            httpClient.DefaultRequestHeaders.Authorization = null;
        }
        //public void Logout()
        //{
          
        //    syncLocalStorageService.RemoveItem("username");  // Bunu da ekle
        //    syncLocalStorageService.RemoveItem("access_token");
        //    syncLocalStorageService.RemoveItem("refresh_token");

        //    ((AuthStateProvider)authStateProvider).NotifyUserLogout();
        //    httpClient.DefaultRequestHeaders.Authorization = null;
        //}
       
    }
}