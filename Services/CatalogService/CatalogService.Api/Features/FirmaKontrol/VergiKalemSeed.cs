using System.Text.Json;
using System.Text.Json.Serialization;
using CatalogService.Api.Features.FirmaKontrol.Domain;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.FirmaKontrol
{
    /// <summary>
    /// Kurumlar vergisi beyanname kalemlerini <c>vergi-kalemleri-kv.json</c> dosyasından yükler.
    /// Katalog firmadan bağımsızdır (tenant'a bağlı değil).
    ///
    /// Idempotent ve eklemeli: her açılışta dosyadaki kodlar taranır, veritabanında olmayan
    /// kod eklenir. Mevcut kayıtların metinlerine dokunulmaz — kullanıcı sistem kaleminin
    /// adını/açıklamasını değiştirebildiği için (kod ve grup kilitli) seed onu geri ezmemeli.
    /// </summary>
    public static class VergiKalemSeed
    {
        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };

        /// <summary>Seed dosyasındaki bir kalem. Bağlı istisna Id yerine kodla verilir.</summary>
        private sealed class KalemKaydi
        {
            public string Kod { get; set; } = string.Empty;
            public string Ad { get; set; } = string.Empty;
            public byte Grup { get; set; }
            public string? AltGrup { get; set; }
            public string? KanunMaddesi { get; set; }
            public string? Aciklama { get; set; }
            public string? Hatirlatma { get; set; }
            public string? OranBilgisi { get; set; }
            public byte? UstSinirTuru { get; set; }
            public decimal? UstSinirDeger { get; set; }
            public bool DevredebilirMi { get; set; }
            public bool IstisnayaIliskinMi { get; set; }

            /// <summary>Büyüteceği istisna kaleminin kodu; Id seed sırasında çözülür.</summary>
            public string? BagliIstisnaKod { get; set; }

            public bool AsgariMatrahtanDuser { get; set; }
            public short SiraNo { get; set; }
        }

        public static async Task SeedAsync(CatalogContext db, IWebHostEnvironment env, CancellationToken ct = default)
        {
            var path = Path.Combine(env.ContentRootPath, "Infrastructure", "Setup", "SeedFiles", "vergi-kalemleri-kv.json");
            if (!File.Exists(path))
                return;

            var raw = await File.ReadAllTextAsync(path, ct);
            var kayitlar = JsonSerializer.Deserialize<List<KalemKaydi>>(raw, JsonOpts) ?? new();
            if (kayitlar.Count == 0)
                return;

            var mevcutKodlar = await db.VergiKalemleri
                                       .Select(k => k.Kod)
                                       .ToListAsync(ct);

            var mevcut = mevcutKodlar.ToHashSet(StringComparer.OrdinalIgnoreCase);

            var eklenecek = kayitlar
                .Where(k => !mevcut.Contains(k.Kod))
                .Select(Olustur)
                .ToList();

            if (eklenecek.Count == 0)
            {
                await BagliIstisnalariBaglaAsync(db, kayitlar, ct);
                return;
            }

            db.VergiKalemleri.AddRange(eklenecek);
            await db.SaveChangesAsync(ct);

            // Bağlı istisna referansı ancak istisna kalemleri kaydedilip Id aldıktan sonra kurulabilir.
            await BagliIstisnalariBaglaAsync(db, kayitlar, ct);
        }

        private static VergiKalemi Olustur(KalemKaydi k) => new()
        {
            Kod = k.Kod,
            Ad = k.Ad,
            Grup = (VergiKalemGrubu)k.Grup,
            AltGrup = k.AltGrup,
            KanunMaddesi = k.KanunMaddesi,
            Aciklama = k.Aciklama,
            Hatirlatma = k.Hatirlatma,
            OranBilgisi = k.OranBilgisi,
            UstSinirTuru = k.UstSinirTuru is null ? null : (UstSinirTuru)k.UstSinirTuru.Value,
            UstSinirDeger = k.UstSinirDeger,
            DevredebilirMi = k.DevredebilirMi,
            IstisnayaIliskinMi = k.IstisnayaIliskinMi,
            AsgariMatrahtanDuser = k.AsgariMatrahtanDuser,
            MukellefiyetTuru = MukellefiyetTuru.KurumlarVergisi,
            SiraNo = k.SiraNo,
            SistemKalemi = true,
            Aktif = true
        };

        /// <summary>
        /// İstisnaya ilişkin KKEG kalemlerini bağlı oldukları istisna kalemine bağlar.
        /// Zaten bağlıysa dokunmaz (kullanıcı KKEGI-05'in bağını değiştirmiş olabilir).
        /// </summary>
        private static async Task BagliIstisnalariBaglaAsync(CatalogContext db, List<KalemKaydi> kayitlar, CancellationToken ct)
        {
            var baglanacak = kayitlar
                .Where(k => !string.IsNullOrWhiteSpace(k.BagliIstisnaKod))
                .ToDictionary(k => k.Kod, k => k.BagliIstisnaKod!, StringComparer.OrdinalIgnoreCase);

            if (baglanacak.Count == 0)
                return;

            var ilgiliKodlar = baglanacak.Keys.Concat(baglanacak.Values).ToList();

            var kalemler = await db.VergiKalemleri
                                   .Where(k => ilgiliKodlar.Contains(k.Kod))
                                   .ToListAsync(ct);

            var kodIndeks = kalemler.ToDictionary(k => k.Kod, StringComparer.OrdinalIgnoreCase);
            var degisiklikVar = false;

            foreach (var (kkegKod, istisnaKod) in baglanacak)
            {
                if (!kodIndeks.TryGetValue(kkegKod, out var kkeg)) continue;
                if (kkeg.BagliIstisnaKalemiId is not null) continue;
                if (!kodIndeks.TryGetValue(istisnaKod, out var istisna)) continue;

                kkeg.BagliIstisnaKalemiId = istisna.Id;
                degisiklikVar = true;
            }

            if (degisiklikVar)
                await db.SaveChangesAsync(ct);
        }
    }
}
