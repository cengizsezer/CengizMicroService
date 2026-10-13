using CatalogService.Api.Features.Yapilacaklar.Domain;
using CatalogService.Api.Features.Yapilacaklar.Dtos;

namespace CatalogService.Api.Features.Yapilacaklar.Services
{
    /// <summary>
    /// Firma Panosu → Yapılacaklar (Aşama 1): kuyruğun satırlarını FİRMA + DÖNEM kartlarına toplar.
    /// Saf fonksiyon; veritabanı ve saat yok, "bugün" parametre.
    ///
    /// <b>Yeni kural yok.</b> Hangi dönemin görüneceği <see cref="YapilacaklarKurucu.Satirlar"/>'ın kuyruk
    /// kuralıdır (gecikmiş açık dönemler + sıradaki açık dönem + önündeki son yapılmış dönem); prosedür
    /// satırları ve "yapılmadı ama notu var" kayıtları orada zaten ayıklanır. Bu sınıf yalnız gruplar,
    /// böler ve sayar.
    ///
    /// <b>Dönem merkezli:</b> madde, DÖNEMİNİN KAPANDIĞI ayın kartına düşer (<see cref="KartDonemi"/>) —
    /// İş Takip Panosu'nun <see cref="IsDonemi.AydaKapananlar"/> kuralı: Eylül KDV'si (son günü 28 Ekim)
    /// Eylül kartında, 3. çeyrek geçici vergi Eylül'de, yıllık iş Aralık'ta, tek seferlik iş tarihinin ayında.
    ///
    /// <b>Sayılar filtreden SONRA:</b> tür ve sorumlu filtresi saymadan önce uygulanır (firma filtresi
    /// servisin girdisinde), sayaçlar ekranda görünen maddelerle tutar.
    /// </summary>
    public static class ChecklistKartKurucu
    {
        /// <summary>Sorumlu filtresinde "sorumlusu olmayan" seçeneği.</summary>
        public const int SorumluYok = 0;

        public static FirmaPanosuDto Kur(DateTime bugun,
                                         IEnumerable<YapilacaklarKurucu.FirmaGirdisi> firmalar,
                                         IEnumerable<IsSatiriDto> satirlar,
                                         FirmaPanosuFiltresi filtre,
                                         IsKategoriBaglami? kategoriler = null,
                                         IReadOnlyDictionary<int, string>? sablonAdlari = null)
        {
            bugun = bugun.Date;
            kategoriler ??= IsKategoriBaglami.Bos;
            sablonAdlari ??= new Dictionary<int, string>();
            var tr = StringComparer.Create(IsTakvimHesabi.Tr, true);
            var firmaById = firmalar.ToDictionary(f => f.FirmaId);

            // Tür filtresi önce: sorumlu seçeneklerinin sayıları türe uyar, sorumlunun kendisine değil
            // (seçili sorumlu hapında öbür sorumlular kaybolmasın).
            var turdeki = satirlar.Where(s => filtre.Turler.Count == 0 || filtre.Turler.Contains(s.KaynakTip)).ToList();
            var sorumlular = turdeki
                .GroupBy(s => s.SorumluKullaniciId ?? SorumluYok)
                .Select(g => new PanoSorumluSecenegiDto
                {
                    Id = g.Key,
                    Ad = g.Key == SorumluYok ? "Sorumlu yok" : g.Select(s => s.SorumluKullaniciAdi).FirstOrDefault(a => !string.IsNullOrWhiteSpace(a)) ?? $"Kullanıcı #{g.Key}",
                    Sayi = g.Count()
                })
                .OrderBy(s => s.Id == SorumluYok)
                .ThenBy(s => s.Ad, tr)
                .ToList();

            var suzulmus = turdeki
                .Where(s => filtre.Sorumlular.Count == 0 || filtre.Sorumlular.Contains(s.SorumluKullaniciId ?? SorumluYok))
                .ToList();

            var ortak = suzulmus.GroupBy(YapilacaklarKurucu.GrupAnahtari)
                                .ToDictionary(g => g.Key, g => g.Select(s => s.FirmaId).Distinct().Count());

            var maddeler = suzulmus.Select(s => new ChecklistMaddesiDto
            {
                Satir = s,
                Kova = Kova(s.SonGun, bugun, s.Tamamlandi),
                OrtakFirmaSayisi = ortak[YapilacaklarKurucu.GrupAnahtari(s)]
            }).ToList();

            var dto = new FirmaPanosuDto
            {
                Bugun = bugun,
                FirmaId = filtre.FirmaId,
                Sayaclar = Sayaclar(maddeler, bugun),
                Sorumlular = sorumlular
            };

            // ---- Kartlar: firma + kartın ayı ----
            var kartlar = maddeler
                .GroupBy(m => (m.Satir.FirmaId, Donem: KartDonemi(m.Satir)))
                .Select(g => Kart(g.Key.FirmaId, g.Key.Donem, g.ToList(), firmaById, tr, kategoriler, sablonAdlari))
                .ToList();

            // Dönemler eskiden yeniye; kart sırası FİRMA ADI — durumdan bağımsız, işaretlenen kart zıplamaz.
            dto.Donemler = kartlar
                .GroupBy(k => k.DonemAnahtari)
                .OrderBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => new ChecklistDonemiDto
                {
                    Anahtar = g.Key,
                    Etiket = g.First().DonemEtiketi,
                    FirmaSayisi = g.Select(k => k.FirmaId).Distinct().Count(),
                    Toplam = g.Sum(k => k.Toplam),
                    Tamam = g.Sum(k => k.Tamam),
                    Gecikmis = g.Sum(k => k.Gecikmis),
                    Kartlar = g.OrderBy(k => k.FirmaAdi, tr).ThenBy(k => k.FirmaId).ToList()
                })
                .ToList();

            dto.KartSayisi = kartlar.Count;
            dto.FirmaSayisi = kartlar.Select(k => k.FirmaId).Distinct().Count();

            // ---- Liste: Yapılacaklar'ın gruplaması ("3 firma") olduğu gibi, yalnız kovası eklenir ----
            dto.Liste = YapilacaklarKurucu.Grupla(suzulmus, bugun)
                .Select(g => new PanoListeSatiriDto
                {
                    Kova = Kova(g.SonGun, bugun, g.Firmalar.All(f => f.Tamamlandi)),
                    Grup = g
                })
                .ToList();

            return dto;
        }

        /// <summary>
        /// Vadeye göre kova — TEK YER. Yapıldı → Tamam; son günü geçmiş → Gecikti; son günü bugünün
        /// ayında → Bu ay; sonraki aylarda → Sonra.
        /// </summary>
        public static PanoKovasi Kova(DateTime sonGun, DateTime bugun, bool tamam)
        {
            if (tamam) return PanoKovasi.Tamam;
            if (sonGun.Date < bugun.Date) return PanoKovasi.Gecikti;
            return sonGun.Year == bugun.Year && sonGun.Month == bugun.Month ? PanoKovasi.BuAy : PanoKovasi.Sonra;
        }

        /// <summary>
        /// Maddenin kartının ayı: dönemin KAPANDIĞI ay (İş Takip Panosu'yla aynı). Aylıkta dönemin kendisi,
        /// çeyrekte çeyreğin son ayı, yıllıkta aralık; tek seferlik ya da çözülemeyen anahtarda son günün ayı.
        /// </summary>
        public static IsDonemi KartDonemi(IsSatiriDto s)
        {
            if (s.Tekrar != IsTekrari.TekSefer && IsDonemi.Coz(s.Tekrar, s.DonemAnahtari) is { } donem)
                return new IsDonemi(IsTekrari.Aylik, donem.Bit.Year, donem.Bit.Month);
            return new IsDonemi(IsTekrari.Aylik, s.SonGun.Year, s.SonGun.Month);
        }

        /// <summary>
        /// Kartın bölümü — Aşama 2: işin kategorisi. Çözüm <see cref="IsKategoriCozucu"/>'da (tek yer); sıra
        /// kategorinin <c>Sira</c>'sı, "Diğer" en sonda.
        /// </summary>
        public static KategoriBolumu Bolum(IsSatiriDto s, IsKategoriBaglami kategoriler) => IsKategoriCozucu.Coz(s, kategoriler);

        private static ChecklistKartiDto Kart(int firmaId, IsDonemi donem, List<ChecklistMaddesiDto> maddeler,
                                              IReadOnlyDictionary<int, YapilacaklarKurucu.FirmaGirdisi> firmalar,
                                              StringComparer tr, IsKategoriBaglami kategoriler,
                                              IReadOnlyDictionary<int, string> sablonAdlari)
        {
            var ilk = maddeler[0].Satir;
            firmalar.TryGetValue(firmaId, out var firma);

            var siradaki = maddeler.Where(m => m.Kova != PanoKovasi.Tamam)
                                   .OrderBy(m => m.Satir.SonGun)
                                   .ThenBy(m => m.Satir.Baslik, tr)
                                   .FirstOrDefault();

            return new ChecklistKartiDto
            {
                Anahtar = $"{firmaId}|{donem.Anahtar}",
                FirmaId = firmaId,
                FirmaAdi = firma?.Ad ?? ilk.FirmaAdi,
                SorumluKullaniciAdi = firma?.SorumluKullaniciAdi,
                DonemAnahtari = donem.Anahtar,
                DonemEtiketi = donem.Etiket,
                Durum = siradaki?.Kova ?? PanoKovasi.Tamam,
                Toplam = maddeler.Count,
                Tamam = maddeler.Count(m => m.Kova == PanoKovasi.Tamam),
                Gecikmis = maddeler.Count(m => m.Kova == PanoKovasi.Gecikti),
                YasalVar = maddeler.Any(m => m.Satir.KaynakTip == IsKaynagi.Yasal),
                SiradakiBaslik = siradaki?.Satir.Baslik,
                SiradakiSonGun = siradaki?.Satir.SonGun,
                SiradakiKova = siradaki?.Kova,
                // Aşama 3: özel maddelerin şablon kökeni (FirmaIsi Id → şablon adı); yasal madde şablondan gelmez.
                Sablonlar = maddeler
                    .Where(m => m.Satir.KaynakTip == IsKaynagi.Ozel)
                    .Select(m => sablonAdlari.GetValueOrDefault(m.Satir.KaynakId))
                    .OfType<string>()
                    .Distinct()
                    .OrderBy(a => a, tr)
                    .ToList(),
                Bolumler = maddeler
                    .GroupBy(m => Bolum(m.Satir, kategoriler))
                    .OrderBy(g => g.Key.Sira)
                    .ThenBy(g => g.Key.Ad, tr)
                    .Select(g => new ChecklistBolumuDto
                    {
                        Anahtar = g.Key.Anahtar,
                        Ad = g.Key.Ad,
                        Toplam = g.Count(),
                        Tamam = g.Count(m => m.Kova == PanoKovasi.Tamam),
                        Gecikmis = g.Count(m => m.Kova == PanoKovasi.Gecikti),
                        // Sıra son gün + ad: durumdan bağımsız, işaretlenen madde yerinden zıplamaz.
                        Maddeler = g.OrderBy(m => m.Satir.SonGun).ThenBy(m => m.Satir.Baslik, tr).ToList()
                    })
                    .ToList()
            };
        }

        private static List<PanoSayaciDto> Sayaclar(List<ChecklistMaddesiDto> maddeler, DateTime bugun)
        {
            List<ChecklistMaddesiDto> Kovada(PanoKovasi k) => maddeler.Where(m => m.Kova == k).ToList();

            var gecikti = Kovada(PanoKovasi.Gecikti);
            var buAy = Kovada(PanoKovasi.BuAy);
            var sonra = Kovada(PanoKovasi.Sonra);
            var tamam = Kovada(PanoKovasi.Tamam);

            return new List<PanoSayaciDto>
            {
                new()
                {
                    Kova = PanoKovasi.Gecikti, Ad = "Gecikti", Sayi = gecikti.Count,
                    Aciklama = gecikti.Count == 0 ? "gecikmiş iş yok"
                        : $"en eskisi {(bugun - gecikti.Min(m => m.Satir.SonGun).Date).Days} gün önce"
                },
                new()
                {
                    Kova = PanoKovasi.BuAy, Ad = "Bu ay", Sayi = buAy.Count,
                    Aciklama = buAy.Count == 0 ? "bu ay iş yok" : $"ilk son gün {buAy.Min(m => m.Satir.SonGun):dd.MM}"
                },
                new()
                {
                    Kova = PanoKovasi.Sonra, Ad = "Sonra", Sayi = sonra.Count,
                    Aciklama = sonra.Count == 0 ? "sonraki aylarda iş yok" : $"ilk son gün {sonra.Min(m => m.Satir.SonGun):dd.MM}"
                },
                new()
                {
                    // Kuyruk her işin yalnız SON yapılmış dönemini gösterir; geçmişin tamamı İş Takip Panosu'nda.
                    Kova = PanoKovasi.Tamam, Ad = "Tamamlanan", Sayi = tamam.Count,
                    Aciklama = "her işin son yapılan dönemi"
                }
            };
        }
    }
}
