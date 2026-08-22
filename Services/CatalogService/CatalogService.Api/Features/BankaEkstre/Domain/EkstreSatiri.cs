namespace CatalogService.Api.Features.BankaEkstre.Domain
{
    /// <summary>
    /// Ekstrenin tek satırı: ham banka verisi + üretilen açıklama + önerilen/onaylanan karşı hesap.
    /// Tenant izolasyonu bağlı olduğu <see cref="EkstreYukleme"/> üzerinden sağlanır
    /// (Muhasebe modülündeki FisSatir ile aynı yaklaşım).
    /// </summary>
    public class EkstreSatiri
    {
        public int Id { get; set; }

        public int EkstreYuklemeId { get; set; }

        /// <summary>Dosyadaki sıra (1'den başlar).</summary>
        public int SiraNo { get; set; }

        /// <summary>
        /// Satırın kaynak dosyadaki Excel satır numarası. Düzeltilmiş ekstre dışa
        /// aktarımında açıklama hücresi bu numarayla bulunur.
        /// </summary>
        public int KaynakSatirNo { get; set; }

        // ---- Ham banka verisi ----

        public DateTime Tarih { get; set; }
        public Yon Yon { get; set; }

        /// <summary>Her zaman pozitif; işaret <see cref="Yon"/> alanında.</summary>
        public decimal Tutar { get; set; }

        public string IslemTipi { get; set; } = string.Empty;
        public string HamAciklama { get; set; } = string.Empty;

        /// <summary>Açıklamadan çıkarılan karşı IBAN (yalnız rakamlar + TR öneki).</summary>
        public string? KarsiIban { get; set; }

        public string? KarsiVkn { get; set; }

        /// <summary>Bankanın kanal alanı (ATM/İnternet/Şube…). Yalnız bilgi amaçlı.</summary>
        public string? Kanal { get; set; }

        // ---- Üretilenler ----

        /// <summary>Muhasebe açıklaması; ORKA kestiği için 50 karakteri aşmaz.</summary>
        public string? UretilenAciklama { get; set; }

        public string? CikarilanUnvan { get; set; }

        /// <summary>
        /// Öğrenme anahtarının çekirdeği (normalize unvan veya "ISLEM:&lt;işlem tipi&gt;").
        /// Yükleme anında hesaplanır; onayda öğrenme kaydı bu anahtarla yazılır, böylece
        /// satırı çözen kayıt ile güncellenen kayıt aynı olur.
        /// </summary>
        public string? AnahtarCekirdek { get; set; }

        /// <summary>Aile tespit edildiyse çekirdeğe eklenen ayırt edici kelime.</summary>
        public string? AyirtEdiciEk { get; set; }

        // ---- Eşleştirme ----

        public string? OnerilenHesapKodu { get; set; }
        public string? OnerilenHesapAdi { get; set; }

        /// <summary>0..1 arası güven skoru.</summary>
        public decimal GuvenSkoru { get; set; }

        public KaynakKatman KaynakKatman { get; set; } = KaynakKatman.Yok;

        /// <summary>Yakın ikinci aday (fark &lt; 0.05 ise dolu); onay ekranında iki aday da gösterilir.</summary>
        public string? IkinciAdayKodu { get; set; }
        public string? IkinciAdayAdi { get; set; }
        public decimal? IkinciAdaySkoru { get; set; }

        /// <summary>
        /// Aynı unvan ailesinden tüm adaylar (kod|ad|skor satırları). Park Plaza gibi
        /// çok üyeli ailelerde onay ekranı iki adayla yetinmesin diye tutulur.
        /// </summary>
        public string? Adaylar { get; set; }

        /// <summary>
        /// Satır çoklu adayla onaya düştüyse belirsizliği üreten n-gram. Kullanıcı adaylardan
        /// birini seçtiğinde karar bu anahtarla öğrenilir ve aynı belirsizlik bir daha sorulmaz.
        /// </summary>
        public string? BelirsizlikAnahtari { get; set; }

        /// <summary>Belirsizliğin aday kümesi özeti; öğrenilen karar bununla doğrulanır.</summary>
        public string? AdayKumesiOzeti { get; set; }

        public string? OnaylananHesapKodu { get; set; }
        public string? OnaylananHesapAdi { get; set; }
        public DateTime? OnayTarihi { get; set; }
        public string? OnaylayanKullanici { get; set; }

        public SatirDurum Durum { get; set; } = SatirDurum.OnayBekliyor;

        /// <summary>
        /// Grup içi transferin karşı bacağı olan satır (diğer firmanın ekstresinde).
        /// Şimdilik yalnız alan ayrıldı; dolduran mantık yok — ileride çapraz doğrulama
        /// (Aday → SMMM transferi) buradan yürüyecek.
        /// </summary>
        public int? EslesenKarsiSatirId { get; set; }

        public EkstreYukleme? EkstreYukleme { get; set; }

        /// <summary>Dışa aktarılacak kod: onaylanan varsa o, yoksa öneri.</summary>
        public string? EtkinHesapKodu => OnaylananHesapKodu ?? OnerilenHesapKodu;
    }
}
