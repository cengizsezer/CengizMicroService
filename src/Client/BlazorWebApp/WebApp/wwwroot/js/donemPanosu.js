// Dönem panosu: hücre panelinin konumu ve Ctrl+V ile kanıt yapıştırma.
//
// Yapıştırma panel açıkken belge düzeyinde dinlenir; panodaki GÖRSEL (ekran görüntüsü)
// data-URL olarak .NET'e geçer. Pano izni yoksa ya da tarayıcı clipboardData.items
// desteklemiyorsa hiçbir şey olmaz — dosya seçme yolu Blazor InputFile ile ayrıca çalışır.
// Metin yapıştırma (not kutusu) ENGELLENMEZ: yalnız dosya öğesi varsa olay yakalanır.
window.donemPanosu = {
    _ref: null,
    _onPaste: null,
    _accept: ['image/png', 'image/jpeg', 'image/jpg'],

    // Paneli hücrenin altına (sığmazsa üstüne) koyar; ekran dışına taşmasın.
    konum: function (el, genislik, yukseklik) {
        if (!el) return { top: 80, left: 80 };
        var r = el.getBoundingClientRect();
        var vw = window.innerWidth, vh = window.innerHeight, pay = 8;
        var left = Math.min(Math.max(pay, r.left), vw - genislik - pay);
        var top = r.bottom + 4;
        if (top + yukseklik > vh - pay) top = Math.max(pay, r.top - yukseklik - 4);
        return { top: Math.round(top), left: Math.round(Math.max(pay, left)) };
    },

    konumId: function (id, genislik, yukseklik) {
        return this.konum(document.getElementById(id), genislik, yukseklik);
    },

    yapistirmaKaydet: function (dotNetRef) {
        this.yapistirmaKaldir();
        this._ref = dotNetRef;
        this._onPaste = function (e) {
            var items = e.clipboardData && e.clipboardData.items;
            if (!items) return;
            for (var i = 0; i < items.length; i++) {
                if (items[i].kind !== 'file') continue;
                var file = items[i].getAsFile();
                if (!file) continue;
                e.preventDefault();
                window.donemPanosu._gonder(file);
            }
        };
        document.addEventListener('paste', this._onPaste);
    },

    _gonder: function (file) {
        var ref = this._ref;
        if (!ref) return;
        if (this._accept.indexOf(file.type) === -1) {
            ref.invokeMethodAsync('YapistirmaReddedildi', file.type || 'bilinmeyen tür');
            return;
        }
        var reader = new FileReader();
        reader.onload = function () {
            if (!window.donemPanosu._ref) return;
            window.donemPanosu._ref.invokeMethodAsync('Yapistirildi', file.type, reader.result, file.size);
        };
        reader.readAsDataURL(file);
    },

    yapistirmaKaldir: function () {
        if (this._onPaste) document.removeEventListener('paste', this._onPaste);
        this._onPaste = null;
        this._ref = null;
    }
};
