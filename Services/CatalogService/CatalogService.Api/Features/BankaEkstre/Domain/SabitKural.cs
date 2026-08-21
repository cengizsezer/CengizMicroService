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
        /// Kural tuttuğunda unvan çıkarma yapılsın mı. Personel avansı gibi satırlarda
        /// açıklamadaki isim bir cari değil, ödeme yapılan kişidir; çıkarılırsa unvan
        /// benzerliği katmanı onu 120/329 altında bir cariye eşliyordu.
        /// </summary>
        public bool UnvanCikarilsin { get; set; } = true;

        /// <summary>
        /// Kural yalnız <b>ana grubu</b> belirliyorsa true. Alt hesap (kişi/muavin) kullanıcı
        /// tarafından seçilmek zorunda olduğu için satır otomatik kapanmaz, onaya düşer.
        /// </summary>
        public bool AltHesapGerekli { get; set; }

        public int Sira { get; set; }

        public bool Aktif { get; set; } = true;
    }
}
