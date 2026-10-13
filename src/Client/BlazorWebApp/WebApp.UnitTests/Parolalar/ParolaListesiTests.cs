using WebApp.Pages.Yonetim.Parolalar;
using WebApp.Shared.Dto.Parolalar;
using WebApp.Shared.Dto.Yonetim;

namespace WebApp.UnitTests.Parolalar
{
    /// <summary>
    /// Parolalar sayfasının saf kuralları: sekme alanları (şifre alanı anahtarları sunucuyla aynı), arama + firma süzgeci,
    /// pasif firma "(pasif)" ekiyle, formda yalnız aktif firmalar (+ düzenlenen kaydın kendi pasif firması) ve "elle".
    /// </summary>
    public class ParolaListesiTests
    {
        private static ParolaDto K(int id, ParolaTuru tur, string firma, string kullanici = "u", string? ent = null,
                                   string? sube = null, bool pasif = false, int? firmaId = 1)
            => new() { Id = id, Tur = tur, FirmaAdi = firma, KullaniciAdi = kullanici, Entegrator = ent, Sube = sube, FirmaPasif = pasif, FirmaId = firmaId };

        private static readonly List<ParolaDto> Liste = new()
        {
            K(1, ParolaTuru.Entegrator, "TEKROM", "ornek.tekrom", "Digital Planet"),
            K(2, ParolaTuru.Entegrator, "AITEX", "10000000001", "Luca e-Fatura"),
            K(3, ParolaTuru.Entegrator, "ESKİ FİRMA", "eski", "Uyumsoft", pasif: true),
            K(4, ParolaTuru.Sgk, "CARRIERE", "100", sube: "İzmir"),
            K(5, ParolaTuru.Sgk, "CARRIERE", "100", sube: "Bursa"),
            K(6, ParolaTuru.Sgk, "CARRIERE", "100"),
            K(7, ParolaTuru.DijitalVergiDairesi, "İNCİ GIDA", "10000002", firmaId: null)
        };

        [Fact]
        public void Sifre_alanlari_tur_bazinda_sunucuyla_ayni_anahtarlar()
        {
            string[] Sifreler(ParolaTuru t) => ParolaListesi.TurBul(t).Alanlar.Where(a => a.Sifre).Select(a => a.Anahtar).ToArray();

            Assert.Equal(new[] { "sifre" }, Sifreler(ParolaTuru.Entegrator));
            Assert.Equal(new[] { "parola", "sifre" }, Sifreler(ParolaTuru.DijitalVergiDairesi));
            Assert.Equal(new[] { "sistemSifresi", "isyeriSifresi" }, Sifreler(ParolaTuru.Sgk));
            Assert.Equal(new[] { "ent", "dvd", "sgk" }, ParolaListesi.Turler.Select(t => t.Kod));
        }

        [Fact]
        public void Suz_sekme_arama_firma_ve_siralama()
        {
            Assert.Equal(new[] { 2, 3, 1 }, ParolaListesi.Suz(Liste, ParolaTuru.Entegrator, null, null).Select(p => p.Id));

            // Arama: firma, kullanıcı adı, entegratör, şube — Türkçe harf duyarsız.
            Assert.Equal(new[] { 2 }, ParolaListesi.Suz(Liste, ParolaTuru.Entegrator, "luca", null).Select(p => p.Id));
            Assert.Equal(new[] { 1 }, ParolaListesi.Suz(Liste, ParolaTuru.Entegrator, "ORNEK.TEK", null).Select(p => p.Id));
            Assert.Equal(new[] { 4 }, ParolaListesi.Suz(Liste, ParolaTuru.Sgk, "izmir", null).Select(p => p.Id));
            Assert.Equal(new[] { 7 }, ParolaListesi.Suz(Liste, ParolaTuru.DijitalVergiDairesi, "inci", null).Select(p => p.Id));

            // SGK: firma → şube (şubesiz önce).
            Assert.Equal(new[] { 6, 5, 4 }, ParolaListesi.Suz(Liste, ParolaTuru.Sgk, "", null).Select(p => p.Id));

            // Firma süzgeci görünen adla (pasif ekli).
            Assert.Equal(new[] { 3 }, ParolaListesi.Suz(Liste, ParolaTuru.Entegrator, null, "ESKİ FİRMA (pasif)").Select(p => p.Id));
        }

        [Fact]
        public void Pasif_firma_kaydi_listede_pasif_ekiyle_suzgecte_de_gorunur()
        {
            Assert.Equal("ESKİ FİRMA (pasif)", ParolaListesi.FirmaGorunenAd(Liste[2]));
            Assert.Equal("TEKROM", ParolaListesi.FirmaGorunenAd(Liste[0]));

            var suzgec = ParolaListesi.SuzgecFirmalari(Liste);
            Assert.Equal(new[] { "AITEX", "CARRIERE", "ESKİ FİRMA (pasif)", "İNCİ GIDA", "TEKROM" }, suzgec);
        }

        [Fact]
        public void Form_firmalari_yalniz_aktifler_duzenlenen_kaydin_pasif_firmasi_ve_elle()
        {
            var firmalar = new List<FirmaDto>
            {
                new() { Id = 5, KisaAd = "LIME", Unvan = "LIME TEKSTİL", Aktif = true },
                new() { Id = 3, KisaAd = "", Unvan = "DGR İÇ VE DIŞ TİC.", Aktif = true },
                new() { Id = 9, KisaAd = "ESKİ", Unvan = "ESKİ A.Ş.", Aktif = false },
                new() { Id = 8, KisaAd = "KAPALI", Unvan = "KAPALI A.Ş.", Aktif = false }
            };

            var yeni = ParolaListesi.FormFirmalari(firmalar, null);
            Assert.Equal(new[] { "f:3", "f:5", ParolaListesi.Elle }, yeni.Select(s => s.Deger));
            Assert.Equal("DGR İÇ VE DIŞ TİC.", yeni[0].Ad);                        // kısa ad boş → unvan
            Assert.Equal("Firmalarım'da yok, elle yazacağım", yeni[^1].Ad);

            var duzenleme = ParolaListesi.FormFirmalari(firmalar, K(1, ParolaTuru.Sgk, "ESKİ", firmaId: 9, pasif: true));
            Assert.Equal(new[] { "f:3", "f:5", "f:9", ParolaListesi.Elle }, duzenleme.Select(s => s.Deger));   // KAPALI yok
            Assert.Equal("ESKİ (pasif)", duzenleme[2].Ad);
        }

        [Fact]
        public void Liste_satiri_duz_degerler_ve_dolu_bilgisi_sifre_degeri_yok()
        {
            var p = new ParolaDto
            {
                KullaniciAdi = "10000000022", KullaniciKoduEk = "22", PortalAdresi = "https://portal.uyumsoft.com.tr/",
                SistemSifresiDolu = true, IsyeriSifresiDolu = false
            };

            Assert.Equal("10000000022", ParolaListesi.DuzDeger(p, "kullanici"));
            Assert.Equal("22", ParolaListesi.DuzDeger(p, "ek"));
            Assert.Null(ParolaListesi.DuzDeger(p, "sistemSifresi"));                   // şifre listede yok
            Assert.True(ParolaListesi.SifreDolu(p, "sistemSifresi"));
            Assert.False(ParolaListesi.SifreDolu(p, "isyeriSifresi"));
            Assert.Equal("portal.uyumsoft.com.tr", ParolaListesi.KisaAdres(p.PortalAdresi!));

            // İstemci DTO'sunda da şifre değeri taşıyabilecek string alan yok.
            var supheli = typeof(ParolaDto).GetProperties()
                .Where(x => x.PropertyType == typeof(string)
                            && (x.Name.Contains("Sifre", StringComparison.OrdinalIgnoreCase) || x.Name.Contains("Parola", StringComparison.OrdinalIgnoreCase)));
            Assert.Empty(supheli);
        }
    }
}
