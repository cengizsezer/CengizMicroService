// Fiş giriş ekranının klavye kısayolları.
// Ctrl+S ve F2 tarayıcının kendi davranışını tetiklediği için document seviyesinde
// yakalanıp preventDefault ediliyor; bunu Blazor'un @onkeydown'ı ile koşullu yapmak
// mümkün değil (preventDefault render anında sabitleniyor).
window.fisKisayol = {
    _handler: null,

    bagla: function (dotNetRef) {
        this.cikar();

        this._handler = function (e) {
            // Açık bir Radzen diyaloğu varsa kısayolları ele alma: Esc diyaloğu kapatmalı,
            // F2 de üst üste ikinci modal açmamalı.
            if (document.querySelector('.rz-dialog')) return;

            var komut = null;

            if (e.ctrlKey && e.key === 'Enter') komut = 'kaydetVeYeni';
            else if (e.ctrlKey && (e.key === 's' || e.key === 'S')) komut = 'kaydet';
            else if (e.key === 'F2') komut = 'hesapAgaci';
            else if (e.key === 'Escape') komut = 'vazgec';

            if (!komut) return;

            e.preventDefault();
            e.stopPropagation();
            dotNetRef.invokeMethodAsync('KisayolAsync', komut);
        };

        // capture: Radzen bileşenleri Escape'i kendi içinde yutabildiği için yakalama
        // fazında dinliyoruz.
        document.addEventListener('keydown', this._handler, true);
    },

    cikar: function () {
        if (this._handler) {
            document.removeEventListener('keydown', this._handler, true);
            this._handler = null;
        }
    },

    odakla: function (id) {
        var el = document.getElementById(id);
        if (el) el.focus();
    }
};
