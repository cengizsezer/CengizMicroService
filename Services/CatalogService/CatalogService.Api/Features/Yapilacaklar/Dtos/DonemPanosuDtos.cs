using CatalogService.Api.Features.Yapilacaklar.Domain;

namespace CatalogService.Api.Features.Yapilacaklar.Dtos
{
    /// <summary>
    /// Matris hücresinin durumu. ÜÇÜ AYRI: "eksik" ile "bu firmada yok" aynı gösterilirse
    /// pano yanıltır (CUBIC'te rapor gönderimi diye bir iş yoksa o eksik değildir).
    /// </summary>
    public enum DonemHucreDurumu : byte
    {
        /// <summary>İş bu firmada bu dönemde geçerli değil.</summary>
        Yok = 0,
        Eksik = 1,
        Yapildi = 2
    }

    /// <summary>Matrisin kolonu: o dönemde en az bir firmada geçerli olan iş.</summary>
    public class DonemPanosuKolonDto
    {
        /// <summary>"Y|0015|2" (yasal: kod + tekrar) ya da "O|BORDRO|2" (özel: başlık + tekrar).</summary>
        public string Anahtar { get; set; } = string.Empty;

        public string Baslik { get; set; } = string.Empty;
        public IsKaynagi KaynakTip { get; set; }
        public IsTekrari Tekrar { get; set; }
        public string? MukellefiyetKodu { get; set; }
    }

    public class DonemPanosuHucreDto
    {
        public DonemHucreDurumu Durum { get; set; }

        // Yok'ta boş; diğerlerinde işaretleme ve hücre paneli için kimlik.
        public IsKaynagi KaynakTip { get; set; }
        public int KaynakId { get; set; }
        public string DonemAnahtari { get; set; } = string.Empty;

        /// <summary>Dönem ekleri (kanıt); varsa köşede ataç. Yapılmamış dönemde de olabilir.</summary>
        public int EkSayisi { get; set; }

        /// <summary>
        /// Eksik hücrede not ya da kanıt varsa düz boş hücreden ayrılır ("yapılmadı · notu var");
        /// yine de EKSİK sayılır — not yazmak işi yapmış saymaz.
        /// </summary>
        public bool NotVar { get; set; }
    }

    public class DonemPanosuSatirDto
    {
        public int FirmaId { get; set; }
        public string FirmaAdi { get; set; } = string.Empty;

        /// <summary>Kolonlarla aynı sırada; firmada geçerli olmayan kolonda <see cref="DonemHucreDurumu.Yok"/>.</summary>
        public List<DonemPanosuHucreDto> Hucreler { get; set; } = new();

        public int Gecerli { get; set; }
        public int Yapilan { get; set; }
    }

    /// <summary>Eksiklerin yoğunlaştığı iş: "Fatura girişi 3 firmada".</summary>
    public class DonemEksikOzetiDto
    {
        public string Baslik { get; set; } = string.Empty;
        public int FirmaSayisi { get; set; }
    }

    public class DonemPanosuDto
    {
        public int Yil { get; set; }
        public int Ay { get; set; }

        /// <summary>"Ağustos 2026".</summary>
        public string Etiket { get; set; } = string.Empty;

        public List<DonemPanosuKolonDto> Kolonlar { get; set; } = new();
        public List<DonemPanosuSatirDto> Satirlar { get; set; } = new();

        /// <summary>Geçerli hücre sayısı (Yok hariç) ve yapılanlar — üst şeridin "54 / 62"si.</summary>
        public int Toplam { get; set; }
        public int Yapilan { get; set; }

        /// <summary>Eksik sayısına göre azalan.</summary>
        public List<DonemEksikOzetiDto> Eksikler { get; set; } = new();

        /// <summary>
        /// Firma filtresi (Prompt 14). Doluysa matris, satır listesi ve YUKARIDAKİ SAYILAR yalnız bu
        /// firmayı sayar — şerit ekrandakiyle çelişmesin.
        /// </summary>
        public int? FirmaId { get; set; }

        /// <summary>Dönem sheet'inin satır listesi: matrisin aynı hücreleri, firma × iş. Sıra durumdan bağımsız.</summary>
        public List<DonemIsSatiriDto> Isler { get; set; } = new();
    }

    /// <summary>Dönem sheet'inin bir satırı (Prompt 14): bir firmanın bir işinin bu dönemi.</summary>
    public class DonemIsSatiriDto
    {
        public int FirmaId { get; set; }
        public string FirmaAdi { get; set; } = string.Empty;

        /// <summary>Matristeki kolonun anahtarı ("Y|0015|2").</summary>
        public string KolonAnahtari { get; set; } = string.Empty;

        public string Baslik { get; set; } = string.Empty;
        public IsKaynagi KaynakTip { get; set; }
        public int KaynakId { get; set; }
        public string DonemAnahtari { get; set; } = string.Empty;
        public string? MukellefiyetKodu { get; set; }
        public IsTekrari Tekrar { get; set; }

        /// <summary>Eksik ya da Yapildi (satır listesinde Yok olmaz).</summary>
        public DonemHucreDurumu Durum { get; set; }
        public int EkSayisi { get; set; }
        public bool NotVar { get; set; }

        /// <summary>Yasalda takvimin son günü, özelde işin gün kuralı (Yapılacaklar'la aynı hesap).</summary>
        public DateTime? SonGun { get; set; }

        /// <summary>Program: özel işte kendi alanı, yasalda prosedür satırının; boş = "program yok".</summary>
        public int? SistemId { get; set; }
        public string? SistemAdi { get; set; }

        public List<string> Kime { get; set; } = new();
        public List<string> Bilgi { get; set; } = new();
    }

    /// <summary>Bir önceki dönemin notu ve kanıtı — "geçen ay nasıl yapmıştım".</summary>
    public class GecenDonemDto
    {
        public string DonemAnahtari { get; set; } = string.Empty;
        public string? DonemEtiketi { get; set; }

        /// <summary>Geçen dönem yapılmamış olabilir; notu yine gösterilir ("neden gecikmişti").</summary>
        public bool Yapildi { get; set; }
        public DateTime? TamamlanmaZamani { get; set; }
        public string? TamamlayanAdi { get; set; }
        public string? Not { get; set; }
        public List<FirmaIsiEkiDto> Ekler { get; set; } = new();
    }

    /// <summary>Hücre paneli.</summary>
    public class DonemHucresiDto
    {
        public int FirmaId { get; set; }
        public string FirmaAdi { get; set; } = string.Empty;
        public string Baslik { get; set; } = string.Empty;

        public IsKaynagi KaynakTip { get; set; }
        public int KaynakId { get; set; }
        public string DonemAnahtari { get; set; } = string.Empty;
        public string? DonemEtiketi { get; set; }

        /// <summary>"Nasıl yapılır" (prosedür paneli) için: yasalda kod, her ikisinde tekrar.</summary>
        public string? MukellefiyetKodu { get; set; }
        public IsTekrari Tekrar { get; set; }

        /// <summary>Dönem kaydı var (yapılmış ya da yalnız not/kanıt) — "Bu dönem kaydını sil" bunun için.</summary>
        public bool KayitVar { get; set; }

        public bool Yapildi { get; set; }
        public DateTime? TamamlanmaZamani { get; set; }
        public string? TamamlayanAdi { get; set; }

        public string? Not { get; set; }
        public List<FirmaIsiEkiDto> Ekler { get; set; } = new();

        /// <summary>"Nasıl yapılır" sekmesinde bu işin tarifi (<c>?sekme=nasil&amp;is=</c>): "y-0015-2" / "o-bordro".</summary>
        public string TarifAnahtari { get; set; } = string.Empty;

        /// <summary>Önceki dönemde not ya da ek yoksa boş — bölüm çizilmez.</summary>
        public GecenDonemDto? GecenDonem { get; set; }
    }

    /// <summary>"Bu döneme not". Yapılmamış dönemde de yazılır (zamanı boş kayıt).</summary>
    public class DonemNotuDto : IsIsaretDto
    {
        public string? Not { get; set; }
    }

    // ---- Takip panosu: sekme şeridi ve Özet sheet (Prompt 14) ----

    /// <summary>Eksik sayısının rengi. Eşik sunucuda tek yerde (<c>DonemSekmeleri.KirmiziEsik</c>).</summary>
    public enum EksikTonu : byte
    {
        /// <summary>Eksik yok (tam ya da geçerli iş yok).</summary>
        Yok = 0,

        /// <summary>Amber: 1 – (eşik − 1).</summary>
        Az = 1,

        /// <summary>Kırmızı: eşik ve üstü.</summary>
        Cok = 2
    }

    /// <summary>Alt şeritteki dönem sekmesi; sayılar o ayın matrisinden.</summary>
    public class DonemSekmesiDto
    {
        public int Yil { get; set; }
        public int Ay { get; set; }

        /// <summary>"2026-09" — adres çubuğundaki <c>?donem=</c>.</summary>
        public string Anahtar { get; set; } = string.Empty;

        /// <summary>"Eylül 2026".</summary>
        public string Etiket { get; set; } = string.Empty;

        public int Toplam { get; set; }
        public int Yapilan { get; set; }

        /// <summary>Eksik hücre sayısı (sekme rozeti).</summary>
        public int Eksik { get; set; }

        /// <summary>En az bir eksiği olan firma sayısı ("3 firmada eksik").</summary>
        public int EksikFirmaSayisi { get; set; }

        public EksikTonu Ton { get; set; }

        /// <summary>Bugünün ayı — işi genelde bitmemiştir.</summary>
        public bool IcindeBulunulan { get; set; }

        /// <summary>Eksiklerin yoğunlaştığı işler, o ayın matrisindekiyle aynı.</summary>
        public List<DonemEksikOzetiDto> Eksikler { get; set; } = new();
    }

    public class DonemOzetHucreDto
    {
        /// <summary>Geçerli iş sayısı; 0 ise o ay bu firmada iş yok.</summary>
        public int Gecerli { get; set; }
        public int Yapilan { get; set; }
        public EksikTonu Ton { get; set; }
    }

    public class DonemOzetSatirDto
    {
        public int FirmaId { get; set; }
        public string FirmaAdi { get; set; } = string.Empty;
        public int? SorumluKullaniciId { get; set; }
        public string? SorumluKullaniciAdi { get; set; }

        /// <summary><see cref="DonemOzetiDto.Donemler"/> ile aynı sırada.</summary>
        public List<DonemOzetHucreDto> Hucreler { get; set; } = new();
    }

    /// <summary>Özet sheet ve sekme şeridi: içinde bulunulan ay + önceki aylar, eskiden yeniye.</summary>
    public class DonemOzetiDto
    {
        public List<DonemSekmesiDto> Donemler { get; set; } = new();
        public List<DonemOzetSatirDto> Satirlar { get; set; } = new();

        /// <summary>Varsayılan açılan sekme: bir önceki ay ("2026-09").</summary>
        public string VarsayilanAnahtar { get; set; } = string.Empty;

        /// <summary>Bilgi için (lejant); renk kararı <see cref="EksikTonu"/>'ndadır.</summary>
        public int KirmiziEsik { get; set; }
    }

    /// <summary>Dönem eki (kanıt). Dosya ÖNCE FileApiService'e yüklenir.</summary>
    public class DonemEkiOlusturDto : IsIsaretDto
    {
        public int FileId { get; set; }
        public string DosyaAdi { get; set; } = string.Empty;
        public string? ContentType { get; set; }
        public long Boyut { get; set; }
    }
}
