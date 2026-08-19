using CatalogService.Api.Features.BankaEkstre.Domain;

namespace CatalogService.Api.Features.BankaEkstre.Dtos
{
    // ---- Banka hesabı ----

    public class BankaHesabiDto
    {
        public int Id { get; set; }
        public string BankaAdi { get; set; } = string.Empty;
        public HesapTipi HesapTipi { get; set; }
        public string ParaBirimi { get; set; } = "TRY";
        public string? Iban { get; set; }
        public string OrkaHesapKodu { get; set; } = string.Empty;
        public string ParserTipi { get; set; } = string.Empty;
        public bool Aktif { get; set; }
    }

    public class BankaHesabiYazDto
    {
        public string BankaAdi { get; set; } = string.Empty;
        public HesapTipi HesapTipi { get; set; } = HesapTipi.Vadesiz;
        public string ParaBirimi { get; set; } = "TRY";
        public string? Iban { get; set; }
        public string OrkaHesapKodu { get; set; } = string.Empty;
        public string ParserTipi { get; set; } = string.Empty;
        public bool Aktif { get; set; } = true;
    }

    /// <summary>Kullanıcıya seçtirilecek ayrıştırıcılar (şimdilik yalnız Vakıfbank vadesiz).</summary>
    public class ParserSecenekDto
    {
        public string Tip { get; set; } = string.Empty;
        public string Ad { get; set; } = string.Empty;
    }

    // ---- Ekstre ----

    public class EkstreSayaclariDto
    {
        public int Toplam { get; set; }
        public int Otomatik { get; set; }
        public int OnayBekleyen { get; set; }
        public int Onaylanan { get; set; }
        public int Cozulemeyen { get; set; }
        public int DigerBankada { get; set; }

        /// <summary>Dışa aktarıma engel olan satır sayısı (onay bekleyen + çözülemeyen).</summary>
        public int Eksik => OnayBekleyen + Cozulemeyen;
    }

    public class EkstreYuklemeDto
    {
        public int Id { get; set; }
        public int BankaHesabiId { get; set; }
        public string BankaAdi { get; set; } = string.Empty;
        public string DosyaAdi { get; set; } = string.Empty;
        public DateTime YuklemeTarihi { get; set; }
        public DateTime? DonemBaslangic { get; set; }
        public DateTime? DonemBitis { get; set; }
        public int SatirSayisi { get; set; }
        public YuklemeDurum Durum { get; set; }
        public string? Uyarilar { get; set; }
        public EkstreSayaclariDto Sayaclar { get; set; } = new();
    }

    public class EkstreSatirDto
    {
        public int Id { get; set; }
        public int SiraNo { get; set; }
        public DateTime Tarih { get; set; }
        public Yon Yon { get; set; }
        public decimal Tutar { get; set; }
        public string IslemTipi { get; set; } = string.Empty;
        public string HamAciklama { get; set; } = string.Empty;
        public string? KarsiIban { get; set; }
        public string? KarsiVkn { get; set; }
        public string? Kanal { get; set; }

        public string? UretilenAciklama { get; set; }
        public string? CikarilanUnvan { get; set; }

        public string? OnerilenHesapKodu { get; set; }
        public string? OnerilenHesapAdi { get; set; }
        public decimal GuvenSkoru { get; set; }
        public KaynakKatman KaynakKatman { get; set; }

        public string? IkinciAdayKodu { get; set; }
        public string? IkinciAdayAdi { get; set; }
        public decimal? IkinciAdaySkoru { get; set; }

        public string? OnaylananHesapKodu { get; set; }
        public string? OnaylananHesapAdi { get; set; }
        public SatirDurum Durum { get; set; }
    }

    public class SatirOnaylaDto
    {
        /// <summary>Boşluklu ORKA kodu, ör. "120 D22".</summary>
        public string HesapKodu { get; set; } = string.Empty;
    }

    /// <summary>ORKA'ya aktarılacak tek satır.</summary>
    public class OrkaSatirDto
    {
        public int SiraNo { get; set; }
        public DateTime Tarih { get; set; }
        public string Aciklama { get; set; } = string.Empty;
        public Yon Yon { get; set; }
        public decimal Tutar { get; set; }

        /// <summary>Karşı hesap (cari/gider/banka).</summary>
        public string HesapKodu { get; set; } = string.Empty;
        public string? HesapAdi { get; set; }

        /// <summary>Ekstresi işlenen banka hesabının ORKA kodu (kaydın diğer bacağı).</summary>
        public string BankaHesapKodu { get; set; } = string.Empty;
    }

    public class DisaAktarimSonucDto
    {
        public int EkstreId { get; set; }
        public string DosyaAdi { get; set; } = string.Empty;
        public int SatirSayisi { get; set; }

        /// <summary>Karşı bacağı başka bankada işlendiği için dışarıda bırakılan satır sayısı.</summary>
        public int DigerBankadaAtlanan { get; set; }

        public List<OrkaSatirDto> Satirlar { get; set; } = new();
    }

    // ---- Hesap planı ----

    public class HesapPlaniKaydiDto
    {
        public int Id { get; set; }
        public string Kod { get; set; } = string.Empty;
        public string Ad { get; set; } = string.Empty;
        public string AnaGrup { get; set; } = string.Empty;
        public bool Aktif { get; set; }
    }

    public class HesapPlaniIceAktarimSonucDto
    {
        public int Okunan { get; set; }
        public int Eklenen { get; set; }
        public int Guncellenen { get; set; }
        public int Atlanan { get; set; }
        public List<string> Uyarilar { get; set; } = new();
    }
}
