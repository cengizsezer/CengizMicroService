using CatalogService.Api.Features.Yapilacaklar.Domain;

namespace CatalogService.Api.Features.Yapilacaklar.Dtos
{
    // Firma Panosu Aşama 2: iş kategorileri (Yönetim → İş Kategorileri).

    public class IsKategorisiDto
    {
        public int Id { get; set; }
        public string Ad { get; set; } = string.Empty;
        public int Sira { get; set; }
        public bool Aktif { get; set; }

        /// <summary>Kendi kategorisi bu olan özel işler (prosedür satırı hariç).</summary>
        public int IsSayisi { get; set; }
        public int TarifSayisi { get; set; }
        public int EslemeSayisi { get; set; }

        /// <summary>Aşama 3: bu kategoriyi taşıyan checklist şablonu maddeleri (kullanım sayılır, silmeyi engeller).</summary>
        public int SablonMaddeSayisi { get; set; }

        /// <summary>
        /// Yasal işin varsayılanı (<c>IsKategorisi.VarsayilanYasal</c>, seed'de Beyanname); eşlemesi olmayan yasal iş
        /// buraya düşer. Bu kategori silinmez, pasife alınmaz; varsayılan başka kategoriye taşınabilir.
        /// </summary>
        public bool YasalVarsayilan { get; set; }
    }

    public class IsKategorisiKaydetDto
    {
        public string Ad { get; set; } = string.Empty;
        public bool Aktif { get; set; } = true;
    }

    /// <summary>Yasal eşleme sekmesinin satırı: takvimdeki her mükellefiyet kodu.</summary>
    public class MukellefiyetEslemeDto
    {
        public string MukellefiyetKodu { get; set; } = string.Empty;

        /// <summary>Takvimdeki adı ("KDV beyannamesi").</summary>
        public string Ad { get; set; } = string.Empty;

        /// <summary>Boş = eşleme yok, yasal iş varsayılana (Beyanname) düşer.</summary>
        public int? KategoriId { get; set; }
    }

    public class MukellefiyetEslemeKaydetDto
    {
        /// <summary>Boş = eşlemeyi kaldır.</summary>
        public int? KategoriId { get; set; }
    }

    /// <summary>
    /// Kategorisiz özel işlerin bir grubu: aynı başlık + tekrar (İş Takip kolon anahtarıyla aynı kural).
    /// Kategorisiz = çözüm "Diğer" (kendi kategorisi de tarifinin kategorisi de yok ya da pasif).
    /// </summary>
    public class KategorisizGrupDto
    {
        /// <summary><c>DonemPanosuKurucu.OzelKolonAnahtari</c> ("O|BORDRO|2"); toplu atama bununla gelir.</summary>
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
}
