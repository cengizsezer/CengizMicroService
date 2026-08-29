namespace CatalogService.Api.Features.Declarations.Dtos
{
    /// <summary>
    /// Özet matrisinin bir hücresinin durumu. Sıra <b>ilerleyiş sırası</b>: sayısal
    /// değerler sözleşmenin parçası, istemci renkleri buna göre veriyor.
    /// </summary>
    public enum BeyannameHucreDurum : byte
    {
        /// <summary>O firmanın o dönemde o türden beyannamesi yok.</summary>
        Yok = 0,

        /// <summary>Kayıt var, henüz onaylanmamış (taslak / hazırlanıyor / hazır).</summary>
        Hazirlandi = 1,

        /// <summary>Onaylanmış ya da gönderilmiş, ödemesi bekliyor.</summary>
        Onaylandi = 2,

        /// <summary>Ödenmiş.</summary>
        Odendi = 3
    }

    public class BeyannameTuruDto
    {
        public int Id { get; set; }
        public string Deger { get; set; } = string.Empty;
        public string? Kod { get; set; }
        public string Ad { get; set; } = string.Empty;
        public int Sira { get; set; }
        public bool Aktif { get; set; }
    }

    /// <summary>
    /// Tanımlar ekranından gelen yazma isteği. <c>Deger</c> beyanname kayıtlarındaki
    /// <c>DeclarationType</c> metniyle eşleşen alandır; ad ve kod yalnız görünümü etkiler.
    /// </summary>
    public class BeyannameTuruYazDto
    {
        public string Deger { get; set; } = string.Empty;
        public string? Kod { get; set; }
        public string Ad { get; set; } = string.Empty;
        public int Sira { get; set; }
        public bool Aktif { get; set; } = true;
    }

    /// <summary>Matrisin tek hücresi: bir firmanın bir türdeki durumu.</summary>
    public class BeyannameOzetHucreDto
    {
        public int TuruId { get; set; }

        /// <summary>Hücreye karşılık gelen beyanname kaydı; yoksa null (hücre tıklanınca yeni kayıt açılır).</summary>
        public int? DeclarationId { get; set; }

        public BeyannameHucreDurum Durum { get; set; }

        public decimal Tutar { get; set; }

        /// <summary>Aynı firmada aynı türden birden fazla kayıt varsa sayısı; normalde 1.</summary>
        public int KayitSayisi { get; set; }

        /// <summary>Bağlı belgelerin türleri; ikonlar dolu/soluk buna göre çizilir.</summary>
        public List<BeyannameEkTuruDto> Ekler { get; set; } = new();

        /// <summary>
        /// Hücrenin arkasındaki beyanname kayıtları. Hücreye tıklanınca açılan detay
        /// bunları gösteriyor; ayrı bir "beyanname getir" ucu eklemek yerine matrisle
        /// birlikte geliyorlar — matris zaten o kayıtları okuyor ve dönem başına
        /// birkaç yüz satırdan fazlası olmuyor.
        /// </summary>
        public List<DeclarationDto> Kayitlar { get; set; } = new();
    }

    /// <summary>Hücrede hangi belgenin bulunduğunu söyleyen küçük özet.</summary>
    public class BeyannameEkTuruDto
    {
        public int EkId { get; set; }
        public Entities.BeyannameEkTuru Tur { get; set; }
    }

    /// <summary>Matrisin bir satırı: firma + hücreleri + satır toplamı.</summary>
    public class BeyannameOzetSatirDto
    {
        public int Sira { get; set; }
        public int CustomerCompanyId { get; set; }
        public string FirmaAdi { get; set; } = string.Empty;
        public string? VergiKimlikNo { get; set; }

        public List<BeyannameOzetHucreDto> Hucreler { get; set; } = new();

        /// <summary>Satırdaki dolu hücre sayısı (Excel'deki "Toplam" kolonu).</summary>
        public int DoluHucreSayisi { get; set; }

        public decimal ToplamTutar { get; set; }
    }

    /// <summary>Bir tür kolonunun alt toplamı.</summary>
    public class BeyannameOzetKolonToplamDto
    {
        public int TuruId { get; set; }
        public int DoluHucreSayisi { get; set; }
        public decimal ToplamTutar { get; set; }
    }

    /// <summary>Firma × beyanname türü matrisi.</summary>
    public class BeyannameOzetDto
    {
        public int Yil { get; set; }
        public int Ay { get; set; }

        /// <summary>Kolonlar: aktif beyanname türleri, tanım tablosundaki sırayla.</summary>
        public List<BeyannameTuruDto> Turler { get; set; } = new();

        public List<BeyannameOzetSatirDto> Satirlar { get; set; } = new();

        public List<BeyannameOzetKolonToplamDto> KolonToplamlari { get; set; } = new();

        public int ToplamBeyanname { get; set; }
        public decimal ToplamTutar { get; set; }

        /// <summary>
        /// Tanım tablosundaki hiçbir türe uymayan beyanname türü metinleri. Sessizce
        /// düşürülmez: kolon yoksa kayıt matriste hiç görünmez ve kullanıcı eksiği fark etmez.
        /// </summary>
        public List<string> EslesmeyenTurler { get; set; } = new();
    }
}
