using System.Text.Json;

namespace CatalogService.Api.Features.Muhasebe.Services
{
    /// <summary>Şablondaki tek bir hesap: kod ve ad. Kodun uzunluğu seviyeyi verir.</summary>
    public sealed record ThpDugum(string Kod, string Ad);

    /// <summary>
    /// Tekdüzen hesap planı şablonunun kaynağı. Dosya erişimi tek yerde toplansın diye
    /// arayüz: hem açılış seed'i hem ekrandaki "Tek düzen hesap planını yükle" düğmesi
    /// aynı kaynaktan okur, testler dosyasız sahte kaynak verebilir (bkz. KARARLAR §84).
    /// </summary>
    public interface ITekDuzenPlanKaynagi
    {
        /// <summary>Şablon okunabiliyor mu? Yayında dosya eksikse false.</summary>
        bool Var { get; }

        /// <summary>Aranan yol; hata mesajında ve logda gösterilir.</summary>
        string Yol { get; }

        /// <summary>Şablondaki hesaplar. <see cref="Var"/> false ise boş liste.</summary>
        IReadOnlyList<ThpDugum> Oku();
    }

    /// <summary>
    /// Şablonu <c>Infrastructure/Setup/SeedFiles/thp-standart.json</c>'dan okur. Dosya
    /// csproj'da <c>PreserveNewest</c> ile çıktıya kopyalanır; yayında eksikse
    /// <see cref="Var"/> false döner ve çağıran <b>sessizce geçmez</b> — açılışta log,
    /// ekranda hata (KARARLAR §84).
    /// </summary>
    public sealed class DosyadanTekDuzenPlanKaynagi : ITekDuzenPlanKaynagi
    {
        public const string DosyaAdi = "thp-standart.json";

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public DosyadanTekDuzenPlanKaynagi(IWebHostEnvironment env)
            => Yol = Path.Combine(env.ContentRootPath, "Infrastructure", "Setup", "SeedFiles", DosyaAdi);

        public string Yol { get; }

        public bool Var => File.Exists(Yol);

        public IReadOnlyList<ThpDugum> Oku()
        {
            if (!Var) return Array.Empty<ThpDugum>();

            var ham = File.ReadAllText(Yol);
            return JsonSerializer.Deserialize<List<ThpDugum>>(ham, JsonOpts) ?? new List<ThpDugum>();
        }
    }
}
