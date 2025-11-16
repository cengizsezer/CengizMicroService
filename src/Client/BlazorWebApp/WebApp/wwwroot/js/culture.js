// wwwroot/js/culture.js
window.blazorCulture = {
    get: () => {
        return localStorage.getItem('BlazorCulture')
            || navigator.language
            || navigator.userLanguage
            || 'tr-TR'; // fallback ekleyin
    },
    set: (value) => {
        localStorage.setItem('BlazorCulture', value);
    }
};

// Extension method için gerekli fonksiyon
window.getBrowserCulture = () => {
    return window.blazorCulture.get();
};