// Firma işleri kartı: Ctrl+V ile tablo (Excel satırları) yapıştırma.
//
// Projedeki yapıştırma deseniyle aynı (jobAttachments.js, donemPanosu.js): JS 'paste'
// olayını dinler, içeriği DotNetObjectReference ile .NET'e geçirir. Farkı: dinleme BELGEDE
// değil yalnız kart öğesinde (kart odaktayken), ve görsel değil METİN (text/plain) alınır.
// Kartın içindeki bir metin kutusuna yapılan yapıştırma ENGELLENMEZ.
window.firmaIsleri = {
    _kayitlar: new Map(),

    kaydet: function (el, dotNetRef) {
        if (!el) return;
        this.kaldir(el);
        var dinleyici = function (e) {
            var hedef = e.target;
            if (hedef && (hedef.tagName === 'INPUT' || hedef.tagName === 'TEXTAREA' || hedef.isContentEditable)) return;
            var metin = e.clipboardData && e.clipboardData.getData('text/plain');
            if (!metin || !metin.trim()) return;
            e.preventDefault();
            dotNetRef.invokeMethodAsync('MetinYapistirildi', metin);
        };
        el.addEventListener('paste', dinleyici);
        this._kayitlar.set(el, dinleyici);
    },

    kaldir: function (el) {
        var d = el && this._kayitlar.get(el);
        if (d) el.removeEventListener('paste', d);
        if (el) this._kayitlar.delete(el);
    },

    // "Yapıştır" düğmesi: izin yoksa ya da tarayıcı desteklemiyorsa null döner — dialog elle
    // yapıştırma kutusuyla açılır.
    panodanOku: async function () {
        try {
            if (!navigator.clipboard || !navigator.clipboard.readText) return null;
            return await navigator.clipboard.readText();
        } catch (e) {
            return null;
        }
    }
};
