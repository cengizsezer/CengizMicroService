using CatalogService.Api.Features.BankaEkstre.Domain;
using CatalogService.Api.Features.BankaEkstre.Services.Parsing;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.BankaEkstre
{
    /// <summary>
    /// Vakıfbank vadesiz TL için açıklama şablonları, unvan desenleri ve sabit kurallar.
    /// Bu üç tablo kasıtlı olarak veritabanındadır: yeni banka eklerken kod değişmez,
    /// yalnız buraya (veya arayüzden) satır eklenir.
    ///
    /// Satır bazında idempotent: aynı (ParserTipi + desen) kaydı ikinci kez eklenmez,
    /// mevcut kayıtların üzerine yazılmaz — kullanıcı düzenlemesi korunur.
    /// İçerik banka bazlı referans olduğundan tenant'tan bağımsız tek sefer çalışır.
    /// </summary>
    public static class BankaEkstreSeed
    {
        private const string Vakifbank = VakifbankVadesizParser.Tip;

        public static async Task SeedAsync(CatalogContext db, CancellationToken ct = default)
        {
            await SablonlariSeedAsync(db, ct);
            await DesenleriSeedAsync(db, ct);
            await KurallariSeedAsync(db, ct);
            await db.SaveChangesAsync(ct);
        }

        // ---- Açıklama şablonları ----

        private static async Task SablonlariSeedAsync(CatalogContext db, CancellationToken ct)
        {
            var mevcut = await db.EkstreAciklamaSablonlari
                .Where(s => s.ParserTipi == Vakifbank)
                .Select(s => s.IslemTipiDeseni)
                .ToListAsync(ct);

            var kayitli = new HashSet<string>(mevcut, StringComparer.OrdinalIgnoreCase);
            var sira = 0;

            void Ekle(string islemTipi, string sablon, bool bankalarArasi = false, EslesmeTuru tur = EslesmeTuru.Tam)
            {
                sira += 10;
                if (!kayitli.Add(islemTipi)) return;

                db.EkstreAciklamaSablonlari.Add(new AciklamaSablonu
                {
                    ParserTipi = Vakifbank,
                    IslemTipiDeseni = islemTipi,
                    EslesmeTuru = tur,
                    Sablon = sablon,
                    BankalarArasi = bankalarArasi,
                    Sira = sira,
                    Aktif = true
                });
            }

            // Gelen para
            Ekle("Gelen EFT Otomatik Yatan", "Gelen Eft - {UNVAN}");
            Ekle("Tös Hesaba Havale", "Gelen Eft - {UNVAN}");
            Ekle("Alınan havale", "Gelen Eft - {UNVAN}");
            Ekle("Gelen FAST Anlık Ödeme", "Gelen Eft - {UNVAN}");
            Ekle("Gelen EFT Ödeme", "Gelen Eft - {UNVAN}");

            // Giden para
            Ekle("FAST Anlık Ödeme", "Giden Eft - {UNVAN}");
            Ekle("Hesaba giden EFT", "Giden Eft - {UNVAN}");
            Ekle("Gönderilen havale", "Giden Eft - {UNVAN}");

            // Bankalar arası: unvan yerine banka adı kullanılır, Katman 3 burada devreye girer.
            Ekle("Otomatik Süpürme İşlemleri Virman", "Otomatik Süpürme Pkf Aday", bankalarArasi: true);
            Ekle("Virman", "Hesaplararası Virman - {HESAP}", bankalarArasi: true);
            Ekle("Hesaplar Arası EFT", "Hesaplar Arası Eft - {BANKA}", bankalarArasi: true, tur: EslesmeTuru.Icerir);

            // Sabit giderler
            Ekle("HGS Bakiye Yükle", "Hgs Bakiye Yüklemesi - {PLAKA}");
            Ekle("Otoyolu Bakiye Yükle", "Hgs Bakiye Yüklemesi - {PLAKA}", tur: EslesmeTuru.Icerir);
            Ekle("MKK Masrafı", "Banka Gideri");
            Ekle("DIT Yp transfer", "Banka Gideri");
            Ekle("Vergi Tahsilatı", "Vergi Ödemesi - {VERGI}");
            Ekle("Kredi Kartı Borç Öde", "Kredi Kartı Borç Ödemesi");
        }

        // ---- Unvan desenleri ----

        private static async Task DesenleriSeedAsync(CatalogContext db, CancellationToken ct)
        {
            var mevcut = await db.EkstreUnvanDesenleri
                .Where(d => d.ParserTipi == Vakifbank)
                .Select(d => d.Desen)
                .ToListAsync(ct);

            var kayitli = new HashSet<string>(mevcut, StringComparer.Ordinal);
            var sira = 0;

            void Ekle(string desen, string aciklama)
            {
                sira += 10;
                if (!kayitli.Add(desen)) return;

                db.EkstreUnvanDesenleri.Add(new UnvanDeseni
                {
                    ParserTipi = Vakifbank,
                    Desen = desen,
                    GrupNo = 1,
                    Sira = sira,
                    Aktif = true,
                    Aciklama = aciklama
                });
            }

            // Sıra ölçülen kapsamaya göre: en çok yakalayan desen önce denenir.
            Ekle(@"sorgu numaralı (.+?) tarafından", "Gelen EFT gövdesi (ölçümde 120 satır)");
            Ekle(@"nolu ([A-ZÇĞİÖŞÜ0-9][^/]{4,70}?) hesab", "\"... nolu X hesabına\" kalıbı (72 satır)");
            Ekle(@"sorgu no'lu \S+ (.+)$", "Sorgu numarasından sonra kalan metin (32 satır)");
            Ekle(@"nolu ([A-ZÇĞİÖŞÜ][A-ZÇĞİÖŞÜ0-9.\s&]{4,60})", "Büyük harfli unvan (12 satır)");
            Ekle(@"^([A-ZÇĞİÖŞÜ0-9][^/]{4,60}?)\s*/\s*[A-ZÇĞİÖŞÜ]", "Eğik çizgi öncesi unvan (6 satır)");
            Ekle(@"^(.+?)\s*\(", "Parantez öncesi metin (~30 satır)");
        }

        // ---- Sabit kurallar (Katman 4) ----

        private static async Task KurallariSeedAsync(CatalogContext db, CancellationToken ct)
        {
            var mevcut = await db.EkstreSabitKurallar
                .Where(k => k.ParserTipi == Vakifbank)
                .Select(k => k.IslemTipiDeseni)
                .ToListAsync(ct);

            var kayitli = new HashSet<string>(mevcut, StringComparer.OrdinalIgnoreCase);
            var sira = 0;

            void Ekle(string islemTipi, string kod, string ad, EslesmeTuru tur = EslesmeTuru.Tam)
            {
                sira += 10;
                if (!kayitli.Add(islemTipi)) return;

                db.EkstreSabitKurallar.Add(new SabitKural
                {
                    ParserTipi = Vakifbank,
                    IslemTipiDeseni = islemTipi,
                    EslesmeTuru = tur,
                    HesapKodu = kod,
                    HesapAdi = ad,
                    Guven = 0.95m,
                    Sira = sira,
                    Aktif = true
                });
            }

            // Kodlar boşluklu ORKA formatında; ana hesap seviyesinde bırakıldı, muavin
            // kırılımı firmadan firmaya değiştiği için arayüzden düzenlenmeli.
            Ekle("MKK Masrafı", "770", "Genel Yönetim Giderleri");
            Ekle("DIT Yp transfer", "770", "Genel Yönetim Giderleri");
            Ekle("Masraf", "770", "Genel Yönetim Giderleri", EslesmeTuru.Icerir);
            Ekle("Komisyon", "770", "Genel Yönetim Giderleri", EslesmeTuru.Icerir);
            Ekle("BSMV", "770", "Genel Yönetim Giderleri", EslesmeTuru.Icerir);
            Ekle("Kambiyo", "770", "Genel Yönetim Giderleri", EslesmeTuru.Icerir);
            Ekle("HGS Bakiye Yükle", "740", "Hizmet Üretim Maliyeti");
            Ekle("Otoyolu Bakiye Yükle", "740", "Hizmet Üretim Maliyeti", EslesmeTuru.Icerir);
        }
    }
}
