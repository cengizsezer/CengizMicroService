using CatalogService.Api.Features.Anasayfa.Dtos;
using CatalogService.Api.Features.BankaEkstre.Dtos;
using CatalogService.Api.Features.Declarations.Entities;

namespace CatalogService.Api.Features.Anasayfa.Services
{
    /// <summary>
    /// Anasayfa özetini kurar. <b>Saf fonksiyon</b>: veritabanı bilmez, hazır listeleri
    /// alır kartların sayılarını verir.
    ///
    /// Burada <b>yeni bir hesaplama yok</b>: sayılar, ekranda tıklanınca gidilecek
    /// sayfanın kendi servisinden geliyor (beyanname kayıtları, banka otomasyonu firma
    /// sayaçları). Bu sınıfın işi süzmek, saymak ve sıralamak.
    /// </summary>
    public static class AnasayfaOzetKurucu
    {
        /// <summary>Yaklaşan ödemelerde gösterilecek en fazla satır; kart uzayıp sayfayı boğmasın.</summary>
        public const int EnFazlaOdeme = 8;


        public static AnasayfaOzetDto Kur(
            int yil,
            int ay,
            DateTime bugun,
            int odemePenceresiGun,
            IReadOnlyList<Declaration> ayinBeyannameleri,
            IReadOnlyList<Declaration> yaklasanBeyannameler,
            IReadOnlyDictionary<int, string> firmaAdlari,
            IReadOnlyList<FirmaBankaOzetiDto> bankaOzetleri)
        {
            var ozet = new AnasayfaOzetDto
            {
                Yil = yil,
                Ay = ay,
                OdemePenceresiGun = odemePenceresiGun
            };

            // "Bekleyen", ÖDEMESİ tamamlanmamış olan demek: beyanname hazırlanmış ve
            // onaylanmış olabilir, para hâlâ çıkmamıştır. Kullanıcının anasayfada
            // aradığı sayı bu.
            var bekleyen = ayinBeyannameleri.Where(b => b.PaymentStatus != PaymentStatus.Paid).ToList();

            ozet.BekleyenBeyannameSayisi = bekleyen.Count;
            ozet.BekleyenVergiTutari = bekleyen.Sum(b => b.Amount);
            ozet.ToplamBeyannameSayisi = ayinBeyannameleri.Count;
            ozet.ToplamVergiTutari = ayinBeyannameleri.Sum(b => b.Amount);

            // KIRPILMAZ: kullanıcı sekiz firmanın işini tek oturumda yapıyor ve anasayfada
            // hepsinin durumunu birlikte görmek istiyor (KARARLAR §99). Kırpma, listenin
            // dışında kalan firmayı görünmez yapıyordu — oysa işi bekleyen firma tam da
            // gözden kaçandır. Firma sayısı iki haneli; liste uzamıyor.
            ozet.BankaOnayBekleyen = bankaOzetleri
                .Where(o => o.OnayBekleyen > 0)
                .OrderByDescending(o => o.OnayBekleyen)
                .Select(o => new AnasayfaBankaSatiriDto
                {
                    FirmaId = o.FirmaId,
                    FirmaAdi = firmaAdlari.TryGetValue(o.FirmaId, out var ad) ? ad : $"Firma {o.FirmaId}",
                    OnayBekleyen = o.OnayBekleyen
                })
                .ToList();

            // Toplam yine ayrı hesaplanır: liste yalnız onay bekleyeni olan firmaları
            // gösterir, toplam ise bütün firmaları kapsar.
            ozet.BankaOnayBekleyenToplam = bankaOzetleri.Sum(o => o.OnayBekleyen);

            ozet.YaklasanOdemeler = yaklasanBeyannameler
                .Where(b => b.PaymentStatus != PaymentStatus.Paid)
                .OrderBy(b => b.DueDate)
                .ThenBy(b => b.Id)
                .Take(EnFazlaOdeme)
                .Select(b => new AnasayfaOdemeDto
                {
                    DeclarationId = b.Id,
                    FirmaAdi = b.CompanyName,
                    BeyannameTuru = b.DeclarationType,
                    SonOdemeTarihi = b.DueDate,
                    Tutar = b.Amount,
                    GunKaldi = (b.DueDate.Date - bugun.Date).Days
                })
                .ToList();

            return ozet;
        }
    }
}
