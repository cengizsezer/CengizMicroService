using CatalogService.Api.Features.TicaretSicil.Domain;
using CatalogService.Api.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.TicaretSicil
{
    /// <summary>
    /// Ticaret Sicil İşlemleri için tek seferlik örnek içerik. Tablo boşsa, eski statik
    /// içerikteki işlem ve adımları ekler. Belge (ek) yüklemesi admin panelinden yapılır.
    /// İçerik ortak referans olduğundan tenant'tan bağımsız tek sefer çalışır.
    /// </summary>
    public static class TicaretSicilSeed
    {
        public static async Task SeedAsync(CatalogContext db, CancellationToken ct = default)
        {
            if (await db.TicaretSicilIslemler.AnyAsync(ct))
                return;

            var islemler = Build();
            db.TicaretSicilIslemler.AddRange(islemler);
            await db.SaveChangesAsync(ct);
        }

        private static List<TicaretSicilIslem> Build()
        {
            int sira = 1;

            TicaretSicilAdim Adim(int s, string baslik, string? aciklama = null) =>
                new() { Sira = s, Baslik = baslik, Aciklama = aciklama };

            return new List<TicaretSicilIslem>
            {
                new()
                {
                    Slug = "anonim", Kategori = "Kuruluş", Ad = "Anonim Şirket Kuruluşu", Sira = sira++,
                    Aciklama = "Anonim şirketin ticaret siciline tescili için izlenecek adımlar ve örnek belgeler.",
                    Adimlar = new List<TicaretSicilAdim>
                    {
                        Adim(1, "Esas sözleşmenin hazırlanması ve MERSİS başvurusu", "Şirket esas sözleşmesi MERSİS üzerinden hazırlanır ve potansiyel vergi numarası alınır."),
                        Adim(2, "Kuruluş dilekçesi ve tescil talebi", "Ticaret sicil müdürlüğüne hitaben kuruluş dilekçesi düzenlenir."),
                        Adim(3, "Sermayenin ödenmesi ve banka dekontu", "Nakdi sermayenin en az 1/4'ü tescilden önce bankaya bloke edilir."),
                        Adim(4, "İmza beyannamesi ve oda kaydı", "Yetkililerin imza beyannamesi alınır ve ticaret/sanayi odasına kayıt yapılır."),
                    }
                },
                new()
                {
                    Slug = "limited", Kategori = "Kuruluş", Ad = "Limited Şirket Kuruluşu", Sira = sira++,
                    Aciklama = "Limited şirketin ticaret siciline tescili için izlenecek adımlar ve örnek belgeler.",
                    Adimlar = new List<TicaretSicilAdim>
                    {
                        Adim(1, "Şirket sözleşmesinin MERSİS'te hazırlanması", "Limited şirket sözleşmesi MERSİS üzerinden hazırlanır."),
                        Adim(2, "Kuruluş dilekçesi"),
                        Adim(3, "Müdür imza beyannamesi ve oda kaydı"),
                    }
                },
                new()
                {
                    Slug = "sahis", Kategori = "Kuruluş", Ad = "Şahıs İşletmesi Kuruluşu", Sira = sira++,
                    Aciklama = "Gerçek kişi (şahıs) ticari işletmesinin tescili için adımlar ve örnek belgeler.",
                    Adimlar = new List<TicaretSicilAdim>
                    {
                        Adim(1, "Kuruluş dilekçesi ve başvuru"),
                        Adim(2, "İmza beyannamesi"),
                        Adim(3, "Oda kaydı"),
                    }
                },
                new()
                {
                    Slug = "sermaye-artirimi", Kategori = "Değişiklik", Ad = "Sermaye Artırımı", Sira = sira++,
                    Aciklama = "Mevcut şirketin sermaye artırımının tescili için adımlar ve örnek belgeler.",
                    Adimlar = new List<TicaretSicilAdim>
                    {
                        Adim(1, "Genel kurul / ortaklar kurulu kararı", "Sermaye artırımına ilişkin karar alınır ve tutanak düzenlenir."),
                        Adim(2, "Tadil metni (esas sözleşme değişikliği)"),
                        Adim(3, "Tescil dilekçesi ve YMM/SMMM raporu", "Önceki sermayenin ödendiğine dair tespit raporu eklenir."),
                    }
                },
                new()
                {
                    Slug = "nevi-degisikligi", Kategori = "Değişiklik", Ad = "Nevi (Tür) Değişikliği", Sira = sira++,
                    Aciklama = "Şirket türünün değiştirilmesi (ör. Ltd → A.Ş.) için adımlar ve örnek belgeler.",
                    Adimlar = new List<TicaretSicilAdim>
                    {
                        Adim(1, "Tür değiştirme planı ve raporu"),
                        Adim(2, "Ara bilanço ve YMM raporu"),
                        Adim(3, "Genel kurul kararı ve yeni esas sözleşme"),
                        Adim(4, "Tescil dilekçesi"),
                    }
                },
                new()
                {
                    Slug = "birlesme", Kategori = "Yapısal İşlem", Ad = "Birleşme", Sira = sira++,
                    Aciklama = "İki veya daha fazla şirketin birleşmesinin tescili için adımlar ve örnek belgeler.",
                    Adimlar = new List<TicaretSicilAdim>
                    {
                        Adim(1, "Birleşme sözleşmesi ve birleşme raporu"),
                        Adim(2, "Genel kurul kararları", "Birleşmeye taraf her şirkette genel kurul kararı alınır."),
                        Adim(3, "Tescil dilekçesi ve alacaklılara çağrı"),
                    }
                },
                new()
                {
                    Slug = "bolunme", Kategori = "Yapısal İşlem", Ad = "Bölünme", Sira = sira++,
                    Aciklama = "Şirketin tam veya kısmi bölünmesinin tescili için adımlar ve örnek belgeler.",
                    Adimlar = new List<TicaretSicilAdim>
                    {
                        Adim(1, "Bölünme planı / sözleşmesi ve raporu"),
                        Adim(2, "Envanter ve genel kurul kararı"),
                        Adim(3, "Tescil dilekçesi"),
                    }
                },
                new()
                {
                    Slug = "yabanci-sermaye", Kategori = "Kuruluş", Ad = "Yabancı Sermayeli Şirket Kuruluşu", Sira = sira++,
                    Aciklama = "Yabancı ortaklı şirketin tescili için adımlar ve örnek belgeler.",
                    Adimlar = new List<TicaretSicilAdim>
                    {
                        Adim(1, "Yabancı ortak belgelerinin temini", "Yabancı gerçek/tüzel kişi belgeleri apostil/tasdik ve yeminli tercüme ile hazırlanır."),
                        Adim(2, "Esas sözleşme ve MERSİS başvurusu"),
                        Adim(3, "Kuruluş dilekçesi ve potansiyel vergi no"),
                        Adim(4, "Oda kaydı ve doğrudan yabancı yatırım bildirimi"),
                    }
                },
            };
        }
    }
}
