using CatalogService.Api.Infrastructure.Domain;

namespace CatalogService.Api.Features.BankaEkstre.Domain
{
    /// <summary>
    /// Öğrenilen anahtar → ORKA hesap kodu eşlemesi. **Firma bazlı** (<c>FirmaId</c>):
    /// hesap planı ve cari kodları firmaya özeldir.
    ///
    /// Anahtar ham açıklamanın hash'i değil, normalize unvan çekirdeğidir
    /// (<see cref="AnahtarCekirdek"/>). Aynı çekirdeği paylaşan birden fazla cari varsa
    /// (Park Plaza Aidat / Elektrik / 19. Kat) çekirdeğe bir ayırt edici kelime eklenir
    /// (<see cref="AyirtEdiciEk"/>); aramada önce genişletilmiş anahtar, tutmazsa sade
    /// çekirdek denenir.
    /// </summary>
    public class HesapEslesmesi : FirmaKapsamliEntity
    {
        public int Id { get; set; }

        /// <summary>Normalize unvan çekirdeği, IBAN veya VKN — <see cref="AnahtarTipi"/>'ne göre.</summary>
        public string AnahtarCekirdek { get; set; } = string.Empty;

        /// <summary>
        /// Aynı çekirdeği paylaşan cari ailesinde satırı ayıran kelime ("AIDAT", "ELEKTRIK").
        /// Aile yoksa null — gereksiz kelime eklemek anahtarın ikinci ay tutmamasına yol açar.
        /// </summary>
        public string? AyirtEdiciEk { get; set; }

        public AnahtarTipi AnahtarTipi { get; set; } = AnahtarTipi.UnvanCekirdek;

        /// <summary>Boşluklu ORKA kodu, ör. "120 D22".</summary>
        public string HesapKodu { get; set; } = string.Empty;

        public string? HesapAdi { get; set; }

        /// <summary>Kaydın öğrenildiği yön; aynı anahtar iki yönde farklı hesaba gidebilir.</summary>
        public Yon Yon { get; set; }

        /// <summary>
        /// <see cref="AnahtarTipi.Belirsizlik"/> kayıtlarında aday kümesinin özeti
        /// (kod listesinin hash'i). Yeni bir cari açılıp küme değişirse eski karar
        /// <b>uygulanmaz</b>, satır tekrar onaya düşer — aksi hâlde yeni açılan bir
        /// Park Plaza hesabı hiç görünmez olurdu.
        /// </summary>
        public string? AdayKumesiOzeti { get; set; }

        public int KullanimSayisi { get; set; } = 1;

        public DateTime SonKullanim { get; set; }

        /// <summary>Arama ve ekran için tek parça anahtar gösterimi.</summary>
        public string TamAnahtar => string.IsNullOrWhiteSpace(AyirtEdiciEk)
            ? AnahtarCekirdek
            : $"{AnahtarCekirdek} + {AyirtEdiciEk}";
    }
}
