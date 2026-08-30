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
//  İki tür token dolaşıyor: hub yalnız AJAN token'ı kabul ediyor, durum ucu ise
//  yalnız KULLANICI token'ı. İstemci ikisini de taşıyor ve birbirlerinin kapısında
//  reddedildiklerini de sınıyor.
//
//  Kullanım:
//    dotnet run --project tools/AgentHubTestClient -- [seçenekler]
//
//    --api   <adres>   Durum ucunun kökü            (varsayılan http://localhost:5004)
//    --hub   <adres>   Hub adresi                   (varsayılan <api>/agenthub)
//    --durum-yolu <yol> Durum ucunun yolu           (varsayılan /api/catalog/agent/baglilar)
//                      Yayında nginx /catalog/ -> gateway -> /api/catalog/ çevirdiği için
//                      dışarıdan doğru yol /catalog/agent/baglilar.
//    --ajan-anahtari <anahtar>
//                      Yönetim ekranında üretilen pkfr_... anahtarı. Verilirse istemci
//                      önce token ucundan ajan token'ı alır, hub'a onunla bağlanır.
//    --token-ucu <adres> Ajan token ucunun tam adresi
//                      (varsayılan <api>/api/auth/agent/token; yayında
//                       https://dijitalmasraf.com/auth/agent/token)
//    --token <jwt>     Hazır KULLANICI token'ı (durum ucu için).
//    --ajan-token <jwt> Hazır AJAN token'ı (hub için).
//    --imza  <anahtar> JWT imza anahtarı            (varsayılan: Development ayarı)
//                      Yukarıdakiler verilmezse iki token da bununla yerelde üretilir.
//    --issuer / --audience / --surum / --makine-id / --makine-adi / --kullanici / --ajan-id
// =============================================================================

var ayar = Ayarlar.Oku(args);

Console.OutputEncoding = Encoding.UTF8;

var rapor = new Rapor();
using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };

// Kullanıcı token'ı durum ucu için, ajan token'ı hub için.
var insanToken = ayar.Token ?? Jwt.Kullanici(ayar);
string ajanToken;
string ajanTokenKaynagi;

try
{
    (ajanToken, ajanTokenKaynagi) = await AjanTokeniBul(http, ayar);
}
catch (Exception ex)
{
    Console.WriteLine($"Ajan token'ı alınamadı: {ex.Message}");
    return 1;
}

Console.WriteLine($"Hub        : {ayar.HubAdresi}");
Console.WriteLine($"Durum ucu  : {ayar.DurumUcu}");
Console.WriteLine($"Makine     : {ayar.MakineAdi} ({ayar.MakineId})");
Console.WriteLine($"Ajan token : {ajanTokenKaynagi}");
Console.WriteLine($"İnsan token: {(ayar.Token is null ? "yerelde üretildi" : "dışarıdan verildi")}");
Console.WriteLine(new string('-', 72));

HubConnection? birinci = null;
HubConnection? ikinci = null;

try
{
    // 1 -----------------------------------------------------------------------
    await rapor.Calistir("Durum ucu token'sız istekte 401 dönüyor", async () =>
    {
        var yanit = await http.GetAsync(ayar.DurumUcu);
        Beklenir(yanit.StatusCode == HttpStatusCode.Unauthorized, $"dönen kod: {(int)yanit.StatusCode}");
    });

    // 2 -----------------------------------------------------------------------
    await rapor.Calistir("Token'sız hub bağlantısı reddediliyor", async () =>
    {
        await using var tokensiz = new HubConnectionBuilder().WithUrl(ayar.HubAdresi).Build();
        await BaglantiReddedilmeli(tokensiz);
    });

    // 3 -----------------------------------------------------------------------
    await rapor.Calistir("Hub kullanıcı token'ını kabul etmiyor", async () =>
    {
        // Ajan olmayan bir istemci ajan gibi kaydolup iş emri bekleyemesin.
        await using var insan = Baglanti(ayar, insanToken);
        await BaglantiReddedilmeli(insan);
    });

    // 4 -----------------------------------------------------------------------
    await rapor.Calistir("Durum ucu ajan token'ını kabul etmiyor", async () =>
    {
        using var istek = new HttpRequestMessage(HttpMethod.Get, ayar.DurumUcu);
        istek.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ajanToken);

        var yanit = await http.SendAsync(istek);
        Beklenir(yanit.StatusCode == HttpStatusCode.Forbidden,
                 $"beklenen 403, dönen kod: {(int)yanit.StatusCode}");
    });

    // 5 -----------------------------------------------------------------------
    await rapor.Calistir("Eski sürümle kayıt reddediliyor, mesaj anlaşılır", async () =>
    {
        await using var eski = Baglanti(ayar, ajanToken);
        await eski.StartAsync();
        var sonuc = await eski.InvokeAsync<KayitSonucu>("Kaydol", Istek(ayar, "0.0.1"));
        Beklenir(!sonuc.Kabul, "kayıt kabul edildi — oysa reddedilmeliydi");
        Console.WriteLine($"      sunucu mesajı: {sonuc.Mesaj}");
        Console.WriteLine($"      sunucu {sonuc.SunucuSurumu} / asgari ajan {sonuc.AsgariAjanSurumu}");
    });

    // 6 -----------------------------------------------------------------------
    birinci = Baglanti(ayar, ajanToken);
    await rapor.Calistir("Geçerli sürümle kayıt kabul ediliyor", async () =>
    {
        await birinci.StartAsync();
        var sonuc = await birinci.InvokeAsync<KayitSonucu>("Kaydol", Istek(ayar, ayar.Surum));
        Beklenir(sonuc.Kabul, $"kayıt reddedildi: {sonuc.Mesaj}");
    });

    // 7 -----------------------------------------------------------------------
    DateTimeOffset ilkAtis = default;
    await rapor.Calistir("Ajan 'baglilar' ucunda görünüyor", async () =>
    {
        var ajan = await AjaniBekle(http, ayar, insanToken, olmali: true);
        Beklenir(ajan is not null, "ajan listede yok");
        Console.WriteLine($"      {ajan!.MakineAdi} / {ajan.AjanSurumu} / {ajan.IsletimSistemi} " +
                          $"/ ajan {ajan.AjanId} / ORKA: {Yazi(ajan.OrkaCalisiyorMu)}");
        ilkAtis = ajan.SonKalpAtisi;
    });

    // 8 -----------------------------------------------------------------------
    await rapor.Calistir("Kalp atışı son atış zamanını ilerletiyor", async () =>
    {
        await Task.Delay(1100); // sunucu saatinin ölçebileceği kadar bekle
        await birinci.InvokeAsync("KalpAtisi");
        var ajan = await AjaniBekle(http, ayar, insanToken, olmali: true);
        Beklenir(ajan!.SonKalpAtisi > ilkAtis,
                 $"son atış ilerlemedi ({ilkAtis:HH:mm:ss.fff} -> {ajan.SonKalpAtisi:HH:mm:ss.fff})");
    });

    // 9 -----------------------------------------------------------------------
    await rapor.Calistir("Aynı MakineId ile ikinci bağlantı eskisini düşürüyor", async () =>
    {
        ikinci = Baglanti(ayar, ajanToken);
        await ikinci.StartAsync();
        var sonuc = await ikinci.InvokeAsync<KayitSonucu>("Kaydol", Istek(ayar, ayar.Surum));
        Beklenir(sonuc.Kabul, $"ikinci kayıt reddedildi: {sonuc.Mesaj}");

        await KosulBekle(() => birinci!.State == HubConnectionState.Disconnected);
        Beklenir(birinci!.State == HubConnectionState.Disconnected,
                 $"eski bağlantı hâlâ ayakta ({birinci.State})");

        var liste = await Baglilar(http, ayar, insanToken);
        var kacTane = liste.Count(a => a.MakineId == ayar.MakineId);
        Beklenir(kacTane == 1, $"listede aynı makineden {kacTane} kayıt var");
    });

    // 10 ----------------------------------------------------------------------
    await rapor.Calistir("Bağlantı kopunca ajan listeden siliniyor", async () =>
    {
        await ikinci!.StopAsync();
        var ajan = await AjaniBekle(http, ayar, insanToken, olmali: false);
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

/// <summary>
/// Hub'a bağlanacak ajan token'ı: hazır verildiyse o, anahtar verildiyse token
/// ucundan alınan, hiçbiri yoksa yerelde üretilen.
/// </summary>
static async Task<(string Token, string Kaynak)> AjanTokeniBul(HttpClient http, Ayarlar ayar)
{
    if (ayar.AjanToken is not null)
        return (ayar.AjanToken, "dışarıdan verildi");

    if (ayar.AjanAnahtari is null)
        return (Jwt.Ajan(ayar), "yerelde üretildi");

    var yanit = await http.PostAsJsonAsync(ayar.TokenUcu, new { AjanAnahtari = ayar.AjanAnahtari });
    if (!yanit.IsSuccessStatusCode)
        throw new DogrulamaHatasi(
            $"token ucu {(int)yanit.StatusCode} döndü ({ayar.TokenUcu}): {await yanit.Content.ReadAsStringAsync()}");

    var icerik = await yanit.Content.ReadFromJsonAsync<AjanTokenYaniti>(
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

    if (string.IsNullOrWhiteSpace(icerik?.Token))
        throw new DogrulamaHatasi($"token ucu boş yanıt verdi ({ayar.TokenUcu})");

    return (icerik!.Token, $"anahtarla alındı ({icerik.AjanAdi} #{icerik.AjanId}, bitiş {icerik.GecerlilikBitisiUtc:u})");
}

static HubConnection Baglanti(Ayarlar ayar, string token) =>
    new HubConnectionBuilder()
        .WithUrl(ayar.HubAdresi, o => o.AccessTokenProvider = () => Task.FromResult<string?>(token))
        .Build();

static async Task BaglantiReddedilmeli(HubConnection baglanti)
{
    try
    {
        await baglanti.StartAsync();
        Beklenir(false, "bağlantı kuruldu — oysa reddedilmeliydi");
    }
    catch (Exception ex) when (ex is not DogrulamaHatasi)
    {
        // Beklenen: negotiate 401/403 döndüğü için StartAsync patlar.
        Console.WriteLine($"      (beklenen hata: {ex.GetType().Name})");
    }
}

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
    using var istek = new HttpRequestMessage(HttpMethod.Get, ayar.DurumUcu);
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
    public string AjanId { get; set; } = string.Empty;
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

class AjanTokenYaniti
{
    public string Token { get; set; } = string.Empty;
    public DateTime GecerlilikBitisiUtc { get; set; }
    public int AjanId { get; set; }
    public string AjanAdi { get; set; } = string.Empty;
}

class Ayarlar
{
    public string ApiAdresi { get; private set; } = "http://localhost:5004";
    public string HubAdresi { get; private set; } = "";
    public string DurumYolu { get; private set; } = "/api/catalog/agent/baglilar";
    public string TokenUcu { get; private set; } = "";
    public string? Token { get; private set; }
    public string? AjanToken { get; private set; }
    public string? AjanAnahtari { get; private set; }
    public string ImzaAnahtari { get; private set; } = "super_secret_dev_key_32bytes_minimum";
    public string Issuer { get; private set; } = "identityserver.tr";
    public string Audience { get; private set; } = "identityclient.tr";
    public string Surum { get; private set; } = "1.0.0";
    public string MakineId { get; private set; } = "TEST-" + Guid.NewGuid().ToString("N")[..8];
    public string MakineAdi { get; private set; } = "TEST-ISTEMCI";
    public string KullaniciId { get; private set; } = "test-istemci";
    public string AjanId { get; private set; } = "9999";

    // Yerelde CatalogService'e doğrudan gidildiği için yol /api/... ; yayında nginx
    // /catalog/ önekini gateway'e verip /api/catalog/'a çevirdiğinden dışarıdan
    // doğru yol /catalog/agent/baglilar.
    public string DurumUcu => $"{ApiAdresi}{DurumYolu}";

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
                case "--durum-yolu": a.DurumYolu = "/" + deger.Trim('/'); break;
                case "--token-ucu": a.TokenUcu = deger; break;
                case "--token": a.Token = deger; break;
                case "--ajan-token": a.AjanToken = deger; break;
                case "--ajan-anahtari": a.AjanAnahtari = deger; break;
                case "--imza": a.ImzaAnahtari = deger; break;
                case "--issuer": a.Issuer = deger; break;
                case "--audience": a.Audience = deger; break;
                case "--surum": a.Surum = deger; break;
                case "--makine-id": a.MakineId = deger; break;
                case "--makine-adi": a.MakineAdi = deger; break;
                case "--kullanici": a.KullaniciId = deger; break;
                case "--ajan-id": a.AjanId = deger; break;
            }
        }

        if (string.IsNullOrEmpty(a.HubAdresi))
            a.HubAdresi = $"{a.ApiAdresi}/agenthub";

        // Yerelde IdentityService ayrı portta; yayında gateway'in /auth/ kuralı
        // taşıyor. İkisi de --token-ucu ile verilebiliyor.
        if (string.IsNullOrEmpty(a.TokenUcu))
            a.TokenUcu = $"{a.ApiAdresi}/api/auth/agent/token";

        return a;
    }
}

// IdentityService'i ayağa kaldırmadan doğrulama yapabilmek için token'lar yerelde
// üretilebiliyor: sunucunun kontrol ettiği şey imza, issuer, audience ve
// claim'ler — hepsi burada taklit edilebilir.
static class Jwt
{
    /// <summary>Durum ucunun kabul ettiği insan token'ı.</summary>
    public static string Kullanici(Ayarlar ayar) => Bas(ayar, new[]
    {
        new Claim("sub", ayar.KullaniciId),
        new Claim(ClaimTypes.NameIdentifier, ayar.KullaniciId),
        new Claim("name", ayar.KullaniciId),
        new Claim("role", "Admin")
    });

    /// <summary>
    /// Hub'ın kabul ettiği ajan token'ı. Ayırt edici claim <c>ajan_id</c>:
    /// sunucu tarafındaki politika buna bakıyor.
    /// </summary>
    public static string Ajan(Ayarlar ayar) => Bas(ayar, new[]
    {
        new Claim("sub", $"ajan-{ayar.AjanId}"),
        new Claim("typ", "agent"),
        new Claim("ajan_id", ayar.AjanId),
        new Claim("ajan_adi", "TEST-AJAN")
    });

    private static string Bas(Ayarlar ayar, IEnumerable<Claim> claims)
    {
        var anahtar = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(ayar.ImzaAnahtari));
        var jeton = new JwtSecurityToken(
            issuer: ayar.Issuer,
            audience: ayar.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(anahtar, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(jeton);
    }
}
