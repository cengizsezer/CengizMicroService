namespace CatalogService.Api.Features.KdvBeyanname.Dtos
{
    // ── Tab 2 — Karşılaştırma ──────────────────────────────────────────────

    public class KarsilastirmaSonucuDto
    {
        public string Donem { get; set; } = string.Empty;
        public int GelenFaturaSayisi { get; set; }
        public int YevmiyeFaturaNoSayisi { get; set; }

        // FATURA RED statüsündeki faturalar karşılaştırmadan elenir; sayım bu alanda
        // bilgi amaçlı dönülür (Tab 2'de "Filtrelenmiş (Reddedildi): X" olarak gösterilir).
        public int ReddedilenSayisi { get; set; }

        public List<KarsilastirmaSatiriDto> Eslesen { get; set; } = new();
        public List<KarsilastirmaSatiriDto> Islenmemis { get; set; } = new();
        public List<KarsilastirmaSatiriDto> Fazla { get; set; } = new();
    }

    public class KarsilastirmaSatiriDto
    {
        public string FaturaNo { get; set; } = string.Empty;
        public string? GondericiVkn { get; set; }
        public string? GondericiUnvan { get; set; }
        public DateTime? FaturaTarihi { get; set; }
        public decimal? ToplamTutar { get; set; }
        public decimal? KdvTutari { get; set; }

        // Yevmiye tarafında: ilk eşleşen satırın açıklaması/hesap kodu (referans).
        public string? YevmiyeHesapKodu { get; set; }
        public string? YevmiyeAciklamasi { get; set; }
    }

    // ── Tab 4 — Sonuç (KDV beyannamesi için hesaplanmış değerler) ──────────

    public class KdvSonucDto
    {
        public string Donem { get; set; } = string.Empty;
        public int Yil { get; set; }
        public int Ay { get; set; }

        public BdpEksiklikDto Eksiklikler { get; set; } = new();

        // Ana hesap bakiyeleri (mizan'dan ana hesap satırı; varsa 1-segmentli kod).
        public decimal Hesaplanan391 { get; set; }   // 391 alacak bakiye
        public decimal Indirilecek191 { get; set; }  // 191 borç bakiye
        public decimal Devreden190 { get; set; }     // 190 borç bakiye (önceki dönemden devreden)
        public decimal Satislar600 { get; set; }     // 600 alacak bakiye (toplam matrah)

        // Hesaplanan değerler (README'deki mantık)
        public decimal ToplamIndirilecek { get; set; }   // 191 + 190
        public decimal Fark { get; set; }                 // 391 - (191 + 190)
        public decimal OdenmesiGerekenKDV { get; set; }
        public decimal SonrakiDonemeDevredenKDV { get; set; }

        // Oran bazlı 3-segment kırılımlar (XML'de tevkifatUygulanmayan / indirilecekKDVOD bloklarına gidecek)
        public List<OranKirilimDto> TevkifatUygulanmayanlar { get; set; } = new();  // 600 + 391 birleşimi (matrah + vergi)
        public List<OranKirilimDto> IndirilecekKdvODler { get; set; } = new();      // 191 (bedel + KDVTutari)
    }

    public class OranKirilimDto
    {
        public string HesapKodu { get; set; } = string.Empty;
        public string HesapAdi { get; set; } = string.Empty;
        public int Oran { get; set; }              // 3. segment (1, 10, 20...)
        public decimal Bedel { get; set; }         // 600 / 191 alt bakiyesinden
        public decimal KdvTutari { get; set; }     // 391 ilgili oran satırından (veya bedel*oran/100)
    }

    public class BdpEksiklikDto
    {
        public bool VarMi => FirmaAlanlari.Count > 0
                             || DuzenleyenAlanlari.Count > 0
                             || MizanHatalari.Count > 0;

        public List<string> FirmaAlanlari { get; set; } = new();
        public List<string> DuzenleyenAlanlari { get; set; } = new();

        // Mizan'daki yapısal sorunlar (ana hesap var, 3 segment alt kırılım yok gibi).
        public List<string> MizanHatalari { get; set; } = new();

        // Tutar farkları gibi non-fatal uyarılar.
        public List<string> MizanUyarilari { get; set; } = new();
    }
}
