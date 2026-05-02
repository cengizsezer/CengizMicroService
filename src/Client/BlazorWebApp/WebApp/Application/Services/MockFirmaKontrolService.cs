using WebApp.Application.Services.Interfaces;
using WebApp.Domain.Models.FirmaKontrol;

namespace WebApp.Application.Services
{
    public class MockFirmaKontrolService : IFirmaKontrolService
    {
        public const string CatDonenVarliklar = "Dönen Varlıklar";
        public const string CatDuranVarlik = "Duran Varlık";
        public const string CatKisaVY = "Kısa V.Y";
        public const string CatBilancoIslemi = "Bilanço İşlemi";
        public const string CatGelirTablosu = "Gelir Tablosu";
        public const string CatBeyannameSonuc = "Beyanname Sonuç";
        public const string CatTicaretSicil = "Ticaret Sicil";

        public static readonly IReadOnlyList<string> CategoryOrder = new[]
        {
            CatDonenVarliklar,
            CatDuranVarlik,
            CatKisaVY,
            CatBilancoIslemi,
            CatGelirTablosu,
            CatBeyannameSonuc,
            CatTicaretSicil
        };

        private readonly IHesapPlaniLoader _hesapPlaniLoader;
        private readonly List<Firma> _firms;
        private readonly Dictionary<int, List<ControlItem>> _itemsByFirm = new();
        private readonly Dictionary<int, Dictionary<string, decimal?>> _rawCariByFirm = new();
        private readonly Dictionary<int, Dictionary<string, decimal?>> _rawOncekiByFirm = new();

        private readonly SemaphoreSlim _initLock = new(1, 1);
        private HesapPlani? _hesapPlaniTemplate;
        private bool _mizanInitialized;

        public MockFirmaKontrolService(IHesapPlaniLoader hesapPlaniLoader)
        {
            _hesapPlaniLoader = hesapPlaniLoader;

            _firms = new List<Firma>
            {
                new() { Id = 1, Ad = "Furkan Mühendislik A.Ş.", VergiNo = "1234567890", Sektor = "İnşaat",   Donem = "2025 Hesap Dönemi", LogoText = "FM" },
                new() { Id = 2, Ad = "Derya Gıda Ltd. Şti.",     VergiNo = "9876543210", Sektor = "Gıda",     Donem = "2025 Hesap Dönemi", LogoText = "DG" },
                new() { Id = 3, Ad = "Altuner Otomotiv A.Ş.",    VergiNo = "5556667778", Sektor = "Otomotiv", Donem = "2025 Hesap Dönemi", LogoText = "AO" },
                new() { Id = 4, Ad = "Yayla Kuruyemiş Ltd. Şti.",VergiNo = "1112223334", Sektor = "Perakende",Donem = "2025 Hesap Dönemi", LogoText = "YK" }
            };

            foreach (var f in _firms)
                _itemsByFirm[f.Id] = BuildTemplate();
        }

        public Task<IReadOnlyList<Firma>> GetFirmsAsync()
            => Task.FromResult<IReadOnlyList<Firma>>(_firms);

        public Task<Firma?> GetFirmAsync(int firmaId)
            => Task.FromResult(_firms.FirstOrDefault(f => f.Id == firmaId));

        public Task<IReadOnlyList<ControlItem>> GetControlItemsAsync(int firmaId)
        {
            if (!_itemsByFirm.TryGetValue(firmaId, out var list))
                return Task.FromResult<IReadOnlyList<ControlItem>>(Array.Empty<ControlItem>());
            return Task.FromResult<IReadOnlyList<ControlItem>>(list);
        }

        public Task UpdateControlItemAsync(int firmaId, ControlItem item)
        {
            if (!_itemsByFirm.TryGetValue(firmaId, out var list))
                return Task.CompletedTask;

            var existing = list.FirstOrDefault(x => x.Id == item.Id);
            if (existing is null) return Task.CompletedTask;

            existing.IsChecked = item.IsChecked;
            existing.Status = item.Status;
            existing.Note = item.Note;
            return Task.CompletedTask;
        }

        public async Task<HesapPlani> GetMizanAsync(int firmaId)
        {
            await EnsureMizanInitializedAsync();

            var firma = _firms.FirstOrDefault(f => f.Id == firmaId);
            return firma?.Mizan ?? new HesapPlani();
        }

        public async Task<MizanUpdateResult> UpdateMizanFromExcelAsync(int firmaId, IEnumerable<MizanExcelRow> rows, Donem donem)
        {
            await EnsureMizanInitializedAsync();

            var result = new MizanUpdateResult();
            var firma = _firms.FirstOrDefault(f => f.Id == firmaId);
            if (firma is null) return result;

            // Tek bir lookup map: hesap kodu -> tüm sections'taki MizanSatir referansları
            var lookup = new Dictionary<string, List<MizanSatir>>(StringComparer.OrdinalIgnoreCase);

            void Index(IEnumerable<MizanSatir> section)
            {
                foreach (var s in section)
                {
                    if (s.Tip != SatirTipi.Account) continue;
                    if (string.IsNullOrWhiteSpace(s.Kod)) continue;

                    if (!lookup.TryGetValue(s.Kod, out var bucket))
                    {
                        bucket = new List<MizanSatir>();
                        lookup[s.Kod] = bucket;
                    }
                    bucket.Add(s);
                }
            }

            Index(firma.Mizan.Aktif);
            Index(firma.Mizan.Pasif);
            Index(firma.Mizan.GelirTablosu);

            // Raw değer haritasını sıfırla — bu yükleme firmanın seçili dönem raw'ını değiştirir
            var raw = new Dictionary<string, decimal?>(StringComparer.OrdinalIgnoreCase);

            foreach (var row in rows)
            {
                if (string.IsNullOrWhiteSpace(row.Kod)) continue;

                // Excel'den gelen "CariDonem" alanı, dönemden bağımsız olarak yüklenen değerdir.
                // Donem parametresi bu değerin Onceki mi Cari döneme yazılacağını belirler.
                var value = row.CariDonem ?? row.OncekiDonem;
                if (!value.HasValue) continue;

                raw[row.Kod] = value;

                if (lookup.TryGetValue(row.Kod, out var targets))
                {
                    foreach (var t in targets)
                    {
                        if (donem == Donem.Cari) t.CariDonem = value;
                        else t.OncekiDonem = value;
                    }
                    result.Matched++;
                }
                else
                {
                    result.Unmatched++;
                    if (result.UnmatchedKodlar.Count < 50)
                        result.UnmatchedKodlar.Add(row.Kod);
                }
            }

            if (donem == Donem.Cari)
                _rawCariByFirm[firmaId] = raw;
            else
                _rawOncekiByFirm[firmaId] = raw;

            return result;
        }

        public async Task<IReadOnlyDictionary<string, decimal?>> GetRawMizanValuesAsync(int firmaId, Donem donem)
        {
            await EnsureMizanInitializedAsync();

            var source = donem == Donem.Cari ? _rawCariByFirm : _rawOncekiByFirm;
            if (source.TryGetValue(firmaId, out var map))
                return map;

            return new Dictionary<string, decimal?>(StringComparer.OrdinalIgnoreCase);
        }

        public Task<VergiHesaplama> GetVergiBilgisiAsync(int firmaId)
        {
            var firma = _firms.FirstOrDefault(f => f.Id == firmaId);
            return Task.FromResult(firma?.VergiBilgisiCariDonem ?? new VergiHesaplama());
        }

        public async Task ResetMizanAsync(int firmaId)
        {
            await EnsureMizanInitializedAsync();

            var firma = _firms.FirstOrDefault(f => f.Id == firmaId);
            if (firma is null || _hesapPlaniTemplate is null) return;
            firma.Mizan = _hesapPlaniTemplate.Clone();
            _rawCariByFirm.Remove(firmaId);
            _rawOncekiByFirm.Remove(firmaId);
        }

        private async Task EnsureMizanInitializedAsync()
        {
            if (_mizanInitialized) return;

            await _initLock.WaitAsync();
            try
            {
                if (_mizanInitialized) return;

                _hesapPlaniTemplate = await _hesapPlaniLoader.LoadAsync();

                foreach (var f in _firms)
                    f.Mizan = _hesapPlaniTemplate.Clone();

                _mizanInitialized = true;
            }
            finally
            {
                _initLock.Release();
            }
        }

        private static List<ControlItem> BuildTemplate()
        {
            var items = new List<ControlItem>();
            int id = 1;

            void Add(string cat, string q) =>
                items.Add(new ControlItem { Id = id++, Category = cat, Question = q });

            // 1. Dönen Varlıklar (11)
            Add(CatDonenVarliklar, "Dövizli kasa bankası çek cari hesap vb var mı, değerlemesi yapılmış mı? Kasa efektif döviz alış, diğer hesaplar döviz alış kuru ile değerle.");
            Add(CatDonenVarliklar, "Kasa sorunu var mı? (Kasanın olağandan fazla bakiye vermesi) Dönem içinde kasanın eksiye düştüğü günler var mı? Örnek kasa sayfasına bak.");
            Add(CatDonenVarliklar, "30.000 TL üzeri nakit ödeme var mı, varsa ilişkisi nedir? Cari hesap ise detayı kontrol edilmeli.");
            Add(CatDonenVarliklar, "101-103 Hesapların devri vadeye göre uygun mu? 31.12 hafta sonuna denk geliyor ise, çeklerin devri Ocak ve sonraki aylara ait olacak şekilde olmalıdır.");
            Add(CatDonenVarliklar, "Banka bakiyelerin 31.12 tarihindeki bakiyesini vermesi gerekir. POS bakiyesi ise banka ile anlaşma bakiyesine uygun olmalı. Örneğin ertesi gün tahsil anlaşması var ise hafta tatil gününe göre 30.12 ve 31.12 bakiyesi devir olmalı. 30 günlük tahsilat anlaşması var ise 1 Aralık - 31 Aralık tarihleri arası tahsilat bakiyesinin kalması gerekir.");
            Add(CatDonenVarliklar, "Verilen çekler hesabı hazır değerler grubunda eksi karakterli çalışan bir hesaptır. Hazır değerler hesap grubunu eksiye düşürmüyor ise verilen çekler bu grupta kalabilir. Eksiye düşürmesi halinde 321 Borç Senetleri Hesabına alınmalı. İhtiyari olarak her zaman 321 Hesaba alınabilir; bu durumda 101-Alınan çeklerin 121 Alacak Senetleri Hesabına alınması gerekir.");
            Add(CatDonenVarliklar, "Yeni yıla devir eden stok miktarı/stok tutarı firmanın mutlaka bilgisi olmalı. Devren stok tutarı mutabakat/bilgi dahilinde devir etmeli.");
            Add(CatDonenVarliklar, "Geçici vergi beyanında atlanmış ve geçici vergi sonrası şüpheli duruma düşmüş alacak var mı, karşılık ayrılması gereken alacak var mı?");
            Add(CatDonenVarliklar, "Dönem sonu itibari ile 120-340 / 320-159 hesap kontrolleri yapılmalı. Finansman gider kısıtlamasına tabi olmasa bile rakamların neden avans hesabında olması gerektiği sorgulanmalı ve ona göre avans hesapları alınmalı.");
            Add(CatDonenVarliklar, "Dönem sonu itibari ile 180 hesap kalmamalı, gelecek yıla ait giderler 280 hesapta takip edilmeli. Açılış fişinden sonra 280 hesaplar 180 hesaplara taşınmalı.");
            Add(CatDonenVarliklar, "Devreden KDV mutlaka Aralık beyanı ile kontrol edilmeli. Geçici vergi beyanından sonra düzeltme verilmiş olabilir. Devir var ise 190 hesap ile son KDV beyanı check edilmeli.");

            // 2. Duran Varlık (3)
            Add(CatDuranVarlik, "Kredi ile alınmış varlık var mı, ilk yıl faiz ve kur farkları maliyet yazılmış mı, kontrol edilmeli. Araç, bina, arsa, makine alımlarında ilk yıl (31.12'ye kadar olan süre) oluşan bu farklar varlıkların maliyetine yazılır.");
            Add(CatDuranVarlik, "25'li grup ile demirbaş tablosu toplamı 257+268 hesap toplamı demirbaş tablosu eşit mi kontrol edilmeli. Şirket dönem içinde bir adres değişikliği yaptıysa 264-Özel maliyet hesabının bakiyesi ve ona ilişkin 268-Amortisman hesapları kontrol edilerek direkt olarak gider yazılmalıdır.");
            Add(CatDuranVarlik, "Varlık satışı nedeni ile 549-Özel Fonların durumu kontrol edilmeli. Dönem içinde fona konu varlık varsa amortisman mahsubu ve üç yıl hesabı kontrol edilmelidir.");

            // 3. Kısa V.Y (4)
            Add(CatKisaVY, "Örnek ortaklar cari hesabına bakılabilir. Ortak hesabı şirkete borçlu ise kar payı ve adat faizi hesaplanma durumu kontrol edilmelidir.");
            Add(CatKisaVY, "Aralık ayına ait KDV, Muhtasar, GEKAP beyanları 360 hesaplar ile uyumlu mu, ödenmeyen vergiler 368 hesaba alınmış mı kontrol edilmeli.");
            Add(CatKisaVY, "361 bakiyesi Aralık bildirgesi ile uyumlu mu kontrol edilmeli.");
            Add(CatKisaVY, "Cari hesap, kredi vb hesapların kur değerlemesi yapılmış mı, varsa finansman gider hesaplaması kapsamında değerlendirilmiş mi?");

            // 4. Bilanço İşlemi (6)
            Add(CatBilancoIslemi, "Geçici vergi beyanından sonra gelecek yıl için dava açılacak firma var ise kurumları yaparken gelecek yıla doğru devriden olunmalı. Örneğin x firmasının 2025 yılından 2026 yılına devir eden bakiyesi olası alacak davası için uygun mu kontrol edilmelidir.");
            Add(CatBilancoIslemi, "Şirket bir grup firması ise grup firmaları cari hesapları mutabakatı yapılmalı.");
            Add(CatBilancoIslemi, "Genel olarak cari hesap kontrolleri yapılmış mı, özellikle PKF Muhasebe ile mutabakat yapılmalı.");
            Add(CatBilancoIslemi, "Genel mizan kontrolleri neticesinde şirketin sermaye artırımına ihtiyaç var mı?");
            Add(CatBilancoIslemi, "İlişkili kişiler ile transfer fiyatlandırmasına konu işlem var mı, ilişkili kişilerin kim olduğu Transfer Fiyatlandırması sayfasında mevcuttur.");
            Add(CatBilancoIslemi, "Şirket bu yıl mı açılmış, sermaye taahhüt kayıtları var mı, sermaye hesapları doğru mu, ortakları doğru açılmış mı?");

            // 5. Gelir Tablosu (3)
            Add(CatGelirTablosu, "Faiz ve fon gelirlerine ilişkin banka yazıları temin edilmeli, kayıtlar ile uyumu 642-645-193 hesaplar ile kontrol edilmeli.");
            Add(CatGelirTablosu, "12 Aylık KDV beyanları toplamı gelir tablosu brüt satışlarına eşit mi, eşit değil ise farklar açıklanabilir mi?");
            Add(CatGelirTablosu, "Kredi faizleri ve banka mevduat faizleri: Taksitli kredilerde faiz giderleri, ödeme tarihine göre ilgili döneme tahakkuk ettirilmelidir. Örneğin ödeme tarihi 10 Ocak ise, 31.12 kapanışı nedeniyle faiz tutarının 20 günlük kısmı içinde bulunulan döneme aittir. Bu durumda 20 günlük faiz için 780 hesaba borç, 381 hesaba alacak kaydı yapılır. Faiz tutarı 50.000 TL ise hesaplama: 50.000 / 30 x 20 = 33.333,33 TL. Aynı uygulama faiz gelirleri için de geçerlidir.");

            // 6. Beyanname Sonuç (16)
            Add(CatBeyannameSonuc, "Ödenmeyen SGK prim var mı, gider hesaplarından çıkarılıp KKEG yapılmış mı, daha önce KKEG yapılıp bu döneme indirim konusu yapılacak SGK ödemesi var mı?");
            Add(CatBeyannameSonuc, "Ödenen/ödenmeyen geçici vergilerin durumu kontrol edilmeli. Ödenmeyen geçici vergiler kurumlar beyanında indirim konusu yapılmamalı.");
            Add(CatBeyannameSonuc, "Kurumlar vergisi iadesi mi çıkıyor, iade çıkıyor ise GEKSİS raporuna uygun kontroller yapılmalı (Brüt satış kontrolleri, ortaklar adat kontrolleri).");
            Add(CatBeyannameSonuc, "Verilen geçici vergi beyanı sonrası kurumlar vergisi ödemesi çıkıyor mu, %10'luk matrah artışı söz konusu mu?");
            Add(CatBeyannameSonuc, "Kar zarar değişiyor mu, değişimin nedeni kontrol edilmeli.");
            Add(CatBeyannameSonuc, "4. Dönem geçiciye göre KKEG değişiyor mu, değişiyor ise nedeni kontrol edilmeli.");
            Add(CatBeyannameSonuc, "Dönem net karı veya zararı gelir tablosunda ve bilançoda eşit mi? Kar çıkmış ise ödenen vergi gelir tablosundan kardan düşülmüş mü? (K-DÖNEM KARI VERGİ VE DİĞER YASAL YÜKÜMLÜLÜK KARŞILIKLARI (-))");
            Add(CatBeyannameSonuc, "370 hesap ile ödenen/ödenecek vergi eşit mi, peşin ödenen vergiler (193 HESAP - 371 eşitlenmiş mi?), 370-371 farkı son dönem geçici kadar mı?");
            Add(CatBeyannameSonuc, "Muhasebe uygulama tebliği uyarınca firmanın net satışları ve aktif toplamı ek mali tablo doldurulmasını zorunlu kılıyor mu? (AKTİF TOP: 240.560.700 TL / NET SATIŞ: 534.574.300 TL)");
            Add(CatBeyannameSonuc, "7326 / 6111 / 7440 SK matrah artırım kayıtları kontrolü.");
            Add(CatBeyannameSonuc, "7326 / 6111 / 7440 SK işleminden kaynaklanan 689 hesapta KKEG var mı?");
            Add(CatBeyannameSonuc, "İstisna işlemi var mı, istisna işlemi kaynaklı zarar var mı, indirimli kurumlar uygulanmış mı? Arsa, daire satışı kaynaklı fon kaydı yapılmış mı, gerekli kurallara uyulmuş mu, yeminli raporuna ihtiyaç var mı?");
            Add(CatBeyannameSonuc, "Bilanço ve Gelir Tablosu dipnotları doldurulmuş mu?");
            Add(CatBeyannameSonuc, "Yabancı para pozisyonu dolduruldu mu?");
            Add(CatBeyannameSonuc, "Örtülü sermaye kontrolü yapıldı mı, ortaklara yürütülen adat var mı, beyannamenin arka sayfasına yazılmış mı? Transfer fiyatlandırmasına konu işlem beyannameye yazılmış mı?");
            Add(CatBeyannameSonuc, "Beyannameye geriye dönük 5 yıllık geçmiş yıl zararı yazıldı mı? 2025 kurumlar beyanında 24-23-22-21-20 yılları zararı mahsup edilebilir. Matrah artırımı dolayısıyla ilgili yılların zararının tamamı değil %50'si zarar olarak yazılır. Firmanın YMM'si var ise ilgili beyanname bölümünde bu bilgi yazılmış mı? Beyannameye aktarılan önceki yıl bilanço ve gelir tablosu aktarımı doğru mu?");

            // 7. Ticaret Sicil (3)
            Add(CatTicaretSicil, "Şirkette hisse devri, sermaye artırımı yapılmış mı, yapılan bu işlemler şirket kayıtlarına yansımış mı, kayıtlar ve gerçek faydalanıcı bildirimi güncellenmeli, kontrol edilmeli.");
            Add(CatTicaretSicil, "Şirketin adres değişikliği var mı, var ise kurumlar vergisi beyanı hangi vergi dairesine veriliyor kontrol edilmeli. Şube veya depo açılışı var mı, beyana yazılmış mı?");
            Add(CatTicaretSicil, "Nakit sermaye artışı yapılmış mı, ne tutarda yapılmış, nakit sermaye artış indirim hakkı var mı, gerekli dilekçe ve banka dekontları hazırlanıp vergi dairesine sunulmuş mu? Sermaye indirimi ilgili tablo hazırlanmış mı?");

            return items;
        }
    }
}
