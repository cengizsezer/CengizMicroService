using System.Globalization;
using WebApp.Shared.Dto.Yonetim;

namespace WebApp.Pages.DeclarationFollow
{
    /// <summary>Beyanname ekranlarındaki firma açılır kutusunun seçeneği (Firmalarım firması).</summary>
    public sealed record BeyannameFirmaSecenegi(int Id, string Ad, bool Aktif);

    /// <summary>
    /// Beyanname firma seçenekleri — kaynak Firmalarım (IFirmaApiClient, dbo.Firmalar; eski CustomerCompanies değil).
    /// Firmalar global; Firmalarım'la aynı kapsam, ek kullanıcı/tenant filtresi yok.
    ///   • Filtre (Takip üstü): yalnız aktif firmalar; "Pasif firmaları da göster" işaretliyse pasifler "(pasif)" ekiyle.
    ///   • Form (yeni / düzenleme): yalnız aktif firmalar; düzenlenen kaydın KENDİ firması pasifse o da "(pasif)"
    ///     ekiyle eklenir (yoksa kutu boş kalır ve kayıt kaydedilemezdi). Sunucu aynı kuralı uygular.
    /// </summary>
    public static class BeyannameFirmaSecenekleri
    {
        private static readonly StringComparer Tr = StringComparer.Create(CultureInfo.GetCultureInfo("tr-TR"), true);

        public static string Ad(FirmaDto f) => string.IsNullOrWhiteSpace(f.KisaAd) ? f.Unvan.Trim() : f.KisaAd.Trim();

        public static List<BeyannameFirmaSecenegi> Filtre(IEnumerable<FirmaDto> firmalar, bool pasifleriGoster)
            => Sirala(firmalar.Where(f => f.Aktif || pasifleriGoster));

        public static List<BeyannameFirmaSecenegi> Form(IEnumerable<FirmaDto> firmalar, int? mevcutFirmaId)
            => Sirala(firmalar.Where(f => f.Aktif || f.Id == mevcutFirmaId));

        private static List<BeyannameFirmaSecenegi> Sirala(IEnumerable<FirmaDto> firmalar)
            => firmalar
                .Select(f => new BeyannameFirmaSecenegi(f.Id, f.Aktif ? Ad(f) : $"{Ad(f)} (pasif)", f.Aktif))
                .OrderBy(s => !s.Aktif)
                .ThenBy(s => s.Ad, Tr)
                .ToList();
    }
}
