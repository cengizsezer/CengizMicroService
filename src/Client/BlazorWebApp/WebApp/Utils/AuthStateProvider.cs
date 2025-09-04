using Blazored.LocalStorage;
using Blazored.SessionStorage;
using Microsoft.AspNetCore.Components.Authorization;
using System;
using System.Net.Http;
using System.Security.Claims;
using System.Threading.Tasks;
using WebApp.Extensions;
using WebApp.Infrastructure;


namespace WebApp.Utils
{
    public class AuthStateProvider : AuthenticationStateProvider
    {
        private readonly ILocalStorageService localStorageService;
        private readonly HttpClient client;
        private readonly AuthenticationState anonymous;
        private readonly AppStateManager appState;
        private readonly ISessionStorageService sessionStorageService;

        public AuthStateProvider(ISessionStorageService sessionStorageService, HttpClient Client, AppStateManager appState)
        {
            this.sessionStorageService = sessionStorageService;
            client = Client;
            anonymous = new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
            this.appState = appState;
        }


public async override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var apiToken = await sessionStorageService.GetItemAsync<string>("token");
            if (string.IsNullOrWhiteSpace(apiToken))
                return anonymous;

            var userName = await sessionStorageService.GetItemAsync<string>("username");

            var cp = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
        new Claim(ClaimTypes.Name, userName)
    }, "jwtAuthType"));

            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiToken);

            return new AuthenticationState(cp);
        }

        public void NotifyUserLogin(String userName)
        {
            var cp = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Name, userName)

            }, "jwtAuthType"));

            var authState = Task.FromResult(new AuthenticationState(cp));

            NotifyAuthenticationStateChanged(authState);
            appState.LoginChanged();
        }

        public void NotifyUserLogout()
        {
            var authState = Task.FromResult(anonymous);
            NotifyAuthenticationStateChanged(authState);
            appState.LoginChanged();
        }
    }
}