window.blazorCulture = {
    get: () => {
        return localStorage.getItem('BlazorCulture')
            || navigator.language
            || navigator.userLanguage
            || '';
    },
    set: (value) => {
        localStorage.setItem('BlazorCulture', value);
    }
};
