using CatalogService.Api.Infrastructure.Domain;

namespace CatalogService.Api.Features.BankaEkstre.Domain
{
    /// <summary>
    /// Ekstresi işlenen banka hesabı. Aynı zamanda "banka kayıt defteri" görevi görür:
    /// bankalar arası hareketlerde karşı taraf bu tablodan bulunur (Katman 3).
    /// </summary>
    public class BankaHesabi : FirmaKapsamliEntity
    {
        public int Id { get; set; }

        /// <summary>
        /// Yalnız <b>kısa</b> banka adı, ör. "Vakıfbank". Bankalar arası eşleştirmede
        /// açıklama metninde bu ad aranır; tam hesap adı yazılırsa ("Vakıfbank, Vadeli Tl -
        /// Otomatik Süpürme Hesabı") hiçbir açıklamada geçmediği için eşleşme hiç olmaz.
        /// Hesabın adı ayrı <see cref="HesapAdi"/> alanında durur.
        /// </summary>
        public string BankaAdi { get; set; } = string.Empty;

        /// <summary>
        /// Bankalar arası eşleştirmede açıklamada aranacak ayırt edici anahtarlar,
        /// virgülle ayrılmış (ör. "Otomatik Süpürme, Süpürme"). Aynı bankada birden fazla
        /// hesap olduğu için tek başına <see cref="BankaAdi"/> yetmiyor: "Vakıfbank"
        /// açıklamanın hem vadesiz hem süpürme hesabına uyar. Anahtarlar önce, banka adı
        /// sonra denenir; en uzun eşleşen anahtar kazanır.
        /// </summary>
        public string? EslestirmeAnahtarlari { get; set; }

        /// <summary>
        /// Hesabın ORKA'daki adı, ör. "VAKIFBANK VADESIZ TL". Toplu içe aktarımda zorunlu
        /// kolon; elle açılan eski kayıtlarda boş olabilir, bu yüzden nullable.
        /// </summary>
        public string? HesapAdi { get; set; }

        /// <summary>
        /// Hesap sahibinin (firmanın) kendi resmî unvanı, ör. "PKF ADAY BAĞIMSIZ DENETİM
        /// ANONİM ŞİRKETİ". Banka açıklamalarında karşı tarafın yanı sıra <b>hesap sahibinin
        /// kendi adı da</b> geçiyor; unvan çıkarıcı onu karşı taraf sanıp benzer adlı bir
        /// cariye ("Bağımsız Denetim Derneği") eşleştiriyordu. Çıkarılan unvanın çekirdeği
        /// buradaki unvanın çekirdeğiyle aynıysa o yakalama atılır.
        ///
        /// Firma bazlı ve tek kez girilir: hesapta boşsa aynı firmanın dolu olan başka bir
        /// hesabından okunur (bkz. <c>EkstreService.HesapSahibiUnvaniBulAsync</c>).
        /// </summary>
        public string? HesapSahibiUnvani { get; set; }

        /// <summary>
        /// Hesap sahibinin <b>diğer yazımları</b>, satır satır. Bankalar aynı firmayı çok
        /// farklı yazıyor; gerçek dosyada altı ayrı yazım sayıldı ("ADAY BAĞIMSIZ DENETİM",
        /// "PKF ADAY", "ADAY BAĞIMSIZ DENETİM VE SMMM A.Ş." …). Tek alan yetmediği için
        /// kalanlar elenmiyor ve karşı taraf sanılıyordu.
        ///
        /// Eleme <see cref="HesapSahibiUnvani"/> ile bu listenin <b>herhangi birinin</b>
        /// çekirdeğine kapsama kontrolüyle yapılır (bkz. <c>HesapSahibiKimligi</c>).
        /// </summary>
        public string? HesapSahibiTakmaAdlari { get; set; }

        public HesapTipi HesapTipi { get; set; } = HesapTipi.Vadesiz;

        /// <summary>ISO kodu, ör. "TRY".</summary>
        public string ParaBirimi { get; set; } = "TRY";

        public string? Iban { get; set; }

        /// <summary>ORKA hesap kodu — boşluklu saklanır ve boşluklu yazılır, ör. "102 1 1 01".</summary>
        public string OrkaHesapKodu { get; set; } = string.Empty;

        /// <summary>
        /// Hangi parser çalışacak, ör. "VAKIFBANK_VADESIZ". <b>İsteğe bağlı:</b> hesapların
        /// çoğuna ekstre yüklenmiyor (vadeli, süpürme, blokaj, yatırım), yalnız karşı hesap
        /// olarak bulunabilmek için tanımlılar. Boşsa hesap İşleme ekranında kart göstermez
        /// ve ekstre kabul etmez, ama banka kayıt defterinde ve eşleştirmede kullanılır.
        /// </summary>
        public string? ParserTipi { get; set; }

        public bool Aktif { get; set; } = true;

        /// <summary>
        /// IBAN öğrenme katmanı bu hesapta çalışsın mı? Varsayılan kapalı: kullanıcı IBAN
        /// verisini düzenli tutmuyor ve güvenilir bulmuyor. Katman kod tarafında duruyor,
        /// yalnız bayrakla kapalı — düzenli IBAN gelen bir bankada açılabilsin.
        /// </summary>
        public bool IbanKatmaniAktif { get; set; }

        /// <summary>
        /// VKN öğrenme katmanı bu hesapta çalışsın mı? Varsayılan kapalı: Vakıfbank
        /// ekstresindeki VKN kolonu karşı tarafın değil hesap sahibinin VKN'si (286 satırın
        /// hepsinde aynı değer). Açık kalsaydı ilk onaydan sonra tüm satırlar güven 1.0 ile
        /// aynı hesaba eşleşir, onaya bile düşmezdi. Başka bankada karşı tarafın VKN'si
        /// gerçekten gelebileceği için katman silinmedi.
        /// </summary>
        public bool VknKatmaniAktif { get; set; }
    }
}
