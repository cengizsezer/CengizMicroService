using CatalogService.Api.Features.Yapilacaklar.Domain;

namespace CatalogService.Api.Features.Yapilacaklar.Dtos
{
    /// <summary>
    /// Firma Panosu sayaç kovası (Aşama 1). VADE merkezli ve dört kova: <see cref="IsDurumu"/>'nun
    /// "Bu hafta"sı yok — son günü bu ayda olan açık madde "Bu ay", sonraki aylarda olan "Sonra"
    /// (28 Ekim'de 3 Kasım son günlü iş Yapılacaklar'da "Bu hafta" idi, burada "Sonra").
    /// Kural tek yerde: <c>ChecklistKartKurucu.Kova</c>.
    /// </summary>
    public enum PanoKovasi : byte
    {
        Gecikti = 1,
        BuAy = 2,
        Sonra = 3,
        Tamam = 4
    }

    /// <summary>Panonun filtresi. Boş küme = filtre yok.</summary>
    public class FirmaPanosuFiltresi
    {
        public int? FirmaId { get; set; }

        /// <summary>Yasal / Firmaya özel; ikisi birden ya da hiçbiri = hepsi.</summary>
        public HashSet<IsKaynagi> Turler { get; set; } = new();

        /// <summary>Maddenin sorumlusu (kendi alanı, boşsa firmanınki). 0 = sorumlusu olmayan.</summary>
        public HashSet<int> Sorumlular { get; set; } = new();
    }

    /// <summary>Üstteki dört sayaç kartından biri. MADDE sayar (firma × iş × dönem).</summary>
    public class PanoSayaciDto
    {
        public PanoKovasi Kova { get; set; }
        public string Ad { get; set; } = string.Empty;
        public int Sayi { get; set; }

        /// <summary>"en eskisi 40 gün önce", "ilk son gün 15.10" …</summary>
        public string Aciklama { get; set; } = string.Empty;
    }

    /// <summary>Kartın içindeki bir madde: kuyruğun satırı + kovası.</summary>
    public class ChecklistMaddesiDto
    {
        /// <summary>Yapılacaklar'ın satırı — işaretleme ve prosedür paneli bunu kullanır.</summary>
        public IsSatiriDto Satir { get; set; } = new();

        public PanoKovasi Kova { get; set; }

        /// <summary>Aynı iş (aynı tür, ad ve son gün) filtredeki kaç firmada var; 1 = yalnız bu firma.</summary>
        public int OrtakFirmaSayisi { get; set; } = 1;
    }

    /// <summary>
    /// Kartın bir bölümü = iş kategorisi (Aşama 2); "Diğer" en sonda, boş bölüm yok.
    /// Çözüm tek yerde: <c>IsKategoriCozucu</c> (<c>ChecklistKartKurucu.Bolum</c> onu çağırır).
    /// </summary>
    public class ChecklistBolumuDto
    {
        /// <summary>"k3" ya da "diger".</summary>
        public string Anahtar { get; set; } = string.Empty;
        public string Ad { get; set; } = string.Empty;
        public int Toplam { get; set; }
        public int Tamam { get; set; }
        public int Gecikmis { get; set; }
        public List<ChecklistMaddesiDto> Maddeler { get; set; } = new();
    }

    /// <summary>Checklist kartı = firma + dönem (dönem merkezli: Ekim'in KDV'si Ekim kartında).</summary>
    public class ChecklistKartiDto
    {
        /// <summary>"12|2026-10" — detay penceresi kartı tazelenen veride bununla bulur.</summary>
        public string Anahtar { get; set; } = string.Empty;

        public int FirmaId { get; set; }
        public string FirmaAdi { get; set; } = string.Empty;
        public string? SorumluKullaniciAdi { get; set; }

        /// <summary>Kartın ayı, "2026-10".</summary>
        public string DonemAnahtari { get; set; } = string.Empty;
        public string DonemEtiketi { get; set; } = string.Empty;

        /// <summary>Hepsi yapıldıysa Tamam; yoksa açık maddelerin EN ERKEN son gününün kovası.</summary>
        public PanoKovasi Durum { get; set; }

        public int Toplam { get; set; }
        public int Tamam { get; set; }
        public int Gecikmis { get; set; }
        public bool YasalVar { get; set; }

        /// <summary>Son günü en erken açık madde; hepsi yapıldıysa boş.</summary>
        public string? SiradakiBaslik { get; set; }
        public DateTime? SiradakiSonGun { get; set; }
        public PanoKovasi? SiradakiKova { get; set; }

        /// <summary>
        /// Aşama 3: maddelerinin geldiği şablonların adları (etiket). Hiçbir maddesi şablondan değilse boş — etiket yok.
        /// Şablon kartı BÖLMEZ; kart yine firma + dönemdir.
        /// </summary>
        public List<string> Sablonlar { get; set; } = new();

        public List<ChecklistBolumuDto> Bolumler { get; set; } = new();
    }

    /// <summary>Kartlar görünümünde bir dönem grubu (ay).</summary>
    public class ChecklistDonemiDto
    {
        /// <summary>"2026-10".</summary>
        public string Anahtar { get; set; } = string.Empty;
        public string Etiket { get; set; } = string.Empty;
        public int FirmaSayisi { get; set; }
        public int Toplam { get; set; }
        public int Tamam { get; set; }
        public int Gecikmis { get; set; }
        public List<ChecklistKartiDto> Kartlar { get; set; } = new();
    }

    /// <summary>Liste görünümünün satırı: Yapılacaklar'ın grubu ("3 firma") + bölüm kovası.</summary>
    public class PanoListeSatiriDto
    {
        public PanoKovasi Kova { get; set; }
        public IsGrubuDto Grup { get; set; } = new();
    }

    /// <summary>Sorumlu hapının seçeneği. <see cref="Id"/> 0 = sorumlusu olmayan.</summary>
    public class PanoSorumluSecenegiDto
    {
        public int Id { get; set; }
        public string Ad { get; set; } = string.Empty;

        /// <summary>Firma ve tür filtresinden sonra, sorumlu filtresinden ÖNCE madde sayısı.</summary>
        public int Sayi { get; set; }
    }

    public class FirmaPanosuDto
    {
        public DateTime Bugun { get; set; }
        public int? FirmaId { get; set; }

        /// <summary>Gecikti · Bu ay · Sonra · Tamamlanan — bu sırada, filtreden SONRA.</summary>
        public List<PanoSayaciDto> Sayaclar { get; set; } = new();

        /// <summary>Kartlar görünümü: dönem grupları eskiden yeniye.</summary>
        public List<ChecklistDonemiDto> Donemler { get; set; } = new();

        /// <summary>Liste görünümü: <c>YapilacaklarKurucu.Grupla</c>'nın çıktısı, aynı sırada.</summary>
        public List<PanoListeSatiriDto> Liste { get; set; } = new();

        public List<PanoSorumluSecenegiDto> Sorumlular { get; set; } = new();

        /// <summary>En az bir kartı olan firma sayısı.</summary>
        public int FirmaSayisi { get; set; }

        public int KartSayisi { get; set; }
    }
}
