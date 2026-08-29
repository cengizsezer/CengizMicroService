using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

// =============================================================================
//  AgentHubTestClient — /agenthub'ı gerçekten bağlanarak doğrulayan asgari istemci
//
//  Ajanın kendisi DEĞİL: FlaUI, ORKA, iş kuyruğu yok. Yaptığı tek şey, A adımının
//  kabul kriterlerini uçtan uca sınamak — token'sız bağlantının reddedildiğini,
//  eski sürümün geri çevrildiğini, kaydolan ajanın "baglilar" ucunda göründüğünü
//  ve bağlantı kopunca listeden düştüğünü. B adımında yazılacak gerçek ajan bu
//  dosyayı değil, kendi bağlantı yönetimini kullanacak.
//
//  Kullanım:
//    dotnet run --project tools/AgentHubTestClient -- [seçenekler]
//
//    --api   <adres>   Durum ucunun kökü            (varsayılan http://localhost:5004)
//    --hub   <adres>   Hub adresi                   (varsayılan <api>/agenthub)
//    --token <jwt>     Hazır token. Verilmezse aşağıdaki anahtarla token üretilir.
//    --imza  <anahtar> JWT imza anahtarı            (varsayılan: Development ayarı)
//    --issuer / --audience / --surum / --makine-id / --makine-adi / --kullanici
// =============================================================================

var ayar = Ayarlar.Oku(args);
var token = ayar.Token ?? Jwt.Uret(ayar);

Console.OutputEncoding = Encoding.UTF8;
Console.WriteLine($"Hub      : {ayar.HubAdresi}");
Console.WriteLine($"Durum ucu: {ayar.ApiAdresi}/api/catalog/agent/baglilar");
Console.WriteLine($"Makine   : {ayar.MakineAdi} ({ayar.MakineId})");
Console.WriteLine($"Token    : {(ayar.Token is null ? "yerelde üretildi" : "dışarıdan verildi")}");
Console.WriteLine(new string('-', 72));

var rapor = new Rapor();
using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };

HubConnection? birinci = null;
HubConnection? ikinci = null;

try
{
    // 1 -----------------------------------------------------------------------
    await rapor.Calistir("Durum ucu token'sız istekte 401 dönüyor", async () =>
    {
        var yanit = await http.GetAsync($"{ayar.ApiAdresi}/api/catalog/agent/baglilar");
        Beklenir(yanit.StatusCode == HttpStatusCode.Unauthorized, $"dönen kod: {(int)yanit.StatusCode}");
    });

    // 2 -----------------------------------------------------------------------
    await rapor.Calistir("Token'sız hub bağlantısı reddediliyor", async () =>
    {
        await using var tokensiz = new HubConnectionBuilder().WithUrl(ayar.HubAdresi).Build();
        try
        {
            await tokensiz.StartAsync();
            Beklenir(false, "bağlantı kuruldu — oysa reddedilmeliydi");
        }
        catch (Exception ex) when (ex is not DogrulamaHatasi)
        {
            // Beklenen: negotiate 401 döndüğü için StartAsync patlar.
            Console.WriteLine($"      (beklenen hata: {ex.GetType().Name})");
        }
    });

    // 3 -----------------------------------------------------------------------
    await rapor.Calistir("Eski sürümle kayıt reddediliyor, mesaj anlaşılır", async () =>
    {
        await using var eski = Baglanti(ayar, token);
        await eski.StartAsync();
        var sonuc = await eski.InvokeAsync<KayitSonucu>("Kaydol", Istek(ayar, "0.0.1"));
        Beklenir(!sonuc.Kabul, "kayıt kabul edildi — oysa reddedilmeliydi");
        Console.WriteLine($"      sunucu mesajı: {sonuc.Mesaj}");
        Console.WriteLine($"      sunucu {sonuc.SunucuSurumu} / asgari ajan {sonuc.AsgariAjanSurumu}");
    });

    // 4 -----------------------------------------------------------------------
    birinci = Baglanti(ayar, token);
    await rapor.Calistir("Geçerli sürümle kayıt kabul ediliyor", async () =>
    {
        await birinci.StartAsync();
        var sonuc = await birinci.InvokeAsync<KayitSonucu>("Kaydol", Istek(ayar, ayar.Surum));
        Beklenir(sonuc.Kabul, $"kayıt reddedildi: {sonuc.Mesaj}");
    });

    // 5 -----------------------------------------------------------------------
    DateTimeOffset ilkAtis = default;
    await rapor.Calistir("Ajan 'baglilar' ucunda görünüyor", async () =>
    {
        var ajan = await AjaniBekle(http, ayar, token, olmali: true);
        Beklenir(ajan is not null, "ajan listede yok");
        Console.WriteLine($"      {ajan!.MakineAdi} / {ajan.AjanSurumu} / {ajan.IsletimSistemi} " +
                          $"/ kullanıcı {ajan.KullaniciId} / ORKA: {Yazi(ajan.OrkaCalisiyorMu)}");
        ilkAtis = ajan.SonKalpAtisi;
    });

    // 6 -----------------------------------------------------------------------
    await rapor.Calistir("Kalp atışı son atış zamanını ilerletiyor", async () =>
    {
        await Task.Delay(1100); // sunucu saatinin ölçebileceği kadar bekle
        await birinci.InvokeAsync("KalpAtisi");
        var ajan = await AjaniBekle(http, ayar, token, olmali: true);
        Beklenir(ajan!.SonKalpAtisi > ilkAtis,
                 $"son atış ilerlemedi ({ilkAtis:HH:mm:ss.fff} -> {ajan.SonKalpAtisi:HH:mm:ss.fff})");
    });

    // 7 -----------------------------------------------------------------------
    await rapor.Calistir("Aynı MakineId ile ikinci bağlantı eskisini düşürüyor", async () =>
    {
        ikinci = Baglanti(ayar, token);
        await ikinci.StartAsync();
        var sonuc = await ikinci.InvokeAsync<KayitSonucu>("Kaydol", Istek(ayar, ayar.Surum));
        Beklenir(sonuc.Kabul, $"ikinci kayıt reddedildi: {sonuc.Mesaj}");

        await KosulBekle(() => birinci!.State == HubConnectionState.Disconnected);
        Beklenir(birinci!.State == HubConnectionState.Disconnected,
                 $"eski bağlantı hâlâ ayakta ({birinci.State})");

        var liste = await Baglilar(http, ayar, token);
        var kacTane = liste.Count(a => a.MakineId == ayar.MakineId);
        Beklenir(kacTane == 1, $"listede aynı makineden {kacTane} kayıt var");
    });

    // 8 -----------------------------------------------------------------------
    await rapor.Calistir("Bağlantı kopunca ajan listeden siliniyor", async () =>
    {
        await ikinci!.StopAsync();
        var ajan = await AjaniBekle(http, ayar, token, olmali: false);
        Beklenir(ajan is null, "ajan hâlâ listede");
    });
}
catch (Exception ex)
{
    Console.WriteLine();
    Console.WriteLine($"Beklenmeyen hata: {ex}");
    rapor.Kirildi = true;
}
finally
{
    if (birinci is not null) await birinci.DisposeAsync();
    if (ikinci is not null) await ikinci.DisposeAsync();
}

Console.WriteLine(new string('-', 72));
Console.WriteLine(rapor.Ozet);
return rapor.BasariliMi ? 0 : 1;

// ---------------------------------------------------------------------------

static HubConnection Baglanti(Ayarlar ayar, string token) =>
    new HubConnectionBuilder()
        .WithUrl(ayar.HubAdresi, o => o.AccessTokenProvider = () => Task.FromResult<string?>(token))
        .Build();

static object Istek(Ayarlar ayar, string surum) => new
{
    MakineId = ayar.MakineId,
    MakineAdi = ayar.MakineAdi,
    AjanSurumu = surum,
    IsletimSistemi = Environment.OSVersion.VersionString,
    OrkaCalisiyorMu = (bool?)null
};

static async Task<List<BagliAjan>> Baglilar(HttpClient http, Ayarlar ayar, string token)
{
    using var istek = new HttpRequestMessage(HttpMethod.Get, $"{ayar.ApiAdresi}/api/catalog/agent/baglilar");
    istek.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

    var yanit = await http.SendAsync(istek);
    if (!yanit.IsSuccessStatusCode)
        throw new DogrulamaHatasi($"durum ucu {(int)yanit.StatusCode} döndü: {await yanit.Content.ReadAsStringAsync()}");

    return await yanit.Content.ReadFromJsonAsync<List<BagliAjan>>(
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
}

// Kopuş bildirimi sunucuya anında ulaşmıyor; kısa bir pencerede yoklanıyor.
static async Task<BagliAjan?> AjaniBekle(HttpClient http, Ayarlar ayar, string token, bool olmali)
{
    BagliAjan? son = null;
    await KosulBekleAsync(async () =>
    {
        son = (await Baglilar(http, ayar, token)).FirstOrDefault(a => a.MakineId == ayar.MakineId);
        return olmali ? son is not null : son is null;
    });
    return son;
}

static Task KosulBekle(Func<bool> kosul) => KosulBekleAsync(() => Task.FromResult(kosul()));

static async Task KosulBekleAsync(Func<Task<bool>> kosul)
{
    for (var i = 0; i < 25; i++)
    {
        if (await kosul()) return;
        await Task.Delay(200);
    }
}

static void Beklenir(bool kosul, string hata)
{
    if (!kosul) throw new DogrulamaHatasi(hata);
}

static string Yazi(bool? deger) => deger switch { true => "evet", false => "hayır", null => "bilinmiyor" };

// ---------------------------------------------------------------------------

class DogrulamaHatasi : Exception
{
    public DogrulamaHatasi(string mesaj) : base(mesaj) { }
}

class Rapor
{
    private int _gecen, _kalan;

    public bool Kirildi { get; set; }
    public bool BasariliMi => !Kirildi && _kalan == 0 && _gecen > 0;
    public string Ozet => $"{_gecen} geçti, {_kalan} kaldı." + (BasariliMi ? " Hub doğrulandı." : " DOĞRULAMA BAŞARISIZ.");

    public async Task Calistir(string ad, Func<Task> adim)
    {
        try
        {
            await adim();
            _gecen++;
            Console.WriteLine($"[ OK ] {ad}");
        }
        catch (DogrulamaHatasi ex)
        {
            _kalan++;
            Console.WriteLine($"[HATA] {ad}");
            Console.WriteLine($"       {ex.Message}");
        }
    }
}

class BagliAjan
{
    public string MakineId { get; set; } = string.Empty;
    public string MakineAdi { get; set; } = string.Empty;
    public string AjanSurumu { get; set; } = string.Empty;
    public string? IsletimSistemi { get; set; }
    public string KullaniciId { get; set; } = string.Empty;
    public DateTimeOffset BaglantiZamani { get; set; }
    public DateTimeOffset SonKalpAtisi { get; set; }
    public bool? OrkaCalisiyorMu { get; set; }
}

class KayitSonucu
{
    public bool Kabul { get; set; }
    public string Mesaj { get; set; } = string.Empty;
    public string SunucuSurumu { get; set; } = string.Empty;
    public string AsgariAjanSurumu { get; set; } = string.Empty;
}

class Ayarlar
{
    public string ApiAdresi { get; private set; } = "http://localhost:5004";
    public string HubAdresi { get; private set; } = "";
    public string? Token { get; private set; }
    public string ImzaAnahtari { get; private set; } = "super_secret_dev_key_32bytes_minimum";
    public string Issuer { get; private set; } = "identityserver.tr";
    public string Audience { get; private set; } = "identityclient.tr";
    public string Surum { get; private set; } = "1.0.0";
    public string MakineId { get; private set; } = "TEST-" + Guid.NewGuid().ToString("N")[..8];
    public string MakineAdi { get; private set; } = "TEST-ISTEMCI";
    public string KullaniciId { get; private set; } = "test-istemci";

    public static Ayarlar Oku(string[] args)
    {
        var a = new Ayarlar();
        for (var i = 0; i + 1 < args.Length; i += 2)
        {
            var deger = args[i + 1];
            switch (args[i])
            {
                case "--api": a.ApiAdresi = deger.TrimEnd('/'); break;
                case "--hub": a.HubAdresi = deger; break;
                case "--token": a.Token = deger; break;
                case "--imza": a.ImzaAnahtari = deger; break;
                case "--issuer": a.Issuer = deger; break;
                case "--audience": a.Audience = deger; break;
                case "--surum": a.Surum = deger; break;
                case "--makine-id": a.MakineId = deger; break;
                case "--makine-adi": a.MakineAdi = deger; break;
                case "--kullanici": a.KullaniciId = deger; break;
            }
        }

        if (string.IsNullOrEmpty(a.HubAdresi))
            a.HubAdresi = $"{a.ApiAdresi}/agenthub";

        return a;
    }
}

// IdentityService'i ayağa kaldırmadan doğrulama yapabilmek için token yerelde
// üretiliyor: hub'ın kontrol ettiği tek şey imza, issuer ve audience.
static class Jwt
{
    public static string Uret(Ayarlar ayar)
    {
        var anahtar = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(ayar.ImzaAnahtari));
        var jeton = new JwtSecurityToken(
            issuer: ayar.Issuer,
            audience: ayar.Audience,
            claims: new[]
            {
                new Claim("sub", ayar.KullaniciId),
                new Claim(ClaimTypes.NameIdentifier, ayar.KullaniciId),
                new Claim("name", ayar.KullaniciId)
            },
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(anahtar, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(jeton);
    }
}
