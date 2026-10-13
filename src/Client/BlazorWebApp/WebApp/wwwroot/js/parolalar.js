// Yönetim › Parolalar — yalnız panoya yazma. Değer Blazor'dan o an gelir (sunucudan "kopyala" ile alınmış), burada
// saklanmaz, loglanmaz, ekrana yazılmaz. navigator.clipboard güvenli bağlam (https / localhost) ister; yoksa geçici,
// görünmez textarea ile execCommand('copy') denenir ve hemen silinir.
window.parolalar = {
    kopyala: async function (metin) {
        try {
            if (navigator.clipboard && window.isSecureContext) {
                await navigator.clipboard.writeText(metin);
                return true;
            }
        } catch (e) {
            // aşağıdaki yedek yola düş
        }
        var ta = document.createElement('textarea');
        ta.value = metin;
        ta.setAttribute('readonly', '');
        ta.style.position = 'fixed';
        ta.style.opacity = '0';
        ta.style.left = '-9999px';
        document.body.appendChild(ta);
        var tamam = false;
        try {
            ta.select();
            tamam = document.execCommand('copy');
        } catch (e) {
            tamam = false;
        } finally {
            ta.value = '';
            ta.remove();
        }
        return tamam;
    }
};
