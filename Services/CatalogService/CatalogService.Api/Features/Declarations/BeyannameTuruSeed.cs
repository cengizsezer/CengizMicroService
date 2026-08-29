using CatalogService.Api.Features.Declarations.Entities;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.Declarations
{
    /// <summary>
    /// Beyanname türü tanımları. İçerik <c>DeclarationFollow.razor</c> içinde sabit bir
    /// <c>List&lt;string&gt;</c> olarak duruyordu; buraya taşındı ki hem Takip hem Özet
    /// aynı listeyi görsün ve yeni tür eklemek kod değiştirmeyi gerektirmesin.
    ///
    /// <b>Deger alanı aynen korundu.</b> Mevcut beyanname kayıtları
    /// <see cref="Declaration.DeclarationType"/> alanına eski listedeki metni yazmış
    /// durumda ("0015 KDV-1"); tanım tablosu o metni taşımasaydı kurulu veritabanlarındaki
    /// hiçbir kayıt matriste bir kolona düşmezdi.
    ///
    /// Satır bazında idempotent: aynı <c>Deger</c> ikinci kez eklenmez, mevcut kaydın
    /// üzerine yazılmaz (kullanıcı adı düzenlemişse korunur).
    /// </summary>
    public static class BeyannameTuruSeed
    {
        /// <summary>(Saklanan değer, vergi kodu, okunur ad) — sıra matris kolonlarının sırasıdır.</summary>
        public static readonly (string Deger, string? Kod, string Ad)[] Turler =
        {
            ("0015 KDV-1", "0015", "KDV (1 No.lu)"),
            ("4017 KDV-2", "4017", "KDV Tevkifat (2 No.lu)"),
            ("0003 STOPAJ MUHTASAR", "0003", "Gelir Vergisi Stopajı"),
            ("0095 UCRET MUHTASAR", "0095", "Ücret Muhtasarı"),
            // Eski listede kodsuz "SGK" yazıyordu; kod tanıma eklendi ki "4101 SGK" diye
            // yazılmış kayıtlar da aynı kolona düşsün. Saklanan değer değişmedi.
            ("SGK", "4101", "SGK Primi"),
            ("0040 DAMGA VERGISI", "0040", "Damga Vergisi"),
            ("0033 GECİCİ VERGI", "0033", "Geçici Vergi"),
            ("0010 KURUMLAR VERGISI", "0010", "Kurumlar Vergisi")
        };

        /// <summary>
        /// Eksik tanımları ekler. Kaynak <b>dosya değil, yukarıdaki dizi</b>: yayında
        /// eksik bir seed dosyası yüzünden sessizce boş kalma ihtimali yok. Sabit tenant
        /// listesine de bağlı değil — tablo global, tek bir tenant'sız context ile bir kez
        /// doldurulur (bkz. KARARLAR §83'teki hesap planı hatası).
        ///
        /// Aynı metot hem açılışta hem de Tanımlar ekranındaki "Varsayılanları yükle"
        /// düğmesinde çalışır; kurulu bir veritabanında tablo boş kalmışsa kullanıcı
        /// deploy beklemeden doldurabilsin.
        /// </summary>
        /// <returns>Eklenen tanım sayısı ve işlem sonrası toplam tanım sayısı.</returns>
        public static async Task<(int Eklenen, int Toplam)> SeedAsync(CatalogContext db,
                                                                      CancellationToken ct = default)
        {
            var mevcut = await db.BeyannameTurleri.Select(t => t.Deger).ToListAsync(ct);
            var kayitli = new HashSet<string>(mevcut, StringComparer.OrdinalIgnoreCase);

            var sira = 0;
            var eklenen = 0;

            foreach (var (deger, kod, ad) in Turler)
            {
                sira += 10;
                if (!kayitli.Add(deger)) continue;

                db.BeyannameTurleri.Add(new BeyannameTuru
                {
                    Deger = deger,
                    Kod = kod,
                    Ad = ad,
                    Sira = sira,
                    Aktif = true
                });

                eklenen++;
            }

            if (eklenen > 0) await db.SaveChangesAsync(ct);

            return (eklenen, mevcut.Count + eklenen);
        }

        /// <summary>
        /// Açılış sarmalayıcısı: sonucu loga yazar. Tablo doluysa da satır düşülür ki
        /// "seed çalıştı mı?" sorusu logdan cevaplanabilsin.
        /// </summary>
        public static async Task SeedVeLoglaAsync(CatalogContext db, ILogger logger,
                                                   CancellationToken ct = default)
        {
            var (eklenen, toplam) = await SeedAsync(db, ct);

            if (eklenen > 0)
                logger.LogInformation("Beyanname türleri: {Eklenen} tanım eklendi, toplam {Toplam}.",
                                      eklenen, toplam);
            else
                logger.LogInformation("Beyanname türleri: eksik tanım yok, toplam {Toplam}.", toplam);

            if (toplam == 0)
                logger.LogError("Beyanname türleri tablosu seed sonrası hâlâ BOŞ. Özet sekmesi " +
                                "kolonlarını bu tablodan üretiyor; ekran boş görünecek.");
        }
    }
}
