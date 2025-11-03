using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components;

namespace WebApp.Application.Services
{
    public class LanguageService
    {
        private readonly ILocalStorageService _ls;
        private readonly NavigationManager _nav;

        public LanguageService(ILocalStorageService ls, NavigationManager nav)
        {
            _ls = ls; _nav = nav;
        }

        public async Task SetCultureAsync(string culture)
        {
            await _ls.SetItemAsync("culture", culture);
            // sayfayı yeni kültürle başlat
            _nav.NavigateTo(_nav.Uri, forceLoad: true);
        }
    }
}
