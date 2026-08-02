namespace CatalogService.Api.Features.FirmaKontrol.Domain
{
    /// <summary>
    /// Beyanname kalemi tanımı (katalog). Firmadan bağımsızdır: seed ile gelen sistem
    /// kalemleri ve kullanıcının eklediği kalemler burada durur.
    /// Oran ve tutarsal sınırlar koda gömülmez, bu tablodan okunur.
    /// </summary>
    public class VergiKalemi
    {
        public int Id { get; set; }

        /// <summary>Kalem kodu (ör. "KKEG-03", "IST-17"). Katalog genelinde tekildir.</summary>
        public string Kod { get; set; } = string.Empty;

        public string Ad { get; set; } = string.Empty;

        public VergiKalemGrubu Grup { get; set; }

        /// <summary>Ekranda kalemleri kümeleyen serbest başlık (ör. "Binek otomobil kısıtlamaları").</summary>
        public string? AltGrup { get; set; }

        /// <summary>Dayanak (ör. "KVK 11/1-d"). Ekranda kalem adının yanında soluk gösterilir.</summary>
        public string? KanunMaddesi { get; set; }

        /// <summary>Kalemin ne olduğunu anlatan açıklama.</summary>
        public string? Aciklama { get; set; }

        /// <summary>Kullanıcıyı uyaran kontrol sorusu; tutar girilmiş kalemlerde her zaman görünür.</summary>
        public string? Hatirlatma { get; set; }

        /// <summary>Oranın metinle anlatımı (ör. "Kurum kazancının %5'i"). Hesaplamada kullanılmaz.</summary>
        public string? OranBilgisi { get; set; }

        public UstSinirTuru? UstSinirTuru { get; set; }

        /// <summary>
        /// <see cref="UstSinirTuru"/> yüzdeyse yüzde değeri (5 = %5), sabit tutarsa tavan tutarı.
        /// </summary>
        public decimal? UstSinirDeger { get; set; }

        /// <summary>Kullanılamayan kısım gelecek yıllara devreder (Ar-Ge, nakdi sermaye indirimi).</summary>
        public bool DevredebilirMi { get; set; }

        /// <summary>
        /// KKEG türü (b): istisna kapsamındaki faaliyette oluşan KKEG. Ticari kâra eklenir
        /// ama aynı tutar <see cref="BagliIstisnaKalemiId"/> kalemini büyütür; matraha net etkisi sıfırdır.
        /// </summary>
        public bool IstisnayaIliskinMi { get; set; }

        /// <summary>İstisnaya ilişkin KKEG'in büyüteceği istisna kalemi.</summary>
        public int? BagliIstisnaKalemiId { get; set; }
        public VergiKalemi? BagliIstisnaKalemi { get; set; }

        /// <summary>KVK 32/C: yurt içi asgari kurumlar vergisi matrahından düşülebilen kalem.</summary>
        public bool AsgariMatrahtanDuser { get; set; }

        public MukellefiyetTuru MukellefiyetTuru { get; set; } = MukellefiyetTuru.KurumlarVergisi;

        /// <summary>Grup içi gösterim sırası (sürükle-bırak ile değişir).</summary>
        public short SiraNo { get; set; }

        /// <summary>Seed ile gelen kalem: kodu ve grubu kilitlidir, silinemez.</summary>
        public bool SistemKalemi { get; set; } = true;

        public bool Aktif { get; set; } = true;
    }
}
