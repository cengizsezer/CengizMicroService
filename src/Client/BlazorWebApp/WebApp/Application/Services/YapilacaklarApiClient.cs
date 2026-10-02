using System.Net.Http.Json;
using System.Text.Json;
using WebApp.Extensions;
using WebApp.Shared.Dto.Yapilacaklar;

namespace WebApp.Application.Services
{
    /// <summary>
    /// Firma işleri kartı, Yapılacaklar ekranı, anasayfa şeridi ve Yönetim → Vergi Takvimi.
    /// Yazma metotları başarıda <c>null</c>, hatada kullanıcıya gösterilecek mesajı döner.
    /// </summary>
    public interface IYapilacaklarApiClient
    {
        Task<FirmaIsleriKartDto?> FirmaIsleriAsync(int firmaId, CancellationToken ct = default);
        Task<string?> IsEkleAsync(int firmaId, FirmaIsiKaydetDto dto, CancellationToken ct = default);
        Task<string?> IsGuncelleAsync(int firmaId, int isId, FirmaIsiKaydetDto dto, CancellationToken ct = default);
        /// <returns>Silinen eklerin FileApi Id'leri (oradan da silinmeli) ya da hata.</returns>
        Task<(List<int> FileIds, string? Hata)> IsSilAsync(int firmaId, int isId, CancellationToken ct = default);

        // Prosedür (Prompt 8)
        Task<IsProsedurDto?> ProsedurAsync(int firmaId, IsKaynagi kaynakTip, int kaynakId, bool tumGecmis,
                                           CancellationToken ct = default);
        Task<(FirmaIsiDto? Is, string? Hata)> YasalProsedurAcAsync(int firmaId, string kod, IsTekrari tekrar,
                                                                   CancellationToken ct = default);
        Task<string?> YasalProsedurKaydetAsync(int firmaId, string kod, IsTekrari tekrar, IsProsedurKaydetDto dto,
                                               CancellationToken ct = default);
        Task<(FirmaIsiEkiDto? Ek, string? Hata)> EkEkleAsync(int firmaId, int isId, FirmaIsiEkiOlusturDto dto,
                                                             CancellationToken ct = default);

        /// <returns>Silinen ekin FileApi Id'si (oradan da silinmeli) ya da hata.</returns>
        Task<(int? FileId, string? Hata)> EkSilAsync(int firmaId, int isId, int ekId, CancellationToken ct = default);

        Task<YapilacaklarDto?> ListeAsync(IsFiltresi filtre, CancellationToken ct = default, int? firmaId = null);
        Task<YapilacaklarOzetDto?> OzetAsync(CancellationToken ct = default);
        Task<string?> IsaretleAsync(IsIsaretleDto dto, CancellationToken ct = default);

        // Toplu ekleme ve kopyalama (Prompt 11)
        Task<(TopluOnizlemeDto? Onizleme, string? Hata)> TopluOnizleAsync(int firmaId, TopluOnizleIstekDto istek, CancellationToken ct = default);
        Task<(TopluSonucDto? Sonuc, string? Hata)> TopluEkleAsync(int firmaId, TopluEkleDto dto, CancellationToken ct = default);
        Task<(TopluSonucDto? Sonuc, string? Hata)> KopyalaAsync(int hedefFirmaId, IsKopyalaDto dto, CancellationToken ct = default);
        Task<(byte[]? Icerik, string? Hata)> IsSablonuAsync(int firmaId, CancellationToken ct = default);

        // Dönem panosu (Prompt 10)

        /// <summary>İşaretle + kaldırılan dönemlerin kanıt dosyaları (FileApi'den silinmeli).</summary>
        Task<(List<int> FileIds, string? Hata)> IsaretleKanitlaAsync(IsIsaretleDto dto, CancellationToken ct = default);

        /// <summary>Ay verilmezse sunucu bir önceki ayı seçer. Hata varsa mesaj döner.</summary>
        Task<(DonemPanosuDto? Pano, string? Hata)> DonemPanosuAsync(int? yil, int? ay, CancellationToken ct = default, int? firmaId = null);
        Task<(DonemOzetiDto? Ozet, string? Hata)> DonemOzetiAsync(CancellationToken ct = default);

        // ---- Nasıl yapılır sheet'i (Prompt 14): iş bazında tarif ----
        Task<(List<IsTarifiOzetDto>? Liste, string? Hata)> TarifListesiAsync(CancellationToken ct = default);
        Task<(IsTarifiDetayDto? Detay, string? Hata)> TarifDetayAsync(string anahtar, CancellationToken ct = default);
        /// <summary>Tarif oluştur: <paramref name="form"/> boşsa en dolu firma metninden; doluysa formdaki metinden ("+ Tarif yaz").</summary>
        Task<(TarifUretimRaporuDto? Rapor, string? Hata)> TarifOlusturAsync(string anahtar, IsTarifiKaydetDto? form = null,
                                                                     int? firmaId = null, CancellationToken ct = default);
        Task<string?> TarifGuncelleAsync(int tarifId, IsTarifiKaydetDto dto, CancellationToken ct = default);
        Task<string?> TarifBaglaAsync(int tarifId, int firmaIsiId, CancellationToken ct = default);
        Task<(FirmaIsiEkiDto? Ek, string? Hata)> TarifEkEkleAsync(int tarifId, FirmaIsiEkiOlusturDto dto, CancellationToken ct = default);
        Task<(int? FileId, string? Hata)> TarifEkSilAsync(int tarifId, int ekId, CancellationToken ct = default);
        Task<(int? Baglanan, string? Hata)> TarifHepsiniBaglaAsync(int tarifId, List<int> firmaIsiIdleri, CancellationToken ct = default);

        // ---- Kişiler ve mailler (Prompt 14) ----
        Task<(KisilerPanosuDto? Pano, string? Hata)> KisilerPanosuAsync(int? yil, int? ay, CancellationToken ct = default, int? firmaId = null);
        Task<string?> KisiEkleAsync(KisiKaydetDto dto, CancellationToken ct = default);
        Task<string?> KisiGuncelleAsync(int kisiId, KisiKaydetDto dto, CancellationToken ct = default);
        Task<string?> KisiBirlestirAsync(int hedefId, int kaynakId, CancellationToken ct = default);
        Task<List<AliciOnizlemeDto>?> AliciOnizleAsync(AliciOnizlemeIstegiDto istek, CancellationToken ct = default);
        Task<(DonemHucresiDto? Hucre, string? Hata)> DonemHucresiAsync(IsIsaretDto hucre, CancellationToken ct = default);
        Task<string?> DonemNotuKaydetAsync(DonemNotuDto dto, CancellationToken ct = default);
        Task<(FirmaIsiEkiDto? Ek, string? Hata)> DonemEkEkleAsync(DonemEkiOlusturDto dto, CancellationToken ct = default);
        Task<(int? FileId, string? Hata)> DonemEkSilAsync(int ekId, CancellationToken ct = default);

        /// <summary>"Bu dönem kaydını sil": işaret + not + kanıt. Dönen dosyalar FileApi'den silinmeli.</summary>
        Task<(List<int> FileIds, string? Hata)> DonemKaydiSilAsync(IsIsaretDto hucre, CancellationToken ct = default);

        Task<List<VergiTakvimiDto>?> TakvimAsync(int? yil, CancellationToken ct = default);
        Task<string?> TakvimEkleAsync(VergiTakvimiKaydetDto dto, CancellationToken ct = default);
        Task<string?> TakvimGuncelleAsync(int id, VergiTakvimiKaydetDto dto, CancellationToken ct = default);
        Task<string?> TakvimSilAsync(int id, CancellationToken ct = default);
        Task<(int? Eklenen, string? Hata)> TakvimVarsayilanlarAsync(CancellationToken ct = default);
    }

    /// <inheritdoc cref="IYapilacaklarApiClient"/>
    public class YapilacaklarApiClient : IYapilacaklarApiClient
    {
        private const string Anasayfa = "/catalog/anasayfa";
        private const string Yapilacaklar = "/catalog/yapilacaklar";
        private const string Takvim = "/catalog/vergi-takvimi";
        private const string SunucuYok = "Sunucuya ulaşılamadı. Bağlantınızı kontrol edip tekrar deneyin.";

        private readonly HttpClient _http;

        public YapilacaklarApiClient(HttpClient http) => _http = http;

        public Task<FirmaIsleriKartDto?> FirmaIsleriAsync(int firmaId, CancellationToken ct = default)
            => OkuAsync<FirmaIsleriKartDto>($"{Anasayfa}/firmalar/{firmaId}/isler");

        public Task<string?> IsEkleAsync(int firmaId, FirmaIsiKaydetDto dto, CancellationToken ct = default)
            => YazAsync(() => _http.PostAsJsonAsync($"{Anasayfa}/firmalar/{firmaId}/isler", dto, ct), ct);

        public Task<string?> IsGuncelleAsync(int firmaId, int isId, FirmaIsiKaydetDto dto, CancellationToken ct = default)
            => YazAsync(() => _http.PutAsJsonAsync($"{Anasayfa}/firmalar/{firmaId}/isler/{isId}", dto, ct), ct);

        public async Task<(List<int> FileIds, string? Hata)> IsSilAsync(int firmaId, int isId, CancellationToken ct = default)
        {
            var (govde, hata) = await GovdeAsync(() => _http.DeleteAsync($"{Anasayfa}/firmalar/{firmaId}/isler/{isId}", ct), ct);
            if (hata is not null) return (new(), hata);

            var idler = new List<int>();
            try
            {
                using var belge = JsonDocument.Parse(govde!);
                if (belge.RootElement.TryGetProperty("fileIds", out var dizi) && dizi.ValueKind == JsonValueKind.Array)
                    idler.AddRange(dizi.EnumerateArray().Select(e => e.GetInt32()));
            }
            catch (JsonException)
            {
                // Eski sunucu boş gövde döner; silinecek dosya yok sayılır.
            }
            return (idler, null);
        }

        public Task<IsProsedurDto?> ProsedurAsync(int firmaId, IsKaynagi kaynakTip, int kaynakId, bool tumGecmis,
                                                  CancellationToken ct = default)
            => OkuAsync<IsProsedurDto>($"{Anasayfa}/firmalar/{firmaId}/isler/prosedur?kaynakTip={(int)kaynakTip}" +
                                       $"&kaynakId={kaynakId}&tumGecmis={(tumGecmis ? "true" : "false")}");

        public async Task<(FirmaIsiDto? Is, string? Hata)> YasalProsedurAcAsync(int firmaId, string kod, IsTekrari tekrar,
                                                                                CancellationToken ct = default)
        {
            var (govde, hata) = await GovdeAsync(() => _http.PostAsync(
                $"{Anasayfa}/firmalar/{firmaId}/isler/yasal/{Uri.EscapeDataString(kod)}/{(int)tekrar}", null, ct), ct);
            return hata is not null ? (null, hata) : (JsonSerializer.Deserialize<FirmaIsiDto>(govde!, Json), null);
        }

        public Task<string?> YasalProsedurKaydetAsync(int firmaId, string kod, IsTekrari tekrar, IsProsedurKaydetDto dto,
                                                      CancellationToken ct = default)
            => YazAsync(() => _http.PutAsJsonAsync(
                $"{Anasayfa}/firmalar/{firmaId}/isler/yasal/{Uri.EscapeDataString(kod)}/{(int)tekrar}/prosedur", dto, ct), ct);

        public async Task<(FirmaIsiEkiDto? Ek, string? Hata)> EkEkleAsync(int firmaId, int isId, FirmaIsiEkiOlusturDto dto,
                                                                          CancellationToken ct = default)
        {
            var (govde, hata) = await GovdeAsync(() => _http.PostAsJsonAsync(
                $"{Anasayfa}/firmalar/{firmaId}/isler/{isId}/ekler", dto, ct), ct);
            return hata is not null ? (null, hata) : (JsonSerializer.Deserialize<FirmaIsiEkiDto>(govde!, Json), null);
        }

        public async Task<(int? FileId, string? Hata)> EkSilAsync(int firmaId, int isId, int ekId, CancellationToken ct = default)
        {
            var (govde, hata) = await GovdeAsync(() => _http.DeleteAsync(
                $"{Anasayfa}/firmalar/{firmaId}/isler/{isId}/ekler/{ekId}", ct), ct);
            if (hata is not null) return (null, hata);

            using var belge = JsonDocument.Parse(govde!);
            return (belge.RootElement.GetProperty("fileId").GetInt32(), null);
        }

        // ---- Toplu ekleme ve kopyalama ----

        public async Task<(TopluOnizlemeDto? Onizleme, string? Hata)> TopluOnizleAsync(int firmaId, TopluOnizleIstekDto istek,
                                                                                     CancellationToken ct = default)
        {
            var (govde, hata) = await GovdeAsync(() => _http.PostAsJsonAsync($"{Anasayfa}/firmalar/{firmaId}/isler/toplu/onizle", istek, ct), ct);
            return hata is not null ? (null, hata) : (JsonSerializer.Deserialize<TopluOnizlemeDto>(govde!, Json), null);
        }

        public async Task<(TopluSonucDto? Sonuc, string? Hata)> TopluEkleAsync(int firmaId, TopluEkleDto dto, CancellationToken ct = default)
        {
            var (govde, hata) = await GovdeAsync(() => _http.PostAsJsonAsync($"{Anasayfa}/firmalar/{firmaId}/isler/toplu/ekle", dto, ct), ct);
            return hata is not null ? (null, hata) : (JsonSerializer.Deserialize<TopluSonucDto>(govde!, Json), null);
        }

        public async Task<(TopluSonucDto? Sonuc, string? Hata)> KopyalaAsync(int hedefFirmaId, IsKopyalaDto dto, CancellationToken ct = default)
        {
            var (govde, hata) = await GovdeAsync(() => _http.PostAsJsonAsync($"{Anasayfa}/firmalar/{hedefFirmaId}/isler/kopyala", dto, ct), ct);
            return hata is not null ? (null, hata) : (JsonSerializer.Deserialize<TopluSonucDto>(govde!, Json), null);
        }

        public async Task<(byte[]? Icerik, string? Hata)> IsSablonuAsync(int firmaId, CancellationToken ct = default)
        {
            try
            {
                using var resp = await _http.GetAsync($"{Anasayfa}/firmalar/{firmaId}/isler/sablon", ct);
                if (!resp.IsSuccessStatusCode) return (null, MesajCoz(await resp.Content.ReadAsStringAsync(ct)) ?? "Şablon indirilemedi.");
                return (await resp.Content.ReadAsByteArrayAsync(ct), null);
            }
            catch (Exception)
            {
                return (null, SunucuYok);
            }
        }

        // ---- Dönem panosu ----

        private const string Pano = "/catalog/donem-panosu";

        public async Task<(List<int> FileIds, string? Hata)> IsaretleKanitlaAsync(IsIsaretleDto dto, CancellationToken ct = default)
        {
            var (govde, hata) = await GovdeAsync(() => _http.PutAsJsonAsync($"{Yapilacaklar}/isaretle", dto, ct), ct);
            if (hata is not null) return (new(), hata);
            return (FileIdleri(govde), null);
        }

        private const string Tarif = "/catalog/is-tarifleri";

        public async Task<(List<IsTarifiOzetDto>? Liste, string? Hata)> TarifListesiAsync(CancellationToken ct = default)
        {
            var (govde, hata) = await GovdeAsync(() => _http.GetAsync(Tarif, ct), ct);
            return hata is not null ? (null, hata) : (JsonSerializer.Deserialize<List<IsTarifiOzetDto>>(govde!, Json), null);
        }

        public async Task<(IsTarifiDetayDto? Detay, string? Hata)> TarifDetayAsync(string anahtar, CancellationToken ct = default)
        {
            var (govde, hata) = await GovdeAsync(() => _http.GetAsync($"{Tarif}/detay?anahtar={Uri.EscapeDataString(anahtar)}", ct), ct);
            return hata is not null ? (null, hata) : (JsonSerializer.Deserialize<IsTarifiDetayDto>(govde!, Json), null);
        }

        public async Task<(TarifUretimRaporuDto? Rapor, string? Hata)> TarifOlusturAsync(string anahtar, IsTarifiKaydetDto? form = null,
                                                                            int? firmaId = null, CancellationToken ct = default)
        {
            var yol = $"{Tarif}?anahtar={Uri.EscapeDataString(anahtar)}" + (firmaId is { } f ? $"&firmaId={f}" : "");
            var (govde, hata) = await GovdeAsync(() => form is null ? _http.PostAsync(yol, null, ct) : _http.PostAsJsonAsync(yol, form, ct), ct);
            return hata is not null ? (null, hata) : (JsonSerializer.Deserialize<TarifUretimRaporuDto>(govde!, Json), null);
        }

        public Task<string?> TarifGuncelleAsync(int tarifId, IsTarifiKaydetDto dto, CancellationToken ct = default)
            => YazAsync(() => _http.PutAsJsonAsync($"{Tarif}/{tarifId}", dto, ct), ct);

        public Task<string?> TarifBaglaAsync(int tarifId, int firmaIsiId, CancellationToken ct = default)
            => YazAsync(() => _http.PostAsync($"{Tarif}/{tarifId}/bagla/{firmaIsiId}", null, ct), ct);

        public async Task<(FirmaIsiEkiDto? Ek, string? Hata)> TarifEkEkleAsync(int tarifId, FirmaIsiEkiOlusturDto dto, CancellationToken ct = default)
        {
            var (govde, hata) = await GovdeAsync(() => _http.PostAsJsonAsync($"{Tarif}/{tarifId}/ekler", dto, ct), ct);
            return hata is not null ? (null, hata) : (JsonSerializer.Deserialize<FirmaIsiEkiDto>(govde!, Json), null);
        }

        public async Task<(int? Baglanan, string? Hata)> TarifHepsiniBaglaAsync(int tarifId, List<int> firmaIsiIdleri, CancellationToken ct = default)
        {
            var (govde, hata) = await GovdeAsync(() => _http.PostAsJsonAsync($"{Tarif}/{tarifId}/bagla-hepsi", firmaIsiIdleri, ct), ct);
            if (hata is not null) return (null, hata);

            using var belge = JsonDocument.Parse(govde!);
            return (belge.RootElement.GetProperty("baglanan").GetInt32(), null);
        }

        private const string Kisi = "/catalog/kisiler";

        public async Task<(KisilerPanosuDto? Pano, string? Hata)> KisilerPanosuAsync(int? yil, int? ay, CancellationToken ct = default,
                                                                                int? firmaId = null)
        {
            var yol = yil is { } y && ay is { } a ? $"{Kisi}/pano?yil={y}&ay={a}" : $"{Kisi}/pano?";
            if (firmaId is { } f) yol += $"&firmaId={f}";
            var (govde, hata) = await GovdeAsync(() => _http.GetAsync(yol, ct), ct);
            return hata is not null ? (null, hata) : (JsonSerializer.Deserialize<KisilerPanosuDto>(govde!, Json), null);
        }

        public Task<string?> KisiEkleAsync(KisiKaydetDto dto, CancellationToken ct = default)
            => YazAsync(() => _http.PostAsJsonAsync(Kisi, dto, ct), ct);

        public Task<string?> KisiGuncelleAsync(int kisiId, KisiKaydetDto dto, CancellationToken ct = default)
            => YazAsync(() => _http.PutAsJsonAsync($"{Kisi}/{kisiId}", dto, ct), ct);

        public async Task<List<AliciOnizlemeDto>?> AliciOnizleAsync(AliciOnizlemeIstegiDto istek, CancellationToken ct = default)
        {
            var (govde, hata) = await GovdeAsync(() => _http.PostAsJsonAsync($"{Kisi}/onizle", istek, ct), ct);
            return hata is not null ? null : JsonSerializer.Deserialize<List<AliciOnizlemeDto>>(govde!, Json);
        }

        public Task<string?> KisiBirlestirAsync(int hedefId, int kaynakId, CancellationToken ct = default)
            => YazAsync(() => _http.PostAsync($"{Kisi}/{hedefId}/birlestir/{kaynakId}", null, ct), ct);

        public async Task<(int? FileId, string? Hata)> TarifEkSilAsync(int tarifId, int ekId, CancellationToken ct = default)
        {
            var (govde, hata) = await GovdeAsync(() => _http.DeleteAsync($"{Tarif}/{tarifId}/ekler/{ekId}", ct), ct);
            if (hata is not null) return (null, hata);

            using var belge = JsonDocument.Parse(govde!);
            return (belge.RootElement.GetProperty("fileId").GetInt32(), null);
        }

        public async Task<(DonemOzetiDto? Ozet, string? Hata)> DonemOzetiAsync(CancellationToken ct = default)
        {
            var (govde, hata) = await GovdeAsync(() => _http.GetAsync($"{Pano}/ozet", ct), ct);
            return hata is not null ? (null, hata) : (JsonSerializer.Deserialize<DonemOzetiDto>(govde!, Json), null);
        }

        public async Task<(DonemPanosuDto? Pano, string? Hata)> DonemPanosuAsync(int? yil, int? ay, CancellationToken ct = default,
                                                                         int? firmaId = null)
        {
            var yol = yil is { } y && ay is { } a ? $"{Pano}?yil={y}&ay={a}" : $"{Pano}?";
            if (firmaId is { } f) yol += $"&firmaId={f}";
            var (govde, hata) = await GovdeAsync(() => _http.GetAsync(yol, ct), ct);
            return hata is not null ? (null, hata) : (JsonSerializer.Deserialize<DonemPanosuDto>(govde!, Json), null);
        }

        public async Task<(DonemHucresiDto? Hucre, string? Hata)> DonemHucresiAsync(IsIsaretDto h, CancellationToken ct = default)
        {
            var yol = $"{Pano}/hucre?kaynakTip={(int)h.KaynakTip}&kaynakId={h.KaynakId}&firmaId={h.FirmaId}" +
                      $"&donemAnahtari={Uri.EscapeDataString(h.DonemAnahtari)}";
            var (govde, hata) = await GovdeAsync(() => _http.GetAsync(yol, ct), ct);
            return hata is not null ? (null, hata) : (JsonSerializer.Deserialize<DonemHucresiDto>(govde!, Json), null);
        }

        public Task<string?> DonemNotuKaydetAsync(DonemNotuDto dto, CancellationToken ct = default)
            => YazAsync(() => _http.PutAsJsonAsync($"{Pano}/hucre/not", dto, ct), ct);

        public async Task<(FirmaIsiEkiDto? Ek, string? Hata)> DonemEkEkleAsync(DonemEkiOlusturDto dto, CancellationToken ct = default)
        {
            var (govde, hata) = await GovdeAsync(() => _http.PostAsJsonAsync($"{Pano}/hucre/ekler", dto, ct), ct);
            return hata is not null ? (null, hata) : (JsonSerializer.Deserialize<FirmaIsiEkiDto>(govde!, Json), null);
        }

        public async Task<(int? FileId, string? Hata)> DonemEkSilAsync(int ekId, CancellationToken ct = default)
        {
            var (govde, hata) = await GovdeAsync(() => _http.DeleteAsync($"{Pano}/hucre/ekler/{ekId}", ct), ct);
            if (hata is not null) return (null, hata);

            using var belge = JsonDocument.Parse(govde!);
            return (belge.RootElement.GetProperty("fileId").GetInt32(), null);
        }

        public async Task<(List<int> FileIds, string? Hata)> DonemKaydiSilAsync(IsIsaretDto h, CancellationToken ct = default)
        {
            var yol = $"{Pano}/hucre?kaynakTip={(int)h.KaynakTip}&kaynakId={h.KaynakId}&firmaId={h.FirmaId}" +
                      $"&donemAnahtari={Uri.EscapeDataString(h.DonemAnahtari)}";
            var (govde, hata) = await GovdeAsync(() => _http.DeleteAsync(yol, ct), ct);
            return hata is not null ? (new(), hata) : (FileIdleri(govde), null);
        }

        /// <summary><c>{ fileIds: [...] }</c> gövdesinden Id'ler; boş/eski gövdede boş liste.</summary>
        private static List<int> FileIdleri(string? govde)
        {
            var idler = new List<int>();
            if (string.IsNullOrWhiteSpace(govde)) return idler;
            try
            {
                using var belge = JsonDocument.Parse(govde);
                if (belge.RootElement.TryGetProperty("fileIds", out var dizi) && dizi.ValueKind == JsonValueKind.Array)
                    idler.AddRange(dizi.EnumerateArray().Select(e => e.GetInt32()));
            }
            catch (JsonException)
            {
                // Silinecek dosya yok sayılır.
            }
            return idler;
        }

        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        /// <summary>Başarıda yanıt gövdesi, hatada kullanıcıya gösterilecek mesaj.</summary>
        private static async Task<(string? Govde, string? Hata)> GovdeAsync(Func<Task<HttpResponseMessage>> istek,
                                                                            CancellationToken ct)
        {
            try
            {
                using var resp = await istek();
                var govde = await resp.Content.ReadAsStringAsync(ct);
                return resp.IsSuccessStatusCode ? (govde, null) : (null, MesajCoz(govde) ?? "Kayıt yapılamadı.");
            }
            catch (Exception)
            {
                return (null, SunucuYok);
            }
        }

        public Task<YapilacaklarDto?> ListeAsync(IsFiltresi filtre, CancellationToken ct = default, int? firmaId = null)
            => OkuAsync<YapilacaklarDto>($"{Yapilacaklar}?filtre={(int)filtre}" + (firmaId is { } f ? $"&firmaId={f}" : ""));

        public Task<YapilacaklarOzetDto?> OzetAsync(CancellationToken ct = default)
            => OkuAsync<YapilacaklarOzetDto>($"{Yapilacaklar}/ozet");

        public Task<string?> IsaretleAsync(IsIsaretleDto dto, CancellationToken ct = default)
            => YazAsync(() => _http.PutAsJsonAsync($"{Yapilacaklar}/isaretle", dto, ct), ct);

        public Task<List<VergiTakvimiDto>?> TakvimAsync(int? yil, CancellationToken ct = default)
            => OkuAsync<List<VergiTakvimiDto>>(yil is null ? Takvim : $"{Takvim}?yil={yil}");

        public Task<string?> TakvimEkleAsync(VergiTakvimiKaydetDto dto, CancellationToken ct = default)
            => YazAsync(() => _http.PostAsJsonAsync(Takvim, dto, ct), ct);

        public Task<string?> TakvimGuncelleAsync(int id, VergiTakvimiKaydetDto dto, CancellationToken ct = default)
            => YazAsync(() => _http.PutAsJsonAsync($"{Takvim}/{id}", dto, ct), ct);

        public Task<string?> TakvimSilAsync(int id, CancellationToken ct = default)
            => YazAsync(() => _http.DeleteAsync($"{Takvim}/{id}", ct), ct);

        public async Task<(int? Eklenen, string? Hata)> TakvimVarsayilanlarAsync(CancellationToken ct = default)
        {
            try
            {
                using var resp = await _http.PostAsync($"{Takvim}/varsayilanlar", null, ct);
                var govde = await resp.Content.ReadAsStringAsync(ct);
                if (!resp.IsSuccessStatusCode) return (null, MesajCoz(govde) ?? "Yüklenemedi.");

                using var belge = JsonDocument.Parse(govde);
                return (belge.RootElement.GetProperty("eklenen").GetInt32(), null);
            }
            catch (Exception)
            {
                return (null, SunucuYok);
            }
        }

        private async Task<T?> OkuAsync<T>(string yol) where T : class
        {
            try
            {
                return await _http.GetResponseAsync<T?>(yol);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static async Task<string?> YazAsync(Func<Task<HttpResponseMessage>> istek, CancellationToken ct)
        {
            try
            {
                using var resp = await istek();
                if (resp.IsSuccessStatusCode) return null;

                return MesajCoz(await resp.Content.ReadAsStringAsync(ct)) ?? "Kayıt yapılamadı.";
            }
            catch (Exception)
            {
                return SunucuYok;
            }
        }

        /// <summary>Sunucunun <c>{ field, message }</c> hata gövdesinden mesajı çıkarır.</summary>
        private static string? MesajCoz(string govde)
        {
            try
            {
                using var belge = JsonDocument.Parse(govde);
                return belge.RootElement.TryGetProperty("message", out var m) ? m.GetString() : null;
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
