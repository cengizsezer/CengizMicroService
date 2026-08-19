// Banka ekstresi onay ekranının odak yardımcıları.
// Ekran fare gerektirmeden kullanılabilmeli: sayfa açılınca odak ilk belirsiz satırın
// kod kutusunda olur, onay sonrası bir sonraki satıra atlar.
window.bankaEkstre = {
    odakla: function (id) {
        var el = document.getElementById(id);
        if (!el) return false;

        el.focus();
        if (typeof el.select === 'function') el.select();

        // Uzun listede satır görünür alana alınır; klavye kullanıcısı satırı kaybetmesin.
        if (typeof el.scrollIntoView === 'function') {
            el.scrollIntoView({ block: 'nearest' });
        }
        return true;
    },

    temizle: function (id) {
        var el = document.getElementById(id);
        if (el) el.value = '';
    }
};
