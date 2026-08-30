using Blazored.LocalStorage;
using WebApp.Extensions;
using WebApp.Shared.Dto.Anasayfa;

namespace WebApp.Application.Services
{
    public interface IAnasayfaApiClient
    {
        Task<AnasayfaOzetDto?> OzetAsync(int? yil = null, int? ay = null, CancellationToken ct = default);

        /// <summary>
        /// Firma paneli: tüm firmaların satırları + seçili firmanın ayrıntısı, tek çağrı.
        /// <paramref name="firmaId"/> boşsa sunucu ilk firmayı seçer.
        /// </summary>
        Task<FirmaPaneliDto?> FirmaPaneliAsync(int? firmaId = null, CancellationToken ct = default);
    }

    /// <inheritdoc cref="IAnasayfaApiClient"/>
    public class AnasayfaApiClient : IAnasayfaApiClient
    {
        private const string Prefix = "/catalog/anasayfa";

        private readonly HttpClient _http;

        public AnasayfaApiClient(HttpClient http) => _http = http;

        public Task<AnasayfaOzetDto?> OzetAsync(int? yil = null, int? ay = null, CancellationToken ct = default)
        {
            var sorgu = new List<string>();
            if (yil.HasValue) sorgu.Add($"yil={yil.Value}");
            if (ay.HasValue) sorgu.Add($"ay={ay.Value}");

            var yol = sorgu.Count == 0 ? $"{Prefix}/ozet" : $"{Prefix}/ozet?{string.Join("&", sorgu)}";
            return _http.GetResponseAsync<AnasayfaOzetDto?>(yol);
        }

        /// <inheritdoc />
        public Task<FirmaPaneliDto?> FirmaPaneliAsync(int? firmaId = null, CancellationToken ct = default)
        {
            // Firma kapsamı Banka Otomasyon'daki gibi ?firmaId= ile gidiyor; parametre
            // yoksa sunucu ilk firmayı seçiyor, istemci varsayılan uydurmuyor.
            var yol = firmaId is > 0
                ? $"{Prefix}/firma-paneli?firmaId={firmaId}"
                : $"{Prefix}/firma-paneli";

            return _http.GetResponseAsync<FirmaPaneliDto?>(yol);
        }
    }

    /// <summary>
    /// "Son kullanılan firmalar" listesi. Kullanıcının kendi gezinme geçmişi olduğu için
    /// tarayıcıda tutuluyor; sunucuya yazılacak bir veri değil ve cihazlar arası
    /// taşınması da beklenmiyor.
    /// </summary>
    public interface ISonFirmalarStore
    {
        Task<List<SonFirmaDto>> GetAsync();

        /// <summary>Firmayı listenin başına alır; liste <see cref="EnFazla"/> kayıtla sınırlı.</summary>
        Task KaydetAsync(int firmaId, string ad, DateTime zaman);
    }

    /// <inheritdoc cref="ISonFirmalarStore"/>
    public class SonFirmalarStore : ISonFirmalarStore
    {
        public const int EnFazla = 6;

        private const string Anahtar = "SonKullanilanFirmalar";

        private readonly ILocalStorageService _depo;

        public SonFirmalarStore(ILocalStorageService depo) => _depo = depo;

        public async Task<List<SonFirmaDto>> GetAsync()
        {
            try
            {
                var liste = await _depo.GetItemAsync<List<SonFirmaDto>>(Anahtar);
                return liste?.OrderByDescending(f => f.SonKullanim).Take(EnFazla).ToList() ?? new();
            }
            catch
            {
                // Bozuk ya da eski biçimli kayıt yüzünden anasayfa açılmasın diye
                // sessizce boş liste; kullanıcı gezindikçe yeniden dolar.
                return new();
            }
        }

        public async Task KaydetAsync(int firmaId, string ad, DateTime zaman)
        {
            if (firmaId <= 0) return;

            var liste = await GetAsync();

            liste.RemoveAll(f => f.FirmaId == firmaId);
            liste.Insert(0, new SonFirmaDto { FirmaId = firmaId, Ad = ad, SonKullanim = zaman });

            if (liste.Count > EnFazla) liste = liste.Take(EnFazla).ToList();

            try
            {
                await _depo.SetItemAsync(Anahtar, liste);
            }
            catch
            {
                // Depolama kapalı olabilir; hızlı erişim listesi kritik değil.
            }
        }
    }
}
