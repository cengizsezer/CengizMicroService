namespace CatalogService.Api.Features.BankaEkstre.Domain
{
    /// <summary>
    /// Katman 4: işlem tipi → hesap kodu doğrudan eşlemesi (banka masrafı → 770,
    /// HGS → 740 vb.). Yapılandırılabilir; kod değişmeden yeni kural eklenir.
    /// </summary>
    public class SabitKural
    {
        public int Id { get; set; }

        public string ParserTipi { get; set; } = string.Empty;

        /// <summary>
        /// Aranacak desen. Hangi metinde arandığı <see cref="Kapsam"/> ile belirlenir;
        /// alan adı geriye dönük uyum için korundu.
        /// </summary>
        public string IslemTipiDeseni { get; set; } = string.Empty;

        /// <summary>Desen işlem tipinde mi, ham açıklamada mı aranacak.</summary>
        public KuralKapsami Kapsam { get; set; } = KuralKapsami.IslemTipi;

        public EslesmeTuru EslesmeTuru { get; set; } = EslesmeTuru.Tam;

        /// <summary>Dolu ise kural yalnız bu yöndeki satırlara uygulanır.</summary>
        public Yon? Yon { get; set; }

        /// <summary>Boşluklu ORKA kodu.</summary>
        public string HesapKodu { get; set; } = string.Empty;

        public string? HesapAdi { get; set; }

        /// <summary>Kuralın güveni; eşik altına düşürülmez, sabit kural kesin kabul edilir.</summary>
        public decimal Guven { get; set; } = 0.95m;

        /// <summary>
        /// Kural tuttuğunda çıkarılan unvan bir <b>cari</b> sayılsın mı. Personel avansı gibi
        /// satırlarda açıklamadaki isim bir cari değil, ödeme yapılan kişidir: false olduğunda
        /// satır için öğrenme anahtarı üretilmez ve unvan benzerliği katmanı (120/329)
        /// çalıştırılmaz — çalışsaydı kişiyi ilgisiz bir cariye eşlerdi.
        ///
        /// Unvanın <b>çıkarılması</b> ayrı bir konu: <see cref="AltHesapGerekli"/> true ise
        /// unvan yine okunur, yalnız kuralın ana grubu içinde alt hesap aramakta kullanılır.
        /// </summary>
        public bool UnvanCikarilsin { get; set; } = true;

        /// <summary>
        /// Kural yalnız <b>ana grubu</b> belirliyorsa true. Alt hesap (kişi/muavin) önce
        /// çıkarılan unvanla bu grubun içinde aranır; bulunamazsa satır ana grupla onaya
        /// düşer ve muavini kullanıcı seçer.
        /// </summary>
        public bool AltHesapGerekli { get; set; }


        /// <summary>
        /// Muhasebe kategorisi (<see cref="IslemKategorisi"/>). Yalnız etiket ve görünüm:
        /// eşleştirme kararına girmez, kural kategorisiz de aynen çalışır. Kategori
        /// silinirse alan boşalır, kural kalır.
        /// </summary>
        public int? IslemKategorisiId { get; set; }

        public int Sira { get; set; }

        public bool Aktif { get; set; } = true;
    }
}
