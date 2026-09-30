// Anasayfa künyesi: "Tamamla" ile açılan ekranda eksik alana odaklanma.
// Hedef bölüm kendi verisini ayrıca yüklediği için eleman hemen DOM'da olmayabilir;
// kısa bir süre (en çok ~4 sn) aranır, bulunamazsa sessizce vazgeçilir.
window.anOdakla = (id) => {
    let deneme = 0;
    const bul = () => {
        const el = document.getElementById(id);
        if (!el) {
            if (++deneme < 40) setTimeout(bul, 100);
            return;
        }
        el.scrollIntoView({ behavior: 'smooth', block: 'center' });
        el.classList.add('an-odak-vurgu');
        setTimeout(() => el.classList.remove('an-odak-vurgu'), 2500);
        const girdi = el.querySelector('input:not([type=hidden]), textarea');
        if (girdi) girdi.focus({ preventScroll: true });
    };
    bul();
};
