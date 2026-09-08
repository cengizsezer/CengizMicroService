using System.Text.Json;

namespace PkfRobot.Core;

/// <summary>Okuyucunun donduru: gorulen satirlar + tablo ekrana sigdi mi.</summary>
public record GridOkumasi(IReadOnlyList<OkunanGridSatiri> Satirlar, bool Kesildi)
{
    public static GridOkumasi Bos { get; } = new(Array.Empty<OkunanGridSatiri>(), false);
}

/// <summary>
/// Ekran goruntusundeki grid'i OKUYAN birim.
///
/// <b>Arayuz olmasinin iki sebebi var.</b> Birincisi test: karsilastirma ve
/// raporlama kurallari aga cikmadan sinanabilmeli. Ikincisi tasima: okuma isi
/// bugun robot makinesinde yapiliyor, yarin DijitalMasraf sunucusuna
/// tasinabilir (goruntuyu zaten yukleyebilen bir uc var). O gun degisecek olan
/// yalnizca bu arayuzun uygulamasi -- baglama noktasi, karsilastirma ve log
/// aynen kalir.
/// </summary>
public interface IGridOkuyucu
{
    /// <summary>
    /// Goruntudeki grid satirlarini okur.
    ///
    /// <b>Hicbir kosulda istisna atmamali:</b> bu bir on eleme katmani ve
    /// robotu durdurmamali. Ag hatasi, kota, zaman asimi, bozuk yanit -- hepsi
    /// uygulamanin icinde loglanip <see cref="GridOkumasi.Bos"/> olarak doner.
    /// </summary>
    Task<GridOkumasi> OkuAsync(string goruntuYolu, CancellationToken ct);
}

/// <summary>
/// Okuyucuya verilen yonerge ve yanitinin cozumu.
///
/// <b>Neden burada:</b> ikisi de saf metin isi ve aga dokunmuyor; prompt'un
/// istedigi bicimle ayristiricinin bekledigi bicim ayni dosyada dursun ki biri
/// degisip digeri unutulmasin.
/// </summary>
public static class GridOkuma
{
    /// <summary>
    /// Modele verilen yonerge.
    ///
    /// <b>Soru "bu grid dogru mu" DEGIL.</b> Muhasebe karari istenmiyor: hangi
    /// kodun dogru oldugunu robot zaten biliyor, modelden tek istenen ekranda
    /// YAZAN'i aktarmak. Yonerge bu yuzden defalarca "yorum yapma" diyor ve
    /// emin olunmayan karakter icin tahmin degil BOS birakmayi soyluyor:
    /// uydurulmus bir kod, tutan bir satir gibi gorunup uyariyi susturur.
    /// </summary>
    public const string Yonerge = """
        Bu bir muhasebe programinin tablo (grid) ekran goruntusu.

        Gorevin yalnizca OKUMAK. Yorum yapma, degerlendirme yapma, dogru olup
        olmadigina karar verme, eksik gordugunu tamamlama. Ekranda YAZAN metni
        oldugu gibi aktar.

        Tabloda "Karsi Hesap Kodu" baslikli (ya da ona en yakin) bir kolon var.
        Baslik satirini SAYMA. Veri satirlarini ustten alta sirayla, her biri icin:
          sira     : 1'den baslayan kendi sayacin (tablodaki numara kolonu degil)
          aciklama : aciklama kolonundaki metin (yoksa bos birak)
          kod      : Karsi Hesap Kodu kolonundaki metin (bos ise bos birak)

        Kodu OLDUGU GIBI yaz: bosluklari, tireleri, bastaki sifirlari koru.
        Bir karakterden emin degilsen TAHMIN ETME, o satirin kodunu bos birak.

        Tablo asagi dogru ekrana sigmiyorsa yalnizca GORUNEN satirlari yaz ve
        kesildi alanini true yap.

        Yalnizca su JSON'u don, oncesinde ve sonrasinda hicbir sey yazma:
        {"kesildi": false, "satirlar": [{"sira": 1, "aciklama": "", "kod": ""}]}
        """;

    /// <summary>
    /// Modelin yanitini cozer. Cozulemezse <see cref="GridOkumasi.Bos"/> --
    /// bos okuma "dogrulanamadi" olarak raporlaniyor, hata olarak degil.
    ///
    /// <b>Neden elle temizlik:</b> yanit kod cercevesi (``` ) ya da bir cumleyle
    /// sarili gelebiliyor. Yonerge bunu yasakliyor ama yonergeye uyulmadi diye
    /// dogrulamayi tumden kaybetmek gereksiz; ilk '{' ile son '}' arasi
    /// aliniyor.
    /// </summary>
    public static GridOkumasi Coz(string? yanit)
    {
        if (string.IsNullOrWhiteSpace(yanit)) return GridOkumasi.Bos;

        var govde = GovdeyiAyikla(yanit);
        if (govde is null) return GridOkumasi.Bos;

        try
        {
            using var belge = JsonDocument.Parse(govde);
            var kok = belge.RootElement;

            if (kok.ValueKind != JsonValueKind.Object) return GridOkumasi.Bos;

            var kesildi = kok.TryGetProperty("kesildi", out var k) &&
                          k.ValueKind == JsonValueKind.True;

            if (!kok.TryGetProperty("satirlar", out var dizi) ||
                dizi.ValueKind != JsonValueKind.Array)
                return new GridOkumasi(Array.Empty<OkunanGridSatiri>(), kesildi);

            var satirlar = new List<OkunanGridSatiri>();
            var sayac = 0;

            foreach (var oge in dizi.EnumerateArray())
            {
                if (oge.ValueKind != JsonValueKind.Object) continue;

                sayac++;

                // Sira modelden okunuyor ama esleme ona bakmiyor (bkz.
                // GridDogrulama.Karsilastir); okunamazsa kendi sayacimiz.
                var sira = oge.TryGetProperty("sira", out var s) && s.TryGetInt32(out var n)
                    ? n
                    : sayac;

                satirlar.Add(new OkunanGridSatiri(sira, Metin(oge, "aciklama"), Metin(oge, "kod")));
            }

            return new GridOkumasi(satirlar, kesildi);
        }
        catch (JsonException)
        {
            return GridOkumasi.Bos;
        }
    }

    private static string Metin(JsonElement oge, string alan)
        => oge.TryGetProperty(alan, out var d) && d.ValueKind == JsonValueKind.String
            ? (d.GetString() ?? string.Empty).Trim()
            : string.Empty;

    private static string? GovdeyiAyikla(string yanit)
    {
        var bas = yanit.IndexOf('{');
        var son = yanit.LastIndexOf('}');

        return bas < 0 || son <= bas ? null : yanit[bas..(son + 1)];
    }
}
