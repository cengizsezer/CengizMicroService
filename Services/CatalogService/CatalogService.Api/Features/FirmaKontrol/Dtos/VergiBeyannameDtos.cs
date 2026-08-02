using CatalogService.Api.Features.FirmaKontrol.Domain;

namespace CatalogService.Api.Features.FirmaKontrol.Dtos
{
    // ───────────────────────── Kalem katalogu ─────────────────────────

    /// <summary>Beyanname kalemi (okuma).</summary>
    public class VergiKalemiDto
    {
        public int Id { get; set; }
        public string Kod { get; set; } = string.Empty;
        public string Ad { get; set; } = string.Empty;
        public VergiKalemGrubu Grup { get; set; }
        public string? AltGrup { get; set; }
        public string? KanunMaddesi { get; set; }
        public string? Aciklama { get; set; }
        public string? Hatirlatma { get; set; }
        public string? OranBilgisi { get; set; }
        public UstSinirTuru? UstSinirTuru { get; set; }
        public decimal? UstSinirDeger { get; set; }
        public bool DevredebilirMi { get; set; }
        public bool IstisnayaIliskinMi { get; set; }
        public int? BagliIstisnaKalemiId { get; set; }
        public string? BagliIstisnaKod { get; set; }
        public bool AsgariMatrahtanDuser { get; set; }
        public MukellefiyetTuru MukellefiyetTuru { get; set; }
        public short SiraNo { get; set; }
        public bool SistemKalemi { get; set; }
        public bool Aktif { get; set; }
    }

    /// <summary>Kalem ekleme/güncelleme. Sistem kaleminde kod ve grup yok sayılır.</summary>
    public class VergiKalemiYazDto
    {
        public string Kod { get; set; } = string.Empty;
        public string Ad { get; set; } = string.Empty;
        public VergiKalemGrubu Grup { get; set; }
        public string? AltGrup { get; set; }
        public string? KanunMaddesi { get; set; }
        public string? Aciklama { get; set; }
        public string? Hatirlatma { get; set; }
        public string? OranBilgisi { get; set; }
        public UstSinirTuru? UstSinirTuru { get; set; }
        public decimal? UstSinirDeger { get; set; }
        public bool DevredebilirMi { get; set; }
        public bool IstisnayaIliskinMi { get; set; }
        public int? BagliIstisnaKalemiId { get; set; }
        public bool AsgariMatrahtanDuser { get; set; }
        public short SiraNo { get; set; }
    }

    /// <summary>Sürükle-bırak sıralama isteği.</summary>
    public class VergiKalemSiraDto
    {
        public int KalemId { get; set; }
        public short SiraNo { get; set; }
    }

    // ───────────────────────── Beyanname girdileri ─────────────────────────

    public class VergiSatirYazDto
    {
        public int VergiKalemiId { get; set; }
        public decimal Tutar { get; set; }
        public decimal? OncekiDonem { get; set; }
        public string? Aciklama { get; set; }
    }

    public class GecmisYilZarariYazDto
    {
        public short ZararYili { get; set; }
        public decimal ZararTutari { get; set; }
    }

    /// <summary>
    /// Beyanname girdileri (kaydetme ve önizleme ortak). Ticari kâr gelir tablosundan
    /// geldiği için istemci gönderir ama ekranda düzenlenemez.
    /// </summary>
    public class VergiBeyannameYazDto
    {
        public short DonemYil { get; set; }
        public decimal TicariKar { get; set; }
        public decimal KvOrani { get; set; } = 25.00m;
        public decimal? IndirimliOran { get; set; }
        public decimal? IndirimliOranMatrahi { get; set; }
        public bool AsgariKvHesapla { get; set; } = true;
        public string? Notlar { get; set; }

        public List<VergiSatirYazDto> Satirlar { get; set; } = new();
        public List<GecmisYilZarariYazDto> GecmisYilZararlari { get; set; } = new();
    }

    /// <summary>Beyanname girdileri + hesaplanmış sonuç.</summary>
    public class VergiBeyannameDto
    {
        public int Id { get; set; }
        public int FirmaId { get; set; }
        public short DonemYil { get; set; }
        public decimal TicariKar { get; set; }
        public decimal KvOrani { get; set; }
        public decimal? IndirimliOran { get; set; }
        public decimal? IndirimliOranMatrahi { get; set; }
        public bool AsgariKvHesapla { get; set; }
        public string? Notlar { get; set; }
        public DateTime GuncellemeT { get; set; }

        public List<VergiSatirDto> Satirlar { get; set; } = new();
        public List<GecmisYilZarariDto> GecmisYilZararlari { get; set; } = new();

        public VergiSonucDto Sonuc { get; set; } = new();
    }

    public class VergiSatirDto
    {
        public int VergiKalemiId { get; set; }
        public string Kod { get; set; } = string.Empty;
        public decimal Tutar { get; set; }
        public decimal? OncekiDonem { get; set; }
        public string? Aciklama { get; set; }
    }

    public class GecmisYilZarariDto
    {
        public short ZararYili { get; set; }
        public decimal ZararTutari { get; set; }
        public decimal MahsupEdilen { get; set; }
    }

    // ───────────────────────── Hesaplama sonucu ─────────────────────────

    /// <summary>Bir kalemin hesaplamadaki durumu; ekran satırları bunu doğrudan basar.</summary>
    public class VergiSonucSatirDto
    {
        public int VergiKalemiId { get; set; }
        public string Kod { get; set; } = string.Empty;
        public string Ad { get; set; } = string.Empty;
        public string? AltGrup { get; set; }
        public string? KanunMaddesi { get; set; }
        public string? Hatirlatma { get; set; }
        public VergiKalemGrubu Grup { get; set; }
        public short SiraNo { get; set; }

        /// <summary>Kullanıcının girdiği tutar.</summary>
        public decimal GirilenTutar { get; set; }

        /// <summary>
        /// Hesaplamaya giren tutar. Grup 2'de ilişkili KKEG ile büyütülmüş tutar,
        /// Grup 3'te üst sınır ve kalan kazanç sonrası uygulanabilen tutar.
        /// </summary>
        public decimal EfektifTutar { get; set; }

        /// <summary>Grup 2: bu istisnayı büyüten istisnaya ilişkin KKEG toplamı.</summary>
        public decimal IliskiliKkeg { get; set; }

        /// <summary>Grup 1: kalem matrahı artırıyor mu (istisnaya ilişkin olmayanlar).</summary>
        public bool MatrahiArtirir { get; set; }

        /// <summary>Grup 3: üst sınır nedeniyle indirilemeyen tutar.</summary>
        public decimal SinirAsimi { get; set; }

        /// <summary>Üst sınırın hesaplanan tutarsal karşılığı (yüzde ise kurum kazancına uygulanmış hâli).</summary>
        public decimal? UstSinirTutari { get; set; }

        /// <summary>Grup 3: kazanç yetersizliği nedeniyle bu dönem kullanılamayan tutar.</summary>
        public decimal KullanilamayanTutar { get; set; }

        /// <summary>Kullanılamayan tutarın devreden kısmı (DevredebilirMi = true ise).</summary>
        public decimal DevredenTutar { get; set; }

        /// <summary>Kullanılamayan ve devretmeyen, yani hakkı yanan kısım.</summary>
        public decimal YananTutar { get; set; }

        public string? Aciklama { get; set; }
    }

    public class ZararMahsupSatirDto
    {
        public short ZararYili { get; set; }
        public decimal ZararTutari { get; set; }
        public decimal MahsupEdilen { get; set; }
        public decimal DevredenTutar { get; set; }

        /// <summary>5 hesap dönemi sınırı içinde mi.</summary>
        public bool MahsupEdilebilir { get; set; }

        public string? Uyari { get; set; }
    }

    /// <summary>Uyarı önem derecesi; ekranda renk seçimini sürer.</summary>
    public enum VergiUyariSeviyesi : byte
    {
        Bilgi = 0,
        Uyari = 1,
        Hata = 2
    }

    public class VergiUyariDto
    {
        public VergiUyariSeviyesi Seviye { get; set; }
        public string? KalemKodu { get; set; }
        public string Mesaj { get; set; } = string.Empty;
    }

    /// <summary>Beyanname sırasına göre hesaplanmış sonuç. Hiçbir alanı veritabanına yazılmaz.</summary>
    public class VergiSonucDto
    {
        public decimal TicariKar { get; set; }

        // ── İlaveler ──
        public List<VergiSonucSatirDto> Ilaveler { get; set; } = new();

        /// <summary>Beyannameye yazılan ham ilave toplamı (istisnaya ilişkin KKEG dâhil).</summary>
        public decimal IlaveHamToplam { get; set; }

        /// <summary>Matraha net etki eden ilave kısmı (istisnaya ilişkin KKEG hariç).</summary>
        public decimal IlaveMatrahaEtkiEden { get; set; }

        public decimal KarVeIlavelerToplami { get; set; }

        // ── Grup 2: zarar olsa dahi ──
        public List<VergiSonucSatirDto> ZararOlsaDahiIndirimler { get; set; } = new();
        public decimal ZararOlsaDahiToplam { get; set; }
        public decimal KarZarar { get; set; }

        // ── Geçmiş yıl zararları ──
        public List<ZararMahsupSatirDto> ZararMahsuplari { get; set; } = new();
        public decimal ZararMahsupToplami { get; set; }
        public decimal MahsupSonrasiKazanc { get; set; }

        // ── Grup 3: kazanç varsa ──
        public List<VergiSonucSatirDto> KazancVarsaIndirimler { get; set; } = new();
        public decimal KazancVarsaToplam { get; set; }

        /// <summary>Üst sınır hesaplarının tabanı (KVK 10 anlamında kurum kazancı).</summary>
        public decimal KurumKazanci { get; set; }

        // ── Matrah ve vergi ──
        public decimal Matrah { get; set; }

        public decimal IndirimliOranMatrahi { get; set; }
        public decimal GenelOranMatrahi { get; set; }
        public decimal NormalVergi { get; set; }

        public bool AsgariKvHesaplandi { get; set; }
        public decimal AsgariMatrah { get; set; }
        public decimal AsgariVergi { get; set; }

        /// <summary>Asgari vergi normal vergiden yüksek olduğu için mi uygulandı.</summary>
        public bool AsgariUygulandi { get; set; }

        /// <summary>MAX(normal, asgari).</summary>
        public decimal HesaplananVergi { get; set; }

        // ── Mahsuplar ──
        public List<VergiSonucSatirDto> Mahsuplar { get; set; } = new();
        public decimal MahsupToplami { get; set; }

        /// <summary>Pozitifse ödenecek, negatifse iade edilecek vergi.</summary>
        public decimal OdenecekVergi { get; set; }

        public List<VergiUyariDto> Uyarilar { get; set; } = new();
    }
}
