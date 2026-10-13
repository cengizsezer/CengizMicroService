using CatalogService.Api.Features.Yapilacaklar.Domain;

namespace CatalogService.Api.Features.Yapilacaklar.Dtos
{
    // Firma Panosu Aşama 3: checklist şablonları ("iş tanımı paketi"). Şablon yalnız FİRMAYA ÖZEL iş üretir.

    public class ChecklistSablonMaddesiDto
    {
        public int Id { get; set; }
        public string Baslik { get; set; } = string.Empty;
        public IsTekrari Tekrar { get; set; } = IsTekrari.Aylik;
        public IsGunKurali GunKurali { get; set; } = IsGunKurali.DonemSonu;
        public int? AyinGunu { get; set; }
        public int? KategoriId { get; set; }
        public int? IsTarifiId { get; set; }

        // ---- yalnız okuma ----
        public string? KategoriAdi { get; set; }

        /// <summary>Kategori pasif: uygulanırken taşınmaz.</summary>
        public bool KategoriPasif { get; set; }

        public string? IsTarifiAdi { get; set; }

        /// <summary>"her ay · ayın 25'i".</summary>
        public string KuralMetni { get; set; } = string.Empty;
    }

    public class ChecklistSablonuDto
    {
        public int Id { get; set; }
        public string Ad { get; set; } = string.Empty;
        public string? Aciklama { get; set; }
        public bool Aktif { get; set; }

        /// <summary>Maddelerin farklı (aktif) kategori sayısı; kategorisiz maddeler "Diğer" olarak bir sayılır.</summary>
        public int KategoriSayisi { get; set; }

        /// <summary>Bu şablondan oluşmuş FirmaIsi sayısı; &gt; 0 ise şablon silinmez, pasife alınır.</summary>
        public int KullanimSayisi { get; set; }

        /// <summary>Sıralı.</summary>
        public List<ChecklistSablonMaddesiDto> Maddeler { get; set; } = new();
    }

    /// <summary>Ekle / güncelle. Madde sırası listenin sırasıdır.</summary>
    public class ChecklistSablonuKaydetDto
    {
        public string Ad { get; set; } = string.Empty;
        public string? Aciklama { get; set; }
        public bool Aktif { get; set; } = true;
        public List<ChecklistSablonMaddesiDto> Maddeler { get; set; } = new();
    }

    public class ChecklistSablonSilSonucDto
    {
        public bool Silindi { get; set; }

        /// <summary>Kullanılmış şablon silinmedi, pasife alındı.</summary>
        public bool PasifeAlindi { get; set; }

        public string Mesaj { get; set; } = string.Empty;
    }

    // ---- Şablondan iş oluşturma ----

    public class SablondanIstekDto
    {
        public int SablonId { get; set; }
        public List<int> FirmaIdleri { get; set; } = new();

        /// <summary>"2026-10" — oluşan bütün işlerin başlangıç dönemi.</summary>
        public string? IlkDonem { get; set; }
    }

    /// <summary>Atlanan madde: başlık + sebep ("bu firmada aynı başlık ve tekrarla iş var").</summary>
    public class SablonAtlananDto
    {
        public string Baslik { get; set; } = string.Empty;
        public IsTekrari Tekrar { get; set; }
        public string Sebep { get; set; } = string.Empty;
    }

    public class SablonFirmaSonucuDto
    {
        public int FirmaId { get; set; }
        public string FirmaAdi { get; set; } = string.Empty;

        /// <summary>Eklenecek (önizleme) / eklenen (yazma) maddelerin başlıkları, şablon sırasıyla.</summary>
        public List<string> Eklenenler { get; set; } = new();

        public List<SablonAtlananDto> Atlananlar { get; set; } = new();
    }

    public class SablonKategoriSayisiDto
    {
        public string Ad { get; set; } = string.Empty;
        public int Sayi { get; set; }
    }

    /// <summary>Önizleme ve yazmanın ortak sonucu (aynı fonksiyondan, <c>ChecklistSablonPlanlayici.Planla</c>).</summary>
    public class SablondanSonucDto
    {
        public int SablonId { get; set; }
        public string SablonAdi { get; set; } = string.Empty;

        /// <summary>Normalize edilmiş başlangıç dönemi ("2026-10").</summary>
        public string? IlkDonem { get; set; }

        /// <summary>Şablonun maddeleri kategori bazında (oluşacak işin çözülen kategorisi; "Diğer" en sonda).</summary>
        public List<SablonKategoriSayisiDto> Kategoriler { get; set; } = new();

        public List<SablonFirmaSonucuDto> Firmalar { get; set; } = new();

        /// <summary>false = önizleme; true = yazıldı.</summary>
        public bool Yazildi { get; set; }
    }

    // ---- Firmadan şablon ----

    public class FirmadanSablonDto
    {
        public int FirmaId { get; set; }
        public string Ad { get; set; } = string.Empty;
        public string? Aciklama { get; set; }
    }

    /// <summary>Firmadan şablon önizlemesi: şablona girecek maddeler ve girmeyen işler (sebebiyle).</summary>
    public class FirmadanSablonOnizlemeDto
    {
        public int FirmaId { get; set; }
        public string FirmaAdi { get; set; } = string.Empty;
        public List<ChecklistSablonMaddesiDto> Maddeler { get; set; } = new();
        public List<SablonAtlananDto> Disarida { get; set; } = new();
    }
}
