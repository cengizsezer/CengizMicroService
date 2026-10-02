namespace WebApp.Shared.Dto.Yapilacaklar
{
    // Sunucudaki CatalogService.Api.Features.Yapilacaklar.Dtos.IsTarifiDtos'un aynası (Prompt 14).

    /// <summary>Sol listenin satırı: bir iş (tarif grubu).</summary>
    public class IsTarifiOzetDto
    {
        /// <summary>"y-0015-2" ya da "o-bordro" — adresteki <c>?is=</c>.</summary>
        public string Anahtar { get; set; } = string.Empty;

        public string Ad { get; set; } = string.Empty;
        public IsKaynagi KaynakTip { get; set; }
        public string? MukellefiyetKodu { get; set; }

        /// <summary>Ritim (grupta karışıksa en sık olanı).</summary>
        public IsTekrari Tekrar { get; set; }

        /// <summary>İşin geçerli olduğu firma sayısı (özelde aktif iş, yasalda mükellefiyet kodu).</summary>
        public int FirmaSayisi { get; set; }

        /// <summary>İşin geçerli olduğu firmalar (firma seçimiyle süzme).</summary>
        public List<int> FirmaIdleri { get; set; } = new();

        public int? TarifId { get; set; }

        /// <summary>Tarifin ek sayısı.</summary>
        public int EkSayisi { get; set; }

        /// <summary>Metni tariften FARKLI olduğu için bağlanmamış kayıt sayısı → amber "farklı tarif var".</summary>
        public int FarkliSayisi { get; set; }

        /// <summary>Metni boş/aynı ama bağlanmamış kayıt → gri "tarife bağlanabilir · N firma".</summary>
        public int BaglanabilirSayisi { get; set; }
    }

    /// <summary>Bir firmanın bu işle ilişkisi (detayın firma satırı).</summary>
    public enum TarifBaglantisi : byte
    {
        /// <summary>Tarife bağlı (<c>IsTarifiId</c>).</summary>
        Bagli = 1,

        /// <summary>Metni tariften FARKLI, bağlanmadı; kendi metniyle çalışıyor. Kullanıcı karar verir.</summary>
        Farkli = 2,

        /// <summary>Metni yok, tarife bağlı değil (sonradan eklenmiş özel iş). Kendiliğinden bağlanmaz.</summary>
        Bagsiz = 3,

        /// <summary>Yasal iş, firmanın kendi prosedür satırı yok ya da metni boş: ortak tarifi kullanır.</summary>
        Ortak = 4
    }

    public class IsTarifiFirmaDto
    {
        public int FirmaId { get; set; }
        public string FirmaAdi { get; set; } = string.Empty;

        /// <summary>İşin firmadaki satırı; yasalda prosedür satırı açılmamışsa boş.</summary>
        public int? FirmaIsiId { get; set; }

        public TarifBaglantisi Baglanti { get; set; }

        /// <summary>Firmanın kendi metni (<c>FirmaIsi.MenuYolu/NasilYapilir</c>) — silinmedi.</summary>
        public string? MenuYolu { get; set; }
        public string? NasilYapilir { get; set; }
        public List<string> Adimlar { get; set; } = new();

        /// <summary>Bağlıyken kendi metni tariften farklı → "Bu firmada farklı".</summary>
        public bool FirmayaOzel { get; set; }

        public string? SistemAdi { get; set; }
        public List<FirmaIsiAlicisiDto> Alicilar { get; set; } = new();
    }

    public class IsTarifiDto
    {
        public int Id { get; set; }
        public string Ad { get; set; } = string.Empty;
        public int? SistemId { get; set; }
        public string? SistemAdi { get; set; }
        public string? MenuYolu { get; set; }
        public string? NasilYapilir { get; set; }
        public List<string> Adimlar { get; set; } = new();
        public List<FirmaIsiEkiDto> Ekler { get; set; } = new();
    }

    /// <summary>Sağ taraf: nerede · kime · nasıl · ekler.</summary>
    public class IsTarifiDetayDto
    {
        public string Anahtar { get; set; } = string.Empty;
        public string Ad { get; set; } = string.Empty;
        public IsKaynagi KaynakTip { get; set; }
        public string? MukellefiyetKodu { get; set; }
        public IsTekrari Tekrar { get; set; }

        /// <summary>Boş = "tarif yok".</summary>
        public IsTarifiDto? Tarif { get; set; }

        /// <summary>Tarif yokken grupta metni dolu kayıt var mı ("Tarif oluştur" anlamlı mı).</summary>
        public bool UretilebilirMi { get; set; }

        public List<IsTarifiFirmaDto> Firmalar { get; set; } = new();
    }

    public class IsTarifiKaydetDto
    {
        public string Ad { get; set; } = string.Empty;
        public int? SistemId { get; set; }
        public string? MenuYolu { get; set; }
        public string? NasilYapilir { get; set; }
    }

    /// <summary>Tarif üretim raporu: seed ve "Tarif oluştur" aynı raporu döner.</summary>
    public class TarifUretimRaporuDto
    {
        public int GrupSayisi { get; set; }
        public int UretilenTarif { get; set; }
        public int BaglananKayit { get; set; }
        public List<TarifFarkiDto> Baglanmayanlar { get; set; } = new();
    }

    public class TarifFarkiDto
    {
        public string Baslik { get; set; } = string.Empty;
        public List<string> Firmalar { get; set; } = new();
        public int KayitSayisi { get; set; }
        public int FarkliMetinSayisi { get; set; }
    }
}
