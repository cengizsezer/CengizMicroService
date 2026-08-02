namespace WebApp.Shared.Dto.FirmaKontrol
{
    /// <summary>Beyanname bölümü. Sunucudaki <c>VergiKalemGrubu</c> ile aynı sayısal değerler.</summary>
    public enum VergiKalemGrubu : byte
    {
        Kkeg = 1,
        ZararOlsaDahi = 2,
        KazancVarsa = 3,
        Mahsup = 4
    }

    public enum UstSinirTuru : byte
    {
        Yok = 0,
        KurumKazanciYuzdesi = 1,
        SabitTutar = 2
    }

    public enum MukellefiyetTuru : byte
    {
        GelirVergisi = 1,
        KurumlarVergisi = 2,
        Ikisi = 3
    }

    public enum VergiUyariSeviyesi : byte
    {
        Bilgi = 0,
        Uyari = 1,
        Hata = 2
    }

    // ── Kalem katalogu ──

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

    public class VergiKalemSiraDto
    {
        public int KalemId { get; set; }
        public short SiraNo { get; set; }
    }

    // ── Beyanname girdileri ──

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

    // ── Hesaplama sonucu ──

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

        public decimal GirilenTutar { get; set; }
        public decimal EfektifTutar { get; set; }
        public decimal IliskiliKkeg { get; set; }
        public bool MatrahiArtirir { get; set; }
        public decimal SinirAsimi { get; set; }
        public decimal? UstSinirTutari { get; set; }
        public decimal KullanilamayanTutar { get; set; }
        public decimal DevredenTutar { get; set; }
        public decimal YananTutar { get; set; }
        public string? Aciklama { get; set; }
    }

    public class ZararMahsupSatirDto
    {
        public short ZararYili { get; set; }
        public decimal ZararTutari { get; set; }
        public decimal MahsupEdilen { get; set; }
        public decimal DevredenTutar { get; set; }
        public bool MahsupEdilebilir { get; set; }
        public string? Uyari { get; set; }
    }

    public class VergiUyariDto
    {
        public VergiUyariSeviyesi Seviye { get; set; }
        public string? KalemKodu { get; set; }
        public string Mesaj { get; set; } = string.Empty;
    }

    public class VergiSonucDto
    {
        public decimal TicariKar { get; set; }

        public List<VergiSonucSatirDto> Ilaveler { get; set; } = new();
        public decimal IlaveHamToplam { get; set; }
        public decimal IlaveMatrahaEtkiEden { get; set; }
        public decimal KarVeIlavelerToplami { get; set; }

        public List<VergiSonucSatirDto> ZararOlsaDahiIndirimler { get; set; } = new();
        public decimal ZararOlsaDahiToplam { get; set; }
        public decimal KarZarar { get; set; }

        public List<ZararMahsupSatirDto> ZararMahsuplari { get; set; } = new();
        public decimal ZararMahsupToplami { get; set; }
        public decimal MahsupSonrasiKazanc { get; set; }

        public List<VergiSonucSatirDto> KazancVarsaIndirimler { get; set; } = new();
        public decimal KazancVarsaToplam { get; set; }
        public decimal KurumKazanci { get; set; }

        public decimal Matrah { get; set; }
        public decimal IndirimliOranMatrahi { get; set; }
        public decimal GenelOranMatrahi { get; set; }
        public decimal NormalVergi { get; set; }

        public bool AsgariKvHesaplandi { get; set; }
        public decimal AsgariMatrah { get; set; }
        public decimal AsgariVergi { get; set; }
        public bool AsgariUygulandi { get; set; }
        public decimal HesaplananVergi { get; set; }

        public List<VergiSonucSatirDto> Mahsuplar { get; set; } = new();
        public decimal MahsupToplami { get; set; }
        public decimal OdenecekVergi { get; set; }

        public List<VergiUyariDto> Uyarilar { get; set; } = new();
    }
}
