namespace CatalogService.Api.Features.FinansmanGiderKisitlamasi.Dtos
{
    /// <summary>
    /// Ekrandaki dört giriş alanı. Hesaplanan beş satır sunucuda üretilir
    /// (<see cref="Services.FinansmanGiderKisitlamasiMotoru"/>).
    /// </summary>
    public class FinansmanKisitlamaHesapRequest
    {
        /// <summary>Kısıtlama oranının alınacağı hesap yılı.</summary>
        public int Yil { get; set; }

        /// <summary>1. satır — özsermaye tutarı. Zorunlu; negatifse sıfır kabul edilir.</summary>
        public decimal? Ozsermaye { get; set; }

        /// <summary>2. satır — yabancı kaynak toplamı (Aktif − Özsermaye).</summary>
        public decimal YabanciKaynakToplami { get; set; }

        /// <summary>5. satır — finansman gider tutarı (780, 660, 656 vb.).</summary>
        public decimal FinansmanGideri { get; set; }

        /// <summary>
        /// 6. satır — örtülü sermayeye ait finansman gideri (KKEG) / aynı yabancı kaynak
        /// nedeniyle elde edilen finansman geliri.
        /// </summary>
        public decimal OrtuluSermayeVeFinansmanGeliri { get; set; }
    }

    /// <summary>TÜRMOB formundaki dokuz satır, ekrandaki sırayla.</summary>
    public class FinansmanKisitlamaSonucDto
    {
        public int Yil { get; set; }

        /// <summary>O yıl için geçerli kısıtlama oranı, yüzde (10 = %10).</summary>
        public decimal KisitlamaOrani { get; set; }

        /// <summary>1. satır — negatif girildiyse sıfırlanmış hâli.</summary>
        public decimal Ozsermaye { get; set; }

        /// <summary>2. satır.</summary>
        public decimal YabanciKaynakToplami { get; set; }

        /// <summary>3. satır — <c>2 − 1</c>. Sıfır veya negatifse kısıtlama yapılmaz.</summary>
        public decimal AsanYabanciKaynak { get; set; }

        /// <summary>4. satır — <c>3 ÷ 2</c>, yüzde.</summary>
        public decimal AsanKisimOrani { get; set; }

        /// <summary>5. satır.</summary>
        public decimal FinansmanGideri { get; set; }

        /// <summary>6. satır.</summary>
        public decimal OrtuluSermayeVeFinansmanGeliri { get; set; }

        /// <summary>7. satır — <c>5 − 6</c>, negatifse sıfır.</summary>
        public decimal DikkateAlinacakFinansmanGideri { get; set; }

        /// <summary>8. satır — <c>4 × 7</c>.</summary>
        public decimal AsanKismaIsabetEdenGider { get; set; }

        /// <summary>9. satır — <c>8 × kısıtlama oranı</c>. KKEG olacak tutar.</summary>
        public decimal Kkeg { get; set; }

        /// <summary>Yabancı kaynak özsermayeyi aşıyor mu; false ise 4–9. satırlar sıfırdır.</summary>
        public bool KisitlamaVar { get; set; }

        /// <summary>Kısıtlama yapılmadığında ekranda gösterilecek gerekçe; aksi hâlde null.</summary>
        public string? Aciklama { get; set; }
    }

    public class FinansmanKisitlamaOraniDto
    {
        public int Id { get; set; }
        public int Yil { get; set; }
        public decimal Oran { get; set; }
        public string? Dayanak { get; set; }
        public string? Not { get; set; }
        public DateTime? GuncellenmeTarihi { get; set; }
    }

    /// <summary>Oran yazma/güncelleme gövdesi; yıl adresten gelir.</summary>
    public class FinansmanKisitlamaOraniSaveDto
    {
        public decimal Oran { get; set; }
        public string? Dayanak { get; set; }
        public string? Not { get; set; }
    }
}
