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

        public string IslemTipiDeseni { get; set; } = string.Empty;

        public EslesmeTuru EslesmeTuru { get; set; } = EslesmeTuru.Tam;

        /// <summary>Dolu ise kural yalnız bu yöndeki satırlara uygulanır.</summary>
        public Yon? Yon { get; set; }

        /// <summary>Boşluklu ORKA kodu.</summary>
        public string HesapKodu { get; set; } = string.Empty;

        public string? HesapAdi { get; set; }

        /// <summary>Kuralın güveni; eşik altına düşürülmez, sabit kural kesin kabul edilir.</summary>
        public decimal Guven { get; set; } = 0.95m;

        public int Sira { get; set; }

        public bool Aktif { get; set; } = true;
    }
}
