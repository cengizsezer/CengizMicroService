using CatalogService.Api.Features.MevzuatGrafigi.Domain;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CatalogService.Api.Features.MevzuatGrafigi
{
    /// <summary>
    /// Mevzuat Grafiği örnek içeriği: kök dizindeki mevzuat-grafigi.html mockup'ının N, E, X
    /// ve views verisi. <c>catalog.MevzuatKartlar</c> BOŞSA hepsi yazılır (tek seferlik). Tek
    /// istisna "yarım seed" izidir (kartlar var, seed tuvalleri ve seed bağlantıları hiç yok):
    /// o zaman yalnız eksik bağlantı/dışlama/tuvaller tamamlanır. Kullanıcının sildiği ya da
    /// değiştirdiği seed kayıtları hiçbir durumda geri gelmez.
    ///
    /// Firma kartları örnektir ("Örnek …"), gerçek Firma kaydına bağlı değildir. Kart içerikleri
    /// örnek amaçlıdır; mevzuat güncelliği doğrulanmalı.
    ///
    /// Yazmadan önce bütün veri <see cref="BaglantiKurali"/> ve <see cref="DislamaKurali"/>'na
    /// karşı denetlenir; tek bir ihlal varsa hiçbir şey yazılmaz, seed ihlalleri listeleyerek durur.
    /// </summary>
    public static class MevzuatGrafigiSeed
    {
        // ---- Mockup verisi (anahtarlar mockup'taki id'ler; yalnız seed içinde kullanılır) ----

        public static readonly IReadOnlyList<(string Anahtar, MevzuatKartTuru Tur, string Baslik, string Madde, string Ozet)> Kartlar = new[]
        {
            ("f1", MevzuatKartTuru.Firma, "Örnek İhracat A.Ş.", "İhracatçı · İthalatçı", "Mal ve hizmet ihracatı yapıyor, üretim girdilerini DİİB ile ithal ediyor. (Örnek firma)"),
            ("f3", MevzuatKartTuru.Firma, "Örnek Tekstil San. Ltd.", "İmalatçı", "Ürettiği malı ihracatçı firmaya ihraç kaydıyla teslim ediyor. (Örnek firma)"),
            ("f2", MevzuatKartTuru.Firma, "Örnek Yazılım Ltd.", "Teknokent firması", "Teknokentte yazılım geliştiriyor, yurt dışı müşterilere de hizmet veriyor. (Örnek firma)"),
            ("i1", MevzuatKartTuru.Islem, "Mal İhracatı", "KDVK 12/1", "Malın yurt dışındaki alıcıya gönderilmesi ve gümrük bölgesinden çıkması."),
            ("i2", MevzuatKartTuru.Islem, "Hizmet İhracatı", "KDVK 12/2", "Hizmetin yurt dışındaki müşteri adına yapılması ve yurt dışında yararlanılması."),
            ("i3", MevzuatKartTuru.Islem, "İhraç Kayıtlı Teslim", "Tedarikçi tarafı", "İmalatçının, malı ihraç edilmek üzere ihracatçıya teslim etmesi."),
            ("i4", MevzuatKartTuru.Islem, "DİİB'li Girdi İthalatı", "Dahilde işleme", "İhraç edilecek ürünün girdilerinin izin belgesi kapsamında ithali."),
            ("i5", MevzuatKartTuru.Islem, "Teknokentte Yazılım / Ar-Ge", "Onaylı proje", "Bölgede yürütülen yazılım ve Ar-Ge projeleri."),
            ("s1", MevzuatKartTuru.Istisna, "KDV İhracat İstisnası", "KDVK 11/1-a", "İhracat teslimleri ve bunlara ilişkin hizmetler KDV'den istisna; yüklenilen KDV iade konusu olabilir."),
            ("s2", MevzuatKartTuru.Istisna, "İhraç Kaydıyla Teslim", "KDVK 11/1-c", "İmalatçı KDV'yi hesaplar ama tahsil etmez; ihracatçı adına tecil edilir, süresinde ihraç edilirse terkin edilir."),
            ("s5", MevzuatKartTuru.Istisna, "DİİB Vergi Teminatı", "DİR Kararı", "Belge kapsamındaki ithalatta gümrük vergileri ve KDV teminata bağlanır; taahhüt kapatılınca çözülür."),
            ("d1", MevzuatKartTuru.Indirim, "Hizmet İhracatı Kazanç İndirimi", "KVK 10/1-ğ", "Yurt dışına verilen belirli hizmetlerden doğan kazancın %50'si beyannamede indirilir."),
            ("s3", MevzuatKartTuru.Istisna, "Teknokent Kazanç İstisnası", "4691 Geç. 2", "Bölgedeki yazılım ve Ar-Ge faaliyetlerinden elde edilen kazançlar gelir/kurumlar vergisinden istisna."),
            ("s4", MevzuatKartTuru.Istisna, "Teknokent Yazılım KDV İstisnası", "KDVK Geç. 20", "Bölgede üretilen belirli yazılım teslim ve hizmetleri KDV'den istisna."),
            ("d2", MevzuatKartTuru.Indirim, "Ar-Ge İndirimi", "5746 md.3", "Ar-Ge ve tasarım harcamaları kurum kazancının tespitinde indirim olarak dikkate alınır."),
            ("k1", MevzuatKartTuru.Kanun, "KDV Kanunu md.11", "3065 · md.11", "İhracat, ihraç kaydıyla teslim ve diğer ihracat istisnaları."),
            ("k2", MevzuatKartTuru.Kanun, "KDV Kanunu md.12", "3065 · md.12", "İhracat teslimi ve hizmet ihracatının tanımı ve şartları."),
            ("k5", MevzuatKartTuru.Kanun, "Kurumlar Vergisi K. md.10", "5520 · md.10", "Kurumlar vergisi beyannamesinde gösterilecek diğer indirimler."),
            ("k7", MevzuatKartTuru.Kanun, "Dahilde İşleme Rejimi Kararı", "2005/8391", "İzin belgesi, ihracat taahhüdü ve belge kapatma usulleri."),
            ("k4", MevzuatKartTuru.Kanun, "4691 sayılı TGB Kanunu Geç. 2", "4691 · Geç.2", "Bölge kazanç istisnası, personel ücret istisnası ve süreleri."),
            ("k8", MevzuatKartTuru.Kanun, "KDV Kanunu Geç. md.20", "3065 · Geç.20", "Teknoloji geliştirme bölgesi yazılım ve Ar-Ge KDV istisnaları."),
            ("k6", MevzuatKartTuru.Kanun, "5746 sayılı Ar-Ge Kanunu md.3", "5746 · md.3", "Ar-Ge indirimi ve diğer Ar-Ge destekleri."),
            ("y1", MevzuatKartTuru.Sart, "Kapanmış Gümrük Beyannamesi", "Tevsik belgesi", "Mal ihracatının tevsiki ve iade dosyasındaki temel belge."),
            ("y2", MevzuatKartTuru.Sart, "3 Ay İçinde İhraç", "Süre şartı", "Teslimi izleyen aybaşından itibaren 3 ay içinde ihraç edilmezse tecil edilen vergi gecikme zammıyla aranır."),
            ("y3", MevzuatKartTuru.Sart, "KDV İade Talebi", "İade dosyası", "Standart iade talep dilekçesi ve listeler; tutara göre YMM raporu, inceleme raporu veya teminat."),
            ("y4", MevzuatKartTuru.Sart, "Bedelin Türkiye'ye Getirilmesi", "İndirim şartı", "İndirimden yararlanmak için hizmet bedelinin Türkiye'ye getirilmiş olması gerekir."),
            ("y5", MevzuatKartTuru.Sart, "DİİB Taahhüt Kapatma", "Süre şartı", "Belge süresi içinde ihracat taahhüdü yerine getirilip belge kapatılmalı; aksi halde teminatlar tahsil edilir."),
            ("y6", MevzuatKartTuru.Sart, "Ayrı Kayıt ve Proje Onayı", "Uygulama şartı", "Bölge faaliyetleri için ayrı kayıt düzeni ve onaylı proje gerekir."),
        };

        public static readonly IReadOnlyList<(string Kaynak, string Hedef)> Baglantilar = new[]
        {
            ("f1", "i1"), ("f1", "i2"), ("f1", "i4"), ("f3", "i3"), ("f2", "i5"), ("f2", "i2"),
            ("i1", "s1"), ("i2", "s1"), ("i2", "d1"), ("i3", "s2"), ("i4", "s5"), ("i5", "s3"), ("i5", "s4"),
            ("s1", "k1"), ("s1", "k2"), ("s1", "y1"), ("s1", "y3"), ("s2", "k1"), ("s2", "y2"), ("s2", "y3"),
            ("s5", "k7"), ("s5", "y5"), ("d1", "k5"), ("d1", "y4"), ("s3", "k4"), ("s3", "y6"), ("s4", "k8"), ("d2", "k6"),
        };

        public static readonly IReadOnlyList<(string A, string B, string Not)> Dislamalar = new[]
        {
            ("s3", "d1", "Aynı kazanç için iki kez yararlanılmaz"),
            ("s3", "d2", "Aynı harcama için mükerrer yararlanılmaz"),
        };

        /// <summary>Tuvaller; kart sırası mockup'takiyle aynı (yerleşim bu sıraya göre hesaplanır).</summary>
        public static readonly IReadOnlyList<(string Ad, string[] Kartlar)> Tuvaller = new[]
        {
            ("İhracat & İthalat", new[] { "f1", "f3", "i1", "i2", "i3", "i4", "s1", "s2", "s5", "d1", "k1", "k2", "k5", "k7", "y1", "y2", "y3", "y4", "y5" }),
            ("Teknokent & Ar-Ge", new[] { "f2", "i5", "i2", "s3", "s4", "s1", "d1", "d2", "k4", "k8", "k1", "k5", "k6", "y6", "y4", "y3" }),
        };

        // ---- Yerleşim: mockup'taki layout() fonksiyonunun birebir karşılığı ----

        private static int Sutun(MevzuatKartTuru tur) => tur switch
        {
            MevzuatKartTuru.Firma   => 0,
            MevzuatKartTuru.Islem   => 1,
            MevzuatKartTuru.Istisna => 2,
            MevzuatKartTuru.Indirim => 2,
            MevzuatKartTuru.Kanun   => 3,
            _                       => 4, // Sart
        };

        /// <summary>
        /// Kartlar türlerine göre sütunlara ayrılır (tuvaldeki sırayla); en kalabalık sütuna göre
        /// kısa sütunlar dikeyde ortalanır: x = 40 + sütun·290, y = 40 + (max − adet)·150/2 + sıra·150.
        /// </summary>
        public static IReadOnlyDictionary<string, (double X, double Y)> Yerlesim(IEnumerable<string> kartlar)
        {
            var turu = Kartlar.ToDictionary(k => k.Anahtar, k => k.Tur);
            var sutunlar = kartlar
                .Select(k => (Anahtar: k, Sutun: Sutun(turu[k])))
                .GroupBy(x => x.Sutun)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Anahtar).ToList());
            if (sutunlar.Count == 0) return new Dictionary<string, (double, double)>();

            var max = sutunlar.Values.Max(l => l.Count);
            var sonuc = new Dictionary<string, (double X, double Y)>();
            foreach (var (sutun, liste) in sutunlar)
            {
                var off = (max - liste.Count) * 150 / 2.0;
                for (var i = 0; i < liste.Count; i++)
                    sonuc[liste[i]] = (40 + sutun * 290, 40 + off + i * 150);
            }
            return sonuc;
        }

        // ---- Denetim ----

        /// <summary>Seed verisinin servis kurallarına uyup uymadığı; boş liste = uygun.</summary>
        public static List<string> Denetle() => Denetle(Kartlar, Baglantilar, Dislamalar, Tuvaller);

        /// <summary>Verilen verinin denetimi (testte bozuk veriyle de çağrılır).</summary>
        public static List<string> Denetle(
            IEnumerable<(string Anahtar, MevzuatKartTuru Tur, string Baslik, string Madde, string Ozet)> kartlarListesi,
            IEnumerable<(string Kaynak, string Hedef)> baglantilarListesi,
            IEnumerable<(string A, string B, string Not)> dislamalarListesi,
            IEnumerable<(string Ad, string[] Kartlar)> tuvallerListesi)
        {
            var ihlaller = new List<string>();
            var turu = new Dictionary<string, MevzuatKartTuru>();
            foreach (var k in kartlarListesi)
                if (!turu.TryAdd(k.Anahtar, k.Tur)) ihlaller.Add($"Kart anahtarı tekrar ediyor: {k.Anahtar}");

            bool Var(string a)
            {
                if (turu.ContainsKey(a)) return true;
                ihlaller.Add($"Tanımsız kart: {a}");
                return false;
            }

            var gorulen = new HashSet<(string, string)>();
            foreach (var (a, b) in baglantilarListesi)
            {
                if (!Var(a) | !Var(b)) continue;
                if (a == b) ihlaller.Add($"Kart kendine bağlanamaz: {a}");
                else if (!BaglantiKurali.IzinliMi(turu[a], turu[b]))
                    ihlaller.Add($"{a} → {b}: {BaglantiKurali.RetMesaji(turu[a], turu[b])}");
                if (!gorulen.Add((a, b))) ihlaller.Add($"Tekrar eden bağlantı: {a} → {b}");
            }

            var dislanan = new HashSet<(string, string)>();
            foreach (var (a, b, _) in dislamalarListesi)
            {
                if (!Var(a) | !Var(b)) continue;
                if (a == b) ihlaller.Add($"Kart kendini dışlayamaz: {a}");
                else if (!DislamaKurali.TesvikMi(turu[a]) || !DislamaKurali.TesvikMi(turu[b]))
                    ihlaller.Add($"{a} - {b}: {DislamaKurali.RetMesaji(turu[a], turu[b])}");
                var cift = string.CompareOrdinal(a, b) < 0 ? (a, b) : (b, a);
                if (!dislanan.Add(cift)) ihlaller.Add($"Tekrar eden dışlama: {a} - {b}");
            }

            foreach (var (ad, kartlar) in tuvallerListesi)
            {
                foreach (var k in kartlar) Var(k);
                foreach (var k in kartlar.GroupBy(x => x).Where(g => g.Count() > 1))
                    ihlaller.Add($"\"{ad}\" tuvalinde kart tekrar ediyor: {k.Key}");
            }
            return ihlaller;
        }

        // ---- Seed ----

        /// <summary>
        /// Seed'in ne yaptığı. <see cref="MevzuatSeedIslemi.Tamamlama"/>: kartları yazılmış ama
        /// bağlantı/dışlama/tuvalleri yazılamamış eski bir seed'in eksikleri eklendi.
        /// </summary>
        public enum MevzuatSeedIslemi { Atlandi, Tam, Tamamlama }

        public sealed record MevzuatSeedSonucu(
            MevzuatSeedIslemi Islem, int Kart = 0, int Baglanti = 0, int Dislama = 0, int Tuval = 0,
            int EksikKart = 0)
        {
            public static readonly MevzuatSeedSonucu Atlandi = new(MevzuatSeedIslemi.Atlandi);
        }

        /// <summary>
        /// Kart tablosu boşsa bütün örnek içerik yazılır. Doluysa yalnız tam olarak "yarım seed"
        /// izi varsa (bkz. <see cref="YarimSeedKartlariAsync"/>) eksik bağlantı, dışlama ve
        /// tuvaller eklenir; kart asla eklenmez. Diğer her durumda hiçbir şeye dokunulmaz.
        /// </summary>
        public static async Task<MevzuatSeedSonucu> SeedAsync(CatalogContext db, CancellationToken ct = default)
        {
            var ihlaller = Denetle();
            if (ihlaller.Count > 0)
                throw new InvalidOperationException(
                    "Mevzuat Grafiği seed'i durduruldu, hiçbir kayıt yazılmadı. Kurala uymayan kayıtlar: " +
                    string.Join(" | ", ihlaller));

            if (!await db.MevzuatKartlar.AnyAsync(ct))
                return await TekBirimdeYazAsync(db, () => TamYazAsync(db, ct), ct);

            var kartIdleri = await YarimSeedKartlariAsync(db, ct);
            if (kartIdleri is null) return MevzuatSeedSonucu.Atlandi;
            return await TekBirimdeYazAsync(db, () => TamamlaAsync(db, kartIdleri, ct), ct);
        }

        /// <summary>
        /// "Yarım seed" izi: seed kartlarından en az biri (tür + başlıkla) duruyor, seed'in iki
        /// tuvalinin İKİSİ DE adıyla yok ve seed bağlantılarının HİÇBİRİ yok. 2026-10-10'da seed
        /// retry stratejisine takılıp yalnız kartlarını bırakmıştı; bu iz o durumu tanır.
        /// Tamamlanmış ya da kullanıcının üzerinde çalıştığı bir grafikte koşul sağlanmaz:
        /// tamamlama tuvalleri ve bağlantıları yazdığı için bir daha tetiklenmez, kullanıcının
        /// sildiği seed kayıtları da geri gelmez.
        /// </summary>
        /// <returns>İz varsa bulunan seed kartlarının anahtar → id eşlemesi; yoksa null.</returns>
        public static async Task<Dictionary<string, int>?> YarimSeedKartlariAsync(CatalogContext db, CancellationToken ct = default)
        {
            var tuvalAdlari = Tuvaller.Select(t => t.Ad).ToList();
            if (await db.MevzuatTuvaller.AnyAsync(t => tuvalAdlari.Contains(t.Ad), ct)) return null;

            // Aynı tür + başlıkta birden çok kart varsa en eskisi (seed önce yazar).
            var mevcut = await db.MevzuatKartlar.AsNoTracking()
                .Select(k => new { k.Id, k.Tur, k.Baslik })
                .ToListAsync(ct);
            var kartIdleri = new Dictionary<string, int>();
            foreach (var k in Kartlar)
            {
                var bulunan = mevcut.Where(m => m.Tur == k.Tur && m.Baslik == k.Baslik).OrderBy(m => m.Id).FirstOrDefault();
                if (bulunan is not null) kartIdleri[k.Anahtar] = bulunan.Id;
            }
            if (kartIdleri.Count == 0) return null;

            var mevcutBaglantilar = (await db.MevzuatBaglantilar.AsNoTracking()
                    .Select(b => new { b.KaynakKartId, b.HedefKartId })
                    .ToListAsync(ct))
                .Select(b => (b.KaynakKartId, b.HedefKartId))
                .ToHashSet();
            var seedBaglantisiVar = Baglantilar.Any(b =>
                kartIdleri.TryGetValue(b.Kaynak, out var a) && kartIdleri.TryGetValue(b.Hedef, out var h) &&
                mevcutBaglantilar.Contains((a, h)));
            return seedBaglantisiVar ? null : kartIdleri;
        }

        /// <summary>
        /// Birden çok SaveChanges'i tek transaction'da yapar. CatalogContext'te retry stratejisi
        /// açık; elle açılan transaction ancak stratejinin içinde açılabilir (yoksa ilk SaveChanges
        /// "does not support user-initiated transactions" ile düşer). Strateji yeniden denerse
        /// önceki denemenin izlenen varlıkları atılır.
        /// </summary>
        private static Task<MevzuatSeedSonucu> TekBirimdeYazAsync(CatalogContext db, Func<Task<MevzuatSeedSonucu>> yaz, CancellationToken ct)
        {
            var deneme = 0;
            return db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                if (++deneme > 1) db.ChangeTracker.Clear();
                IDbContextTransaction? tx = db.Database.IsRelational()
                    ? await db.Database.BeginTransactionAsync(ct)
                    : null;
                await using (tx)
                {
                    var sonuc = await yaz();
                    if (tx is not null) await tx.CommitAsync(ct);
                    return sonuc;
                }
            });
        }

        private static async Task<MevzuatSeedSonucu> TamYazAsync(CatalogContext db, CancellationToken ct)
        {
            var simdi = DateTime.Now;
            var kartlar = Kartlar.ToDictionary(k => k.Anahtar, k => new MevzuatKart
            {
                Tur = k.Tur, Baslik = k.Baslik, Madde = k.Madde, Ozet = k.Ozet, OlusturmaTarihi = simdi,
            });
            db.MevzuatKartlar.AddRange(kartlar.Values);
            await db.SaveChangesAsync(ct); // kart id'leri bağlantılardan önce lazım

            var kalan = await BagliKayitlariEkleAsync(db, kartlar.ToDictionary(k => k.Key, k => k.Value.Id), ilkSira: 1, ct);
            return kalan with { Islem = MevzuatSeedIslemi.Tam, Kart = kartlar.Count };
        }

        private static async Task<MevzuatSeedSonucu> TamamlaAsync(CatalogContext db, Dictionary<string, int> kartIdleri, CancellationToken ct)
        {
            // Kullanıcının tuvallerinin arkasına dizilir.
            var ilkSira = ((await db.MevzuatTuvaller.MaxAsync(t => (int?)t.Sira, ct)) ?? 0) + 1;
            var sonuc = await BagliKayitlariEkleAsync(db, kartIdleri, ilkSira, ct);
            return sonuc with { Islem = MevzuatSeedIslemi.Tamamlama, EksikKart = Kartlar.Count - kartIdleri.Count };
        }

        /// <summary>
        /// Bağlantı, dışlama ve tuvaller. Yalnız iki ucu da <paramref name="kartIdleri"/>'nde olan
        /// kayıtlar yazılır: kullanıcının sildiği seed kartının bağlantısı/yerleşimi geri gelmez.
        /// Konumlar her zaman tam tuval listesine göre hesaplanır (eksik kart yer kaydırmaz).
        /// </summary>
        private static async Task<MevzuatSeedSonucu> BagliKayitlariEkleAsync(
            CatalogContext db, IReadOnlyDictionary<string, int> kartIdleri, int ilkSira, CancellationToken ct)
        {
            bool Var(string k) => kartIdleri.ContainsKey(k);

            var baglantilar = Baglantilar.Where(b => Var(b.Kaynak) && Var(b.Hedef))
                .Select(b => new MevzuatBaglanti { KaynakKartId = kartIdleri[b.Kaynak], HedefKartId = kartIdleri[b.Hedef] })
                .ToList();
            db.MevzuatBaglantilar.AddRange(baglantilar);

            // Dışlama yönsüz, A < B sıralı saklanır (servisle aynı); zaten olan çift atlanır.
            var mevcutDislamalar = (await db.MevzuatDislamalar.AsNoTracking()
                    .Select(d => new { d.AKartId, d.BKartId }).ToListAsync(ct))
                .Select(d => (d.AKartId, d.BKartId)).ToHashSet();
            var dislamalar = Dislamalar.Where(d => Var(d.A) && Var(d.B))
                .Select(d => new MevzuatDislama
                {
                    AKartId = Math.Min(kartIdleri[d.A], kartIdleri[d.B]),
                    BKartId = Math.Max(kartIdleri[d.A], kartIdleri[d.B]),
                    Not = d.Not,
                })
                .Where(d => !mevcutDislamalar.Contains((d.AKartId, d.BKartId)))
                .ToList();
            db.MevzuatDislamalar.AddRange(dislamalar);

            var sira = ilkSira;
            foreach (var (ad, anahtarlar) in Tuvaller)
            {
                var yer = Yerlesim(anahtarlar);
                db.MevzuatTuvaller.Add(new MevzuatTuval
                {
                    Ad = ad,
                    Sira = sira++,
                    Kartlar = anahtarlar.Where(Var)
                        .Select(k => new MevzuatTuvalKarti { KartId = kartIdleri[k], X = yer[k].X, Y = yer[k].Y })
                        .ToList(),
                });
            }

            await db.SaveChangesAsync(ct);
            return new MevzuatSeedSonucu(MevzuatSeedIslemi.Tam, Kart: 0, Baglanti: baglantilar.Count,
                                         Dislama: dislamalar.Count, Tuval: Tuvaller.Count);
        }

        public static async Task SeedVeLoglaAsync(CatalogContext db, ILogger logger, CancellationToken ct = default)
        {
            var s = await SeedAsync(db, ct);
            switch (s.Islem)
            {
                case MevzuatSeedIslemi.Tam:
                    logger.LogInformation(
                        "Mevzuat Grafiği: örnek içerik eklendi ({Kart} kart, {Baglanti} bağlantı, {Dislama} dışlama, {Tuval} tuval).",
                        s.Kart, s.Baglanti, s.Dislama, s.Tuval);
                    break;
                case MevzuatSeedIslemi.Tamamlama:
                    logger.LogWarning(
                        "Mevzuat Grafiği: yarım kalmış örnek içerik tamamlandı ({Baglanti} bağlantı, {Dislama} dışlama, " +
                        "{Tuval} tuval eklendi; kart eklenmedi, bulunamayan {EksikKart} seed kartı atlandı).",
                        s.Baglanti, s.Dislama, s.Tuval, s.EksikKart);
                    break;
                default:
                    logger.LogInformation("Mevzuat Grafiği: kart tablosu dolu ve yarım seed izi yok, örnek içerik atlandı.");
                    break;
            }
        }
    }
}
