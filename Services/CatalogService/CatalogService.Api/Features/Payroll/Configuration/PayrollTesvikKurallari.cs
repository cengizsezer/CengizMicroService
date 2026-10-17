namespace CatalogService.Api.Features.Payroll.Configuration
{
    /// <summary>Teşvikin hesap biçimi.</summary>
    public enum TesvikHesapTuru
    {
        Yok = 0,
        /// <summary>Taban × kapsanan prim oranları × <see cref="TesvikKurali.Oran"/>.</summary>
        PrimIndirimi = 1,
        /// <summary>5510/81-ı: SGK matrahı × yıl parametresindeki puan (imalat / diğer).</summary>
        Puan5510 = 2,
        /// <summary>Günlük tutar × prim günü; SGK primine dokunmaz, ayrı satırda düşer.</summary>
        GunlukUcretDestegi = 3,
        /// <summary>Elle girilen aylık tutar; SGK primine dokunmaz, ayrı satırda düşer.</summary>
        AylikTutarDestegi = 4,
        /// <summary>Elle oran ya da aylık tutar; işveren SGK'sından düşer.</summary>
        Manuel = 5
    }

    public enum TesvikTabani
    {
        /// <summary>min(brüt, asgari ücret × tavan katsayısı) — prime esas kazanç.</summary>
        SgkMatrahi = 0,
        /// <summary>min(brüt, yılın asgari ücreti).</summary>
        AsgariUcret = 1
    }

    [Flags]
    public enum TesvikKalemleri
    {
        Yok = 0,
        IsverenMyo = 1,
        IsverenGss = 2,
        IsverenKvsk = 4,
        IsverenSgk = IsverenMyo | IsverenGss | IsverenKvsk,
        IsverenIsp = 8,
        /// <summary>Sigortalı SGK hissesi (çalışanın kesintisi) — net ücret değişir.</summary>
        SigortaliSgk = 16,
        /// <summary>İşverenin KVSK hissesinin yalnız 1 puanı (4447/50: 15921).</summary>
        IsverenKvskBirPuan = 32,
        /// <summary>Sigortalı işsizlik sigortası hissesi — net ücret değişir.</summary>
        SigortaliIsp = 64,
        Sigortali = SigortaliSgk | SigortaliIsp
    }

    public enum GvTesvikTuru
    {
        Yok = 0,
        /// <summary>AÜ istisnasından sonra kalan GV'nin tamamı.</summary>
        Tam = 1,
        /// <summary>Kalan GV'nin eğitim durumuna göre %80/%90/%95'i.</summary>
        EgitimeGore = 2
    }

    /// <summary>Alt seçim (bölge, dönem): kuralın SGK kodunu, kalemlerini ya da oranını ezer.</summary>
    public sealed record TesvikVaryanti(
        int Deger,
        string Ad,
        string? SgkKodu = null,
        TesvikKalemleri? Kalemler = null,
        decimal? Oran = null);

    public sealed record TesvikKurali
    {
        /// <summary>İç anahtar — istekteki LawCode. SGK belge kodundan ayrı.</summary>
        public required string Anahtar { get; init; }
        public string? SgkKodu { get; init; }
        public required string Ad { get; init; }
        public int Sira { get; init; }

        public TesvikHesapTuru Hesap { get; init; }
        public TesvikTabani Taban { get; init; }
        public TesvikKalemleri Kalemler { get; init; }
        public decimal Oran { get; init; } = 1m;

        public GvTesvikTuru Gv { get; init; }
        public bool DvMuaf { get; init; }

        /// <summary>Emekli (SGDP) çalışanda uygulanmaz, uyarı verilir (2026+).</summary>
        public bool EmekliyeUygulanmaz { get; init; }
        /// <summary>Engel Durumu en az 1. derece olmalı (2026+).</summary>
        public bool EngelZorunlu { get; init; }

        /// <summary>Teşvik kolonunun başlığında görünen kaynak.</summary>
        public string? Kaynak { get; init; }

        public string? AltSecimBasligi { get; init; }
        public IReadOnlyList<TesvikVaryanti> Varyantlar { get; init; } = Array.Empty<TesvikVaryanti>();

        // Günlük ücret desteği (Turizm)
        public decimal GunlukDestekTutari { get; init; }
        public (int Yil, int Ay)? DestekBaslangic { get; init; }
        public (int Yil, int Ay)? DestekBitis { get; init; }
        public string? OnayMetni { get; init; }

        /// <summary>null: her yıl. Dolu: o yıldan itibaren listede.</summary>
        public int? IlkYil { get; init; }
        public int? SonYil { get; init; }

        public bool AyriSatirDestek =>
            Hesap is TesvikHesapTuru.GunlukUcretDestegi or TesvikHesapTuru.AylikTutarDestegi;

        public bool YilIcinGecerli(int yil) =>
            (IlkYil is null || yil >= IlkYil) && (SonYil is null || yil <= SonYil);

        /// <summary>Açılır kutu etiketi: "kod – ad"; SGK kodu yoksa yalnız ad.</summary>
        public string Etiket => SgkKodu is null ? Ad : $"{SgkKodu} – {Ad}";

        /// <summary>Alt seçimi uygular. Seçim yoksa ya da tanımsızsa kuralın kendisi.</summary>
        public CozulmusTesvik Coz(int? altSecim)
        {
            var v = Varyantlar.FirstOrDefault(x => x.Deger == altSecim);
            // Varyant kod vermiyorsa (Kültür) kuralın kendi kodu.
            var kod = v?.SgkKodu ?? SgkKodu;
            var ad = v is null ? Ad : $"{Ad} ({v.Ad})";
            return new CozulmusTesvik(
                this,
                v,
                kod is null ? ad : $"{kod} – {ad}",
                v?.Kalemler ?? Kalemler,
                v?.Oran ?? Oran);
        }
    }

    public sealed record CozulmusTesvik(
        TesvikKurali Kural,
        TesvikVaryanti? Varyant,
        string Etiket,
        TesvikKalemleri Kalemler,
        decimal Oran);

    /// <summary>
    /// Bordro teşviklerinin TEK kural tablosu: liste, sıra, ad, SGK kodu ve 2026+ hesap kuralı
    /// burada. Açılır kutu, hesaplayıcı (<c>TesvikKuraliStratejisi</c>), GV/DV teşviki,
    /// doğrulama ve Excel/PDF etiketi hep buradan okur.
    ///
    /// 2026 öncesi yıllar: liste yine bu tablodan (IlkYil null olanlar) gelir ama hesap
    /// eski strateji sınıflarıyla yapılır (<c>PayrollIncentiveStrategyFactory</c>) — 2024/2025
    /// sonuçları bilinçli olarak dondurulmuş (yanlış oranlar dahil).
    /// </summary>
    public static class PayrollTesvikKurallari
    {
        public const int YeniKurallarYili = 2026;
        public const string Indirimsiz = "00000";

        private const string Hazine = "Hazine";
        private const string IssizlikFonu = "İşsizlik Sigortası Fonu";

        public static readonly IReadOnlyList<TesvikKurali> Tumu = new List<TesvikKurali>
        {
            new() { Sira = 1, Anahtar = "00000", SgkKodu = "00000", Ad = "İndirimsiz / Teşviksiz",
                    Hesap = TesvikHesapTuru.Yok },

            new() { Sira = 2, Anahtar = "05510", SgkKodu = "05510", Ad = "5510 - Genel prim indirimi",
                    Hesap = TesvikHesapTuru.Puan5510, Kaynak = Hazine, EmekliyeUygulanmaz = true },

            new() { Sira = 3, Anahtar = "04691", SgkKodu = "04691", Ad = "Teknopark / 4691",
                    Hesap = TesvikHesapTuru.PrimIndirimi, Taban = TesvikTabani.SgkMatrahi,
                    Kalemler = TesvikKalemleri.IsverenSgk, Oran = 0.50m,
                    Gv = GvTesvikTuru.Tam, DvMuaf = true, Kaynak = Hazine },

            new() { Sira = 4, Anahtar = "05746", SgkKodu = "05746", Ad = "Ar-Ge / Tasarım - 5746",
                    Hesap = TesvikHesapTuru.PrimIndirimi, Taban = TesvikTabani.SgkMatrahi,
                    Kalemler = TesvikKalemleri.IsverenSgk, Oran = 0.50m,
                    Gv = GvTesvikTuru.EgitimeGore, DvMuaf = true, Kaynak = Hazine },

            new() { Sira = 5, Anahtar = "05746-TEKMER", SgkKodu = "05746", Ad = "TEKMER / Teknoloji Merkezi",
                    Hesap = TesvikHesapTuru.PrimIndirimi, Taban = TesvikTabani.SgkMatrahi,
                    Kalemler = TesvikKalemleri.IsverenSgk, Oran = 0.50m,
                    Gv = GvTesvikTuru.EgitimeGore, DvMuaf = true, Kaynak = Hazine, IlkYil = 2026 },

            // İSP kapsam dışı (2025 ve öncesi hesap İSP'yi de sıfırlıyordu).
            new() { Sira = 6, Anahtar = "14857", SgkKodu = "14857", Ad = "Engelli istihdam teşviki",
                    Hesap = TesvikHesapTuru.PrimIndirimi, Taban = TesvikTabani.AsgariUcret,
                    Kalemler = TesvikKalemleri.IsverenSgk, EngelZorunlu = true, EmekliyeUygulanmaz = true,
                    Kaynak = Hazine },

            // İSP kapsam dışı.
            new() { Sira = 7, Anahtar = "06111", SgkKodu = "06111", Ad = "Genç / Kadın / Mesleki Belge",
                    Hesap = TesvikHesapTuru.PrimIndirimi, Taban = TesvikTabani.AsgariUcret,
                    Kalemler = TesvikKalemleri.IsverenSgk, EmekliyeUygulanmaz = true, Kaynak = IssizlikFonu },

            // 4447/50: işveren MYÖ + GSS + KVSK'nın 1 puanı, sigortalı MYÖ + GSS (%34,5). İşsizlik primi kapsam dışı.
            new() { Sira = 8, Anahtar = "15921", SgkKodu = "15921", Ad = "İşsizlik ödeneği alan istihdamı",
                    Hesap = TesvikHesapTuru.PrimIndirimi, Taban = TesvikTabani.AsgariUcret,
                    Kalemler = TesvikKalemleri.IsverenMyo | TesvikKalemleri.IsverenGss
                             | TesvikKalemleri.IsverenKvskBirPuan | TesvikKalemleri.SigortaliSgk,
                    EmekliyeUygulanmaz = true, Kaynak = IssizlikFonu, IlkYil = 2026 },

            // TODO-TEYIT: 6. bölgede sigortalı İSP hissesi de karşılanıyor mu, karşılanan sigortalı
            // hissesi GV matrahından yine düşülüyor mu? Uygulanan: yalnız sigortalı SGK hissesi
            // (asgari ücret tabanında), GV matrahı değişmez, yalnız kesinti toplamından düşer.
            new() { Sira = 9, Anahtar = "YATIRIM", SgkKodu = "16322/26322", Ad = "Yatırım teşviki istihdam",
                    Hesap = TesvikHesapTuru.PrimIndirimi, Taban = TesvikTabani.AsgariUcret,
                    Kalemler = TesvikKalemleri.IsverenSgk, EmekliyeUygulanmaz = true, Kaynak = Hazine,
                    AltSecimBasligi = "Bölge",
                    Varyantlar = new[]
                    {
                        new TesvikVaryanti(1, "1–5. bölge", SgkKodu: "16322", Kalemler: TesvikKalemleri.IsverenSgk),
                        new TesvikVaryanti(2, "6. bölge", SgkKodu: "26322",
                            Kalemler: TesvikKalemleri.IsverenSgk | TesvikKalemleri.SigortaliSgk),
                    } },

            new() { Sira = 10, Anahtar = "03294", SgkKodu = "03294", Ad = "Sosyal yardım alan istihdamı",
                    Hesap = TesvikHesapTuru.PrimIndirimi, Taban = TesvikTabani.AsgariUcret,
                    Kalemler = TesvikKalemleri.IsverenSgk, EmekliyeUygulanmaz = true, Kaynak = IssizlikFonu,
                    IlkYil = 2026 },

            // 2828: işveren + sigortalı SGK, işveren + sigortalı İSP (%38,75), asgari ücret tabanında.
            new() { Sira = 11, Anahtar = "02828", SgkKodu = "02828", Ad = "Sosyal hizmet modelleri istihdamı",
                    Hesap = TesvikHesapTuru.PrimIndirimi, Taban = TesvikTabani.AsgariUcret,
                    Kalemler = TesvikKalemleri.IsverenSgk | TesvikKalemleri.IsverenIsp | TesvikKalemleri.Sigortali,
                    EmekliyeUygulanmaz = true, Kaynak = Hazine },

            new() { Sira = 12, Anahtar = "05225", SgkKodu = "05225", Ad = "Kültür yatırımı/girişimi",
                    Hesap = TesvikHesapTuru.PrimIndirimi, Taban = TesvikTabani.SgkMatrahi,
                    Kalemler = TesvikKalemleri.IsverenSgk, Kaynak = Hazine, IlkYil = 2026,
                    AltSecimBasligi = "Dönem",
                    Varyantlar = new[]
                    {
                        new TesvikVaryanti(1, "Yatırım dönemi %50", Oran: 0.50m),
                        new TesvikVaryanti(2, "İşletme dönemi %25", Oran: 0.25m),
                    } },

            new() { Sira = 13, Anahtar = "TURIZM", Ad = "Turizm ücret desteği",
                    Hesap = TesvikHesapTuru.GunlukUcretDestegi, Kaynak = "Turizm Desteği",
                    GunlukDestekTutari = 116.67m, DestekBaslangic = (2026, 5), DestekBitis = (2026, 12),
                    OnayMetni = "Konaklama tesisi (NACE 55.xx)", IlkYil = 2026, SonYil = 2026 },

            new() { Sira = 14, Anahtar = "ISG", Ad = "İSG desteği",
                    Hesap = TesvikHesapTuru.AylikTutarDestegi, Kaynak = "İSG Desteği", IlkYil = 2026 },

            new() { Sira = 15, Anahtar = "MANUEL", Ad = "Özel / Manuel teşvik",
                    Hesap = TesvikHesapTuru.Manuel, Taban = TesvikTabani.SgkMatrahi,
                    Kalemler = TesvikKalemleri.IsverenSgk, Kaynak = "Manuel", IlkYil = 2026 },
        };

        public static TesvikKurali? Bul(string? anahtar) =>
            Tumu.FirstOrDefault(x => x.Anahtar == anahtar);

        public static IReadOnlyList<TesvikKurali> YilIcin(int yil) =>
            Tumu.Where(x => x.YilIcinGecerli(yil)).OrderBy(x => x.Sira).ToList();

        /// <summary>Excel/PDF başlığı: "kod – ad (alt seçim)". Tanımsız anahtar ham haliyle döner.</summary>
        public static string Etiket(string? anahtar, int? altSecim) =>
            Bul(anahtar)?.Coz(altSecim).Etiket ?? anahtar ?? string.Empty;
    }
}
