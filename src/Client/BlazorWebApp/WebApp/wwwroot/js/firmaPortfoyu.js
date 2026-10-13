// Firma Portföyü grafikleri (Pages/FirmaPortfoyu, Aşama 3) — taslak: kök dizindeki firma-portfoyu.html.
//
// Projedeki interop deseni (mevzuatGrafigi.js / firmaIsleri.js): sayfa kök öğeyi ve DotNetObjectReference'ı verir,
// JS yalnız ÇİZER. Rakamlar sunucudan (PortfoyDto.Seri ve göstergeler) gelir; burada hesap yok, yalnız ölçek.
//
// Bir sayfada birden çok grafik var (12 ay çizgisi, firma çubukları, detay penceresindeki küçük çizgi); her kök öğe
// kayıt tablosunda kendi örneğini tutar. guncelle(kok, veri) o örneği yeniden çizer; kaldir(kok) içeriği ve
// dinleyicileri söker, referansı bırakır. Dinleyiciler yalnız kökün içindeki SVG'ye bağlanır (window/document'a
// hiçbir şey eklenmez) — sayfa kapanınca sızıntı kalmaz.
//
// Veri:
//   çizgi: { tur:'cizgi', aylar:[{kisa:'Eki 25', uzun:'Ekim 2025'}], seriler:[{ad, renk, degerler:[n|null]}],
//            notlar:[metin|null], secili:11, yukseklik:260, aria }
//   çubuk: { tur:'cubuk', satirlar:[{firmaId, ad, deger}], seciliFirmaId, aria }
// Değeri olmayan ay (null) çizgide BOŞLUKTUR: sıfır çizilmez, iki yanı birleştirilmez.
window.firmaPortfoyu = {
    _kayitlar: new Map(),

    baslat: function (kokEl, dotNetRef, veri) {
        if (!kokEl) return;
        this.kaldir(kokEl);
        // Blazor öğeyi yeniden oluşturduysa eskisi DOM'dan kopmuştur: örneği ve referansı burada bırak.
        var kayitlar = this._kayitlar;
        kayitlar.forEach(function (o, k) { if (!k.isConnected) { o.sok(); kayitlar.delete(k); } });
        var o = firmaPortfoyuGrafik(kokEl, dotNetRef);
        this._kayitlar.set(kokEl, o);
        o.ciz(veri);
    },

    guncelle: function (kokEl, veri) {
        var o = kokEl && this._kayitlar.get(kokEl);
        if (o) o.ciz(veri);
    },

    kaldir: function (kokEl) {
        var o = kokEl && this._kayitlar.get(kokEl);
        if (!o) return;
        this._kayitlar.delete(kokEl);
        o.sok();
    }
};

function firmaPortfoyuGrafik(kok, ref) {
    'use strict';

    // ---- biçim (PfBicim.Tl ile aynı) ----
    const tr = (v, min, max) => v.toLocaleString('tr-TR', { minimumFractionDigits: min, maximumFractionDigits: max });
    const tl = v => {
        if (v == null) return '—';
        const a = Math.abs(v), s = v < 0 ? '−' : '';
        if (a >= 1e6) return s + tr(a / 1e6, 2, 2) + ' Mn ₺';
        if (a >= 1e3) return s + tr(Math.round(a / 1e3), 0, 0) + ' bin ₺';
        return s + tr(Math.round(a), 0, 0) + ' ₺';
    };
    const tlk = v => {
        const a = Math.abs(v), s = v < 0 ? '−' : '';
        if (a >= 1e6) return s + tr(a / 1e6, 0, 1) + ' Mn';
        if (a >= 1e3) return s + tr(Math.round(a / 1e3), 0, 0) + ' bin';
        return s + tr(Math.round(a), 0, 0);
    };
    const esc = s => String(s == null ? '' : s).replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));

    let sokuldu = false;

    // ---- 12 aylık çizgi grafik (crosshair + tooltip) ----
    function cizgi(v) {
        const aylar = v.aylar || [], seriler = v.seriler || [], n = aylar.length;
        const W = 760, H = v.yukseklik || 260, L = 64, R = 14, T = 14, B = 28;

        const degerler = seriler.flatMap(s => (s.degerler || []).filter(x => x != null));
        if (n === 0 || degerler.length === 0) {
            kok.innerHTML = '<div class="pf-grafik-bos">Bu 12 ayda gösterilecek aylık değer yok (mizan yüklenmedi ya da önceki ay eksik).</div>';
            return;
        }

        let mn = Math.min(0, ...degerler), mx = Math.max(0, ...degerler);
        if (mx === mn) mx = mn + 1;
        const step = Math.pow(10, Math.floor(Math.log10((mx - mn) / 4 || 1)));
        const nice = [1, 2, 2.5, 5, 10].map(m => m * step).find(s => (mx - mn) / s <= 5) || step * 10;
        mn = Math.floor(mn / nice) * nice; mx = Math.ceil(mx / nice) * nice;
        const x = i => n === 1 ? (L + W - R) / 2 : L + i * (W - L - R) / (n - 1);
        const y = val => T + (mx - val) / (mx - mn) * (H - T - B);

        let g = '';
        for (let val = mn; val <= mx + 1e-6; val += nice) {
            g += `<line x1="${L}" x2="${W - R}" y1="${y(val)}" y2="${y(val)}" class="pf-izgara"${Math.abs(val) < 1e-9 ? '' : ' stroke-dasharray="3 4"'}/>` +
                 `<text x="${L - 8}" y="${y(val) + 3.5}" text-anchor="end">${tlk(val)}</text>`;
        }
        aylar.forEach((a, i) => {
            if (i % 2 === 0 || i === n - 1) g += `<text x="${x(i)}" y="${H - 8}" text-anchor="middle">${esc(a.kisa)}</text>`;
        });
        if (v.secili != null) g += `<rect x="${x(v.secili) - 10}" y="${T}" width="20" height="${H - T - B}" class="pf-secili-ay" rx="4"/>`;

        // Boşluklu çizgi: null değer yolu keser (M ile yeniden başlar). Komşusu olmayan tek nokta çizgide
        // görünmeyeceği için noktayla işaretlenir; son değerin üstünde de nokta var (taslaktaki gibi).
        seriler.forEach(s => {
            const d = s.degerler || [];
            let yol = '', kalem = false;
            d.forEach((val, i) => {
                if (val == null) { kalem = false; return; }
                yol += (kalem ? 'L' : 'M') + x(i).toFixed(1) + ' ' + y(val).toFixed(1);
                kalem = true;
            });
            if (yol) g += `<path d="${yol}" fill="none" stroke="${s.renk}" stroke-width="2" stroke-linejoin="round" stroke-linecap="round"/>`;
            d.forEach((val, i) => {
                const yalniz = val != null && (i === 0 || d[i - 1] == null) && (i === n - 1 || d[i + 1] == null);
                if (yalniz) g += `<circle cx="${x(i)}" cy="${y(val)}" r="3" fill="${s.renk}"/>`;
            });
            let son = -1;
            d.forEach((val, i) => { if (val != null) son = i; });
            if (son >= 0) g += `<circle cx="${x(son)}" cy="${y(d[son])}" r="4" fill="${s.renk}" class="pf-nokta"/>`;
        });

        g += `<line class="pf-arti" x1="0" x2="0" y1="${T}" y2="${H - B}" visibility="hidden"/><g class="pf-noktalar"></g>` +
             `<rect class="pf-isabet" x="${L}" y="${T}" width="${W - L - R}" height="${H - T - B}" fill="transparent"/>`;

        kok.innerHTML = `<svg viewBox="0 0 ${W} ${H}" role="img" aria-label="${esc(v.aria || '12 aylık grafik')}">${g}</svg><div class="pf-ipucu" hidden></div>`;

        const svg = kok.querySelector('svg'), ipucu = kok.querySelector('.pf-ipucu');
        const arti = svg.querySelector('.pf-arti'), noktalar = svg.querySelector('.pf-noktalar'), isabet = svg.querySelector('.pf-isabet');

        isabet.addEventListener('pointermove', e => {
            const r = svg.getBoundingClientRect();
            const px = (e.clientX - r.left) * W / r.width;
            const i = n === 1 ? 0 : Math.max(0, Math.min(n - 1, Math.round((px - L) / ((W - L - R) / (n - 1)))));
            arti.setAttribute('x1', x(i)); arti.setAttribute('x2', x(i)); arti.setAttribute('visibility', 'visible');
            noktalar.innerHTML = seriler.map(s => s.degerler[i] != null
                ? `<circle cx="${x(i)}" cy="${y(s.degerler[i])}" r="4" fill="${s.renk}" class="pf-nokta"/>` : '').join('');
            const not = (v.notlar || [])[i];
            ipucu.innerHTML = `<b>${esc(aylar[i].uzun)}</b>` + seriler.map(s =>
                `<div class="pf-ipucu-satir"><span><i style="background:${s.renk}"></i>${esc(s.ad)}</span>` +
                `<span class="pf-sayi">${s.degerler[i] != null ? tl(s.degerler[i]) : '—'}</span></div>`).join('') +
                (not ? `<div class="pf-ipucu-not">${esc(not)}</div>` : '');
            ipucu.hidden = false;
            const tx = x(i) / W * r.width;
            ipucu.style.left = Math.min(r.width - 200, Math.max(0, tx + 12)) + 'px';
            ipucu.style.top = '8px';
        });
        isabet.addEventListener('pointerleave', () => {
            ipucu.hidden = true; arti.setAttribute('visibility', 'hidden'); noktalar.innerHTML = '';
        });
    }

    // ---- firma bazında net satış çubukları (seçili firma tam opak) ----
    function cubuk(v) {
        const satirlar = v.satirlar || [];
        if (satirlar.length === 0) {
            kok.innerHTML = '<div class="pf-grafik-bos">Bu dönemde aylık net satışı hesaplanabilen firma yok.</div>';
            return;
        }
        const W = 520, rowH = 26, L = 150, R = 70, H = satirlar.length * rowH + 8;
        const mx = Math.max(1, ...satirlar.map(r => Math.abs(r.deger)));
        const g = satirlar.map((r, j) => {
            const w = Math.max(2, (W - L - R) * Math.max(0, r.deger) / mx), yy = j * rowH + 4;
            const kisa = r.ad.length > 20 ? r.ad.slice(0, 19) + '…' : r.ad;
            const secili = r.firmaId === v.seciliFirmaId;
            return `<g class="pf-cubuk" data-firma="${r.firmaId}" tabindex="0" role="button" aria-label="${esc(r.ad)}: ${tl(r.deger)}${secili ? ' (seçili)' : ''}">` +
                `<rect x="0" y="${yy}" width="${W}" height="${rowH}" fill="transparent"/>` +
                `<text x="${L - 8}" y="${yy + 16}" text-anchor="end" class="${secili ? 'pf-cubuk-ad pf-cubuk-secili' : 'pf-cubuk-ad'}">${esc(kisa)}</text>` +
                `<rect x="${L}" y="${yy + 5}" width="${w}" height="${rowH - 10}" rx="4" fill="#2a78d6" opacity="${secili ? 1 : 0.45}"/>` +
                `<text x="${L + w + 6}" y="${yy + 16}">${tlk(r.deger)}</text><title>${esc(r.ad)}: ${tl(r.deger)}</title></g>`;
        }).join('');
        kok.innerHTML = `<svg viewBox="0 0 ${W} ${H}" role="group" aria-label="${esc(v.aria || 'Firma bazında net satış')}">${g}</svg>`;

        const svg = kok.querySelector('svg');
        const sec = e => {
            const c = e.target.closest('.pf-cubuk');
            if (!c || !ref || sokuldu) return;
            ref.invokeMethodAsync('CubukSecildi', +c.dataset.firma).catch(() => { /* sayfa kapanmış olabilir */ });
        };
        svg.addEventListener('click', sec);
        svg.addEventListener('keydown', e => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); sec(e); } });
    }

    return {
        ciz: function (v) {
            if (sokuldu || !v) return;
            if (v.tur === 'cubuk') cubuk(v); else cizgi(v);
        },
        sok: function () {
            sokuldu = true;
            kok.innerHTML = '';   // SVG ve üzerindeki dinleyiciler birlikte gider
            ref = null;
        }
    };
}
