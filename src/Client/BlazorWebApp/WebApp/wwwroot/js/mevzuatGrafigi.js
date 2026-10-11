// Mevzuat Grafiği: Shader Graph tarzı kart/bağlantı tuvali (Pages/MevzuatGrafigi).
//
// Motor kök dizindeki mevzuat-grafigi.html mockup'ının motoru; çizim ve sürükleme JS'te,
// kalıcı kayıt .NET'te. Projedeki interop deseniyle aynı (firmaIsleri.js): sayfa
// DotNetObjectReference verir, JS değişikliği önce ekrana uygular, sonra [JSInvokable]
// metodu çağırır. Sunucu reddederse değişiklik geri alınır ve sunucunun Türkçe mesajı
// mockup'taki toast ile gösterilir.
//
// window/document'a eklenen her dinleyici örneğin kayıt tablosunda tutulur; kaldir() hepsini
// söker (SPA'da sayfa kapanınca Delete/Escape gibi tuşlar başka sayfaya sızmasın).
window.mevzuatGrafigi = {
    _kayitlar: new Map(),

    baslat: function (kokEl, dotNetRef, veri) {
        if (!kokEl) return;
        this.kaldir(kokEl);
        this._kayitlar.set(kokEl, mevzuatGrafigiOlustur(kokEl, dotNetRef, veri));
    },

    // Sunucudan yeniden okunan grafiği açık örneğe uygular (aktif tuval ve seçim korunur).
    guncelle: function (veri) {
        this._kayitlar.forEach(function (o) { o.guncelle(veri); });
    },

    // Promise döner: Inspector'da bekleyen (debounce'u dolmamış) düzenlemeler önce gönderilir,
    // promise o gönderimler bitince çözülür. Sayfa bunu await etmeden DotNetObjectReference'ı
    // serbest bırakmamalı; yoksa son düzenleme kaybolur. Asla reddedilmez.
    kaldir: function (kokEl) {
        var o = kokEl && this._kayitlar.get(kokEl);
        if (kokEl) this._kayitlar.delete(kokEl);
        return o ? o.sok() : Promise.resolve();
    }
};

function mevzuatGrafigiOlustur(kok, ref, veri) {
    'use strict';

    // Sunucudaki MevzuatKartTuru (byte) ↔ mockup anahtarları. Tek eşleme tablosu burası.
    const TUR = { 1: 'firma', 2: 'islem', 3: 'istisna', 4: 'indirim', 5: 'kanun', 6: 'yuk' };
    const TUR_NO = Object.fromEntries(Object.entries(TUR).map(([no, k]) => [k, +no]));

    const TYPES = {
        firma:   { label: 'Firma',       inL: null,     outL: 'İşlem / Teşvik' },
        islem:   { label: 'İşlem',       inL: 'Firma',  outL: 'Teşvikler' },
        istisna: { label: 'İstisna',     inL: 'Firma / İşlem', outL: 'Dayanak / Şart' },
        indirim: { label: 'İndirim',     inL: 'Firma / İşlem', outL: 'Dayanak / Şart' },
        kanun:   { label: 'Kanun',       inL: 'Teşvik', outL: null },
        yuk:     { label: 'Şart / Süre', inL: 'Teşvik', outL: null },
    };
    const ORDER = ['firma', 'islem', 'istisna', 'indirim', 'kanun', 'yuk'];
    const COL = { firma: 0, islem: 1, istisna: 2, indirim: 2, kanun: 3, yuk: 4 };
    // Sunucudaki BaglantiKurali ile aynı kalmalı.
    const ALLOWED = { firma: ['islem', 'istisna', 'indirim'], islem: ['istisna', 'indirim'], istisna: ['kanun', 'yuk'], indirim: ['kanun', 'yuk'], kanun: [], yuk: [] };
    const REL = { islem: 'yapar', istisna: 'yararlanır', indirim: 'yararlanır', kanun: 'dayanağı', yuk: 'şartıyla' };
    const SIRA = 'Firma → İşlem veya doğrudan İstisna / İndirim → Kanun / Şart';
    const tesvikMi = t => t === 'istisna' || t === 'indirim';
    // Bağlantı etiketi: Firma → teşvik doğrudan bağlantısı "kullanıyor"; diğerleri hedef türüne göre.
    const relOf = (a, b) => byId(a).type === 'firma' && tesvikMi(byId(b).type) ? 'kullanıyor' : REL[byId(b).type];

    // Alan sınırları sunucudaki MevzuatGrafigiService ile aynı.
    const MAX = { baslik: 200, madde: 100, ozet: 1000, tuval: 100 };

    // ---- Veri (mockup'taki N, E, X, views; sunucudan gelir) ----
    // Kart id'leri DOM'daki data-id ile karşılaştırıldığı için metin tutulur; kaydı süren
    // yeni kartlar 'g' önekli geçici id taşır, sunucu id'si gelince değiştirilir.
    let N = [], E = [], X = [], views = [];
    const BOS_TUVAL = { id: null, name: '', nodes: [], pos: {} };

    function yukle(v) {
        v = v || {};
        N = (v.kartlar || []).filter(k => TUR[k.tur]).map(k => {
            const n = { id: String(k.id), type: TUR[k.tur], title: k.baslik || '', madde: k.madde || '', ozet: k.ozet || '' };
            n._kayitli = { title: n.title, madde: n.madde, ozet: n.ozet };
            return n;
        });
        E = (v.baglantilar || []).map(b => ({ id: b.id, a: String(b.kaynakKartId), b: String(b.hedefKartId) }));
        X = (v.dislamalar || []).map(d => ({ id: d.id, a: String(d.aKartId), b: String(d.bKartId), note: d.not || '' }));
        views = (v.tuvaller || []).map(t => {
            const yer = t.yerlesimler || [];
            const pos = {};
            yer.forEach(y => { pos[String(y.kartId)] = { x: y.x, y: y.y }; });
            return { id: t.id, name: t.ad, nodes: yer.map(y => String(y.kartId)), pos: pos };
        });
        S.checks = {};
        (v.sartTakipleri || []).forEach(s => {
            (S.checks[String(s.firmaKartId)] = S.checks[String(s.firmaKartId)] || {})[String(s.sartKartId)] = s.tamamlandi;
        });
        views.forEach(layout);
    }

    const W = 220, PORT_Y = 39, H_EST = 132;
    const S = { view: 0, px: 0, py: 0, z: 1, sel: null, checks: {}, panels: { bb: true, insp: true, prev: true }, duzen: null, silOnay: null, siliniyor: false };
    const $ = s => kok.querySelector(s);
    const stage = $('#mg-stage'), world = $('#mg-world'), svg = $('#mg-edges'), nodesEl = $('#mg-nodes');
    const byId = id => N.find(n => n.id === id);
    const V = () => views[S.view] || BOS_TUVAL;
    const esc = s => String(s ?? '').replace(/[&<>"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));

    let geciciSayac = 0;
    const yeniGeciciId = () => 'g' + (++geciciSayac);
    const geciciMi = id => typeof id === 'string' && id.charAt(0) === 'g';

    // ---- Dinleyici kaydı ve söküm ----
    const dinleyiciler = [];
    function dinle(hedef, olay, fn, secenek) {
        hedef.addEventListener(olay, fn, secenek);
        dinleyiciler.push([hedef, olay, fn, secenek]);
    }
    // Kart id → { t: debounce zamanlayıcısı, n: kart }. Kartı da tutuyoruz ki bekleyen
    // düzenleme süre dolmadan gönderilebilsin (seçim/tuval değişimi, sayfa kapanışı).
    const kartZamanlayicilari = new Map();
    let toastT, sokuldu = false;

    // Kapanış: dinleyiciler sökülür, bekleyen düzenlemeler hemen gönderilir. Ekran gidiyor;
    // geri alma ve toast yok, hata yalnız console'a yazılır. Promise asla reddedilmez.
    function sok() {
        dinleyiciler.forEach(([h, o, f, s]) => h.removeEventListener(o, f, s));
        dinleyiciler.length = 0;
        clearTimeout(toastT);
        const bekleyen = bekleyenleriAl();
        sokuldu = true;
        return Promise.all(bekleyen.map(n =>
            Promise.resolve(n._turSozu).then(() => ref.invokeMethodAsync('KartGuncelle', +n.id, kartGovdesi(n))).then(
                r => { if (!r || !r.basarili) console.error(`Mevzuat Grafiği: "${n.title}" kartındaki son düzenleme kaydedilemedi: ${(r && r.mesaj) || 'bilinmeyen hata'}`); },
                e => console.error(`Mevzuat Grafiği: "${n.title}" kartındaki son düzenleme kaydedilemedi.`, e))))
            .then(() => undefined);
    }

    // ---- .NET köprüsü ----
    // Başarısızlıkta sunucunun mesajını toast'lar ve null döner; çağıran geri alır.
    async function cagir(ad, args, sessiz) {
        let r;
        try {
            r = await ref.invokeMethodAsync(ad, ...args);
        } catch (e) {
            if (!sokuldu && !sessiz) toast('İşlem kaydedilemedi. Bağlantıyı kontrol edip tekrar deneyin.');
            return null;
        }
        if (sokuldu) return null;
        if (!r || !r.basarili) {
            if (!sessiz) toast((r && r.mesaj) || 'İşlem kaydedilemedi.');
            return null;
        }
        return r;
    }

    function layout(v) {
        const cols = {};
        v.nodes.forEach(id => { const n = byId(id); if (!n) return; const c = COL[n.type]; (cols[c] = cols[c] || []).push(id); });
        const lens = Object.values(cols).map(a => a.length);
        if (!lens.length) return;
        const max = Math.max(...lens);
        Object.entries(cols).forEach(([c, ids]) => {
            const off = (max - ids.length) * 150 / 2;
            ids.forEach((id, i) => { if (!v.pos[id]) v.pos[id] = { x: 40 + c * 290, y: 40 + off + i * 150 }; });
        });
    }

    /* ---------- graph helpers ---------- */
    function walk(id, dir) {
        const seen = new Set(), st = [id];
        while (st.length) {
            const c = st.pop();
            E.forEach(e => { const [f, t] = dir > 0 ? [e.a, e.b] : [e.b, e.a]; if (f === c && !seen.has(t)) { seen.add(t); st.push(t); } });
        }
        return seen;
    }
    function related(id) { const s = new Set([id]); walk(id, 1).forEach(x => s.add(x)); walk(id, -1).forEach(x => s.add(x)); return s; }

    /* ---------- transform ---------- */
    function applyT() {
        world.style.transform = `translate(${S.px}px,${S.py}px) scale(${S.z})`;
        const a = 20 * S.z, b = 100 * S.z;
        stage.style.backgroundSize = `${a}px ${a}px,${a}px ${a}px,${b}px ${b}px,${b}px ${b}px`;
        const p = `${S.px}px ${S.py}px`; stage.style.backgroundPosition = [p, p, p, p].join(',');
        $('#mg-z-lbl').textContent = Math.round(S.z * 100) + '%';
    }
    function visibleRect() {
        const r = stage.getBoundingClientRect(); const narrow = r.width < 820;
        const l = (!narrow && S.panels.bb) ? 274 : 0, rt = (!narrow && (S.panels.insp || S.panels.prev)) ? 324 : 0;
        return { x: l, y: 0, w: Math.max(200, r.width - l - rt), h: r.height };
    }
    function fit() {
        const v = V(); if (!v.nodes.length) return;
        let x0 = 1e9, y0 = 1e9, x1 = -1e9, y1 = -1e9;
        v.nodes.forEach(id => { const p = v.pos[id]; x0 = Math.min(x0, p.x); y0 = Math.min(y0, p.y); x1 = Math.max(x1, p.x + W); y1 = Math.max(y1, p.y + H_EST); });
        const vr = visibleRect(), pad = 40;
        S.z = Math.max(.3, Math.min(1.1, Math.min((vr.w - pad * 2) / (x1 - x0), (vr.h - pad * 2) / (y1 - y0))));
        S.px = vr.x + (vr.w - (x1 - x0) * S.z) / 2 - x0 * S.z;
        S.py = vr.y + (vr.h - (y1 - y0) * S.z) / 2 - y0 * S.z;
        applyT();
    }
    function zoomAt(f, cx, cy) {
        const z2 = Math.max(.3, Math.min(1.8, S.z * f));
        S.px = cx - (cx - S.px) * (z2 / S.z); S.py = cy - (cy - S.py) * (z2 / S.z); S.z = z2; applyT();
    }
    function centerOn(id) {
        const p = V().pos[id], vr = visibleRect();
        S.px = vr.x + vr.w / 2 - (p.x + W / 2) * S.z; S.py = vr.y + vr.h / 2 - (p.y + H_EST / 2) * S.z; applyT();
    }
    function toWorld(cx, cy) { const r = stage.getBoundingClientRect(); return { x: (cx - r.left - S.px) / S.z, y: (cy - r.top - S.py) / S.z }; }

    /* ---------- render ---------- */
    function focusSet() { return S.sel?.kind === 'node' ? related(S.sel.id) : null; }

    const hasIn = id => { const inV = new Set(V().nodes); return E.some(e => e.b === id && inV.has(e.a)); };
    const hasOut = id => { const inV = new Set(V().nodes); return E.some(e => e.a === id && inV.has(e.b)); };
    function renderNodes() {
        const fs = focusSet(), v = V();
        nodesEl.innerHTML = v.nodes.map(id => {
            const n = byId(id), p = v.pos[id]; if (!n || !p) return '';
            const T = TYPES[n.type];
            const cls = ['mg-node', 'mg-t-' + n.type, S.sel?.kind === 'node' && S.sel.id === id ? 'mg-sel' : '', fs && !fs.has(id) ? 'mg-dim' : ''].join(' ');
            return `<div class="${cls}" data-id="${id}" style="left:${p.x}px;top:${p.y}px">
      <div class="mg-nh"><span class="mg-dot"></span><span class="mg-nt">${esc(n.title)}</span><span class="mg-ty">${T.label}</span></div>
      <div class="mg-np">${T.inL ? `<span class="mg-port mg-in${hasIn(id) ? ' mg-on' : ''}" data-id="${id}" style="--mg-pc:var(--mg-c)" title="Bağlantıyı tutup başka karta taşı"></span><span>${T.inL}</span>` : ''}
        ${T.outL ? `<span class="mg-pr">${T.outL}</span><span class="mg-port mg-out${hasOut(id) ? ' mg-on' : ''}" data-id="${id}" style="--mg-pc:var(--mg-c)" title="Sürükleyip bağla"></span>` : ''}</div>
      <div class="mg-nb"><div class="mg-madde">${esc(n.madde) || '—'}</div><div class="mg-oz">${esc(n.ozet)}</div></div>
    </div>`;
        }).join('');
    }
    const colorOf = t => `var(--mg-c-${t})`;
    function bez(x1, y1, x2, y2) { const dx = Math.max(60, Math.abs(x2 - x1) * .5); return [x1, y1, x1 + dx, y1, x2 - dx, y2, x2, y2]; }
    function mid(p) { return [(p[0] + 3 * p[2] + 3 * p[4] + p[6]) / 8, (p[1] + 3 * p[3] + 3 * p[5] + p[7]) / 8]; }
    function renderEdges(extra = '') {
        const v = V(), inV = new Set(v.nodes), fs = focusSet(); let out = '', labels = '';
        E.forEach((e, i) => {
            if (!inV.has(e.a) || !inV.has(e.b)) return;
            const A = v.pos[e.a], B = v.pos[e.b];
            const p = bez(A.x + W, A.y + PORT_Y, B.x, B.y + PORT_Y), d = `M${p[0]},${p[1]} C${p[2]},${p[3]} ${p[4]},${p[5]} ${p[6]},${p[7]}`;
            const sel = S.sel?.kind === 'edge' && S.sel.i === i, dim = fs && !(fs.has(e.a) && fs.has(e.b));
            out += `<path class="mg-e${sel ? ' mg-sel' : ''}${dim ? ' mg-dim' : ''}" d="${d}" stroke="${colorOf(byId(e.a).type)}"/><path class="mg-ehit" data-i="${i}" d="${d}"/>`;
            if (sel || (S.sel?.kind === 'node' && (S.sel.id === e.a || S.sel.id === e.b))) { const m = mid(p); labels += `<text x="${m[0]}" y="${m[1] - 6}" text-anchor="middle">${relOf(e.a, e.b)}</text>`; }
        });
        X.forEach(x => {
            if (!inV.has(x.a) || !inV.has(x.b)) return;
            const A = v.pos[x.a], B = v.pos[x.b], ay = A.y + 70, by = B.y + 70;
            const dim = fs && !(fs.has(x.a) || fs.has(x.b));
            out += `<path class="mg-x${dim ? ' mg-dim' : ''}" d="M${A.x},${ay} C${A.x - 90},${ay} ${B.x - 90},${by} ${B.x},${by}"/>`;
            if (!dim) labels += `<text class="mg-xl" x="${(A.x + B.x) / 2 - 74}" y="${(ay + by) / 2 + 4}" text-anchor="middle">dışlar</text>`;
        });
        svg.innerHTML = out + labels + extra;
    }

    // Sekme şeridi + yeni tuval / yeniden adlandırma kutusu (mockup'ta yok; sekme yerinde açılır).
    let sekmeCiziliyor = false;
    function renderTabs() {
        const d = S.duzen;
        const kutu = deger => `<input class="mg-tab-inp" id="mg-tab-inp" maxlength="${MAX.tuval}" placeholder="Tuval adı" autocomplete="off" value="${esc(deger)}">`;
        let html = views.map((v, i) => d && d.hedef === i
            ? kutu(d.deger)
            : `<button class="mg-tab" role="tab" aria-selected="${i === S.view}" data-v="${i}">${esc(v.name)} <span style="color:var(--mg-faint)">${v.nodes.length}</span></button>`).join('');
        html += d && d.hedef === 'yeni'
            ? kutu(d.deger)
            : `<button class="mg-tab" data-yeni="1" title="Yeni tuval" aria-label="Yeni tuval">+</button>`;
        sekmeCiziliyor = true;
        $('#mg-tabs').innerHTML = html;
        sekmeCiziliyor = false;
        const inp = $('#mg-tab-inp');
        if (inp) {
            inp.focus();
            if (d.ilk) { inp.select(); d.ilk = false; } else inp.setSelectionRange(inp.value.length, inp.value.length);
        }
        $('#mg-bos-tuval').hidden = views.length > 0;
    }
    function renderBB() {
        const q = $('#mg-bb-q').value.trim().toLocaleLowerCase('tr'), inV = new Set(V().nodes);
        $('#mg-bb-count').textContent = N.length + ' tekil kart';
        $('#mg-bb-list').innerHTML = ORDER.map(t => {
            const items = N.filter(n => n.type === t && (!q || (n.title + ' ' + n.madde).toLocaleLowerCase('tr').includes(q)));
            if (!items.length) return '';
            return `<div class="mg-group mg-t-${t}"><div class="mg-gh"><i></i>${TYPES[t].label}</div>${items.map(n =>
                `<button class="mg-li mg-t-${t}${inV.has(n.id) ? '' : ' mg-out'}" data-lib="${n.id}" title="${inV.has(n.id) ? 'Tuvalde göster' : 'Bu tuvale ekle'}"><span class="mg-st">${inV.has(n.id) ? '●' : '+'}</span><span class="mg-t">${esc(n.title)}</span><span class="mg-m">${esc(n.madde)}</span></button>`).join('')}</div>`;
        }).join('') || '<div class="mg-empty">Eşleşen kart yok.</div>';
    }
    function renderInsp() {
        const b = $('#mg-insp-body'), sub = $('#mg-insp-sub');
        if (!S.sel) {
            sub.textContent = V().name;
            b.innerHTML = `<div class="mg-empty">Bir karta tıklayın: içeriğini burada düzenler, bağlantılarını görürsünüz. Bir <b style="color:var(--mg-c-firma)">Firma</b> kartı seçince yalnızca o firmaya uygulanan zincir aydınlanır.</div>
    <div class="mg-note">Bu tuvalde ${V().nodes.length} kart. Kütüphanedeki ● işaretli kartlar burada, + işaretliler başka tuvallerde.</div>`; return;
        }
        if (S.sel.kind === 'edge') {
            const e = E[S.sel.i], A = byId(e.a), B = byId(e.b); sub.textContent = 'Bağlantı';
            b.innerHTML = `<div class="mg-conn">
      <button class="mg-cn mg-t-${A.type}" data-go="${A.id}"><span class="mg-r">kaynak</span><span class="mg-d"></span><span class="mg-t">${esc(A.title)}</span></button>
      <button class="mg-cn mg-t-${B.type}" data-go="${B.id}"><span class="mg-r">${relOf(A.id, B.id)}</span><span class="mg-d"></span><span class="mg-t">${esc(B.title)}</span></button></div>
      <div class="mg-row"><button class="mg-btn mg-danger" id="mg-del-edge">Bağlantıyı sil</button></div><div class="mg-note">Klavyede Delete tuşu da siler.</div>`; return;
        }
        const n = byId(S.sel.id), T = TYPES[n.type]; sub.textContent = T.label + ' kartı';
        const ins = E.filter(e => e.b === n.id), outs = E.filter(e => e.a === n.id), xs = X.filter(x => x.a === n.id || x.b === n.id);
        const cn = (id, r, x) => { const m = byId(id); return `<button class="mg-cn mg-t-${m.type}${x ? ' mg-x' : ''}" data-go="${id}"><span class="mg-r">${r}</span><span class="mg-d"></span><span class="mg-t">${esc(m.title)}</span></button>`; };
        const kilitli = n._turSozu || geciciMi(n.id) ? ' disabled' : '';
        b.innerHTML = `<span class="mg-chip mg-t-${n.type}">${T.label}</span>
    <div class="mg-field"><label for="mg-in-tur">Kart türü</label><select class="mg-inp" id="mg-in-tur"${kilitli}>${ORDER.map(t => `<option value="${t}"${t === n.type ? ' selected' : ''}>${TYPES[t].label}</option>`).join('')}</select></div>
    <div class="mg-field"><label for="mg-in-title">Kart adı</label><input class="mg-inp" id="mg-in-title" maxlength="${MAX.baslik}" value="${esc(n.title)}"></div>
    <div class="mg-field"><label for="mg-in-madde">${n.type === 'firma' ? 'Profil' : 'Madde / etiket'}</label><input class="mg-inp" id="mg-in-madde" maxlength="${MAX.madde}" value="${esc(n.madde)}" style="font-family:var(--mg-f-mono)"></div>
    <div class="mg-field"><label for="mg-in-ozet">Özet</label><textarea class="mg-inp" id="mg-in-ozet" maxlength="${MAX.ozet}">${esc(n.ozet)}</textarea></div>
    <div class="mg-field"><span class="mg-lbl">Bağlantılar</span><div class="mg-conn">
      ${ins.map(e => cn(e.a, '← gelen')).join('')}${outs.map(e => cn(e.b, relOf(e.a, e.b))).join('')}${xs.map(x => cn(x.a === n.id ? x.b : x.a, 'dışlar', 1)).join('')}
      ${!ins.length && !outs.length && !xs.length ? '<div class="mg-empty">Henüz bağlantı yok. Sağ porttan sürükleyip bağlayın.</div>' : ''}</div></div>
    <div class="mg-field"><span class="mg-lbl">Göründüğü tuvaller</span><div class="mg-views">${views.map((v, i) => v.nodes.includes(n.id) ? `<span class="mg-vt${i === S.view ? ' mg-cur' : ''}">${esc(v.name)}</span>` : '').join('')}</div>
      <div class="mg-note">Tek kart: burada yaptığınız değişiklik bütün tuvallerde görünür.</div></div>
    ${S.silOnay === n.id ? silOnayHtml(n) : '<div class="mg-row"><button class="mg-btn" id="mg-rm-view">Bu tuvalden kaldır</button><button class="mg-btn mg-danger" id="mg-del-card">Kartı sil</button></div>'}`;
    }
    // Sayfa içi silme onayı (confirm() yok); sayılar silinecek her şeyi gösterir.
    function silOnayHtml(n) {
        const tuval = views.filter(v => v.nodes.includes(n.id)).length;
        const bag = E.filter(e => e.a === n.id || e.b === n.id).length;
        const dis = X.filter(x => x.a === n.id || x.b === n.id).length;
        const k = S.siliniyor ? ' disabled' : '';
        return `<div class="mg-warn mg-sil-onay" role="alertdialog" aria-labelledby="mg-sil-msg">
      <div id="mg-sil-msg">Bu kart bütün tuvallerden, bağlantıları ve dışlamalarıyla birlikte silinecek.</div>
      <div class="mg-sil-say"><b>${tuval}</b> tuvalde · <b>${bag}</b> bağlantıda${dis ? ` · <b>${dis}</b> dışlamada` : ''}</div>
      <div class="mg-row"><button class="mg-btn mg-danger" id="mg-del-yes"${k}>${S.siliniyor ? 'Siliniyor…' : 'Sil'}</button><button class="mg-btn" id="mg-del-no"${k}>Vazgeç</button></div></div>`;
    }
    function li(id) { const m = byId(id); return `<li class="mg-t-${m.type}"><span class="mg-d"></span><span>${esc(m.title)}</span><span class="mg-m">${esc(m.madde)}</span></li>`; }
    function renderPrev() {
        const b = $('#mg-prev-body'), sub = $('#mg-prev-sub');
        if (!S.sel || S.sel.kind !== 'node') {
            sub.textContent = '';
            b.innerHTML = '<div class="mg-empty">Bir firma kartı seçin: o firmanın yararlandığı istisna ve indirimler, dayanak maddeler ve takip edilecek şartlar burada özetlenir.</div>'; return;
        }
        const n = byId(S.sel.id);
        if (n.type === 'firma') {
            sub.textContent = n.title;
            // Kullanıyor: firmaya DOĞRUDAN bağlı teşvikler. Uygun olabilir: firmanın İşlem kartları
            // üzerinden ulaşılan ama doğrudan bağlı olmayanlar. Şart, dayanak ve çakışma yalnız
            // "kullanıyor"dan hesaplanır.
            const cikis = id => E.filter(e => e.a === id).map(e => e.b);
            const turSirali = ids => [...ids.filter(id => byId(id).type === 'istisna'), ...ids.filter(id => byId(id).type === 'indirim')];
            const kul = turSirali([...new Set(cikis(n.id).filter(id => tesvikMi(byId(id).type)))]);
            const kulSet = new Set(kul);
            const uyg = turSirali([...new Set(cikis(n.id).filter(id => byId(id).type === 'islem')
                .flatMap(cikis).filter(id => tesvikMi(byId(id).type) && !kulSet.has(id)))]);
            const d = new Set(); kul.forEach(t => walk(t, 1).forEach(x => d.add(x)));
            const g = t => [...d].filter(id => byId(id).type === t);
            const yk = g('yuk'), kn = g('kanun');
            const warns = X.filter(x => kulSet.has(x.a) && kulSet.has(x.b));
            const ck = S.checks[n.id] || (S.checks[n.id] = {});
            b.innerHTML = `${warns.map(x => `<div class="mg-warn"><b>Çakışma:</b> ${esc(byId(x.a).title)} ile ${esc(byId(x.b).title)}. ${esc(x.note)}.</div>`).join('')}
      <div class="mg-sum"><h3>Kullanıyor (${kul.length})</h3><ul>${kul.map(li).join('') || '<li class="mg-empty">Henüz doğrudan bağlı teşvik yok. Firma kartının sağ portundan bir İstisna ya da İndirim kartına bağlayın.</li>'}</ul></div>
      ${uyg.length ? `<div class="mg-sum mg-uygun"><h3>Uygun olabilir (${uyg.length})</h3><ul>${uyg.map(li).join('')}</ul></div>` : ''}
      <div class="mg-sum"><h3>Takip edilecek şartlar</h3><div style="display:flex;flex-direction:column;gap:4px">${yk.map(id => {
                const m = byId(id);
                return `<label class="mg-chk${ck[id] ? ' mg-done' : ''}"><input type="checkbox" id="mg-chk-${n.id}-${id}" data-chk="${id}" ${ck[id] ? 'checked' : ''}><span>${esc(m.title)} <span style="color:var(--mg-faint)">· ${esc(m.madde)}</span></span></label>`;
            }).join('') || '<span class="mg-empty">Yok</span>'}</div></div>
      <div class="mg-sum"><h3>Dayanak maddeler</h3><ul>${kn.map(li).join('') || '<li class="mg-empty">Yok</li>'}</ul></div>`;
        } else {
            sub.textContent = n.title;
            const firms = [...walk(n.id, -1)].filter(id => byId(id).type === 'firma');
            const xs = X.filter(x => x.a === n.id || x.b === n.id);
            b.innerHTML = `<div class="mg-sum mg-t-${n.type}"><div class="mg-madde">${esc(n.madde)}</div><div class="mg-oz" style="-webkit-line-clamp:unset">${esc(n.ozet)}</div></div>
      ${xs.map(x => `<div class="mg-warn"><b>Birlikte uygulanmaz:</b> ${esc(byId(x.a === n.id ? x.b : x.a).title)}. ${esc(x.note)}.</div>`).join('')}
      <div class="mg-sum"><h3>Bu kartla ilişkili firmalar</h3><ul>${firms.map(li).join('') || '<li class="mg-empty">Henüz bir firmaya bağlı değil</li>'}</ul></div>`;
        }
    }
    function renderAll() { renderTabs(); renderNodes(); renderEdges(); renderBB(); renderInsp(); renderPrev(); }

    /* ---------- actions ---------- */
    function toast(msg, good) {
        if (sokuldu) return;
        const t = $('#mg-toast'); t.textContent = msg; t.className = 'mg-toast' + (good ? ' mg-good' : ''); t.hidden = false;
        clearTimeout(toastT); toastT = setTimeout(() => t.hidden = true, 3600);
    }
    // Seçimi değiştiren her yol bekleyen Inspector düzenlemesini önce gönderir (bekleyen yoksa etkisiz).
    function select(sel) { bekleyenleriGonder(); if (!S.siliniyor) S.silOnay = null; S.sel = sel; renderNodes(); renderEdges(); renderInsp(); renderPrev(); }
    function addToView(id) {
        const v = V(); if (v.nodes.includes(id)) return;
        const r = stage.getBoundingClientRect(), vr = visibleRect(), c = toWorld(r.left + vr.x + vr.w / 2, r.top + vr.h / 2);
        v.nodes.push(id); v.pos[id] = { x: c.x - W / 2, y: c.y - H_EST / 2 };
        // Kaydı süren yeni kart tuvale, kart kaydı bitince eklenir (kartOlustur).
        if (!geciciMi(id)) tuvaleEkleKaydet(v, id);
    }
    function goTo(id) {
        if (!views.length) { toast('Önce bir tuval oluşturun: sekme şeridindeki + ile.'); return; }
        bekleyenleriGonder();
        if (!S.siliniyor) S.silOnay = null;
        if (!V().nodes.includes(id)) { addToView(id); toast(`"${byId(id).title}" bu tuvale eklendi. Aynı kart, kopya değil.`, true); }
        S.sel = { kind: 'node', id }; renderAll(); centerOn(id);
    }
    function tryConnect(a, b, eski) {
        const A = byId(a), B = byId(b);
        if (a === b) return false;
        if (!ALLOWED[A.type].includes(B.type)) {
            toast(`${TYPES[A.type].label} kartı ${TYPES[B.type].label} kartına bağlanamaz. Sıra: ${SIRA}.`); return false;
        }
        if (E.some(e => e.a === a && e.b === b)) { toast('Bu bağlantı zaten var.'); return false; }
        if (geciciMi(a) || geciciMi(b)) { toast('Kart henüz kaydediliyor; birazdan tekrar deneyin.'); return false; }
        const yeni = { id: null, a, b };
        bekleyenleriGonder();
        E.push(yeni); toast(`${A.title} ${relOf(a, b)} → ${B.title}`, true);
        S.sel = { kind: 'edge', i: E.length - 1 }; renderAll();
        baglantiKaydet(yeni, eski);
        return true;
    }
    function markPorts(fn) { nodesEl.querySelectorAll('.mg-port').forEach(p => { const r = fn(p); if (r !== null) p.classList.add(r ? 'mg-ok' : 'mg-no'); }); stage.classList.add('mg-wiring'); }
    function startWire(from, detached) {
        const tp = byId(from).type;
        markPorts(p => p.classList.contains('mg-in') ? (ALLOWED[tp].includes(byId(p.dataset.id).type) && p.dataset.id !== from) : null);
        drag = { kind: 'wire', id: from, detached };
    }
    function startReverse(to) {
        const tp = byId(to).type;
        markPorts(p => p.classList.contains('mg-out') ? (ALLOWED[byId(p.dataset.id).type].includes(tp) && p.dataset.id !== to) : null);
        drag = { kind: 'rwire', id: to };
    }

    /* ---------- kayıt (JS → [JSInvokable] → API) ---------- */
    function edgeCikar(e) {
        const i = E.indexOf(e); if (i < 0) return;
        E.splice(i, 1);
        if (S.sel?.kind === 'edge') { if (S.sel.i === i) S.sel = null; else if (S.sel.i > i) S.sel.i--; }
    }

    // Ekleme; taşımada (eski dolu) önce eski silinir. Ekleme başarısızsa silinen eski geri eklenir.
    async function baglantiKaydet(yeni, eski) {
        if (eski && eski.id != null) {
            if (!await cagir('BaglantiSil', [eski.id])) { edgeCikar(yeni); E.push(eski); renderAll(); return; }
        }
        const r = await cagir('BaglantiEkle', [{ kaynakKartId: +yeni.a, hedefKartId: +yeni.b }]);
        if (r) { yeni.id = r.veri.id; return; }
        edgeCikar(yeni);
        if (eski && eski.id != null) {
            const g = await cagir('BaglantiEkle', [{ kaynakKartId: +eski.a, hedefKartId: +eski.b }], true);
            if (g) E.push({ id: g.veri.id, a: eski.a, b: eski.b });
        }
        renderAll();
    }

    // Yerelden zaten çıkarılmış bağlantıyı sunucudan siler; red gelirse geri koyar.
    async function baglantiSil(e) {
        if (!await cagir('BaglantiSil', [e.id])) { E.push(e); renderAll(); }
    }

    function seciliBaglantiyiSil(bildir) {
        const e = E[S.sel.i];
        if (e.id == null) { toast('Bağlantı henüz kaydediliyor; birazdan tekrar deneyin.'); return; }
        E.splice(S.sel.i, 1); S.sel = null; renderAll();
        if (bildir) toast('Bağlantı silindi.', true);
        baglantiSil(e);
    }

    // Sürükleme bitince: yalnız aktif tuvalde olan (kaydı bitmiş) kartlar; tuvalde olmayan kart
    // listede olursa sunucu hiçbirini yazmaz.
    async function yerlesimKaydet(v, geriAl) {
        const liste = v.nodes.filter(id => !geciciMi(id)).map(id => ({ kartId: +id, x: v.pos[id].x, y: v.pos[id].y }));
        if (!await cagir('YerlesimKaydet', [v.id, liste])) geriAl();
    }

    async function tuvaleEkleKaydet(v, id) {
        const p = v.pos[id];
        if (await cagir('TuvaleKartEkle', [v.id, { kartId: +id, x: p.x, y: p.y }])) return;
        v.nodes = v.nodes.filter(x => x !== id); delete v.pos[id];
        if (S.sel?.kind === 'node' && S.sel.id === id && V() === v) S.sel = null;
        renderAll();
    }

    function idDegistir(eski, yeni) {
        const n = byId(eski); if (n) n.id = yeni;
        views.forEach(v => {
            const i = v.nodes.indexOf(eski);
            if (i >= 0) { v.nodes[i] = yeni; v.pos[yeni] = v.pos[eski]; delete v.pos[eski]; }
        });
        if (S.sel?.kind === 'node' && S.sel.id === eski) S.sel.id = yeni;
    }

    async function kartOlustur(n, v) {
        const r = await cagir('KartEkle', [{ tur: TUR_NO[n.type], baslik: n.title, madde: n.madde || null, ozet: n.ozet || null }]);
        if (!r) {
            N = N.filter(x => x !== n);
            views.forEach(w => { w.nodes = w.nodes.filter(x => x !== n.id); delete w.pos[n.id]; });
            if (S.sel?.kind === 'node' && S.sel.id === n.id) S.sel = null;
            renderAll(); return;
        }
        const yeniId = String(r.veri.id);
        idDegistir(n.id, yeniId);
        n._kayitli = { title: r.veri.baslik, madde: r.veri.madde || '', ozet: r.veri.ozet || '' };
        // Kayıt sürerken Inspector'da yazılanlar varsa onları da gönder.
        if (n.title !== n._kayitli.title || n.madde !== n._kayitli.madde || n.ozet !== n._kayitli.ozet) kartKaydetGecikmeli(n);
        if (v && v.nodes.includes(yeniId)) tuvaleEkleKaydet(v, yeniId);
        // Inspector yeniden çizilmez: kullanıcı o sırada yazıyor olabilir, kutularda id yok.
        renderTabs(); renderNodes(); renderEdges(); renderBB(); renderPrev();
    }

    function kartKaydetGecikmeli(n) {
        if (geciciMi(n.id)) return; // kart kaydı bitince kartOlustur farkı gönderir
        clearTimeout(kartZamanlayicilari.get(n.id)?.t);
        kartZamanlayicilari.set(n.id, { n, t: setTimeout(() => { kartZamanlayicilari.delete(n.id); kartKaydet(n); }, 600) });
    }

    // Bekleyen düzenlemelerin zamanlayıcılarını durdurur ve kartlarını döner.
    function bekleyenleriAl() {
        const liste = [...kartZamanlayicilari.values()];
        liste.forEach(z => clearTimeout(z.t));
        kartZamanlayicilari.clear();
        return liste.map(z => z.n);
    }

    // Seçim ya da tuval değişirken: debounce beklenmeden normal kayıt yolu (red → geri al + toast).
    function bekleyenleriGonder() { bekleyenleriAl().forEach(kartKaydet); }

    const kartGovdesi = n => ({ tur: TUR_NO[n.type], baslik: n.title, madde: n.madde || null, ozet: n.ozet || null });

    async function kartKaydet(n) {
        // Tür kaydı sürerken gönderilen gövde eski türü taşır ve sunucuda türü geri alırdı;
        // tür kaydı bitince yeniden denenir.
        if (n._turSozu) { kartKaydetGecikmeli(n); return; }
        const r = await cagir('KartGuncelle', [+n.id, kartGovdesi(n)]);
        if (r) { n._kayitli = { title: r.veri.baslik, madde: r.veri.madde || '', ozet: r.veri.ozet || '' }; return; }
        if (kartZamanlayicilari.has(n.id)) return; // kullanıcı yazmaya devam ediyor; sıradaki kayıt karar verir
        Object.assign(n, n._kayitli);
        renderNodes(); renderEdges(); renderBB(); renderPrev();
        if (S.sel?.kind === 'node' && S.sel.id === n.id) renderInsp();
    }

    // Tek kartın bekleyen düzenlemesini durdurur (gövdesi başka bir istekle gidecekse).
    function kartBekleyeniniAl(id) {
        const z = kartZamanlayicilari.get(id); if (!z) return;
        clearTimeout(z.t); kartZamanlayicilari.delete(id);
    }
    const kaydedilmemis = n => n.title !== n._kayitli.title || n.madde !== n._kayitli.madde || n.ozet !== n._kayitli.ozet;

    // Tür değişikliği iyimser değildir: sunucu bağlantı/dışlama kuralıyla reddedebilir. Onay
    // gelene kadar kart eski türünde kalır ve select kilitlenir; red → select eski değere döner.
    async function turDegistir(n, yeniTur) {
        if (!n || n.type === yeniTur || n._turSozu || geciciMi(n.id)) return;
        kartBekleyeniniAl(n.id); // gövde güncel metni de taşır
        const govde = { ...kartGovdesi(n), tur: TUR_NO[yeniTur] };
        const sec = $('#mg-in-tur'); if (sec) sec.disabled = true;
        let bitir;
        n._turSozu = new Promise(r => { bitir = r; });
        const r = await cagir('KartGuncelle', [+n.id, govde]);
        n._turSozu = null; bitir();
        if (sokuldu) return;
        if (r) {
            n.type = yeniTur;
            n._kayitli = { title: r.veri.baslik, madde: r.veri.madde || '', ozet: r.veri.ozet || '' };
            if (kaydedilmemis(n)) kartKaydetGecikmeli(n);
            renderAll(); // renk, port etiketleri, kütüphane grubu, kenar rengi, önizleme
            return;
        }
        if (kaydedilmemis(n)) kartKaydetGecikmeli(n);
        if (S.sel?.kind === 'node' && S.sel.id === n.id) {
            const s = $('#mg-in-tur');
            if (s) { s.value = n.type; s.disabled = false; }
        }
    }

    // Silme de iyimser değildir: başarısızlıkta hiçbir şey silinmez, mesaj toast'ta görünür.
    async function kartSil(n) {
        if (!n || S.siliniyor) return;
        if (geciciMi(n.id)) { toast('Kart henüz kaydediliyor; birazdan tekrar deneyin.'); return; }
        if (n._turSozu) { toast('Kartın türü kaydediliyor; birazdan tekrar deneyin.'); return; }
        const id = n.id;
        kartBekleyeniniAl(id);
        S.siliniyor = true; renderInsp();
        const r = await cagir('KartSil', [+id]);
        S.siliniyor = false;
        if (sokuldu) return;
        if (!r) {
            if (kaydedilmemis(n)) kartKaydetGecikmeli(n); // durdurulan düzenleme kaybolmasın
            if (S.sel?.kind === 'node' && S.sel.id === id) renderInsp();
            return;
        }
        N = N.filter(x => x.id !== id);
        E = E.filter(e => e.a !== id && e.b !== id);
        X = X.filter(x => x.a !== id && x.b !== id);
        views.forEach(v => { v.nodes = v.nodes.filter(x => x !== id); delete v.pos[id]; });
        delete S.checks[id];
        Object.values(S.checks).forEach(c => { delete c[id]; });
        S.silOnay = null; S.sel = null;
        renderAll();
        toast(`"${n.title}" silindi.`, true);
    }

    async function tuvalAdiKaydet() {
        const d = S.duzen; const ad = d.deger.trim();
        if (!ad) { toast('Tuval adı zorunlu.'); return; }
        S.duzen = null;
        if (d.hedef === 'yeni') {
            renderTabs();
            const r = await cagir('TuvalEkle', [{ ad }]);
            if (!r) { S.duzen = { hedef: 'yeni', deger: ad, ilk: false }; renderTabs(); return; }
            views.push({ id: r.veri.id, name: r.veri.ad, nodes: [], pos: {} });
            bekleyenleriGonder();
            S.view = views.length - 1; S.sel = null; renderAll(); fit();
            toast(`"${r.veri.ad}" tuvali oluşturuldu.`, true);
        } else {
            const v = views[d.hedef], eski = v.name;
            v.name = ad; renderTabs(); renderInsp();
            if (!await cagir('TuvalGuncelle', [v.id, { ad }])) { v.name = eski; renderTabs(); renderInsp(); }
        }
    }

    /* ---------- pointer ---------- */
    let drag = null;
    dinle(stage, 'pointerdown', e => {
        if (e.button !== 0 && e.pointerType === 'mouse') return;
        if (e.target.closest('.mg-panel,.mg-toast')) return;
        const port = e.target.closest('.mg-port.mg-out');
        if (port) { e.preventDefault(); startWire(port.dataset.id); return; }
        const pin = e.target.closest('.mg-port.mg-in');
        if (pin) {
            e.preventDefault(); const id = pin.dataset.id, inV = new Set(V().nodes);
            // Shader Graph gibi: bağlı giriş portundan tutunca bağlantı kopar ve imlece yapışır.
            // Kaydı süren (id'siz) bağlantı koparılmaz: sunucuda yetim kayıt kalırdı.
            let idx = (S.sel?.kind === 'edge' && E[S.sel.i]?.b === id && E[S.sel.i].id != null) ? S.sel.i : -1;
            if (idx < 0) E.forEach((x, i) => { if (x.b === id && inV.has(x.a) && x.id != null) idx = i; });
            if (idx >= 0) { bekleyenleriGonder(); const ed = E.splice(idx, 1)[0]; S.sel = null; renderAll(); startWire(ed.a, ed); }
            else startReverse(id);
            return;
        }
        const hit = e.target.closest('.mg-ehit');
        if (hit) { select({ kind: 'edge', i: +hit.dataset.i }); return; }
        const node = e.target.closest('.mg-node');
        if (node) {
            const id = node.dataset.id;
            if (!(S.sel?.kind === 'node' && S.sel.id === id)) select({ kind: 'node', id });
            if (e.target.closest('.mg-nh')) { e.preventDefault(); const p = V().pos[id]; drag = { kind: 'node', id, v: V(), sx: e.clientX, sy: e.clientY, ox: p.x, oy: p.y }; }
            return;
        }
        drag = { kind: 'pan', sx: e.clientX, sy: e.clientY, ox: S.px, oy: S.py, moved: false }; stage.classList.add('mg-panning');
    });
    dinle(window, 'pointermove', e => {
        if (!drag) return;
        if (drag.kind === 'pan') { const dx = e.clientX - drag.sx, dy = e.clientY - drag.sy; if (Math.abs(dx) + Math.abs(dy) > 3) drag.moved = true; S.px = drag.ox + dx; S.py = drag.oy + dy; applyT(); }
        else if (drag.kind === 'node') {
            const p = V().pos[drag.id]; p.x = drag.ox + (e.clientX - drag.sx) / S.z; p.y = drag.oy + (e.clientY - drag.sy) / S.z;
            const el = nodesEl.querySelector(`.mg-node[data-id="${drag.id}"]`); el.style.left = p.x + 'px'; el.style.top = p.y + 'px'; renderEdges();
        }
        else if (drag.kind === 'wire') {
            const p = V().pos[drag.id], w = toWorld(e.clientX, e.clientY), b = bez(p.x + W, p.y + PORT_Y, w.x, w.y);
            renderEdges(`<path class="mg-wire" stroke="${colorOf(byId(drag.id).type)}" d="M${b[0]},${b[1]} C${b[2]},${b[3]} ${b[4]},${b[5]} ${b[6]},${b[7]}"/>`);
        }
        else if (drag.kind === 'rwire') {
            const p = V().pos[drag.id], w = toWorld(e.clientX, e.clientY), b = bez(w.x, w.y, p.x, p.y + PORT_Y);
            renderEdges(`<path class="mg-wire" stroke="${colorOf(byId(drag.id).type)}" d="M${b[0]},${b[1]} C${b[2]},${b[3]} ${b[4]},${b[5]} ${b[6]},${b[7]}"/>`);
        }
    });
    dinle(window, 'pointerup', e => {
        if (!drag) return; const d = drag; drag = null; stage.classList.remove('mg-panning', 'mg-wiring');
        if (d.kind === 'pan' && !d.moved && S.sel) select(null);
        if (d.kind === 'node') {
            const p = d.v.pos[d.id];
            if (p && (p.x !== d.ox || p.y !== d.oy) && !geciciMi(d.id)) {
                yerlesimKaydet(d.v, () => {
                    const q = d.v.pos[d.id]; if (q) { q.x = d.ox; q.y = d.oy; }
                    renderNodes(); renderEdges();
                });
            }
        }
        if (d.kind === 'wire' || d.kind === 'rwire') {
            nodesEl.querySelectorAll('.mg-port').forEach(p => p.classList.remove('mg-ok', 'mg-no'));
            const el = document.elementFromPoint(e.clientX, e.clientY), t = el && el.closest('.mg-node');
            if (d.kind === 'rwire') { renderEdges(); if (t) tryConnect(t.dataset.id, d.id); return; }
            if (t) {
                if (d.detached && t.dataset.id === d.detached.b) {
                    // Aynı karta geri bırakıldı: mockup'taki gibi seçilir, sunucuda değişiklik yok.
                    const A = byId(d.detached.a), B = byId(d.detached.b);
                    bekleyenleriGonder();
                    E.push(d.detached); toast(`${A.title} ${relOf(A.id, B.id)} → ${B.title}`, true);
                    S.sel = { kind: 'edge', i: E.length - 1 }; renderAll();
                } else {
                    const ok = tryConnect(d.id, t.dataset.id, d.detached);
                    if (!ok && d.detached) { E.push(d.detached); renderAll(); }   // geçersiz yere bırakıldı: eski bağlantı geri gelir
                }
            } else if (d.detached) { renderAll(); toast('Bağlantı koparıldı. Boşluğa bırakınca silinir.', true); baglantiSil(d.detached); }
            else renderEdges();
        }
    });
    dinle(stage, 'wheel', e => {
        if (e.target.closest('.mg-panel')) return; e.preventDefault();
        const r = stage.getBoundingClientRect(); zoomAt(Math.exp(-e.deltaY * 0.0015), e.clientX - r.left, e.clientY - r.top);
    }, { passive: false });
    dinle(window, 'keydown', e => {
        if (e.target.matches('input,textarea,select')) return;
        if ((e.key === 'Delete' || e.key === 'Backspace') && S.sel?.kind === 'edge') seciliBaglantiyiSil(false);
        if (e.key === 'Escape') select(null);
    });

    /* ---------- UI events ---------- */
    let sonTik = {};
    dinle($('#mg-tabs'), 'click', e => {
        if (e.target.closest('#mg-tab-inp')) return;
        if (e.target.closest('[data-yeni]')) { S.duzen = { hedef: 'yeni', deger: '', ilk: true }; renderTabs(); return; }
        const t = e.target.closest('[data-v]'); if (!t) return;
        const i = +t.dataset.v, simdi = Date.now();
        // Çift tıklama elle ölçülüyor: ilk tık sekmeleri yeniden çizdiği için dblclick güvenilmez.
        if (sonTik.v === i && simdi - sonTik.t < 400) { sonTik = {}; S.duzen = { hedef: i, deger: views[i].name, ilk: true }; renderTabs(); return; }
        sonTik = { v: i, t: simdi };
        bekleyenleriGonder();
        S.view = i; if (S.sel?.kind === 'node' && !V().nodes.includes(S.sel.id)) S.sel = null; if (S.sel?.kind === 'edge') S.sel = null; renderAll(); fit();
    });
    dinle($('#mg-tabs'), 'input', e => { if (e.target.id === 'mg-tab-inp' && S.duzen) S.duzen.deger = e.target.value; });
    dinle($('#mg-tabs'), 'keydown', e => {
        if (e.target.id !== 'mg-tab-inp' || !S.duzen) return;
        if (e.key === 'Enter') { e.preventDefault(); tuvalAdiKaydet(); }
        else if (e.key === 'Escape') { e.preventDefault(); S.duzen = null; renderTabs(); }
    });
    dinle($('#mg-tabs'), 'focusout', e => {
        if (sekmeCiziliyor || e.target.id !== 'mg-tab-inp' || !S.duzen) return;
        S.duzen = null; renderTabs();
    });
    dinle($('#mg-bb-q'), 'input', renderBB);
    dinle($('#mg-bb-list'), 'click', e => { const t = e.target.closest('[data-lib]'); if (t) goTo(t.dataset.lib); });
    $('#mg-nk-type').innerHTML = ORDER.map(t => `<option value="${t}">${TYPES[t].label}</option>`).join('');
    $('#mg-nk-title').maxLength = MAX.baslik;
    dinle($('#mg-nk-add'), 'click', () => {
        const title = $('#mg-nk-title').value.trim(); if (!title) { $('#mg-nk-title').focus(); return; }
        const id = yeniGeciciId(), n = { id, type: $('#mg-nk-type').value, title, madde: '', ozet: '' };
        N.push(n);
        $('#mg-nk-title').value = '';
        if (!views.length) { renderBB(); kartOlustur(n, null); return; }
        const v = V();
        goTo(id);
        kartOlustur(n, v);
    });
    dinle($('#mg-nk-title'), 'keydown', e => { if (e.key === 'Enter') $('#mg-nk-add').click(); });
    dinle($('#mg-insp-body'), 'input', e => {
        if (!S.sel || S.sel.kind !== 'node') return; const n = byId(S.sel.id);
        const f = { 'mg-in-title': 'title', 'mg-in-madde': 'madde', 'mg-in-ozet': 'ozet' }[e.target.id]; if (!f) return;
        n[f] = e.target.value; renderNodes(); renderEdges(); renderBB(); renderPrev();
        kartKaydetGecikmeli(n);
    });
    dinle($('#mg-insp-body'), 'click', e => {
        const g = e.target.closest('[data-go]'); if (g) { goTo(g.dataset.go); return; }
        if (e.target.id === 'mg-del-edge') seciliBaglantiyiSil(true);
        if (e.target.id === 'mg-del-card') {
            if (geciciMi(S.sel.id)) { toast('Kart henüz kaydediliyor; birazdan tekrar deneyin.'); return; }
            S.silOnay = S.sel.id; renderInsp(); $('#mg-del-no')?.focus(); return;
        }
        if (e.target.id === 'mg-del-no') { S.silOnay = null; renderInsp(); return; }
        if (e.target.id === 'mg-del-yes') { kartSil(byId(S.sel.id)); return; }
        if (e.target.id === 'mg-rm-view') {
            bekleyenleriGonder();
            const v = V(), id = S.sel.id, idx = v.nodes.indexOf(id), eskiPos = v.pos[id];
            v.nodes = v.nodes.filter(x => x !== id); delete v.pos[id]; S.sel = null; renderAll();
            toast(`Kart bu tuvalden kaldırıldı; kütüphanede ve diğer tuvallerde duruyor.`, true);
            if (!geciciMi(id)) {
                cagir('TuvaldenKartKaldir', [v.id, +id]).then(r => {
                    if (r) return;
                    v.nodes.splice(idx, 0, id); v.pos[id] = eskiPos; renderAll();
                });
            }
        }
    });
    dinle($('#mg-insp-body'), 'change', e => {
        if (e.target.id !== 'mg-in-tur' || S.sel?.kind !== 'node') return;
        turDegistir(byId(S.sel.id), e.target.value);
    });
    dinle($('#mg-prev-body'), 'change', e => {
        const c = e.target.dataset.chk; if (!c) return;
        const fid = S.sel.id, deger = e.target.checked;
        S.checks[fid][c] = deger; renderPrev();
        if (geciciMi(fid) || geciciMi(c)) return;
        cagir('SartTakipKaydet', [{ firmaKartId: +fid, sartKartId: +c, tamamlandi: deger }]).then(r => {
            if (r) return;
            S.checks[fid][c] = !deger; if (S.sel?.kind === 'node' && S.sel.id === fid) renderPrev();
        });
    });
    ['bb', 'insp', 'prev'].forEach(k => {
        dinle($('#mg-tg-' + k), 'click', () => { S.panels[k] = !S.panels[k]; syncPanels(); });
    });
    function syncPanels() { ['bb', 'insp', 'prev'].forEach(k => { $('#mg-' + k).hidden = !S.panels[k]; $('#mg-tg-' + k).setAttribute('aria-pressed', S.panels[k]); }); }
    dinle($('#mg-z-in'), 'click', () => { const r = stage.getBoundingClientRect(); zoomAt(1.2, r.width / 2, r.height / 2); });
    dinle($('#mg-z-out'), 'click', () => { const r = stage.getBoundingClientRect(); zoomAt(1 / 1.2, r.width / 2, r.height / 2); });
    dinle($('#mg-z-fit'), 'click', fit);

    // ---- açılış ----
    yukle(veri);
    if (stage.getBoundingClientRect().width < 820) { S.panels = { bb: false, insp: false, prev: false }; }
    syncPanels();
    // Mockup ilk firma kartı seçili açılıyordu; gerçek veride aktif tuvalin ilk Firma kartı.
    const ilkFirma = V().nodes.find(id => byId(id)?.type === 'firma');
    S.sel = ilkFirma ? { kind: 'node', id: ilkFirma } : null;
    renderAll(); fit();

    return {
        sok: sok,
        guncelle: function (yeniVeri) {
            if (sokuldu) return;
            const seciliTuval = V().id;
            yukle(yeniVeri);
            const i = views.findIndex(v => v.id === seciliTuval);
            S.view = i >= 0 ? i : 0;
            if (S.sel?.kind === 'node' && !byId(S.sel.id)) S.sel = null;
            if (S.sel?.kind === 'edge') S.sel = null;
            renderAll();
        }
    };
}
