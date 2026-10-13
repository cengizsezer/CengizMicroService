namespace WebApp.Shared.Dto.Yapilacaklar
{
    // Sunucudaki CatalogService.Api.Features.Yapilacaklar.Dtos.IsKategoriDtos'un aynası (Firma Panosu Aşama 2).

    public class IsKategorisiDto
    {
        public int Id { get; set; }
        public string Ad { get; set; } = string.Empty;
        public int Sira { get; set; }
        public bool Aktif { get; set; }
        public int IsSayisi { get; set; }
        public int TarifSayisi { get; set; }
        public int EslemeSayisi { get; set; }

        /// <summary>Aşama 3: bu kategoriyi taşıyan checklist şablonu maddeleri.</summary>
        public int SablonMaddeSayisi { get; set; }

        /// <summary>
        /// Eşlemesi olmayan yasal işin düştüğü kategori (seed'de "Beyanname"; ad değil sunucudaki kolon belirler).
        /// Silinmez, pasife alınmaz; başka kategoriye taşınabilir.
        /// </summary>
        public bool YasalVarsayilan { get; set; }

        public int KullanimSayisi => IsSayisi + TarifSayisi + EslemeSayisi + SablonMaddeSayisi;
    }

    public class IsKategorisiKaydetDto
    {
        public string Ad { get; set; } = string.Empty;
        public bool Aktif { get; set; } = true;
    }

    public class MukellefiyetEslemeDto
    {
        public string MukellefiyetKodu { get; set; } = string.Empty;
        public string Ad { get; set; } = string.Empty;
        public int? KategoriId { get; set; }
    }

    public class MukellefiyetEslemeKaydetDto
    {
        public int? KategoriId { get; set; }
    }

    public class KategorisizGrupDto
    {
        public string Anahtar { get; set; } = string.Empty;
        public string Baslik { get; set; } = string.Empty;
        public IsTekrari Tekrar { get; set; }
        public int FirmaSayisi { get; set; }
        public int IsSayisi { get; set; }
    }

    public class TopluKategoriAtaDto
    {
        public string Anahtar { get; set; } = string.Empty;
        public int KategoriId { get; set; }
    }

    public class TopluKategoriSonucDto
    {
        public int Guncellenen { get; set; }
    }

    /// <summary>"Diğer" bölümü: tabloda kayıt değil, sunucunun sabit son bölümü (IsKategoriCozucu ile aynı ad/anahtar).</summary>
    public static class IsKategoriMetin
    {
        public const string Diger = "Diğer";
        public const string DigerAnahtari = "diger";
    }
}
