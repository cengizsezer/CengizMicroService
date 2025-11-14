using CatalogService.Api.Features.AccountPlan;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Infrastructure.Context
{
    public static class AccountPlanSeed
    {
        public static async Task SeedAsync(CatalogContext context, ILogger logger)
        {
            // Tüm planı memory'de hazırla
            var allNodes = GetAll();

            // Tabloda hiç kayıt yoksa: full seed
            if (!await context.AccountNodes.AnyAsync())
            {
                logger.LogInformation("📘 Account plan tamamen boş, full seeding başlatılıyor.");

                await context.AccountNodes.AddRangeAsync(allNodes);
                await context.SaveChangesAsync();

                logger.LogInformation("✅ Account plan full yüklendi. {count} kayıt eklendi.", allNodes.Count);
                return;
            }

            // Buraya geldiysek tabloda bir şeyler var (senin 1’ler gibi).
            // Var olan hesap kodlarını çekelim:
            var existingCodes = await context.AccountNodes
                .Select(x => x.Code)
                .ToListAsync();

            // Memory'deki full plandan, DB'de olmayanları süzelim:
            var missingNodes = allNodes
                .Where(n => !existingCodes.Contains(n.Code))
                .ToList();

            if (!missingNodes.Any())
            {
                logger.LogInformation("📘 Account plan zaten tam, eklenecek eksik hesap bulunamadı.");
                return;
            }

            logger.LogInformation("📘 Account plan kısmen var, {count} eksik hesap eklenecek.", missingNodes.Count);

            await context.AccountNodes.AddRangeAsync(missingNodes);
            await context.SaveChangesAsync();

            logger.LogInformation("✅ Account plan güncellendi. {count} yeni hesap eklendi.", missingNodes.Count);
        }

        public static List<AccountNode> GetAll()
        {
            var id = 1;
            var nodes = new List<AccountNode>();

            // ------------------------------
            // 1 - DÖNEN VARLIKLAR (LEVEL 1)
            // ------------------------------
            nodes.Add(new AccountNode
            {
                Id = id,
                Code = "1",
                Name = "Dönen Varlıklar",
                Description = "Dönen Varlıklar",
                Level = 1,
                Order = 1
            });
            var p1 = id++;
            // ---------------------------------------------
            // 10 - HAZIR DEĞERLER
            // ---------------------------------------------
            nodes.Add(new AccountNode { Id = id, Code = "10", Name = "Hazır Değerler", Description = "Hazır Değerler", Level = 2, ParentId = p1, Order = 10 }); var p10 = id++;
            nodes.Add(new AccountNode { Id = id++, Code = "100", Name = "Kasa", Description = "Kasa", Level = 3, ParentId = p10, Order = 100 });
            nodes.Add(new AccountNode { Id = id++, Code = "101", Name = "Alınan Çekler", Description = "Alınan Çekler", Level = 3, ParentId = p10, Order = 101 });
            nodes.Add(new AccountNode { Id = id++, Code = "102", Name = "Bankalar", Description = "Bankalar", Level = 3, ParentId = p10, Order = 102 });
            nodes.Add(new AccountNode { Id = id++, Code = "103", Name = "Verilen Çekler ve Ödeme Emirleri (-)", Description = "Verilen Çekler ve Ödeme Emirleri (-)", Level = 3, ParentId = p10, Order = 103 });
            nodes.Add(new AccountNode { Id = id++, Code = "108", Name = "Diğer Hazır Değerler", Description = "Diğer Hazır Değerler", Level = 3, ParentId = p10, Order = 108 });

            // ---------------------------------------------
            // 11 - MENKUL KIYMETLER
            // ---------------------------------------------
            nodes.Add(new AccountNode { Id = id, Code = "11", Name = "Menkul Kıymetler", Description = "Menkul Kıymetler", Level = 2, ParentId = p1, Order = 11 }); var p11 = id++;
            nodes.Add(new AccountNode { Id = id++, Code = "110", Name = "Hisse Senetleri", Description = "Hisse Senetleri", Level = 3, ParentId = p11, Order = 110 });
            nodes.Add(new AccountNode { Id = id++, Code = "111", Name = "Özel Kesim Tahvil, Senet ve Bonoları", Description = "Özel Kesim Tahvil, Senet ve Bonoları", Level = 3, ParentId = p11, Order = 111 });
            nodes.Add(new AccountNode { Id = id++, Code = "112", Name = "Kamu Kesimi Tahvil, Senet ve Bonoları", Description = "Kamu Kesimi Tahvil, Senet ve Bonoları", Level = 3, ParentId = p11, Order = 112 });
            nodes.Add(new AccountNode { Id = id++, Code = "118", Name = "Diğer Menkul Kıymetler", Description = "Diğer Menkul Kıymetler", Level = 3, ParentId = p11, Order = 118 });
            nodes.Add(new AccountNode { Id = id++, Code = "119", Name = "Menkul Kıymetler Değer Düşüklüğü Karşılığı (-)", Description = "Menkul Kıymetler Değer Düşüklüğü Karşılığı (-)", Level = 3, ParentId = p11, Order = 119 });

            // ---------------------------------------------
            // 12 - TİCARİ ALACAKLAR
            // ---------------------------------------------
            nodes.Add(new AccountNode { Id = id, Code = "12", Name = "Ticari Alacaklar", Description = "Ticari Alacaklar", Level = 2, ParentId = p1, Order = 12 }); var p12 = id++;
            nodes.Add(new AccountNode { Id = id++, Code = "120", Name = "Alıcılar", Description = "Alıcılar", Level = 3, ParentId = p12, Order = 120 });
            nodes.Add(new AccountNode { Id = id++, Code = "121", Name = "Alacak Senetleri", Description = "Alacak Senetleri", Level = 3, ParentId = p12, Order = 121 });
            nodes.Add(new AccountNode { Id = id++, Code = "122", Name = "Alacak Senetleri Reeskontu (-)", Description = "Alacak Senetleri Reeskontu (-)", Level = 3, ParentId = p12, Order = 122 });
            nodes.Add(new AccountNode { Id = id++, Code = "126", Name = "Kazançlanmamış Finansal Kiralama Faiz Gelirleri (-)", Description = "Kazançlanmamış Finansal Kiralama Faiz Gelirleri (-)", Level = 3, ParentId = p12, Order = 126 });
            nodes.Add(new AccountNode { Id = id++, Code = "127", Name = "Diğer Ticari Alacaklar", Description = "Diğer Ticari Alacaklar", Level = 3, ParentId = p12, Order = 127 });
            nodes.Add(new AccountNode { Id = id++, Code = "128", Name = "Şüpheli Ticari Alacaklar", Description = "Şüpheli Ticari Alacaklar", Level = 3, ParentId = p12, Order = 128 });
            nodes.Add(new AccountNode { Id = id++, Code = "129", Name = "Şüpheli Ticari Alacaklar Karşılığı (-)", Description = "Şüpheli Ticari Alacaklar Karşılığı (-)", Level = 3, ParentId = p12, Order = 129 });

            // ---------------------------------------------
            // 13 - DİĞER ALACAKLAR
            // ---------------------------------------------
            nodes.Add(new AccountNode { Id = id, Code = "13", Name = "Diğer Alacaklar", Description = "Diğer Alacaklar", Level = 2, ParentId = p1, Order = 13 }); var p13 = id++;
            nodes.Add(new AccountNode { Id = id++, Code = "131", Name = "Ortaklardan Alacaklar", Description = "Ortaklardan Alacaklar", Level = 3, ParentId = p13, Order = 131 });
            nodes.Add(new AccountNode { Id = id++, Code = "132", Name = "İştiraklerden Alacaklar", Description = "İştiraklerden Alacaklar", Level = 3, ParentId = p13, Order = 132 });
            nodes.Add(new AccountNode { Id = id++, Code = "133", Name = "Bağlı Ortaklıklardan Alacaklar", Description = "Bağlı Ortaklıklardan Alacaklar", Level = 3, ParentId = p13, Order = 133 });
            nodes.Add(new AccountNode { Id = id++, Code = "135", Name = "Personelden Alacaklar", Description = "Personelden Alacaklar", Level = 3, ParentId = p13, Order = 135 });
            nodes.Add(new AccountNode { Id = id++, Code = "136", Name = "Diğer Çeşitli Alacaklar", Description = "Diğer Çeşitli Alacaklar", Level = 3, ParentId = p13, Order = 136 });
            nodes.Add(new AccountNode { Id = id++, Code = "137", Name = "Diğer Alacak Senetleri Reeskontu (-)", Description = "Diğer Alacak Senetleri Reeskontu (-)", Level = 3, ParentId = p13, Order = 137 });
            nodes.Add(new AccountNode { Id = id++, Code = "138", Name = "Şüpheli Diğer Alacaklar", Description = "Şüpheli Diğer Alacaklar", Level = 3, ParentId = p13, Order = 138 });
            nodes.Add(new AccountNode { Id = id++, Code = "139", Name = "Şüpheli Diğer Alacaklar Karşılığı (-)", Description = "Şüpheli Diğer Alacaklar Karşılığı (-)", Level = 3, ParentId = p13, Order = 139 });

            // ---------------------------------------------
            // 15 - STOKLAR
            // ---------------------------------------------
            nodes.Add(new AccountNode { Id = id, Code = "15", Name = "Stoklar", Description = "Stoklar", Level = 2, ParentId = p1, Order = 15 }); var p15 = id++;
            nodes.Add(new AccountNode { Id = id++, Code = "150", Name = "İlk Madde ve Malzeme", Description = "İlk Madde ve Malzeme", Level = 3, ParentId = p15, Order = 150 });
            nodes.Add(new AccountNode { Id = id++, Code = "151", Name = "Yarı Mamuller – Üretim", Description = "Yarı Mamuller – Üretim", Level = 3, ParentId = p15, Order = 151 });
            nodes.Add(new AccountNode { Id = id++, Code = "152", Name = "Mamuller", Description = "Mamuller", Level = 3, ParentId = p15, Order = 152 });
            nodes.Add(new AccountNode { Id = id++, Code = "153", Name = "Ticari Mallar", Description = "Ticari Mallar", Level = 3, ParentId = p15, Order = 153 });
            nodes.Add(new AccountNode { Id = id++, Code = "157", Name = "Diğer Stoklar", Description = "Diğer Stoklar", Level = 3, ParentId = p15, Order = 157 });
            nodes.Add(new AccountNode { Id = id++, Code = "158", Name = "Stok Değer Düşüklüğü Karşılığı (-)", Description = "Stok Değer Düşüklüğü Karşılığı (-)", Level = 3, ParentId = p15, Order = 158 });
            nodes.Add(new AccountNode { Id = id++, Code = "159", Name = "Verilen Sipariş Avansları", Description = "Verilen Sipariş Avansları", Level = 3, ParentId = p15, Order = 159 });

            // ---------------------------------------------
            // 17 – YILLARA YAYGIN İNŞAAT VE ONARIM MALİYETLERİ
            // ---------------------------------------------
            nodes.Add(new AccountNode { Id = id, Code = "17", Name = "Yıllara Yaygın İnşaat ve Onarım Maliyetleri", Description = "Yıllara Yaygın İnşaat ve Onarım Maliyetleri", Level = 2, ParentId = p1, Order = 17 }); var p17 = id++;
            nodes.Add(new AccountNode { Id = id++, Code = "170", Name = "Yıllara Yaygın İnşaat ve Onarım Maliyetleri", Description = "Yıllara Yaygın İnşaat ve Onarım Maliyetleri", Level = 3, ParentId = p17, Order = 170 });
            nodes.Add(new AccountNode { Id = id++, Code = "171", Name = "Yıllara Yaygın İnşaat Enflasyon Düzeltme Hesabı", Description = "Yıllara Yaygın İnşaat Enflasyon Düzeltme Hesabı", Level = 3, ParentId = p17, Order = 171 });
            nodes.Add(new AccountNode { Id = id++, Code = "179", Name = "Taşeronlara Verilen Avanslar", Description = "Taşeronlara Verilen Avanslar", Level = 3, ParentId = p17, Order = 179 });

            // ---------------------------------------------
            // 18 – GELECEK AYLARA AİT GİDERLER VE GELİR TAHAKKUKLARI
            // ---------------------------------------------
            nodes.Add(new AccountNode { Id = id, Code = "18", Name = "Gelecek Aylara Ait Giderler ve Gelir Tahakkukları", Description = "Gelecek Aylara Ait Giderler ve Gelir Tahakkukları", Level = 2, ParentId = p1, Order = 18 }); var p18 = id++;
            nodes.Add(new AccountNode { Id = id++, Code = "180", Name = "Gelecek Aylara Ait Giderler", Description = "Gelecek Aylara Ait Giderler", Level = 3, ParentId = p18, Order = 180 });
            nodes.Add(new AccountNode { Id = id++, Code = "181", Name = "Gelir Tahakkukları", Description = "Gelir Tahakkukları", Level = 3, ParentId = p18, Order = 181 });

            // ---------------------------------------------
            // 19 – DİĞER DÖNEN VARLIKLAR
            // ---------------------------------------------
            nodes.Add(new AccountNode { Id = id, Code = "19", Name = "Diğer Dönen Varlıklar", Description = "Diğer Dönen Varlıklar", Level = 2, ParentId = p1, Order = 19 }); var p19 = id++;
            nodes.Add(new AccountNode { Id = id++, Code = "190", Name = "Devreden KDV", Description = "Devreden KDV", Level = 3, ParentId = p19, Order = 190 });
            nodes.Add(new AccountNode { Id = id++, Code = "191", Name = "İndirilecek KDV", Description = "İndirilecek KDV", Level = 3, ParentId = p19, Order = 191 });
            nodes.Add(new AccountNode { Id = id++, Code = "192", Name = "Diğer KDV", Description = "Diğer KDV", Level = 3, ParentId = p19, Order = 192 });
            nodes.Add(new AccountNode { Id = id++, Code = "193", Name = "Peşin Ödenen Vergi ve Fonlar", Description = "Peşin Ödenen Vergi ve Fonlar", Level = 3, ParentId = p19, Order = 193 });
            nodes.Add(new AccountNode { Id = id++, Code = "197", Name = "Sayım ve Tesellüm Fazlaları", Description = "Sayım ve Tesellüm Fazlaları", Level = 3, ParentId = p19, Order = 197 });
            nodes.Add(new AccountNode { Id = id++, Code = "198", Name = "Diğer Çeşitli Dönen Varlıklar", Description = "Diğer Çeşitli Dönen Varlıklar", Level = 3, ParentId = p19, Order = 198 });
            nodes.Add(new AccountNode { Id = id++, Code = "199", Name = "Diğer Dönen Varlıklar Karşılığı (-)", Description = "Diğer Dönen Varlıklar Karşılığı (-)", Level = 3, ParentId = p19, Order = 199 });

            // ---------------------------------------------


            // ==========================================================
            // 2 – DURAN VARLIKLAR
            // ==========================================================
            nodes.Add(new AccountNode
            {
                Id = id,
                Code = "2",
                Name = "Duran Varlıklar",
                Description = "Duran Varlıklar",
                Level = 1,
                Order = 2
            });
            var p2 = id++;

            // ==========================================================
            // 22 – TİCARİ ALACAKLAR
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "22", Name = "Ticari Alacaklar", Description = "Ticari Alacaklar", Level = 2, ParentId = p2, Order = 22 });
            var p22 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "220", Name = "Alıcılar", Description = "Alıcılar", Level = 3, ParentId = p22, Order = 220 });
            nodes.Add(new AccountNode { Id = id++, Code = "221", Name = "Alacak Senetleri", Description = "Alacak Senetleri", Level = 3, ParentId = p22, Order = 221 });
            nodes.Add(new AccountNode { Id = id++, Code = "222", Name = "Alacak Senetleri Reeskontu (-)", Description = "Alacak Senetleri Reeskontu (-)", Level = 3, ParentId = p22, Order = 222 });
            nodes.Add(new AccountNode { Id = id++, Code = "224", Name = "Kazanılmamış Finansal Kiralama Faiz Gelirleri", Description = "Kazanılmamış Finansal Kiralama Faiz Gelirleri", Level = 3, ParentId = p22, Order = 224 });
            nodes.Add(new AccountNode { Id = id++, Code = "226", Name = "Verilen Depozito ve Teminatlar", Description = "Verilen Depozito ve Teminatlar", Level = 3, ParentId = p22, Order = 226 });
            nodes.Add(new AccountNode { Id = id++, Code = "229", Name = "Şüpheli Alacaklar Karşılığı (-)", Description = "Şüpheli Alacaklar Karşılığı (-)", Level = 3, ParentId = p22, Order = 229 });

            // ==========================================================
            // 23 – DİĞER ALACAKLAR
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "23", Name = "Diğer Alacaklar", Description = "Diğer Alacaklar", Level = 2, ParentId = p2, Order = 23 });
            var p23 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "231", Name = "Ortaklardan Alacaklar", Description = "Ortaklardan Alacaklar", Level = 3, ParentId = p23, Order = 231 });
            nodes.Add(new AccountNode { Id = id++, Code = "232", Name = "İştiraklerden Alacaklar", Description = "İştiraklerden Alacaklar", Level = 3, ParentId = p23, Order = 232 });
            nodes.Add(new AccountNode { Id = id++, Code = "233", Name = "Bağlı Ortaklıklardan Alacaklar", Description = "Bağlı Ortaklıklardan Alacaklar", Level = 3, ParentId = p23, Order = 233 });
            nodes.Add(new AccountNode { Id = id++, Code = "235", Name = "Personelden Alacaklar", Description = "Personelden Alacaklar", Level = 3, ParentId = p23, Order = 235 });
            nodes.Add(new AccountNode { Id = id++, Code = "236", Name = "Diğer Çeşitli Alacaklar", Description = "Diğer Çeşitli Alacaklar", Level = 3, ParentId = p23, Order = 236 });
            nodes.Add(new AccountNode { Id = id++, Code = "238", Name = "Diğer Alacak Senetleri Reeskontu (-)", Description = "Diğer Alacak Senetleri Reeskontu (-)", Level = 3, ParentId = p23, Order = 238 });
            nodes.Add(new AccountNode { Id = id++, Code = "239", Name = "Şüpheli Diğer Alacaklar Karşılığı (-)", Description = "Şüpheli Diğer Alacaklar Karşılığı (-)", Level = 3, ParentId = p23, Order = 239 });

            // ==========================================================
            // 24 – MALİ DURAN VARLIKLAR
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "24", Name = "Mali Duran Varlıklar", Description = "Mali Duran Varlıklar", Level = 2, ParentId = p2, Order = 24 });
            var p24 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "240", Name = "Bağlı Menkul Kıymetler", Description = "Bağlı Menkul Kıymetler", Level = 3, ParentId = p24, Order = 240 });
            nodes.Add(new AccountNode { Id = id++, Code = "241", Name = "Bağlı Menkul Kıymetler Değer Düşüklüğü Karşılığı (-)", Description = "Bağlı Menkul Kıymetler Değer Düşüklüğü Karşılığı (-)", Level = 3, ParentId = p24, Order = 241 });
            nodes.Add(new AccountNode { Id = id++, Code = "242", Name = "İştirakler", Description = "İştirakler", Level = 3, ParentId = p24, Order = 242 });
            nodes.Add(new AccountNode { Id = id++, Code = "243", Name = "İştiraklere Sermaye Taahhütleri", Description = "İştiraklere Sermaye Taahhütleri", Level = 3, ParentId = p24, Order = 243 });
            nodes.Add(new AccountNode { Id = id++, Code = "244", Name = "İştirakler Sermaye Payları Değer Düşüklüğü Karşılığı (-)", Description = "İştirakler Sermaye Payları Değer Düşüklüğü Karşılığı (-)", Level = 3, ParentId = p24, Order = 244 });
            nodes.Add(new AccountNode { Id = id++, Code = "245", Name = "Bağlı Ortaklıklar", Description = "Bağlı Ortaklıklar", Level = 3, ParentId = p24, Order = 245 });
            nodes.Add(new AccountNode { Id = id++, Code = "246", Name = "Bağlı Ortaklıklara Sermaye Taahhütleri (-)", Description = "Bağlı Ortaklıklara Sermaye Taahhütleri (-)", Level = 3, ParentId = p24, Order = 246 });
            nodes.Add(new AccountNode { Id = id++, Code = "247", Name = "Bağlı Ortaklıklar Sermaye Payları Değer Düşüklüğü Karşılığı (-)", Description = "Bağlı Ortaklıklar Sermaye Payları Değer Düşüklüğü Karşılığı (-)", Level = 3, ParentId = p24, Order = 247 });
            nodes.Add(new AccountNode { Id = id++, Code = "248", Name = "Diğer Mali Duran Varlıklar", Description = "Diğer Mali Duran Varlıklar", Level = 3, ParentId = p24, Order = 248 });
            nodes.Add(new AccountNode { Id = id++, Code = "249", Name = "Diğer Mali Duran Varlıklar Karşılığı (-)", Description = "Diğer Mali Duran Varlıklar Karşılığı (-)", Level = 3, ParentId = p24, Order = 249 });

            // ==========================================================
            // 25 – MADDİ DURAN VARLIKLAR
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "25", Name = "Maddi Duran Varlıklar", Description = "Maddi Duran Varlıklar", Level = 2, ParentId = p2, Order = 25 });
            var p25 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "250", Name = "Arazi ve Arsalar", Description = "Arazi ve Arsalar", Level = 3, ParentId = p25, Order = 250 });
            nodes.Add(new AccountNode { Id = id++, Code = "251", Name = "Yer Altı ve Yer Üstü Düzenleri", Description = "Yer Altı ve Yer Üstü Düzenleri", Level = 3, ParentId = p25, Order = 251 });
            nodes.Add(new AccountNode { Id = id++, Code = "252", Name = "Binalar", Description = "Binalar", Level = 3, ParentId = p25, Order = 252 });
            nodes.Add(new AccountNode { Id = id++, Code = "253", Name = "Tesis, Makine ve Cihazlar", Description = "Tesis, Makine ve Cihazlar", Level = 3, ParentId = p25, Order = 253 });
            nodes.Add(new AccountNode { Id = id++, Code = "254", Name = "Taşıtlar", Description = "Taşıtlar", Level = 3, ParentId = p25, Order = 254 });
            nodes.Add(new AccountNode { Id = id++, Code = "255", Name = "Demirbaşlar", Description = "Demirbaşlar", Level = 3, ParentId = p25, Order = 255 });
            nodes.Add(new AccountNode { Id = id++, Code = "256", Name = "Diğer Maddi Duran Varlıklar", Description = "Diğer Maddi Duran Varlıklar", Level = 3, ParentId = p25, Order = 256 });
            nodes.Add(new AccountNode { Id = id++, Code = "257", Name = "Birikmiş Amortismanlar (-)", Description = "Birikmiş Amortismanlar (-)", Level = 3, ParentId = p25, Order = 257 });
            nodes.Add(new AccountNode { Id = id++, Code = "258", Name = "Yapılmakta Olan Yatırımlar", Description = "Yapılmakta Olan Yatırımlar", Level = 3, ParentId = p25, Order = 258 });
            nodes.Add(new AccountNode { Id = id++, Code = "259", Name = "Verilen Avanslar", Description = "Verilen Avanslar", Level = 3, ParentId = p25, Order = 259 });

            // ==========================================================
            // 26 – MADDİ OLMAYAN DURAN VARLIKLAR
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "26", Name = "Maddi Olmayan Duran Varlıklar", Description = "Maddi Olmayan Duran Varlıklar", Level = 2, ParentId = p2, Order = 26 });
            var p26 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "260", Name = "Haklar", Description = "Haklar", Level = 3, ParentId = p26, Order = 260 });
            nodes.Add(new AccountNode { Id = id++, Code = "261", Name = "Şerefiye", Description = "Şerefiye", Level = 3, ParentId = p26, Order = 261 });
            nodes.Add(new AccountNode { Id = id++, Code = "262", Name = "Kuruluş ve Örgütlenme Giderleri", Description = "Kuruluş ve Örgütlenme Giderleri", Level = 3, ParentId = p26, Order = 262 });
            nodes.Add(new AccountNode { Id = id++, Code = "263", Name = "Araştırma ve Geliştirme Giderleri", Description = "Araştırma ve Geliştirme Giderleri", Level = 3, ParentId = p26, Order = 263 });
            nodes.Add(new AccountNode { Id = id++, Code = "264", Name = "Özel Maliyetler", Description = "Özel Maliyetler", Level = 3, ParentId = p26, Order = 264 });
            nodes.Add(new AccountNode { Id = id++, Code = "267", Name = "Diğer Maddi Olmayan Duran Varlıklar", Description = "Diğer Maddi Olmayan Duran Varlıklar", Level = 3, ParentId = p26, Order = 267 });
            nodes.Add(new AccountNode { Id = id++, Code = "268", Name = "Birikmiş Amortismanlar (-)", Description = "Birikmiş Amortismanlar (-)", Level = 3, ParentId = p26, Order = 268 });
            nodes.Add(new AccountNode { Id = id++, Code = "269", Name = "Verilen Avanslar", Description = "Verilen Avanslar", Level = 3, ParentId = p26, Order = 269 });

            // ==========================================================
            // 27 – ÖZEL TÜKENMEYE TABİ VARLIKLAR
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "27", Name = "Özel Tükenmeye Tabi Varlıklar", Description = "Özel Tükenmeye Tabi Varlıklar", Level = 2, ParentId = p2, Order = 27 });
            var p27 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "271", Name = "Arama Giderleri", Description = "Arama Giderleri", Level = 3, ParentId = p27, Order = 271 });
            nodes.Add(new AccountNode { Id = id++, Code = "272", Name = "Hazırlık ve Geliştirme Giderleri", Description = "Hazırlık ve Geliştirme Giderleri", Level = 3, ParentId = p27, Order = 272 });
            nodes.Add(new AccountNode { Id = id++, Code = "277", Name = "Diğer Özel Tükenmeye Tabi Varlıklar", Description = "Diğer Özel Tükenmeye Tabi Varlıklar", Level = 3, ParentId = p27, Order = 277 });
            nodes.Add(new AccountNode { Id = id++, Code = "278", Name = "Birikmiş Tükenme Payları (-)", Description = "Birikmiş Tükenme Payları (-)", Level = 3, ParentId = p27, Order = 278 });
            nodes.Add(new AccountNode { Id = id++, Code = "279", Name = "Verilen Avanslar", Description = "Verilen Avanslar", Level = 3, ParentId = p27, Order = 279 });

            // ==========================================================
            // 28 – GELECEK YILLARA AİT GİDERLER VE GELİR TAHAKKUKLARI
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "28", Name = "Gelecek Yıllara Ait Giderler ve Gelir Tahakkukları", Description = "Gelecek Yıllara Ait Giderler ve Gelir Tahakkukları", Level = 2, ParentId = p2, Order = 28 });
            var p28 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "280", Name = "Gelecek Yıllara Ait Giderler", Description = "Gelecek Yıllara Ait Giderler", Level = 3, ParentId = p28, Order = 280 });
            nodes.Add(new AccountNode { Id = id++, Code = "281", Name = "Gelir Tahakkukları", Description = "Gelir Tahakkukları", Level = 3, ParentId = p28, Order = 281 });

            // ==========================================================
            // 29 – DİĞER DURAN VARLIKLAR
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "29", Name = "Diğer Duran Varlıklar", Description = "Diğer Duran Varlıklar", Level = 2, ParentId = p2, Order = 29 });
            var p29 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "291", Name = "Gelecek Yıllarda İndirilecek KDV", Description = "Gelecek Yıllarda İndirilecek KDV", Level = 3, ParentId = p29, Order = 291 });
            nodes.Add(new AccountNode { Id = id++, Code = "292", Name = "Diğer Katma Değer Vergisi", Description = "Diğer Katma Değer Vergisi", Level = 3, ParentId = p29, Order = 292 });
            nodes.Add(new AccountNode { Id = id++, Code = "293", Name = "Gelecek Yıllar İhtiyacı Stoklar", Description = "Gelecek Yıllar İhtiyacı Stoklar", Level = 3, ParentId = p29, Order = 293 });
            nodes.Add(new AccountNode { Id = id++, Code = "294", Name = "Elden Çıkarılacak Stoklar ve Maddi Duran Varlıklar", Description = "Elden Çıkarılacak Stoklar ve Maddi Duran Varlıklar", Level = 3, ParentId = p29, Order = 294 });
            nodes.Add(new AccountNode { Id = id++, Code = "295", Name = "Peşin Ödenen Vergiler ve Fonlar", Description = "Peşin Ödenen Vergiler ve Fonlar", Level = 3, ParentId = p29, Order = 295 });
            nodes.Add(new AccountNode { Id = id++, Code = "297", Name = "Diğer Çeşitli Duran Varlıklar", Description = "Diğer Çeşitli Duran Varlıklar", Level = 3, ParentId = p29, Order = 297 });
            nodes.Add(new AccountNode { Id = id++, Code = "298", Name = "Stok Değer Düşüklüğü Karşılığı (-)", Description = "Stok Değer Düşüklüğü Karşılığı (-)", Level = 3, ParentId = p29, Order = 298 });
            nodes.Add(new AccountNode { Id = id++, Code = "299", Name = "Birikmiş Amortismanlar (-)", Description = "Birikmiş Amortismanlar (-)", Level = 3, ParentId = p29, Order = 299 });


            // ==========================================================
            // 3 – KISA VADELİ YABANCI KAYNAKLAR
            // ==========================================================
            nodes.Add(new AccountNode
            {
                Id = id,
                Code = "3",
                Name = "Kısa Vadeli Yabancı Kaynaklar",
                Description = "Kısa Vadeli Yabancı Kaynaklar",
                Level = 1,
                Order = 3
            });
            var p3 = id++;

            // ==========================================================
            // 30 – MALİ BORÇLAR
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "30", Name = "Mali Borçlar", Description = "Mali Borçlar", Level = 2, ParentId = p3, Order = 30 });
            var p30 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "300", Name = "Banka Kredileri", Description = "Banka Kredileri", Level = 3, ParentId = p30, Order = 300 });
            nodes.Add(new AccountNode { Id = id++, Code = "301", Name = "Finansal Kiralama İşlemlerinden Borçlar", Description = "Finansal Kiralama İşlemlerinden Borçlar", Level = 3, ParentId = p30, Order = 301 });
            nodes.Add(new AccountNode { Id = id++, Code = "302", Name = "Ertelenmiş Finansal Kiralama Borçlanma Maliyetleri (-)", Description = "Ertelenmiş Finansal Kiralama Borçlanma Maliyetleri (-)", Level = 3, ParentId = p30, Order = 302 });
            nodes.Add(new AccountNode { Id = id++, Code = "303", Name = "Uzun Vadeli Kredilerin Anapara Taksit ve Faizleri", Description = "Uzun Vadeli Kredilerin Anapara Taksit ve Faizleri", Level = 3, ParentId = p30, Order = 303 });
            nodes.Add(new AccountNode { Id = id++, Code = "304", Name = "Tahvil Anapara Borç, Taksit ve Faizleri", Description = "Tahvil Anapara Borç, Taksit ve Faizleri", Level = 3, ParentId = p30, Order = 304 });
            nodes.Add(new AccountNode { Id = id++, Code = "305", Name = "Çıkarılmış Bonolar ve Senetler", Description = "Çıkarılmış Bonolar ve Senetler", Level = 3, ParentId = p30, Order = 305 });
            nodes.Add(new AccountNode { Id = id++, Code = "306", Name = "Çıkarılmış Diğer Menkul Kıymetler", Description = "Çıkarılmış Diğer Menkul Kıymetler", Level = 3, ParentId = p30, Order = 306 });
            nodes.Add(new AccountNode { Id = id++, Code = "308", Name = "Menkul Kıymetler İhraç Farkı (-)", Description = "Menkul Kıymetler İhraç Farkı (-)", Level = 3, ParentId = p30, Order = 308 });
            nodes.Add(new AccountNode { Id = id++, Code = "309", Name = "Diğer Mali Borçlar", Description = "Diğer Mali Borçlar", Level = 3, ParentId = p30, Order = 309 });

            // ==========================================================
            // 32 – TİCARİ BORÇLAR
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "32", Name = "Ticari Borçlar", Description = "Ticari Borçlar", Level = 2, ParentId = p3, Order = 32 });
            var p32 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "320", Name = "Satıcılar", Description = "Satıcılar", Level = 3, ParentId = p32, Order = 320 });
            nodes.Add(new AccountNode { Id = id++, Code = "321", Name = "Borç Senetleri", Description = "Borç Senetleri", Level = 3, ParentId = p32, Order = 321 });
            nodes.Add(new AccountNode { Id = id++, Code = "322", Name = "Borç Senetleri Reeskontu (-)", Description = "Borç Senetleri Reeskontu (-)", Level = 3, ParentId = p32, Order = 322 });
            nodes.Add(new AccountNode { Id = id++, Code = "326", Name = "Alınan Depozito ve Teminatlar", Description = "Alınan Depozito ve Teminatlar", Level = 3, ParentId = p32, Order = 326 });
            nodes.Add(new AccountNode { Id = id++, Code = "329", Name = "Diğer Ticari Borçlar", Description = "Diğer Ticari Borçlar", Level = 3, ParentId = p32, Order = 329 });

            // ==========================================================
            // 33 – DİĞER BORÇLAR
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "33", Name = "Diğer Borçlar", Description = "Diğer Borçlar", Level = 2, ParentId = p3, Order = 33 });
            var p33 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "331", Name = "Ortaklara Borçlar", Description = "Ortaklara Borçlar", Level = 3, ParentId = p33, Order = 331 });
            nodes.Add(new AccountNode { Id = id++, Code = "332", Name = "İştiraklere Borçlar", Description = "İştiraklere Borçlar", Level = 3, ParentId = p33, Order = 332 });
            nodes.Add(new AccountNode { Id = id++, Code = "333", Name = "Bağlı Ortaklıklara Borçlar", Description = "Bağlı Ortaklıklara Borçlar", Level = 3, ParentId = p33, Order = 333 });
            nodes.Add(new AccountNode { Id = id++, Code = "335", Name = "Personele Borçlar", Description = "Personele Borçlar", Level = 3, ParentId = p33, Order = 335 });
            nodes.Add(new AccountNode { Id = id++, Code = "336", Name = "Diğer Çeşitli Borçlar", Description = "Diğer Çeşitli Borçlar", Level = 3, ParentId = p33, Order = 336 });
            nodes.Add(new AccountNode { Id = id++, Code = "337", Name = "Diğer Borç Senetleri Reeskontu (-)", Description = "Diğer Borç Senetleri Reeskontu (-)", Level = 3, ParentId = p33, Order = 337 });

            // ==========================================================
            // 34 – ALINAN AVANSLAR
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "34", Name = "Alınan Avanslar", Description = "Alınan Avanslar", Level = 2, ParentId = p3, Order = 34 });
            var p34 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "340", Name = "Alınan Sipariş Avansları", Description = "Alınan Sipariş Avansları", Level = 3, ParentId = p34, Order = 340 });
            nodes.Add(new AccountNode { Id = id++, Code = "349", Name = "Diğer Alınan Avanslar", Description = "Diğer Alınan Avanslar", Level = 3, ParentId = p34, Order = 349 });

            // ==========================================================
            // 35 – YILLARA YAYGIN İNŞAAT VE ONARIM HAKEDİŞLERİ
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "35", Name = "Yıllara Yaygın İnşaat ve Onarım Hakedişleri", Description = "Yıllara Yaygın İnşaat ve Onarım Hakedişleri", Level = 2, ParentId = p3, Order = 35 });
            var p35 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "357", Name = "Yıllara Yaygın İnşaat ve Onarım Hakediş Bedelleri", Description = "Yıllara Yaygın İnşaat ve Onarım Hakediş Bedelleri", Level = 3, ParentId = p35, Order = 350 });
            nodes.Add(new AccountNode { Id = id++, Code = "358", Name = "Yıllara Yaygın İnşaat Enflasyon Düzeltme Hesabı", Description = "Yıllara Yaygın İnşaat Enflasyon Düzeltme Hesabı", Level = 3, ParentId = p35, Order = 358 });

            // ==========================================================
            // 36 – ÖDENECEK VERGİ VE DİĞER YÜKÜMLÜLÜKLER
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "36", Name = "Ödenecek Vergi ve Diğer Yükümlülükler", Description = "Ödenecek Vergi ve Diğer Yükümlülükler", Level = 2, ParentId = p3, Order = 36 });
            var p36 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "360", Name = "Ödenecek Vergi ve Fonlar", Description = "Ödenecek Vergi ve Fonlar", Level = 3, ParentId = p36, Order = 360 });
            nodes.Add(new AccountNode { Id = id++, Code = "361", Name = "Ödenecek Sosyal Güvenlik Kesintileri", Description = "Ödenecek Sosyal Güvenlik Kesintileri", Level = 3, ParentId = p36, Order = 361 });
            nodes.Add(new AccountNode { Id = id++, Code = "368", Name = "Vadesi Gelmiş Ertelenmiş veya Taksitlendirilmiş Vergi ve Diğer Yükümlülükler", Description = "Vadesi Gelmiş Ertelenmiş veya Taksitlendirilmiş Vergi ve Diğer Yükümlülükler", Level = 3, ParentId = p36, Order = 368 });
            nodes.Add(new AccountNode { Id = id++, Code = "369", Name = "Ödenecek Diğer Yükümlülükler", Description = "Ödenecek Diğer Yükümlülükler", Level = 3, ParentId = p36, Order = 369 });

            // ==========================================================
            // 37 – BORÇ VE GİDER KARŞILIKLARI
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "37", Name = "Borç ve Gider Karşılıkları", Description = "Borç ve Gider Karşılıkları", Level = 2, ParentId = p3, Order = 37 });
            var p37 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "370", Name = "Dönem Kârı Vergi ve Diğer Yasal Yükümlülük Karşılıkları", Description = "Dönem Kârı Vergi ve Diğer Yasal Yükümlülük Karşılıkları", Level = 3, ParentId = p37, Order = 370 });
            nodes.Add(new AccountNode { Id = id++, Code = "371", Name = "Dönem Kârının Peşin Ödenen Vergi ve Diğer Yükümlülükleri (-)", Description = "Dönem Kârının Peşin Ödenen Vergi ve Diğer Yükümlülükleri (-)", Level = 3, ParentId = p37, Order = 371 });
            nodes.Add(new AccountNode { Id = id++, Code = "372", Name = "Kıdem Tazminatı Karşılığı", Description = "Kıdem Tazminatı Karşılığı", Level = 3, ParentId = p37, Order = 372 });
            nodes.Add(new AccountNode { Id = id++, Code = "373", Name = "Maliyet Giderleri Karşılığı", Description = "Maliyet Giderleri Karşılığı", Level = 3, ParentId = p37, Order = 373 });
            nodes.Add(new AccountNode { Id = id++, Code = "379", Name = "Diğer Borç ve Gider Karşılıkları", Description = "Diğer Borç ve Gider Karşılıkları", Level = 3, ParentId = p37, Order = 379 });

            // ==========================================================
            // 38 – GELECEK AYLARA AİT GELİRLER VE GİDER TAHAKKUKLARI
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "38", Name = "Gelecek Aylara Ait Gelirler ve Gider Tahakkukları", Description = "Gelecek Aylara Ait Gelirler ve Gider Tahakkukları", Level = 2, ParentId = p3, Order = 38 });
            var p38 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "380", Name = "Gelecek Aylara Ait Gelirler", Description = "Gelecek Aylara Ait Gelirler", Level = 3, ParentId = p38, Order = 380 });
            nodes.Add(new AccountNode { Id = id++, Code = "381", Name = "Gider Tahakkukları", Description = "Gider Tahakkukları", Level = 3, ParentId = p38, Order = 381 });

            // ==========================================================
            // 39 – DİĞER KISA VADELİ YABANCI KAYNAKLAR
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "39", Name = "Diğer Kısa Vadeli Yabancı Kaynaklar", Description = "Diğer Kısa Vadeli Yabancı Kaynaklar", Level = 2, ParentId = p3, Order = 39 });
            var p39 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "391", Name = "Hesaplanan KDV", Description = "Hesaplanan KDV", Level = 3, ParentId = p39, Order = 391 });
            nodes.Add(new AccountNode { Id = id++, Code = "392", Name = "Diğer KDV", Description = "Diğer KDV", Level = 3, ParentId = p39, Order = 392 });
            nodes.Add(new AccountNode { Id = id++, Code = "393", Name = "Merkez ve Şubeler Cari Hesabı", Description = "Merkez ve Şubeler Cari Hesabı", Level = 3, ParentId = p39, Order = 393 });
            nodes.Add(new AccountNode { Id = id++, Code = "397", Name = "Sayım ve Tesellüm Fazlaları", Description = "Sayım ve Tesellüm Fazlaları", Level = 3, ParentId = p39, Order = 397 });
            nodes.Add(new AccountNode { Id = id++, Code = "399", Name = "Diğer Çeşitli Yabancı Kaynaklar", Description = "Diğer Çeşitli Yabancı Kaynaklar", Level = 3, ParentId = p39, Order = 399 });


            // ==========================================================
            // 4 – UZUN VADELİ YABANCI KAYNAKLAR
            // ==========================================================
            nodes.Add(new AccountNode
            {
                Id = id,
                Code = "4",
                Name = "Uzun Vadeli Yabancı Kaynaklar",
                Description = "Uzun Vadeli Yabancı Kaynaklar",
                Level = 1,
                Order = 4
            });
            var p4 = id++;

            // ==========================================================
            // 40 – MALİ BORÇLAR
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "40", Name = "Mali Borçlar", Description = "Mali Borçlar", Level = 2, ParentId = p4, Order = 40 }); var p40 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "400", Name = "Banka Kredileri", Description = "Banka Kredileri", Level = 3, ParentId = p40, Order = 400 });
            nodes.Add(new AccountNode { Id = id++, Code = "401", Name = "Finansal Kiralama İşlemlerinden Borçlar", Description = "Finansal Kiralama İşlemlerinden Borçlar", Level = 3, ParentId = p40, Order = 401 });
            nodes.Add(new AccountNode { Id = id++, Code = "402", Name = "Ertelenmiş Finansal Kiralama Borçlanma Maliyetleri (-)", Description = "Ertelenmiş Finansal Kiralama Borçlanma Maliyetleri (-)", Level = 3, ParentId = p40, Order = 402 });
            nodes.Add(new AccountNode { Id = id++, Code = "405", Name = "Çıkarılmış Tahviller", Description = "Çıkarılmış Tahviller", Level = 3, ParentId = p40, Order = 405 });
            nodes.Add(new AccountNode { Id = id++, Code = "407", Name = "Çıkarılmış Diğer Menkul Kıymetler", Description = "Çıkarılmış Diğer Menkul Kıymetler", Level = 3, ParentId = p40, Order = 407 });
            nodes.Add(new AccountNode { Id = id++, Code = "408", Name = "Menkul Kıymetler İhraç Farkı (-)", Description = "Menkul Kıymetler İhraç Farkı (-)", Level = 3, ParentId = p40, Order = 408 });
            nodes.Add(new AccountNode { Id = id++, Code = "409", Name = "Diğer Mali Borçlar", Description = "Diğer Mali Borçlar", Level = 3, ParentId = p40, Order = 409 });

            // ==========================================================
            // 42 – TİCARİ BORÇLAR
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "42", Name = "Ticari Borçlar", Description = "Ticari Borçlar", Level = 2, ParentId = p4, Order = 42 }); var p42 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "420", Name = "Satıcılar", Description = "Satıcılar", Level = 3, ParentId = p42, Order = 420 });
            nodes.Add(new AccountNode { Id = id++, Code = "421", Name = "Borç Senetleri", Description = "Borç Senetleri", Level = 3, ParentId = p42, Order = 421 });
            nodes.Add(new AccountNode { Id = id++, Code = "422", Name = "Borç Senetleri Reeskontu (-)", Description = "Borç Senetleri Reeskontu (-)", Level = 3, ParentId = p42, Order = 422 });
            nodes.Add(new AccountNode { Id = id++, Code = "426", Name = "Alınan Depozito ve Teminatlar", Description = "Alınan Depozito ve Teminatlar", Level = 3, ParentId = p42, Order = 426 });
            nodes.Add(new AccountNode { Id = id++, Code = "429", Name = "Diğer Ticari Borçlar", Description = "Diğer Ticari Borçlar", Level = 3, ParentId = p42, Order = 429 });

            // ==========================================================
            // 43 – DİĞER BORÇLAR
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "43", Name = "Diğer Borçlar", Description = "Diğer Borçlar", Level = 2, ParentId = p4, Order = 43 }); var p43 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "430", Name = "Alınan Depozito ve Teminatlar", Description = "Alınan Depozito ve Teminatlar", Level = 3, ParentId = p43, Order = 430 });
            nodes.Add(new AccountNode { Id = id++, Code = "431", Name = "Ortaklara Borçlar", Description = "Ortaklara Borçlar", Level = 3, ParentId = p43, Order = 431 });
            nodes.Add(new AccountNode { Id = id++, Code = "432", Name = "İştiraklere Borçlar", Description = "İştiraklere Borçlar", Level = 3, ParentId = p43, Order = 432 });
            nodes.Add(new AccountNode { Id = id++, Code = "433", Name = "Bağlı Ortaklıklara Borçlar", Description = "Bağlı Ortaklıklara Borçlar", Level = 3, ParentId = p43, Order = 433 });
            nodes.Add(new AccountNode { Id = id++, Code = "437", Name = "Diğer Borç Senetleri Reeskontu (-)", Description = "Diğer Borç Senetleri Reeskontu (-)", Level = 3, ParentId = p43, Order = 437 });
            nodes.Add(new AccountNode { Id = id++, Code = "438", Name = "Kamuya Olan Ertelenmiş veya Taksitlendirilmiş Borçlar", Description = "Kamuya Olan Ertelenmiş veya Taksitlendirilmiş Borçlar", Level = 3, ParentId = p43, Order = 438 });
            nodes.Add(new AccountNode { Id = id++, Code = "439", Name = "Diğer Borçlar", Description = "Diğer Borçlar", Level = 3, ParentId = p43, Order = 439 });

            // ==========================================================
            // 44 – ALINAN AVANSLAR
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "44", Name = "Alınan Avanslar", Description = "Alınan Avanslar", Level = 2, ParentId = p4, Order = 44 }); var p44 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "440", Name = "Alınan Sipariş Avansları", Description = "Alınan Sipariş Avansları", Level = 3, ParentId = p44, Order = 440 });
            nodes.Add(new AccountNode { Id = id++, Code = "449", Name = "Diğer Alınan Avanslar", Description = "Diğer Alınan Avanslar", Level = 3, ParentId = p44, Order = 449 });

            // ==========================================================
            // 47 – BORÇ VE GİDER KARŞILIKLARI
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "47", Name = "Borç ve Gider Karşılıkları", Description = "Borç ve Gider Karşılıkları", Level = 2, ParentId = p4, Order = 47 }); var p47 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "470", Name = "Vergi ve Diğer Yükümlülük Karşılıkları", Description = "Vergi ve Diğer Yükümlülük Karşılıkları", Level = 3, ParentId = p47, Order = 470 });
            nodes.Add(new AccountNode { Id = id++, Code = "471", Name = "Dönem Kârı Vergi ve Diğer Yasal Yükümlülük Karşılıkları", Description = "Dönem Kârı Vergi ve Diğer Yasal Yükümlülük Karşılıkları", Level = 3, ParentId = p47, Order = 471 });
            nodes.Add(new AccountNode { Id = id++, Code = "472", Name = "Kıdem Tazminatı Karşılığı", Description = "Kıdem Tazminatı Karşılığı", Level = 3, ParentId = p47, Order = 472 });
            nodes.Add(new AccountNode { Id = id++, Code = "479", Name = "Diğer Borç ve Gider Karşılıkları", Description = "Diğer Borç ve Gider Karşılıkları", Level = 3, ParentId = p47, Order = 479 });

            // ==========================================================
            // 48 – GELECEK YILLARA AİT GELİRLER
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "48", Name = "Gelecek Yıllara Ait Gelirler", Description = "Gelecek Yıllara Ait Gelirler", Level = 2, ParentId = p4, Order = 48 }); var p48 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "480", Name = "Gelecek Yıllara Ait Gelirler", Description = "Gelecek Yıllara Ait Gelirler", Level = 3, ParentId = p48, Order = 480 });
            nodes.Add(new AccountNode { Id = id++, Code = "481", Name = "Gelir Tahakkukları", Description = "Gelir Tahakkukları", Level = 3, ParentId = p48, Order = 481 });

            // ==========================================================
            // 49 – DİĞER UZUN VADELİ YABANCI KAYNAKLAR
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "49", Name = "Diğer Uzun Vadeli Yabancı Kaynaklar", Description = "Diğer Uzun Vadeli Yabancı Kaynaklar", Level = 2, ParentId = p4, Order = 49 }); var p49 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "490", Name = "Gelecek Yıllara Ertelenen Giderler (-)", Description = "Gelecek Yıllara Ertelenen Giderler (-)", Level = 3, ParentId = p49, Order = 490 });
            nodes.Add(new AccountNode { Id = id++, Code = "492", Name = "Gelecek Yıllara Ertelenen Diğer Gelirler veya Terkin Edilecek KDV", Description = "Gelecek Yıllara Ertelenen Diğer Gelirler veya Terkin Edilecek KDV", Level = 3, ParentId = p49, Order = 492 });
            nodes.Add(new AccountNode { Id = id++, Code = "493", Name = "Çeşitli Karşılıklar", Description = "Çeşitli Karşılıklar", Level = 3, ParentId = p49, Order = 493 });
            nodes.Add(new AccountNode { Id = id++, Code = "497", Name = "Sayım ve Tesellüm Fazlaları", Description = "Sayım ve Tesellüm Fazlaları", Level = 3, ParentId = p49, Order = 497 });
            nodes.Add(new AccountNode { Id = id++, Code = "499", Name = "Diğer Çeşitli Uzun Vadeli Yabancı Kaynaklar", Description = "Diğer Çeşitli Uzun Vadeli Yabancı Kaynaklar", Level = 3, ParentId = p49, Order = 499 });

            // ==========================================================
            // 5 – ÖZ KAYNAKLAR
            // ==========================================================
            nodes.Add(new AccountNode
            {
                Id = id,
                Code = "5",
                Name = "Öz Kaynaklar",
                Description = "Öz Kaynaklar",
                Level = 1,
                Order = 5
            });
            var p5 = id++;

            // ==========================================================
            // 50 – ÖDENMİŞ SERMAYE
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "50", Name = "Ödenmiş Sermaye", Description = "Ödenmiş Sermaye", Level = 2, ParentId = p5, Order = 50 });
            var p50 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "500", Name = "Sermaye", Description = "Sermaye", Level = 3, ParentId = p50, Order = 500 });
            nodes.Add(new AccountNode { Id = id++, Code = "501", Name = "Ödenmemiş Sermaye (-)", Description = "Ödenmemiş Sermaye (-)", Level = 3, ParentId = p50, Order = 501 });
            nodes.Add(new AccountNode { Id = id++, Code = "502", Name = "Sermaye Düzeltmesi Olumlu Farkları", Description = "Sermaye Düzeltmesi Olumlu Farkları", Level = 3, ParentId = p50, Order = 502 });
            nodes.Add(new AccountNode { Id = id++, Code = "503", Name = "Sermaye Düzeltmesi Olumsuz Farkları (-)", Description = "Sermaye Düzeltmesi Olumsuz Farkları (-)", Level = 3, ParentId = p50, Order = 503 });


            // ==========================================================
            // 52 – SERMAYE YEDEKLERİ
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "52", Name = "Sermaye Yedekleri", Description = "Sermaye Yedekleri", Level = 2, ParentId = p5, Order = 52 });
            var p52 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "520", Name = "Hisse Senetleri İhraç Primleri", Description = "Hisse Senetleri İhraç Primleri", Level = 3, ParentId = p52, Order = 520 });
            nodes.Add(new AccountNode { Id = id++, Code = "521", Name = "Hisse Senedi İptal Kârları", Description = "Hisse Senedi İptal Kârları", Level = 3, ParentId = p52, Order = 521 });
            nodes.Add(new AccountNode { Id = id++, Code = "522", Name = "M.D.V. Yeniden Değerleme Artışları", Description = "M.D.V. Yeniden Değerleme Artışları", Level = 3, ParentId = p52, Order = 522 });
            nodes.Add(new AccountNode { Id = id++, Code = "523", Name = "İştirakler Yeniden Değerleme Artışları", Description = "İştirakler Yeniden Değerleme Artışları", Level = 3, ParentId = p52, Order = 523 });
            nodes.Add(new AccountNode { Id = id++, Code = "524", Name = "Maliyet Bedeli Artışları Fonu", Description = "Maliyet Bedeli Artışları Fonu", Level = 3, ParentId = p52, Order = 524 });
            nodes.Add(new AccountNode { Id = id++, Code = "529", Name = "Diğer Sermaye Yedekleri", Description = "Diğer Sermaye Yedekleri", Level = 3, ParentId = p52, Order = 529 });


            // ==========================================================
            // 54 – KÂR YEDEKLERİ
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "54", Name = "Kâr Yedekleri", Description = "Kâr Yedekleri", Level = 2, ParentId = p5, Order = 54 });
            var p54 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "540", Name = "Yasal Yedekler", Description = "Yasal Yedekler", Level = 3, ParentId = p54, Order = 540 });
            nodes.Add(new AccountNode { Id = id++, Code = "541", Name = "Statü Yedekleri", Description = "Statü Yedekleri", Level = 3, ParentId = p54, Order = 541 });
            nodes.Add(new AccountNode { Id = id++, Code = "542", Name = "Olağanüstü Yedekler", Description = "Olağanüstü Yedekler", Level = 3, ParentId = p54, Order = 542 });
            nodes.Add(new AccountNode { Id = id++, Code = "548", Name = "Diğer Kâr Yedekleri", Description = "Diğer Kâr Yedekleri", Level = 3, ParentId = p54, Order = 548 });
            nodes.Add(new AccountNode { Id = id++, Code = "549", Name = "Özel Fonlar", Description = "Özel Fonlar", Level = 3, ParentId = p54, Order = 549 });


            // ==========================================================
            // 57 – GEÇMİŞ YILLAR KÂRLARI
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "57", Name = "Geçmiş Yıllar Kârları", Description = "Geçmiş Yıllar Kârları", Level = 2, ParentId = p5, Order = 57 });
            var p57 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "570", Name = "Geçmiş Yıllar Kârları", Description = "Geçmiş Yıllar Kârları", Level = 3, ParentId = p57, Order = 570 });


            // ==========================================================
            // 58 – GEÇMİŞ YILLAR ZARARLARI
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "58", Name = "Geçmiş Yıllar Zararları", Description = "Geçmiş Yıllar Zararları", Level = 2, ParentId = p5, Order = 58 });
            var p58 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "580", Name = "Geçmiş Yıl Zararları (-)", Description = "Geçmiş Yıl Zararları (-)", Level = 3, ParentId = p58, Order = 580 });


            // ==========================================================
            // 59 – DÖNEM NET KÂRI / ZARARI
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "59", Name = "Dönem Net Kârı (Zararı)", Description = "Dönem Net Kârı (Zararı)", Level = 2, ParentId = p5, Order = 59 });
            var p59 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "590", Name = "Dönem Net Kârı", Description = "Dönem Net Kârı", Level = 3, ParentId = p59, Order = 590 });
            nodes.Add(new AccountNode { Id = id++, Code = "591", Name = "Dönem Net Zararı (-)", Description = "Dönem Net Zararı (-)", Level = 3, ParentId = p59, Order = 591 });

            // ----------------------------------------------------------
            // 6 - GELİRLER (LEVEL 1)
            // ----------------------------------------------------------
            // ----------------------------------------------------------
            // 6 - GELİR TABLOSU HESAPLARI (LEVEL 1)
            // ----------------------------------------------------------
            nodes.Add(new AccountNode
            {
                Id = id,
                Code = "6",
                Name = "Gelir Tablosu Hesapları",
                Description = "Gelir Tablosu Hesapları",
                Level = 1,
                Order = 6
            });
            var p6 = id++;

            // ==========================================================
            // 60 – BRÜT SATIŞLAR
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "60", Name = "Brüt Satışlar", Description = "Brüt Satışlar", Level = 2, ParentId = p6, Order = 60 });
            var p60 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "600", Name = "Yurtiçi Satışlar", Description = "Yurtiçi Satışlar", Level = 3, ParentId = p60, Order = 600 });
            nodes.Add(new AccountNode { Id = id++, Code = "601", Name = "Yurtdışı Satışlar", Description = "Yurtdışı Satışlar", Level = 3, ParentId = p60, Order = 601 });
            nodes.Add(new AccountNode { Id = id++, Code = "602", Name = "Diğer Gelirler", Description = "Diğer Gelirler", Level = 3, ParentId = p60, Order = 602 });

            // ==========================================================
            // 61 – SATIŞ İNDİRİMLERİ (-)
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "61", Name = "Satış İndirimleri (-)", Description = "Satış İndirimleri (-)", Level = 2, ParentId = p6, Order = 61 });
            var p61 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "610", Name = "Satıştan İadeler (-)", Description = "Satıştan İadeler (-)", Level = 3, ParentId = p61, Order = 610 });
            nodes.Add(new AccountNode { Id = id++, Code = "611", Name = "Satış İskontoları (-)", Description = "Satış İskontoları (-)", Level = 3, ParentId = p61, Order = 611 });
            nodes.Add(new AccountNode { Id = id++, Code = "612", Name = "Diğer İndirimler (-)", Description = "Diğer İndirimler (-)", Level = 3, ParentId = p61, Order = 612 });

            // ==========================================================
            // 62 – SATIŞLARIN MALİYETİ (-)
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "62", Name = "Satışların Maliyeti (-)", Description = "Satışların Maliyeti (-)", Level = 2, ParentId = p6, Order = 62 });
            var p62 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "620", Name = "Satılan Mamuller Maliyeti (-)", Description = "Satılan Mamuller Maliyeti (-)", Level = 3, ParentId = p62, Order = 620 });
            nodes.Add(new AccountNode { Id = id++, Code = "621", Name = "Satılan Ticari Mallar Maliyeti (-)", Description = "Satılan Ticari Mallar Maliyeti (-)", Level = 3, ParentId = p62, Order = 621 });
            nodes.Add(new AccountNode { Id = id++, Code = "622", Name = "Satılan Hizmet Maliyeti (-)", Description = "Satılan Hizmet Maliyeti (-)", Level = 3, ParentId = p62, Order = 622 });
            nodes.Add(new AccountNode { Id = id++, Code = "623", Name = "Diğer Satışların Maliyeti (-)", Description = "Diğer Satışların Maliyeti (-)", Level = 3, ParentId = p62, Order = 623 });

            // ==========================================================
            // 63 – FAALİYET GİDERLERİ (-)
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "63", Name = "Faaliyet Giderleri (-)", Description = "Faaliyet Giderleri (-)", Level = 2, ParentId = p6, Order = 63 });
            var p63 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "630", Name = "Araştırma ve Geliştirme Giderleri (-)", Description = "Araştırma ve Geliştirme Giderleri (-)", Level = 3, ParentId = p63, Order = 630 });
            nodes.Add(new AccountNode { Id = id++, Code = "631", Name = "Pazarlama Satış ve Dağıtım Giderleri (-)", Description = "Pazarlama Satış ve Dağıtım Giderleri (-)", Level = 3, ParentId = p63, Order = 631 });
            nodes.Add(new AccountNode { Id = id++, Code = "632", Name = "Genel Yönetim Giderleri (-)", Description = "Genel Yönetim Giderleri (-)", Level = 3, ParentId = p63, Order = 632 });

            // ==========================================================
            // 64 – DİĞER FAALİYETLERDEN OLAĞAN GELİR VE KÂRLAR
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "64", Name = "Diğer Faaliyetlerden Olağan Gelir ve Kârlar", Description = "Diğer Faaliyetlerden Olağan Gelir ve Kârlar", Level = 2, ParentId = p6, Order = 64 });
            var p64 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "640", Name = "İştiraklerden Temettü Gelirleri", Description = "İştiraklerden Temettü Gelirleri", Level = 3, ParentId = p64, Order = 640 });
            nodes.Add(new AccountNode { Id = id++, Code = "641", Name = "Bağlı Ortaklıklardan Temettü Gelirleri", Description = "Bağlı Ortaklıklardan Temettü Gelirleri", Level = 3, ParentId = p64, Order = 641 });
            nodes.Add(new AccountNode { Id = id++, Code = "642", Name = "Faiz Gelirleri", Description = "Faiz Gelirleri", Level = 3, ParentId = p64, Order = 642 });
            nodes.Add(new AccountNode { Id = id++, Code = "643", Name = "Komisyon Gelirleri", Description = "Komisyon Gelirleri", Level = 3, ParentId = p64, Order = 643 });
            nodes.Add(new AccountNode { Id = id++, Code = "644", Name = "Konusu Kalmayan Karşılıklar", Description = "Konusu Kalmayan Karşılıklar", Level = 3, ParentId = p64, Order = 644 });
            nodes.Add(new AccountNode { Id = id++, Code = "645", Name = "Menkul Kıymet Satış Karları", Description = "Menkul Kıymet Satış Karları", Level = 3, ParentId = p64, Order = 645 });
            nodes.Add(new AccountNode { Id = id++, Code = "646", Name = "Kambiyo Karları", Description = "Kambiyo Karları", Level = 3, ParentId = p64, Order = 646 });
            nodes.Add(new AccountNode { Id = id++, Code = "647", Name = "Reeskont Faiz Gelirleri", Description = "Reeskont Faiz Gelirleri", Level = 3, ParentId = p64, Order = 647 });
            nodes.Add(new AccountNode { Id = id++, Code = "648", Name = "Enflasyon Düzeltmesi Karları", Description = "Enflasyon Düzeltmesi Karları", Level = 3, ParentId = p64, Order = 648 });
            nodes.Add(new AccountNode { Id = id++, Code = "649", Name = "Diğer Olağan Gelir ve Karlar", Description = "Diğer Olağan Gelir ve Karlar", Level = 3, ParentId = p64, Order = 649 });

            // ==========================================================
            // 65 – DİĞER FAALİYETLERDEN OLAĞAN GİDER VE ZARARLAR (-)
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "65", Name = "Diğer Faaliyetlerden Olağan Gider ve Zararlar (-)", Description = "Diğer Faaliyetlerden Olağan Gider ve Zararlar (-)", Level = 2, ParentId = p6, Order = 65 });
            var p65 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "653", Name = "Komisyon Giderleri (-)", Description = "Komisyon Giderleri (-)", Level = 3, ParentId = p65, Order = 653 });
            nodes.Add(new AccountNode { Id = id++, Code = "654", Name = "Karşılık Giderleri (-)", Description = "Karşılık Giderleri (-)", Level = 3, ParentId = p65, Order = 654 });
            nodes.Add(new AccountNode { Id = id++, Code = "655", Name = "Menkul Kıymet Satış Zararları (-)", Description = "Menkul Kıymet Satış Zararları (-)", Level = 3, ParentId = p65, Order = 655 });
            nodes.Add(new AccountNode { Id = id++, Code = "656", Name = "Kambiyo Zararları (-)", Description = "Kambiyo Zararları (-)", Level = 3, ParentId = p65, Order = 656 });
            nodes.Add(new AccountNode { Id = id++, Code = "657", Name = "Reeskont Faiz Giderleri (-)", Description = "Reeskont Faiz Giderleri (-)", Level = 3, ParentId = p65, Order = 657 });
            nodes.Add(new AccountNode { Id = id++, Code = "658", Name = "Enflasyon Düzeltmesi Zararları (-)", Description = "Enflasyon Düzeltmesi Zararları (-)", Level = 3, ParentId = p65, Order = 658 });
            nodes.Add(new AccountNode { Id = id++, Code = "659", Name = "Diğer Olağan Gider ve Zararlar (-)", Description = "Diğer Olağan Gider ve Zararlar (-)", Level = 3, ParentId = p65, Order = 659 });

            // ==========================================================
            // 66 – FİNANSMAN GİDERLERİ (-)
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "66", Name = "Finansman Giderleri (-)", Description = "Finansman Giderleri (-)", Level = 2, ParentId = p6, Order = 66 });
            var p66 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "660", Name = "Kısa Vadeli Borçlanma Giderleri (-)", Description = "Kısa Vadeli Borçlanma Giderleri (-)", Level = 3, ParentId = p66, Order = 660 });
            nodes.Add(new AccountNode { Id = id++, Code = "661", Name = "Uzun Vadeli Borçlanma Giderleri (-)", Description = "Uzun Vadeli Borçlanma Giderleri (-)", Level = 3, ParentId = p66, Order = 661 });

            // ==========================================================
            // 67 – OLAĞANDIŞI GELİR VE KÂRLAR
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "67", Name = "Olağandışı Gelir ve Karlar", Description = "Olağandışı Gelir ve Karlar", Level = 2, ParentId = p6, Order = 67 });
            var p67 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "671", Name = "Önceki Dönem Gelir ve Karları", Description = "Önceki Dönem Gelir ve Karları", Level = 3, ParentId = p67, Order = 671 });
            nodes.Add(new AccountNode { Id = id++, Code = "679", Name = "Diğer Olağandışı Gelir ve Karlar", Description = "Diğer Olağandışı Gelir ve Karlar", Level = 3, ParentId = p67, Order = 679 });

            // ==========================================================
            // 68 – OLAĞANDIŞI GİDER VE ZARARLAR (-)
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "68", Name = "Olağandışı Gider ve Zararlar (-)", Description = "Olağandışı Gider ve Zararlar (-)", Level = 2, ParentId = p6, Order = 68 });
            var p68 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "680", Name = "Çalışmayan Kısım Gider ve Zararları (-)", Description = "Çalışmayan Kısım Gider ve Zararları (-)", Level = 3, ParentId = p68, Order = 680 });
            nodes.Add(new AccountNode { Id = id++, Code = "681", Name = "Önceki Dönem Gider ve Zararları (-)", Description = "Önceki Dönem Gider ve Zararları (-)", Level = 3, ParentId = p68, Order = 681 });
            nodes.Add(new AccountNode { Id = id++, Code = "689", Name = "Diğer Olağandışı Gider ve Zararlar (-)", Description = "Diğer Olağandışı Gider ve Zararlar (-)", Level = 3, ParentId = p68, Order = 689 });

            // ==========================================================
            // 69 – DÖNEM NET KARI (ZARARI)
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "69", Name = "Dönem Net Karı (Zararı)", Description = "Dönem Net Karı (Zararı)", Level = 2, ParentId = p6, Order = 69 });
            var p69 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "690", Name = "Dönem Karı veya Zararı", Description = "Dönem Karı veya Zararı", Level = 3, ParentId = p69, Order = 690 });
            nodes.Add(new AccountNode { Id = id++, Code = "691", Name = "Dönem Karı Vergi ve Diğer Yasal Yükümlülük Karşılıkları (-)", Description = "Dönem Karı Vergi ve Diğer Yasal Yükümlülük Karşılıkları (-)", Level = 3, ParentId = p69, Order = 691 });
            nodes.Add(new AccountNode { Id = id++, Code = "692", Name = "Dönem Net Karı veya Zararı", Description = "Dönem Net Karı veya Zararı", Level = 3, ParentId = p69, Order = 692 });
            nodes.Add(new AccountNode { Id = id++, Code = "697", Name = "Yıllara Yaygın İnşaat Enflasyon Düzeltme Hesabı", Description = "Yıllara Yaygın İnşaat Enflasyon Düzeltme Hesabı", Level = 3, ParentId = p69, Order = 697 });
            nodes.Add(new AccountNode { Id = id++, Code = "698", Name = "Enflasyon Düzeltme Hesabı", Description = "Enflasyon Düzeltme Hesabı", Level = 3, ParentId = p69, Order = 698 });

            // ----------------------------------------------------------
            // 7 - MALİYET HESAPLARI (7/A ve 7/B SEÇENEĞİ)
            // ----------------------------------------------------------
            nodes.Add(new AccountNode
            {
                Id = id,
                Code = "7",
                Name = "Maliyet Hesapları",
                Description = "Maliyet Hesapları (7/A ve 7/B Seçeneği)",
                Level = 1,
                Order = 7
            });
            var p7 = id++;

            // ==========================================================
            // 70 – MALİYET MUHASEBESİ BAĞLANTI HESAPLARI
            // ==========================================================
            nodes.Add(new AccountNode
            {
                Id = id,
                Code = "70",
                Name = "Maliyet Muhasebesi Bağlantı Hesapları",
                Description = "Maliyet Muhasebesi Bağlantı Hesapları",
                Level = 2,
                ParentId = p7,
                Order = 70
            });
            var p70 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "700", Name = "Maliyet Muhasebesi Bağlantı Hesabı", Description = "Maliyet Muhasebesi Bağlantı Hesabı", Level = 3, ParentId = p70, Order = 700 });
            nodes.Add(new AccountNode { Id = id++, Code = "701", Name = "Maliyet Muhasebesi Yansıtma Hesabı", Description = "Maliyet Muhasebesi Yansıtma Hesabı", Level = 3, ParentId = p70, Order = 701 });

            // ==========================================================
            // 71 – DİREKT İLKMADDE VE MALZEME GİDERLERİ
            // ==========================================================
            nodes.Add(new AccountNode
            {
                Id = id,
                Code = "71",
                Name = "Direkt İlk Madde ve Malzeme Giderleri",
                Description = "Direkt İlk Madde ve Malzeme Giderleri",
                Level = 2,
                ParentId = p7,
                Order = 71
            });
            var p71 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "710", Name = "Direkt İlk Madde ve Malzeme Giderleri", Description = "Direkt İlk Madde ve Malzeme Giderleri", Level = 3, ParentId = p71, Order = 710 });
            nodes.Add(new AccountNode { Id = id++, Code = "711", Name = "Direkt İlk Madde ve Malzeme Yansıtma Hesabı", Description = "Direkt İlk Madde ve Malzeme Yansıtma Hesabı", Level = 3, ParentId = p71, Order = 711 });
            nodes.Add(new AccountNode { Id = id++, Code = "712", Name = "Direkt İlk Madde ve Malzeme Fiyat Farkı", Description = "Direkt İlk Madde ve Malzeme Fiyat Farkı", Level = 3, ParentId = p71, Order = 712 });
            nodes.Add(new AccountNode { Id = id++, Code = "713", Name = "Direkt İlk Madde ve Malzeme Miktar Farkı", Description = "Direkt İlk Madde ve Malzeme Miktar Farkı", Level = 3, ParentId = p71, Order = 713 });

            // ==========================================================
            // 72 – DİREKT İŞÇİLİK GİDERLERİ
            // ==========================================================
            nodes.Add(new AccountNode
            {
                Id = id,
                Code = "72",
                Name = "Direkt İşçilik Giderleri",
                Description = "Direkt İşçilik Giderleri",
                Level = 2,
                ParentId = p7,
                Order = 72
            });
            var p72 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "720", Name = "Direkt İşçilik Giderleri", Description = "Direkt İşçilik Giderleri", Level = 3, ParentId = p72, Order = 720 });
            nodes.Add(new AccountNode { Id = id++, Code = "721", Name = "Direkt İşçilik Giderleri Yansıtma Hesabı", Description = "Direkt İşçilik Giderleri Yansıtma Hesabı", Level = 3, ParentId = p72, Order = 721 });
            nodes.Add(new AccountNode { Id = id++, Code = "722", Name = "Direkt İşçilik Ücret Farkları", Description = "Direkt İşçilik Ücret Farkları", Level = 3, ParentId = p72, Order = 722 });
            nodes.Add(new AccountNode { Id = id++, Code = "723", Name = "Direkt İşçilik Süre (Zaman) Farkları", Description = "Direkt İşçilik Süre (Zaman) Farkları", Level = 3, ParentId = p72, Order = 723 });

            // ==========================================================
            // 73 – GENEL ÜRETİM GİDERLERİ
            // ==========================================================
            nodes.Add(new AccountNode
            {
                Id = id,
                Code = "73",
                Name = "Genel Üretim Giderleri",
                Description = "Genel Üretim Giderleri",
                Level = 2,
                ParentId = p7,
                Order = 73
            });
            var p73 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "730", Name = "Genel Üretim Giderleri", Description = "Genel Üretim Giderleri", Level = 3, ParentId = p73, Order = 730 });
            nodes.Add(new AccountNode { Id = id++, Code = "731", Name = "Genel Üretim Giderleri Yansıtma Hesabı", Description = "Genel Üretim Giderleri Yansıtma Hesabı", Level = 3, ParentId = p73, Order = 731 });
            nodes.Add(new AccountNode { Id = id++, Code = "732", Name = "Genel Üretim Giderleri Bütçe Farkları", Description = "Genel Üretim Giderleri Bütçe Farkları", Level = 3, ParentId = p73, Order = 732 });
            nodes.Add(new AccountNode { Id = id++, Code = "733", Name = "Genel Üretim Giderleri Verimlilik Farkları", Description = "Genel Üretim Giderleri Verimlilik Farkları", Level = 3, ParentId = p73, Order = 733 });
            nodes.Add(new AccountNode { Id = id++, Code = "734", Name = "Genel Üretim Giderleri Kapasite Farkları", Description = "Genel Üretim Giderleri Kapasite Farkları", Level = 3, ParentId = p73, Order = 734 });

            // ==========================================================
            // 74 – HİZMET ÜRETİM MALİYETİ
            // ==========================================================
            nodes.Add(new AccountNode
            {
                Id = id,
                Code = "74",
                Name = "Hizmet Üretim Maliyeti",
                Description = "Hizmet Üretim Maliyeti",
                Level = 2,
                ParentId = p7,
                Order = 74
            });
            var p74 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "740", Name = "Hizmet Üretim Maliyeti", Description = "Hizmet Üretim Maliyeti", Level = 3, ParentId = p74, Order = 740 });
            nodes.Add(new AccountNode { Id = id++, Code = "741", Name = "Hizmet Üretim Maliyeti Yansıtma Hesabı", Description = "Hizmet Üretim Maliyeti Yansıtma Hesabı", Level = 3, ParentId = p74, Order = 741 });
            nodes.Add(new AccountNode { Id = id++, Code = "742", Name = "Hizmet Üretim Maliyeti Fark Hesapları", Description = "Hizmet Üretim Maliyeti Fark Hesapları", Level = 3, ParentId = p74, Order = 742 });

            // ==========================================================
            // 75 – ARAŞTIRMA VE GELİŞTİRME GİDERLERİ
            // ==========================================================
            nodes.Add(new AccountNode
            {
                Id = id,
                Code = "75",
                Name = "Araştırma ve Geliştirme Giderleri",
                Description = "Araştırma ve Geliştirme Giderleri",
                Level = 2,
                ParentId = p7,
                Order = 75
            });
            var p75 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "750", Name = "Araştırma ve Geliştirme Giderleri", Description = "Araştırma ve Geliştirme Giderleri", Level = 3, ParentId = p75, Order = 750 });
            nodes.Add(new AccountNode { Id = id++, Code = "751", Name = "Araştırma ve Geliştirme Giderleri Yansıtma Hesabı", Description = "Araştırma ve Geliştirme Giderleri Yansıtma Hesabı", Level = 3, ParentId = p75, Order = 751 });
            nodes.Add(new AccountNode { Id = id++, Code = "752", Name = "Araştırma ve Geliştirme Gider Farkları", Description = "Araştırma ve Geliştirme Gider Farkları", Level = 3, ParentId = p75, Order = 752 });

            // ==========================================================
            // 76 – PAZARLAMA SATIŞ VE DAĞITIM GİDERLERİ
            // ==========================================================
            nodes.Add(new AccountNode
            {
                Id = id,
                Code = "76",
                Name = "Pazarlama Satış ve Dağıtım Giderleri",
                Description = "Pazarlama Satış ve Dağıtım Giderleri",
                Level = 2,
                ParentId = p7,
                Order = 76
            });
            var p76 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "760", Name = "Pazarlama Satış ve Dağıtım Giderleri", Description = "Pazarlama Satış ve Dağıtım Giderleri", Level = 3, ParentId = p76, Order = 760 });
            nodes.Add(new AccountNode { Id = id++, Code = "761", Name = "Pazarlama Satış ve Dağıtım Giderleri Yansıtma Hesabı", Description = "Pazarlama Satış ve Dağıtım Giderleri Yansıtma Hesabı", Level = 3, ParentId = p76, Order = 761 });
            nodes.Add(new AccountNode { Id = id++, Code = "762", Name = "Pazarlama Satış ve Dağıtım Giderleri Fark Hesabı", Description = "Pazarlama Satış ve Dağıtım Giderleri Fark Hesabı", Level = 3, ParentId = p76, Order = 762 });

            // ==========================================================
            // 77 – GENEL YÖNETİM GİDERLERİ
            // ==========================================================
            nodes.Add(new AccountNode
            {
                Id = id,
                Code = "77",
                Name = "Genel Yönetim Giderleri",
                Description = "Genel Yönetim Giderleri",
                Level = 2,
                ParentId = p7,
                Order = 77
            });
            var p77 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "770", Name = "Genel Yönetim Giderleri", Description = "Genel Yönetim Giderleri", Level = 3, ParentId = p77, Order = 770 });
            nodes.Add(new AccountNode { Id = id++, Code = "771", Name = "Genel Yönetim Giderleri Yansıtma Hesabı", Description = "Genel Yönetim Giderleri Yansıtma Hesabı", Level = 3, ParentId = p77, Order = 771 });
            nodes.Add(new AccountNode { Id = id++, Code = "772", Name = "Genel Yönetim Gider Farkları Hesabı", Description = "Genel Yönetim Gider Farkları Hesabı", Level = 3, ParentId = p77, Order = 772 });

            // ==========================================================
            // 78 – FİNANSMAN GİDERLERİ
            // ==========================================================
            nodes.Add(new AccountNode
            {
                Id = id,
                Code = "78",
                Name = "Finansman Giderleri",
                Description = "Finansman Giderleri",
                Level = 2,
                ParentId = p7,
                Order = 78
            });
            var p78 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "780", Name = "Finansman Giderleri", Description = "Finansman Giderleri", Level = 3, ParentId = p78, Order = 780 });
            nodes.Add(new AccountNode { Id = id++, Code = "781", Name = "Finansman Giderleri Yansıtma Hesabı", Description = "Finansman Giderleri Yansıtma Hesabı", Level = 3, ParentId = p78, Order = 781 });
            nodes.Add(new AccountNode { Id = id++, Code = "782", Name = "Finansman Giderleri Fark Hesabı", Description = "Finansman Giderleri Fark Hesabı", Level = 3, ParentId = p78, Order = 782 });

            // ==========================================================
            // 79 – GİDER ÇEŞİTLERİ (7/B SEÇENEĞİ)
            // ==========================================================
            nodes.Add(new AccountNode
            {
                Id = id,
                Code = "79",
                Name = "Gider Çeşitleri (7/B Seçeneği)",
                Description = "Gider Çeşitleri (7/B Seçeneği)",
                Level = 2,
                ParentId = p7,
                Order = 79
            });
            var p79 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "790", Name = "İlk Madde ve Malzeme Giderleri", Description = "İlk Madde ve Malzeme Giderleri", Level = 3, ParentId = p79, Order = 790 });
            nodes.Add(new AccountNode { Id = id++, Code = "791", Name = "Memur Ücret ve Giderleri", Description = "Memur Ücret ve Giderleri", Level = 3, ParentId = p79, Order = 791 });
            nodes.Add(new AccountNode { Id = id++, Code = "792", Name = "İşçi Ücret ve Giderleri", Description = "İşçi Ücret ve Giderleri", Level = 3, ParentId = p79, Order = 792 });
            nodes.Add(new AccountNode { Id = id++, Code = "793", Name = "Dışarıdan Sağlanan Fayda ve Hizmetler", Description = "Dışarıdan Sağlanan Fayda ve Hizmetler", Level = 3, ParentId = p79, Order = 793 });
            nodes.Add(new AccountNode { Id = id++, Code = "794", Name = "Çeşitli Giderler", Description = "Çeşitli Giderler", Level = 3, ParentId = p79, Order = 794 });
            nodes.Add(new AccountNode { Id = id++, Code = "795", Name = "Vergi, Resim ve Harçlar", Description = "Vergi, Resim ve Harçlar", Level = 3, ParentId = p79, Order = 795 });
            nodes.Add(new AccountNode { Id = id++, Code = "796", Name = "Amortismanlar ve Tükenme Payları", Description = "Amortismanlar ve Tükenme Payları", Level = 3, ParentId = p79, Order = 796 });
            nodes.Add(new AccountNode { Id = id++, Code = "797", Name = "Finansman Giderleri", Description = "Finansman Giderleri", Level = 3, ParentId = p79, Order = 797 });
            nodes.Add(new AccountNode { Id = id++, Code = "798", Name = "Gider Çeşitleri Yansıtma Hesabı", Description = "Gider Çeşitleri Yansıtma Hesabı", Level = 3, ParentId = p79, Order = 798 });
            nodes.Add(new AccountNode { Id = id++, Code = "799", Name = "Üretim Maliyet Hesabı", Description = "Üretim Maliyet Hesabı", Level = 3, ParentId = p79, Order = 799 });


            // ----------------------------------------------------------
            // 8 - NAZIM HESAPLAR (LEVEL 1)
            // ----------------------------------------------------------
            nodes.Add(new AccountNode
            {
                Id = id,
                Code = "8",
                Name = "Nazım Hesaplar",
                Description = "Nazım Hesaplar",
                Level = 1,
                Order = 8
            });
            var p8 = id++;

            // ==========================================================
            // 80 – GELECEK AYLARA AİT GİDERLER / GELİRLER 
            // (Nazım hesap olarak takip edilenler)
            // ==========================================================
            nodes.Add(new AccountNode
            {
                Id = id,
                Code = "80",
                Name = "Gelecek Aylara Ait İşlemler",
                Description = "Gelecek Aylara Ait İşlemler",
                Level = 2,
                ParentId = p8,
                Order = 80
            });
            var p80 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "800", Name = "Gelecek Aylara Ait Giderler", Description = "Gelecek Aylara Ait Giderler", Level = 3, ParentId = p80, Order = 800 });
            nodes.Add(new AccountNode { Id = id++, Code = "801", Name = "Gelecek Aylara Ait Gelirler", Description = "Gelecek Aylara Ait Gelirler", Level = 3, ParentId = p80, Order = 801 });

            // ==========================================================
            // 81 – YANSITMA HESAPLARI (Nazım Amaçlı Kullanılanlar)
            // ==========================================================
            nodes.Add(new AccountNode
            {
                Id = id,
                Code = "81",
                Name = "Yansıtma Hesapları",
                Description = "Yansıtma Hesapları",
                Level = 2,
                ParentId = p8,
                Order = 81
            });
            var p81 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "810", Name = "Üretim Giderleri Yansıtma Hesabı", Description = "Üretim Giderleri Yansıtma Hesabı", Level = 3, ParentId = p81, Order = 810 });
            nodes.Add(new AccountNode { Id = id++, Code = "811", Name = "Genel Yönetim Giderleri Yansıtma Hesabı", Description = "Genel Yönetim Giderleri Yansıtma Hesabı", Level = 3, ParentId = p81, Order = 811 });
            nodes.Add(new AccountNode { Id = id++, Code = "812", Name = "Pazarlama Satış Dağıtım Giderleri Yansıtma Hesabı", Description = "Pazarlama Satış Dağıtım Giderleri Yansıtma Hesabı", Level = 3, ParentId = p81, Order = 812 });

            // ==========================================================
            // 89 – DİĞER NAZIM HESAPLAR
            // ==========================================================
            nodes.Add(new AccountNode
            {
                Id = id,
                Code = "89",
                Name = "Diğer Nazım Hesaplar",
                Description = "Diğer Nazım Hesaplar",
                Level = 2,
                ParentId = p8,
                Order = 89
            });
            var p89 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "890", Name = "Teminat Mektupları", Description = "Teminat Mektupları", Level = 3, ParentId = p89, Order = 890 });
            nodes.Add(new AccountNode { Id = id++, Code = "891", Name = "Verilen Garanti ve Kefaletler", Description = "Verilen Garanti ve Kefaletler", Level = 3, ParentId = p89, Order = 891 });
            nodes.Add(new AccountNode { Id = id++, Code = "892", Name = "Alınan Garanti ve Kefaletler", Description = "Alınan Garanti ve Kefaletler", Level = 3, ParentId = p89, Order = 892 });
            nodes.Add(new AccountNode { Id = id++, Code = "893", Name = "Emanet ve Vekalet Hesapları", Description = "Emanet ve Vekalet Hesapları", Level = 3, ParentId = p89, Order = 893 });
            nodes.Add(new AccountNode { Id = id++, Code = "899", Name = "Diğer Nazım Hesaplar", Description = "Diğer Nazım Hesaplar", Level = 3, ParentId = p89, Order = 899 });



            // ----------------------------
            // 9 - YÖNETİMSEL EK HESAPLAR
            // ----------------------------
            nodes.Add(new AccountNode
            {
                Id = id,
                Code = "9",
                Name = "Yönetimsel Hesaplar",
                Description = "Yönetimsel Hesaplar",
                Level = 1,
                Order = 900
            });
            var p9 = id++;

            // 90 Maliyet Muhasebesi
            nodes.Add(new AccountNode
            {
                Id = id,
                Code = "90",
                Name = "Maliyet Muhasebesi Hesapları",
                Description = "Maliyet Muhasebesi Hesapları",
                Level = 2,
                ParentId = p9,
                Order = 900
            });
            var p90 = id++;

            // 91 Bütçe
            nodes.Add(new AccountNode
            {
                Id = id,
                Code = "91",
                Name = "Bütçe Hesapları",
                Description = "Bütçe Hesapları",
                Level = 2,
                ParentId = p9,
                Order = 910
            });
            var p91 = id++;

            // 92 Yönetim Muhasebesi
            nodes.Add(new AccountNode
            {
                Id = id,
                Code = "92",
                Name = "Yönetim Muhasebesi Hesapları",
                Description = "Yönetim Muhasebesi Hesapları",
                Level = 2,
                ParentId = p9,
                Order = 920
            });
            var p92 = id++;

            // 93 Operasyon Hesapları
            nodes.Add(new AccountNode
            {
                Id = id,
                Code = "93",
                Name = "Operasyon Takip Hesapları",
                Description = "Operasyon Takip Hesapları",
                Level = 2,
                ParentId = p9,
                Order = 930
            });
            var p93 = id++;

            // 98 Evrak / İş Takip
            nodes.Add(new AccountNode
            {
                Id = id,
                Code = "98",
                Name = "Evrak Takip Hesapları",
                Description = "Evrak Takip Hesapları",
                Level = 2,
                ParentId = p9,
                Order = 980
            });
            var p98 = id++;

            // 99 Kapanış
            nodes.Add(new AccountNode
            {
                Id = id,
                Code = "99",
                Name = "Kapanış ve Envanter Hesapları",
                Description = "Kapanış ve Envanter Hesapları",
                Level = 2,
                ParentId = p9,
                Order = 990
            });
            var p99 = id++;


            return nodes;

        }

        public static void Seed(ModelBuilder b)
        {
            var id = 1;
            var nodes = new List<AccountNode>();

            // ------------------------------
            // 1 - DÖNEN VARLIKLAR (LEVEL 1)
            // ------------------------------
            nodes.Add(new AccountNode
            {
                Id = id,
                Code = "1",
                Name = "Dönen Varlıklar",
                Description = "Dönen Varlıklar",
                Level = 1,
                Order = 1
            });
            var p1 = id++;
            // ---------------------------------------------
            // 10 - HAZIR DEĞERLER
            // ---------------------------------------------
            nodes.Add(new AccountNode { Id = id, Code = "10", Name = "Hazır Değerler", Description = "Hazır Değerler", Level = 2, ParentId = p1, Order = 10 }); var p10 = id++;
            nodes.Add(new AccountNode { Id = id++, Code = "100", Name = "Kasa", Description = "Kasa", Level = 3, ParentId = p10, Order = 100 });
            nodes.Add(new AccountNode { Id = id++, Code = "101", Name = "Alınan Çekler", Description = "Alınan Çekler", Level = 3, ParentId = p10, Order = 101 });
            nodes.Add(new AccountNode { Id = id++, Code = "102", Name = "Bankalar", Description = "Bankalar", Level = 3, ParentId = p10, Order = 102 });
            nodes.Add(new AccountNode { Id = id++, Code = "103", Name = "Verilen Çekler ve Ödeme Emirleri (-)", Description = "Verilen Çekler ve Ödeme Emirleri (-)", Level = 3, ParentId = p10, Order = 103 });
            nodes.Add(new AccountNode { Id = id++, Code = "108", Name = "Diğer Hazır Değerler", Description = "Diğer Hazır Değerler", Level = 3, ParentId = p10, Order = 108 });

            // ---------------------------------------------
            // 11 - MENKUL KIYMETLER
            // ---------------------------------------------
            nodes.Add(new AccountNode { Id = id, Code = "11", Name = "Menkul Kıymetler", Description = "Menkul Kıymetler", Level = 2, ParentId = p1, Order = 11 }); var p11 = id++;
            nodes.Add(new AccountNode { Id = id++, Code = "110", Name = "Hisse Senetleri", Description = "Hisse Senetleri", Level = 3, ParentId = p11, Order = 110 });
            nodes.Add(new AccountNode { Id = id++, Code = "111", Name = "Özel Kesim Tahvil, Senet ve Bonoları", Description = "Özel Kesim Tahvil, Senet ve Bonoları", Level = 3, ParentId = p11, Order = 111 });
            nodes.Add(new AccountNode { Id = id++, Code = "112", Name = "Kamu Kesimi Tahvil, Senet ve Bonoları", Description = "Kamu Kesimi Tahvil, Senet ve Bonoları", Level = 3, ParentId = p11, Order = 112 });
            nodes.Add(new AccountNode { Id = id++, Code = "118", Name = "Diğer Menkul Kıymetler", Description = "Diğer Menkul Kıymetler", Level = 3, ParentId = p11, Order = 118 });
            nodes.Add(new AccountNode { Id = id++, Code = "119", Name = "Menkul Kıymetler Değer Düşüklüğü Karşılığı (-)", Description = "Menkul Kıymetler Değer Düşüklüğü Karşılığı (-)", Level = 3, ParentId = p11, Order = 119 });

            // ---------------------------------------------
            // 12 - TİCARİ ALACAKLAR
            // ---------------------------------------------
            nodes.Add(new AccountNode { Id = id, Code = "12", Name = "Ticari Alacaklar", Description = "Ticari Alacaklar", Level = 2, ParentId = p1, Order = 12 }); var p12 = id++;
            nodes.Add(new AccountNode { Id = id++, Code = "120", Name = "Alıcılar", Description = "Alıcılar", Level = 3, ParentId = p12, Order = 120 });
            nodes.Add(new AccountNode { Id = id++, Code = "121", Name = "Alacak Senetleri", Description = "Alacak Senetleri", Level = 3, ParentId = p12, Order = 121 });
            nodes.Add(new AccountNode { Id = id++, Code = "122", Name = "Alacak Senetleri Reeskontu (-)", Description = "Alacak Senetleri Reeskontu (-)", Level = 3, ParentId = p12, Order = 122 });
            nodes.Add(new AccountNode { Id = id++, Code = "126", Name = "Kazançlanmamış Finansal Kiralama Faiz Gelirleri (-)", Description = "Kazançlanmamış Finansal Kiralama Faiz Gelirleri (-)", Level = 3, ParentId = p12, Order = 126 });
            nodes.Add(new AccountNode { Id = id++, Code = "127", Name = "Diğer Ticari Alacaklar", Description = "Diğer Ticari Alacaklar", Level = 3, ParentId = p12, Order = 127 });
            nodes.Add(new AccountNode { Id = id++, Code = "128", Name = "Şüpheli Ticari Alacaklar", Description = "Şüpheli Ticari Alacaklar", Level = 3, ParentId = p12, Order = 128 });
            nodes.Add(new AccountNode { Id = id++, Code = "129", Name = "Şüpheli Ticari Alacaklar Karşılığı (-)", Description = "Şüpheli Ticari Alacaklar Karşılığı (-)", Level = 3, ParentId = p12, Order = 129 });

            // ---------------------------------------------
            // 13 - DİĞER ALACAKLAR
            // ---------------------------------------------
            nodes.Add(new AccountNode { Id = id, Code = "13", Name = "Diğer Alacaklar", Description = "Diğer Alacaklar", Level = 2, ParentId = p1, Order = 13 }); var p13 = id++;
            nodes.Add(new AccountNode { Id = id++, Code = "131", Name = "Ortaklardan Alacaklar", Description = "Ortaklardan Alacaklar", Level = 3, ParentId = p13, Order = 131 });
            nodes.Add(new AccountNode { Id = id++, Code = "132", Name = "İştiraklerden Alacaklar", Description = "İştiraklerden Alacaklar", Level = 3, ParentId = p13, Order = 132 });
            nodes.Add(new AccountNode { Id = id++, Code = "133", Name = "Bağlı Ortaklıklardan Alacaklar", Description = "Bağlı Ortaklıklardan Alacaklar", Level = 3, ParentId = p13, Order = 133 });
            nodes.Add(new AccountNode { Id = id++, Code = "135", Name = "Personelden Alacaklar", Description = "Personelden Alacaklar", Level = 3, ParentId = p13, Order = 135 });
            nodes.Add(new AccountNode { Id = id++, Code = "136", Name = "Diğer Çeşitli Alacaklar", Description = "Diğer Çeşitli Alacaklar", Level = 3, ParentId = p13, Order = 136 });
            nodes.Add(new AccountNode { Id = id++, Code = "137", Name = "Diğer Alacak Senetleri Reeskontu (-)", Description = "Diğer Alacak Senetleri Reeskontu (-)", Level = 3, ParentId = p13, Order = 137 });
            nodes.Add(new AccountNode { Id = id++, Code = "138", Name = "Şüpheli Diğer Alacaklar", Description = "Şüpheli Diğer Alacaklar", Level = 3, ParentId = p13, Order = 138 });
            nodes.Add(new AccountNode { Id = id++, Code = "139", Name = "Şüpheli Diğer Alacaklar Karşılığı (-)", Description = "Şüpheli Diğer Alacaklar Karşılığı (-)", Level = 3, ParentId = p13, Order = 139 });

            // ---------------------------------------------
            // 15 - STOKLAR
            // ---------------------------------------------
            nodes.Add(new AccountNode { Id = id, Code = "15", Name = "Stoklar", Description = "Stoklar", Level = 2, ParentId = p1, Order = 15 }); var p15 = id++;
            nodes.Add(new AccountNode { Id = id++, Code = "150", Name = "İlk Madde ve Malzeme", Description = "İlk Madde ve Malzeme", Level = 3, ParentId = p15, Order = 150 });
            nodes.Add(new AccountNode { Id = id++, Code = "151", Name = "Yarı Mamuller – Üretim", Description = "Yarı Mamuller – Üretim", Level = 3, ParentId = p15, Order = 151 });
            nodes.Add(new AccountNode { Id = id++, Code = "152", Name = "Mamuller", Description = "Mamuller", Level = 3, ParentId = p15, Order = 152 });
            nodes.Add(new AccountNode { Id = id++, Code = "153", Name = "Ticari Mallar", Description = "Ticari Mallar", Level = 3, ParentId = p15, Order = 153 });
            nodes.Add(new AccountNode { Id = id++, Code = "157", Name = "Diğer Stoklar", Description = "Diğer Stoklar", Level = 3, ParentId = p15, Order = 157 });
            nodes.Add(new AccountNode { Id = id++, Code = "158", Name = "Stok Değer Düşüklüğü Karşılığı (-)", Description = "Stok Değer Düşüklüğü Karşılığı (-)", Level = 3, ParentId = p15, Order = 158 });
            nodes.Add(new AccountNode { Id = id++, Code = "159", Name = "Verilen Sipariş Avansları", Description = "Verilen Sipariş Avansları", Level = 3, ParentId = p15, Order = 159 });

            // ---------------------------------------------
            // 17 – YILLARA YAYGIN İNŞAAT VE ONARIM MALİYETLERİ
            // ---------------------------------------------
            nodes.Add(new AccountNode { Id = id, Code = "17", Name = "Yıllara Yaygın İnşaat ve Onarım Maliyetleri", Description = "Yıllara Yaygın İnşaat ve Onarım Maliyetleri", Level = 2, ParentId = p1, Order = 17 }); var p17 = id++;
            nodes.Add(new AccountNode { Id = id++, Code = "170", Name = "Yıllara Yaygın İnşaat ve Onarım Maliyetleri", Description = "Yıllara Yaygın İnşaat ve Onarım Maliyetleri", Level = 3, ParentId = p17, Order = 170 });
            nodes.Add(new AccountNode { Id = id++, Code = "171", Name = "Yıllara Yaygın İnşaat Enflasyon Düzeltme Hesabı", Description = "Yıllara Yaygın İnşaat Enflasyon Düzeltme Hesabı", Level = 3, ParentId = p17, Order = 171 });
            nodes.Add(new AccountNode { Id = id++, Code = "179", Name = "Taşeronlara Verilen Avanslar", Description = "Taşeronlara Verilen Avanslar", Level = 3, ParentId = p17, Order = 179 });

            // ---------------------------------------------
            // 18 – GELECEK AYLARA AİT GİDERLER VE GELİR TAHAKKUKLARI
            // ---------------------------------------------
            nodes.Add(new AccountNode { Id = id, Code = "18", Name = "Gelecek Aylara Ait Giderler ve Gelir Tahakkukları", Description = "Gelecek Aylara Ait Giderler ve Gelir Tahakkukları", Level = 2, ParentId = p1, Order = 18 }); var p18 = id++;
            nodes.Add(new AccountNode { Id = id++, Code = "180", Name = "Gelecek Aylara Ait Giderler", Description = "Gelecek Aylara Ait Giderler", Level = 3, ParentId = p18, Order = 180 });
            nodes.Add(new AccountNode { Id = id++, Code = "181", Name = "Gelir Tahakkukları", Description = "Gelir Tahakkukları", Level = 3, ParentId = p18, Order = 181 });

            // ---------------------------------------------
            // 19 – DİĞER DÖNEN VARLIKLAR
            // ---------------------------------------------
            nodes.Add(new AccountNode { Id = id, Code = "19", Name = "Diğer Dönen Varlıklar", Description = "Diğer Dönen Varlıklar", Level = 2, ParentId = p1, Order = 19 }); var p19 = id++;
            nodes.Add(new AccountNode { Id = id++, Code = "190", Name = "Devreden KDV", Description = "Devreden KDV", Level = 3, ParentId = p19, Order = 190 });
            nodes.Add(new AccountNode { Id = id++, Code = "191", Name = "İndirilecek KDV", Description = "İndirilecek KDV", Level = 3, ParentId = p19, Order = 191 });
            nodes.Add(new AccountNode { Id = id++, Code = "192", Name = "Diğer KDV", Description = "Diğer KDV", Level = 3, ParentId = p19, Order = 192 });
            nodes.Add(new AccountNode { Id = id++, Code = "193", Name = "Peşin Ödenen Vergi ve Fonlar", Description = "Peşin Ödenen Vergi ve Fonlar", Level = 3, ParentId = p19, Order = 193 });
            nodes.Add(new AccountNode { Id = id++, Code = "197", Name = "Sayım ve Tesellüm Fazlaları", Description = "Sayım ve Tesellüm Fazlaları", Level = 3, ParentId = p19, Order = 197 });
            nodes.Add(new AccountNode { Id = id++, Code = "198", Name = "Diğer Çeşitli Dönen Varlıklar", Description = "Diğer Çeşitli Dönen Varlıklar", Level = 3, ParentId = p19, Order = 198 });
            nodes.Add(new AccountNode { Id = id++, Code = "199", Name = "Diğer Dönen Varlıklar Karşılığı (-)", Description = "Diğer Dönen Varlıklar Karşılığı (-)", Level = 3, ParentId = p19, Order = 199 });

            // ---------------------------------------------


            // ==========================================================
            // 2 – DURAN VARLIKLAR
            // ==========================================================
            nodes.Add(new AccountNode
            {
                Id = id,
                Code = "2",
                Name = "Duran Varlıklar",
                Description = "Duran Varlıklar",
                Level = 1,
                Order = 2
            });
            var p2 = id++;

            // ==========================================================
            // 22 – TİCARİ ALACAKLAR
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "22", Name = "Ticari Alacaklar", Description = "Ticari Alacaklar", Level = 2, ParentId = p2, Order = 22 });
            var p22 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "220", Name = "Alıcılar", Description = "Alıcılar", Level = 3, ParentId = p22, Order = 220 });
            nodes.Add(new AccountNode { Id = id++, Code = "221", Name = "Alacak Senetleri", Description = "Alacak Senetleri", Level = 3, ParentId = p22, Order = 221 });
            nodes.Add(new AccountNode { Id = id++, Code = "222", Name = "Alacak Senetleri Reeskontu (-)", Description = "Alacak Senetleri Reeskontu (-)", Level = 3, ParentId = p22, Order = 222 });
            nodes.Add(new AccountNode { Id = id++, Code = "224", Name = "Kazanılmamış Finansal Kiralama Faiz Gelirleri", Description = "Kazanılmamış Finansal Kiralama Faiz Gelirleri", Level = 3, ParentId = p22, Order = 224 });
            nodes.Add(new AccountNode { Id = id++, Code = "226", Name = "Verilen Depozito ve Teminatlar", Description = "Verilen Depozito ve Teminatlar", Level = 3, ParentId = p22, Order = 226 });
            nodes.Add(new AccountNode { Id = id++, Code = "229", Name = "Şüpheli Alacaklar Karşılığı (-)", Description = "Şüpheli Alacaklar Karşılığı (-)", Level = 3, ParentId = p22, Order = 229 });

            // ==========================================================
            // 23 – DİĞER ALACAKLAR
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "23", Name = "Diğer Alacaklar", Description = "Diğer Alacaklar", Level = 2, ParentId = p2, Order = 23 });
            var p23 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "231", Name = "Ortaklardan Alacaklar", Description = "Ortaklardan Alacaklar", Level = 3, ParentId = p23, Order = 231 });
            nodes.Add(new AccountNode { Id = id++, Code = "232", Name = "İştiraklerden Alacaklar", Description = "İştiraklerden Alacaklar", Level = 3, ParentId = p23, Order = 232 });
            nodes.Add(new AccountNode { Id = id++, Code = "233", Name = "Bağlı Ortaklıklardan Alacaklar", Description = "Bağlı Ortaklıklardan Alacaklar", Level = 3, ParentId = p23, Order = 233 });
            nodes.Add(new AccountNode { Id = id++, Code = "235", Name = "Personelden Alacaklar", Description = "Personelden Alacaklar", Level = 3, ParentId = p23, Order = 235 });
            nodes.Add(new AccountNode { Id = id++, Code = "236", Name = "Diğer Çeşitli Alacaklar", Description = "Diğer Çeşitli Alacaklar", Level = 3, ParentId = p23, Order = 236 });
            nodes.Add(new AccountNode { Id = id++, Code = "238", Name = "Diğer Alacak Senetleri Reeskontu (-)", Description = "Diğer Alacak Senetleri Reeskontu (-)", Level = 3, ParentId = p23, Order = 238 });
            nodes.Add(new AccountNode { Id = id++, Code = "239", Name = "Şüpheli Diğer Alacaklar Karşılığı (-)", Description = "Şüpheli Diğer Alacaklar Karşılığı (-)", Level = 3, ParentId = p23, Order = 239 });

            // ==========================================================
            // 24 – MALİ DURAN VARLIKLAR
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "24", Name = "Mali Duran Varlıklar", Description = "Mali Duran Varlıklar", Level = 2, ParentId = p2, Order = 24 });
            var p24 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "240", Name = "Bağlı Menkul Kıymetler", Description = "Bağlı Menkul Kıymetler", Level = 3, ParentId = p24, Order = 240 });
            nodes.Add(new AccountNode { Id = id++, Code = "241", Name = "Bağlı Menkul Kıymetler Değer Düşüklüğü Karşılığı (-)", Description = "Bağlı Menkul Kıymetler Değer Düşüklüğü Karşılığı (-)", Level = 3, ParentId = p24, Order = 241 });
            nodes.Add(new AccountNode { Id = id++, Code = "242", Name = "İştirakler", Description = "İştirakler", Level = 3, ParentId = p24, Order = 242 });
            nodes.Add(new AccountNode { Id = id++, Code = "243", Name = "İştiraklere Sermaye Taahhütleri", Description = "İştiraklere Sermaye Taahhütleri", Level = 3, ParentId = p24, Order = 243 });
            nodes.Add(new AccountNode { Id = id++, Code = "244", Name = "İştirakler Sermaye Payları Değer Düşüklüğü Karşılığı (-)", Description = "İştirakler Sermaye Payları Değer Düşüklüğü Karşılığı (-)", Level = 3, ParentId = p24, Order = 244 });
            nodes.Add(new AccountNode { Id = id++, Code = "245", Name = "Bağlı Ortaklıklar", Description = "Bağlı Ortaklıklar", Level = 3, ParentId = p24, Order = 245 });
            nodes.Add(new AccountNode { Id = id++, Code = "246", Name = "Bağlı Ortaklıklara Sermaye Taahhütleri (-)", Description = "Bağlı Ortaklıklara Sermaye Taahhütleri (-)", Level = 3, ParentId = p24, Order = 246 });
            nodes.Add(new AccountNode { Id = id++, Code = "247", Name = "Bağlı Ortaklıklar Sermaye Payları Değer Düşüklüğü Karşılığı (-)", Description = "Bağlı Ortaklıklar Sermaye Payları Değer Düşüklüğü Karşılığı (-)", Level = 3, ParentId = p24, Order = 247 });
            nodes.Add(new AccountNode { Id = id++, Code = "248", Name = "Diğer Mali Duran Varlıklar", Description = "Diğer Mali Duran Varlıklar", Level = 3, ParentId = p24, Order = 248 });
            nodes.Add(new AccountNode { Id = id++, Code = "249", Name = "Diğer Mali Duran Varlıklar Karşılığı (-)", Description = "Diğer Mali Duran Varlıklar Karşılığı (-)", Level = 3, ParentId = p24, Order = 249 });

            // ==========================================================
            // 25 – MADDİ DURAN VARLIKLAR
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "25", Name = "Maddi Duran Varlıklar", Description = "Maddi Duran Varlıklar", Level = 2, ParentId = p2, Order = 25 });
            var p25 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "250", Name = "Arazi ve Arsalar", Description = "Arazi ve Arsalar", Level = 3, ParentId = p25, Order = 250 });
            nodes.Add(new AccountNode { Id = id++, Code = "251", Name = "Yer Altı ve Yer Üstü Düzenleri", Description = "Yer Altı ve Yer Üstü Düzenleri", Level = 3, ParentId = p25, Order = 251 });
            nodes.Add(new AccountNode { Id = id++, Code = "252", Name = "Binalar", Description = "Binalar", Level = 3, ParentId = p25, Order = 252 });
            nodes.Add(new AccountNode { Id = id++, Code = "253", Name = "Tesis, Makine ve Cihazlar", Description = "Tesis, Makine ve Cihazlar", Level = 3, ParentId = p25, Order = 253 });
            nodes.Add(new AccountNode { Id = id++, Code = "254", Name = "Taşıtlar", Description = "Taşıtlar", Level = 3, ParentId = p25, Order = 254 });
            nodes.Add(new AccountNode { Id = id++, Code = "255", Name = "Demirbaşlar", Description = "Demirbaşlar", Level = 3, ParentId = p25, Order = 255 });
            nodes.Add(new AccountNode { Id = id++, Code = "256", Name = "Diğer Maddi Duran Varlıklar", Description = "Diğer Maddi Duran Varlıklar", Level = 3, ParentId = p25, Order = 256 });
            nodes.Add(new AccountNode { Id = id++, Code = "257", Name = "Birikmiş Amortismanlar (-)", Description = "Birikmiş Amortismanlar (-)", Level = 3, ParentId = p25, Order = 257 });
            nodes.Add(new AccountNode { Id = id++, Code = "258", Name = "Yapılmakta Olan Yatırımlar", Description = "Yapılmakta Olan Yatırımlar", Level = 3, ParentId = p25, Order = 258 });
            nodes.Add(new AccountNode { Id = id++, Code = "259", Name = "Verilen Avanslar", Description = "Verilen Avanslar", Level = 3, ParentId = p25, Order = 259 });

            // ==========================================================
            // 26 – MADDİ OLMAYAN DURAN VARLIKLAR
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "26", Name = "Maddi Olmayan Duran Varlıklar", Description = "Maddi Olmayan Duran Varlıklar", Level = 2, ParentId = p2, Order = 26 });
            var p26 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "260", Name = "Haklar", Description = "Haklar", Level = 3, ParentId = p26, Order = 260 });
            nodes.Add(new AccountNode { Id = id++, Code = "261", Name = "Şerefiye", Description = "Şerefiye", Level = 3, ParentId = p26, Order = 261 });
            nodes.Add(new AccountNode { Id = id++, Code = "262", Name = "Kuruluş ve Örgütlenme Giderleri", Description = "Kuruluş ve Örgütlenme Giderleri", Level = 3, ParentId = p26, Order = 262 });
            nodes.Add(new AccountNode { Id = id++, Code = "263", Name = "Araştırma ve Geliştirme Giderleri", Description = "Araştırma ve Geliştirme Giderleri", Level = 3, ParentId = p26, Order = 263 });
            nodes.Add(new AccountNode { Id = id++, Code = "264", Name = "Özel Maliyetler", Description = "Özel Maliyetler", Level = 3, ParentId = p26, Order = 264 });
            nodes.Add(new AccountNode { Id = id++, Code = "267", Name = "Diğer Maddi Olmayan Duran Varlıklar", Description = "Diğer Maddi Olmayan Duran Varlıklar", Level = 3, ParentId = p26, Order = 267 });
            nodes.Add(new AccountNode { Id = id++, Code = "268", Name = "Birikmiş Amortismanlar (-)", Description = "Birikmiş Amortismanlar (-)", Level = 3, ParentId = p26, Order = 268 });
            nodes.Add(new AccountNode { Id = id++, Code = "269", Name = "Verilen Avanslar", Description = "Verilen Avanslar", Level = 3, ParentId = p26, Order = 269 });

            // ==========================================================
            // 27 – ÖZEL TÜKENMEYE TABİ VARLIKLAR
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "27", Name = "Özel Tükenmeye Tabi Varlıklar", Description = "Özel Tükenmeye Tabi Varlıklar", Level = 2, ParentId = p2, Order = 27 });
            var p27 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "271", Name = "Arama Giderleri", Description = "Arama Giderleri", Level = 3, ParentId = p27, Order = 271 });
            nodes.Add(new AccountNode { Id = id++, Code = "272", Name = "Hazırlık ve Geliştirme Giderleri", Description = "Hazırlık ve Geliştirme Giderleri", Level = 3, ParentId = p27, Order = 272 });
            nodes.Add(new AccountNode { Id = id++, Code = "277", Name = "Diğer Özel Tükenmeye Tabi Varlıklar", Description = "Diğer Özel Tükenmeye Tabi Varlıklar", Level = 3, ParentId = p27, Order = 277 });
            nodes.Add(new AccountNode { Id = id++, Code = "278", Name = "Birikmiş Tükenme Payları (-)", Description = "Birikmiş Tükenme Payları (-)", Level = 3, ParentId = p27, Order = 278 });
            nodes.Add(new AccountNode { Id = id++, Code = "279", Name = "Verilen Avanslar", Description = "Verilen Avanslar", Level = 3, ParentId = p27, Order = 279 });

            // ==========================================================
            // 28 – GELECEK YILLARA AİT GİDERLER VE GELİR TAHAKKUKLARI
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "28", Name = "Gelecek Yıllara Ait Giderler ve Gelir Tahakkukları", Description = "Gelecek Yıllara Ait Giderler ve Gelir Tahakkukları", Level = 2, ParentId = p2, Order = 28 });
            var p28 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "280", Name = "Gelecek Yıllara Ait Giderler", Description = "Gelecek Yıllara Ait Giderler", Level = 3, ParentId = p28, Order = 280 });
            nodes.Add(new AccountNode { Id = id++, Code = "281", Name = "Gelir Tahakkukları", Description = "Gelir Tahakkukları", Level = 3, ParentId = p28, Order = 281 });

            // ==========================================================
            // 29 – DİĞER DURAN VARLIKLAR
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "29", Name = "Diğer Duran Varlıklar", Description = "Diğer Duran Varlıklar", Level = 2, ParentId = p2, Order = 29 });
            var p29 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "291", Name = "Gelecek Yıllarda İndirilecek KDV", Description = "Gelecek Yıllarda İndirilecek KDV", Level = 3, ParentId = p29, Order = 291 });
            nodes.Add(new AccountNode { Id = id++, Code = "292", Name = "Diğer Katma Değer Vergisi", Description = "Diğer Katma Değer Vergisi", Level = 3, ParentId = p29, Order = 292 });
            nodes.Add(new AccountNode { Id = id++, Code = "293", Name = "Gelecek Yıllar İhtiyacı Stoklar", Description = "Gelecek Yıllar İhtiyacı Stoklar", Level = 3, ParentId = p29, Order = 293 });
            nodes.Add(new AccountNode { Id = id++, Code = "294", Name = "Elden Çıkarılacak Stoklar ve Maddi Duran Varlıklar", Description = "Elden Çıkarılacak Stoklar ve Maddi Duran Varlıklar", Level = 3, ParentId = p29, Order = 294 });
            nodes.Add(new AccountNode { Id = id++, Code = "295", Name = "Peşin Ödenen Vergiler ve Fonlar", Description = "Peşin Ödenen Vergiler ve Fonlar", Level = 3, ParentId = p29, Order = 295 });
            nodes.Add(new AccountNode { Id = id++, Code = "297", Name = "Diğer Çeşitli Duran Varlıklar", Description = "Diğer Çeşitli Duran Varlıklar", Level = 3, ParentId = p29, Order = 297 });
            nodes.Add(new AccountNode { Id = id++, Code = "298", Name = "Stok Değer Düşüklüğü Karşılığı (-)", Description = "Stok Değer Düşüklüğü Karşılığı (-)", Level = 3, ParentId = p29, Order = 298 });
            nodes.Add(new AccountNode { Id = id++, Code = "299", Name = "Birikmiş Amortismanlar (-)", Description = "Birikmiş Amortismanlar (-)", Level = 3, ParentId = p29, Order = 299 });


            // ==========================================================
            // 3 – KISA VADELİ YABANCI KAYNAKLAR
            // ==========================================================
            nodes.Add(new AccountNode
            {
                Id = id,
                Code = "3",
                Name = "Kısa Vadeli Yabancı Kaynaklar",
                Description = "Kısa Vadeli Yabancı Kaynaklar",
                Level = 1,
                Order = 3
            });
            var p3 = id++;

            // ==========================================================
            // 30 – MALİ BORÇLAR
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "30", Name = "Mali Borçlar", Description = "Mali Borçlar", Level = 2, ParentId = p3, Order = 30 });
            var p30 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "300", Name = "Banka Kredileri", Description = "Banka Kredileri", Level = 3, ParentId = p30, Order = 300 });
            nodes.Add(new AccountNode { Id = id++, Code = "301", Name = "Finansal Kiralama İşlemlerinden Borçlar", Description = "Finansal Kiralama İşlemlerinden Borçlar", Level = 3, ParentId = p30, Order = 301 });
            nodes.Add(new AccountNode { Id = id++, Code = "302", Name = "Ertelenmiş Finansal Kiralama Borçlanma Maliyetleri (-)", Description = "Ertelenmiş Finansal Kiralama Borçlanma Maliyetleri (-)", Level = 3, ParentId = p30, Order = 302 });
            nodes.Add(new AccountNode { Id = id++, Code = "303", Name = "Uzun Vadeli Kredilerin Anapara Taksit ve Faizleri", Description = "Uzun Vadeli Kredilerin Anapara Taksit ve Faizleri", Level = 3, ParentId = p30, Order = 303 });
            nodes.Add(new AccountNode { Id = id++, Code = "304", Name = "Tahvil Anapara Borç, Taksit ve Faizleri", Description = "Tahvil Anapara Borç, Taksit ve Faizleri", Level = 3, ParentId = p30, Order = 304 });
            nodes.Add(new AccountNode { Id = id++, Code = "305", Name = "Çıkarılmış Bonolar ve Senetler", Description = "Çıkarılmış Bonolar ve Senetler", Level = 3, ParentId = p30, Order = 305 });
            nodes.Add(new AccountNode { Id = id++, Code = "306", Name = "Çıkarılmış Diğer Menkul Kıymetler", Description = "Çıkarılmış Diğer Menkul Kıymetler", Level = 3, ParentId = p30, Order = 306 });
            nodes.Add(new AccountNode { Id = id++, Code = "308", Name = "Menkul Kıymetler İhraç Farkı (-)", Description = "Menkul Kıymetler İhraç Farkı (-)", Level = 3, ParentId = p30, Order = 308 });
            nodes.Add(new AccountNode { Id = id++, Code = "309", Name = "Diğer Mali Borçlar", Description = "Diğer Mali Borçlar", Level = 3, ParentId = p30, Order = 309 });

            // ==========================================================
            // 32 – TİCARİ BORÇLAR
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "32", Name = "Ticari Borçlar", Description = "Ticari Borçlar", Level = 2, ParentId = p3, Order = 32 });
            var p32 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "320", Name = "Satıcılar", Description = "Satıcılar", Level = 3, ParentId = p32, Order = 320 });
            nodes.Add(new AccountNode { Id = id++, Code = "321", Name = "Borç Senetleri", Description = "Borç Senetleri", Level = 3, ParentId = p32, Order = 321 });
            nodes.Add(new AccountNode { Id = id++, Code = "322", Name = "Borç Senetleri Reeskontu (-)", Description = "Borç Senetleri Reeskontu (-)", Level = 3, ParentId = p32, Order = 322 });
            nodes.Add(new AccountNode { Id = id++, Code = "326", Name = "Alınan Depozito ve Teminatlar", Description = "Alınan Depozito ve Teminatlar", Level = 3, ParentId = p32, Order = 326 });
            nodes.Add(new AccountNode { Id = id++, Code = "329", Name = "Diğer Ticari Borçlar", Description = "Diğer Ticari Borçlar", Level = 3, ParentId = p32, Order = 329 });

            // ==========================================================
            // 33 – DİĞER BORÇLAR
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "33", Name = "Diğer Borçlar", Description = "Diğer Borçlar", Level = 2, ParentId = p3, Order = 33 });
            var p33 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "331", Name = "Ortaklara Borçlar", Description = "Ortaklara Borçlar", Level = 3, ParentId = p33, Order = 331 });
            nodes.Add(new AccountNode { Id = id++, Code = "332", Name = "İştiraklere Borçlar", Description = "İştiraklere Borçlar", Level = 3, ParentId = p33, Order = 332 });
            nodes.Add(new AccountNode { Id = id++, Code = "333", Name = "Bağlı Ortaklıklara Borçlar", Description = "Bağlı Ortaklıklara Borçlar", Level = 3, ParentId = p33, Order = 333 });
            nodes.Add(new AccountNode { Id = id++, Code = "335", Name = "Personele Borçlar", Description = "Personele Borçlar", Level = 3, ParentId = p33, Order = 335 });
            nodes.Add(new AccountNode { Id = id++, Code = "336", Name = "Diğer Çeşitli Borçlar", Description = "Diğer Çeşitli Borçlar", Level = 3, ParentId = p33, Order = 336 });
            nodes.Add(new AccountNode { Id = id++, Code = "337", Name = "Diğer Borç Senetleri Reeskontu (-)", Description = "Diğer Borç Senetleri Reeskontu (-)", Level = 3, ParentId = p33, Order = 337 });

            // ==========================================================
            // 34 – ALINAN AVANSLAR
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "34", Name = "Alınan Avanslar", Description = "Alınan Avanslar", Level = 2, ParentId = p3, Order = 34 });
            var p34 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "340", Name = "Alınan Sipariş Avansları", Description = "Alınan Sipariş Avansları", Level = 3, ParentId = p34, Order = 340 });
            nodes.Add(new AccountNode { Id = id++, Code = "349", Name = "Diğer Alınan Avanslar", Description = "Diğer Alınan Avanslar", Level = 3, ParentId = p34, Order = 349 });

            // ==========================================================
            // 35 – YILLARA YAYGIN İNŞAAT VE ONARIM HAKEDİŞLERİ
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "35", Name = "Yıllara Yaygın İnşaat ve Onarım Hakedişleri", Description = "Yıllara Yaygın İnşaat ve Onarım Hakedişleri", Level = 2, ParentId = p3, Order = 35 });
            var p35 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "357", Name = "Yıllara Yaygın İnşaat ve Onarım Hakediş Bedelleri", Description = "Yıllara Yaygın İnşaat ve Onarım Hakediş Bedelleri", Level = 3, ParentId = p35, Order = 350 });
            nodes.Add(new AccountNode { Id = id++, Code = "358", Name = "Yıllara Yaygın İnşaat Enflasyon Düzeltme Hesabı", Description = "Yıllara Yaygın İnşaat Enflasyon Düzeltme Hesabı", Level = 3, ParentId = p35, Order = 358 });

            // ==========================================================
            // 36 – ÖDENECEK VERGİ VE DİĞER YÜKÜMLÜLÜKLER
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "36", Name = "Ödenecek Vergi ve Diğer Yükümlülükler", Description = "Ödenecek Vergi ve Diğer Yükümlülükler", Level = 2, ParentId = p3, Order = 36 });
            var p36 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "360", Name = "Ödenecek Vergi ve Fonlar", Description = "Ödenecek Vergi ve Fonlar", Level = 3, ParentId = p36, Order = 360 });
            nodes.Add(new AccountNode { Id = id++, Code = "361", Name = "Ödenecek Sosyal Güvenlik Kesintileri", Description = "Ödenecek Sosyal Güvenlik Kesintileri", Level = 3, ParentId = p36, Order = 361 });
            nodes.Add(new AccountNode { Id = id++, Code = "368", Name = "Vadesi Gelmiş Ertelenmiş veya Taksitlendirilmiş Vergi ve Diğer Yükümlülükler", Description = "Vadesi Gelmiş Ertelenmiş veya Taksitlendirilmiş Vergi ve Diğer Yükümlülükler", Level = 3, ParentId = p36, Order = 368 });
            nodes.Add(new AccountNode { Id = id++, Code = "369", Name = "Ödenecek Diğer Yükümlülükler", Description = "Ödenecek Diğer Yükümlülükler", Level = 3, ParentId = p36, Order = 369 });

            // ==========================================================
            // 37 – BORÇ VE GİDER KARŞILIKLARI
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "37", Name = "Borç ve Gider Karşılıkları", Description = "Borç ve Gider Karşılıkları", Level = 2, ParentId = p3, Order = 37 });
            var p37 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "370", Name = "Dönem Kârı Vergi ve Diğer Yasal Yükümlülük Karşılıkları", Description = "Dönem Kârı Vergi ve Diğer Yasal Yükümlülük Karşılıkları", Level = 3, ParentId = p37, Order = 370 });
            nodes.Add(new AccountNode { Id = id++, Code = "371", Name = "Dönem Kârının Peşin Ödenen Vergi ve Diğer Yükümlülükleri (-)", Description = "Dönem Kârının Peşin Ödenen Vergi ve Diğer Yükümlülükleri (-)", Level = 3, ParentId = p37, Order = 371 });
            nodes.Add(new AccountNode { Id = id++, Code = "372", Name = "Kıdem Tazminatı Karşılığı", Description = "Kıdem Tazminatı Karşılığı", Level = 3, ParentId = p37, Order = 372 });
            nodes.Add(new AccountNode { Id = id++, Code = "373", Name = "Maliyet Giderleri Karşılığı", Description = "Maliyet Giderleri Karşılığı", Level = 3, ParentId = p37, Order = 373 });
            nodes.Add(new AccountNode { Id = id++, Code = "379", Name = "Diğer Borç ve Gider Karşılıkları", Description = "Diğer Borç ve Gider Karşılıkları", Level = 3, ParentId = p37, Order = 379 });

            // ==========================================================
            // 38 – GELECEK AYLARA AİT GELİRLER VE GİDER TAHAKKUKLARI
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "38", Name = "Gelecek Aylara Ait Gelirler ve Gider Tahakkukları", Description = "Gelecek Aylara Ait Gelirler ve Gider Tahakkukları", Level = 2, ParentId = p3, Order = 38 });
            var p38 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "380", Name = "Gelecek Aylara Ait Gelirler", Description = "Gelecek Aylara Ait Gelirler", Level = 3, ParentId = p38, Order = 380 });
            nodes.Add(new AccountNode { Id = id++, Code = "381", Name = "Gider Tahakkukları", Description = "Gider Tahakkukları", Level = 3, ParentId = p38, Order = 381 });

            // ==========================================================
            // 39 – DİĞER KISA VADELİ YABANCI KAYNAKLAR
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "39", Name = "Diğer Kısa Vadeli Yabancı Kaynaklar", Description = "Diğer Kısa Vadeli Yabancı Kaynaklar", Level = 2, ParentId = p3, Order = 39 });
            var p39 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "391", Name = "Hesaplanan KDV", Description = "Hesaplanan KDV", Level = 3, ParentId = p39, Order = 391 });
            nodes.Add(new AccountNode { Id = id++, Code = "392", Name = "Diğer KDV", Description = "Diğer KDV", Level = 3, ParentId = p39, Order = 392 });
            nodes.Add(new AccountNode { Id = id++, Code = "393", Name = "Merkez ve Şubeler Cari Hesabı", Description = "Merkez ve Şubeler Cari Hesabı", Level = 3, ParentId = p39, Order = 393 });
            nodes.Add(new AccountNode { Id = id++, Code = "397", Name = "Sayım ve Tesellüm Fazlaları", Description = "Sayım ve Tesellüm Fazlaları", Level = 3, ParentId = p39, Order = 397 });
            nodes.Add(new AccountNode { Id = id++, Code = "399", Name = "Diğer Çeşitli Yabancı Kaynaklar", Description = "Diğer Çeşitli Yabancı Kaynaklar", Level = 3, ParentId = p39, Order = 399 });


            // ==========================================================
            // 4 – UZUN VADELİ YABANCI KAYNAKLAR
            // ==========================================================
            nodes.Add(new AccountNode
            {
                Id = id,
                Code = "4",
                Name = "Uzun Vadeli Yabancı Kaynaklar",
                Description = "Uzun Vadeli Yabancı Kaynaklar",
                Level = 1,
                Order = 4
            });
            var p4 = id++;

            // ==========================================================
            // 40 – MALİ BORÇLAR
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "40", Name = "Mali Borçlar", Description = "Mali Borçlar", Level = 2, ParentId = p4, Order = 40 }); var p40 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "400", Name = "Banka Kredileri", Description = "Banka Kredileri", Level = 3, ParentId = p40, Order = 400 });
            nodes.Add(new AccountNode { Id = id++, Code = "401", Name = "Finansal Kiralama İşlemlerinden Borçlar", Description = "Finansal Kiralama İşlemlerinden Borçlar", Level = 3, ParentId = p40, Order = 401 });
            nodes.Add(new AccountNode { Id = id++, Code = "402", Name = "Ertelenmiş Finansal Kiralama Borçlanma Maliyetleri (-)", Description = "Ertelenmiş Finansal Kiralama Borçlanma Maliyetleri (-)", Level = 3, ParentId = p40, Order = 402 });
            nodes.Add(new AccountNode { Id = id++, Code = "405", Name = "Çıkarılmış Tahviller", Description = "Çıkarılmış Tahviller", Level = 3, ParentId = p40, Order = 405 });
            nodes.Add(new AccountNode { Id = id++, Code = "407", Name = "Çıkarılmış Diğer Menkul Kıymetler", Description = "Çıkarılmış Diğer Menkul Kıymetler", Level = 3, ParentId = p40, Order = 407 });
            nodes.Add(new AccountNode { Id = id++, Code = "408", Name = "Menkul Kıymetler İhraç Farkı (-)", Description = "Menkul Kıymetler İhraç Farkı (-)", Level = 3, ParentId = p40, Order = 408 });
            nodes.Add(new AccountNode { Id = id++, Code = "409", Name = "Diğer Mali Borçlar", Description = "Diğer Mali Borçlar", Level = 3, ParentId = p40, Order = 409 });

            // ==========================================================
            // 42 – TİCARİ BORÇLAR
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "42", Name = "Ticari Borçlar", Description = "Ticari Borçlar", Level = 2, ParentId = p4, Order = 42 }); var p42 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "420", Name = "Satıcılar", Description = "Satıcılar", Level = 3, ParentId = p42, Order = 420 });
            nodes.Add(new AccountNode { Id = id++, Code = "421", Name = "Borç Senetleri", Description = "Borç Senetleri", Level = 3, ParentId = p42, Order = 421 });
            nodes.Add(new AccountNode { Id = id++, Code = "422", Name = "Borç Senetleri Reeskontu (-)", Description = "Borç Senetleri Reeskontu (-)", Level = 3, ParentId = p42, Order = 422 });
            nodes.Add(new AccountNode { Id = id++, Code = "426", Name = "Alınan Depozito ve Teminatlar", Description = "Alınan Depozito ve Teminatlar", Level = 3, ParentId = p42, Order = 426 });
            nodes.Add(new AccountNode { Id = id++, Code = "429", Name = "Diğer Ticari Borçlar", Description = "Diğer Ticari Borçlar", Level = 3, ParentId = p42, Order = 429 });

            // ==========================================================
            // 43 – DİĞER BORÇLAR
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "43", Name = "Diğer Borçlar", Description = "Diğer Borçlar", Level = 2, ParentId = p4, Order = 43 }); var p43 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "430", Name = "Alınan Depozito ve Teminatlar", Description = "Alınan Depozito ve Teminatlar", Level = 3, ParentId = p43, Order = 430 });
            nodes.Add(new AccountNode { Id = id++, Code = "431", Name = "Ortaklara Borçlar", Description = "Ortaklara Borçlar", Level = 3, ParentId = p43, Order = 431 });
            nodes.Add(new AccountNode { Id = id++, Code = "432", Name = "İştiraklere Borçlar", Description = "İştiraklere Borçlar", Level = 3, ParentId = p43, Order = 432 });
            nodes.Add(new AccountNode { Id = id++, Code = "433", Name = "Bağlı Ortaklıklara Borçlar", Description = "Bağlı Ortaklıklara Borçlar", Level = 3, ParentId = p43, Order = 433 });
            nodes.Add(new AccountNode { Id = id++, Code = "437", Name = "Diğer Borç Senetleri Reeskontu (-)", Description = "Diğer Borç Senetleri Reeskontu (-)", Level = 3, ParentId = p43, Order = 437 });
            nodes.Add(new AccountNode { Id = id++, Code = "438", Name = "Kamuya Olan Ertelenmiş veya Taksitlendirilmiş Borçlar", Description = "Kamuya Olan Ertelenmiş veya Taksitlendirilmiş Borçlar", Level = 3, ParentId = p43, Order = 438 });
            nodes.Add(new AccountNode { Id = id++, Code = "439", Name = "Diğer Borçlar", Description = "Diğer Borçlar", Level = 3, ParentId = p43, Order = 439 });

            // ==========================================================
            // 44 – ALINAN AVANSLAR
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "44", Name = "Alınan Avanslar", Description = "Alınan Avanslar", Level = 2, ParentId = p4, Order = 44 }); var p44 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "440", Name = "Alınan Sipariş Avansları", Description = "Alınan Sipariş Avansları", Level = 3, ParentId = p44, Order = 440 });
            nodes.Add(new AccountNode { Id = id++, Code = "449", Name = "Diğer Alınan Avanslar", Description = "Diğer Alınan Avanslar", Level = 3, ParentId = p44, Order = 449 });

            // ==========================================================
            // 47 – BORÇ VE GİDER KARŞILIKLARI
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "47", Name = "Borç ve Gider Karşılıkları", Description = "Borç ve Gider Karşılıkları", Level = 2, ParentId = p4, Order = 47 }); var p47 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "470", Name = "Vergi ve Diğer Yükümlülük Karşılıkları", Description = "Vergi ve Diğer Yükümlülük Karşılıkları", Level = 3, ParentId = p47, Order = 470 });
            nodes.Add(new AccountNode { Id = id++, Code = "471", Name = "Dönem Kârı Vergi ve Diğer Yasal Yükümlülük Karşılıkları", Description = "Dönem Kârı Vergi ve Diğer Yasal Yükümlülük Karşılıkları", Level = 3, ParentId = p47, Order = 471 });
            nodes.Add(new AccountNode { Id = id++, Code = "472", Name = "Kıdem Tazminatı Karşılığı", Description = "Kıdem Tazminatı Karşılığı", Level = 3, ParentId = p47, Order = 472 });
            nodes.Add(new AccountNode { Id = id++, Code = "479", Name = "Diğer Borç ve Gider Karşılıkları", Description = "Diğer Borç ve Gider Karşılıkları", Level = 3, ParentId = p47, Order = 479 });

            // ==========================================================
            // 48 – GELECEK YILLARA AİT GELİRLER
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "48", Name = "Gelecek Yıllara Ait Gelirler", Description = "Gelecek Yıllara Ait Gelirler", Level = 2, ParentId = p4, Order = 48 }); var p48 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "480", Name = "Gelecek Yıllara Ait Gelirler", Description = "Gelecek Yıllara Ait Gelirler", Level = 3, ParentId = p48, Order = 480 });
            nodes.Add(new AccountNode { Id = id++, Code = "481", Name = "Gelir Tahakkukları", Description = "Gelir Tahakkukları", Level = 3, ParentId = p48, Order = 481 });

            // ==========================================================
            // 49 – DİĞER UZUN VADELİ YABANCI KAYNAKLAR
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "49", Name = "Diğer Uzun Vadeli Yabancı Kaynaklar", Description = "Diğer Uzun Vadeli Yabancı Kaynaklar", Level = 2, ParentId = p4, Order = 49 }); var p49 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "490", Name = "Gelecek Yıllara Ertelenen Giderler (-)", Description = "Gelecek Yıllara Ertelenen Giderler (-)", Level = 3, ParentId = p49, Order = 490 });
            nodes.Add(new AccountNode { Id = id++, Code = "492", Name = "Gelecek Yıllara Ertelenen Diğer Gelirler veya Terkin Edilecek KDV", Description = "Gelecek Yıllara Ertelenen Diğer Gelirler veya Terkin Edilecek KDV", Level = 3, ParentId = p49, Order = 492 });
            nodes.Add(new AccountNode { Id = id++, Code = "493", Name = "Çeşitli Karşılıklar", Description = "Çeşitli Karşılıklar", Level = 3, ParentId = p49, Order = 493 });
            nodes.Add(new AccountNode { Id = id++, Code = "497", Name = "Sayım ve Tesellüm Fazlaları", Description = "Sayım ve Tesellüm Fazlaları", Level = 3, ParentId = p49, Order = 497 });
            nodes.Add(new AccountNode { Id = id++, Code = "499", Name = "Diğer Çeşitli Uzun Vadeli Yabancı Kaynaklar", Description = "Diğer Çeşitli Uzun Vadeli Yabancı Kaynaklar", Level = 3, ParentId = p49, Order = 499 });

            // ==========================================================
            // 5 – ÖZ KAYNAKLAR
            // ==========================================================
            nodes.Add(new AccountNode
            {
                Id = id,
                Code = "5",
                Name = "Öz Kaynaklar",
                Description = "Öz Kaynaklar",
                Level = 1,
                Order = 5
            });
            var p5 = id++;

            // ==========================================================
            // 50 – ÖDENMİŞ SERMAYE
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "50", Name = "Ödenmiş Sermaye", Description = "Ödenmiş Sermaye", Level = 2, ParentId = p5, Order = 50 });
            var p50 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "500", Name = "Sermaye", Description = "Sermaye", Level = 3, ParentId = p50, Order = 500 });
            nodes.Add(new AccountNode { Id = id++, Code = "501", Name = "Ödenmemiş Sermaye (-)", Description = "Ödenmemiş Sermaye (-)", Level = 3, ParentId = p50, Order = 501 });
            nodes.Add(new AccountNode { Id = id++, Code = "502", Name = "Sermaye Düzeltmesi Olumlu Farkları", Description = "Sermaye Düzeltmesi Olumlu Farkları", Level = 3, ParentId = p50, Order = 502 });
            nodes.Add(new AccountNode { Id = id++, Code = "503", Name = "Sermaye Düzeltmesi Olumsuz Farkları (-)", Description = "Sermaye Düzeltmesi Olumsuz Farkları (-)", Level = 3, ParentId = p50, Order = 503 });


            // ==========================================================
            // 52 – SERMAYE YEDEKLERİ
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "52", Name = "Sermaye Yedekleri", Description = "Sermaye Yedekleri", Level = 2, ParentId = p5, Order = 52 });
            var p52 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "520", Name = "Hisse Senetleri İhraç Primleri", Description = "Hisse Senetleri İhraç Primleri", Level = 3, ParentId = p52, Order = 520 });
            nodes.Add(new AccountNode { Id = id++, Code = "521", Name = "Hisse Senedi İptal Kârları", Description = "Hisse Senedi İptal Kârları", Level = 3, ParentId = p52, Order = 521 });
            nodes.Add(new AccountNode { Id = id++, Code = "522", Name = "M.D.V. Yeniden Değerleme Artışları", Description = "M.D.V. Yeniden Değerleme Artışları", Level = 3, ParentId = p52, Order = 522 });
            nodes.Add(new AccountNode { Id = id++, Code = "523", Name = "İştirakler Yeniden Değerleme Artışları", Description = "İştirakler Yeniden Değerleme Artışları", Level = 3, ParentId = p52, Order = 523 });
            nodes.Add(new AccountNode { Id = id++, Code = "524", Name = "Maliyet Bedeli Artışları Fonu", Description = "Maliyet Bedeli Artışları Fonu", Level = 3, ParentId = p52, Order = 524 });
            nodes.Add(new AccountNode { Id = id++, Code = "529", Name = "Diğer Sermaye Yedekleri", Description = "Diğer Sermaye Yedekleri", Level = 3, ParentId = p52, Order = 529 });


            // ==========================================================
            // 54 – KÂR YEDEKLERİ
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "54", Name = "Kâr Yedekleri", Description = "Kâr Yedekleri", Level = 2, ParentId = p5, Order = 54 });
            var p54 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "540", Name = "Yasal Yedekler", Description = "Yasal Yedekler", Level = 3, ParentId = p54, Order = 540 });
            nodes.Add(new AccountNode { Id = id++, Code = "541", Name = "Statü Yedekleri", Description = "Statü Yedekleri", Level = 3, ParentId = p54, Order = 541 });
            nodes.Add(new AccountNode { Id = id++, Code = "542", Name = "Olağanüstü Yedekler", Description = "Olağanüstü Yedekler", Level = 3, ParentId = p54, Order = 542 });
            nodes.Add(new AccountNode { Id = id++, Code = "548", Name = "Diğer Kâr Yedekleri", Description = "Diğer Kâr Yedekleri", Level = 3, ParentId = p54, Order = 548 });
            nodes.Add(new AccountNode { Id = id++, Code = "549", Name = "Özel Fonlar", Description = "Özel Fonlar", Level = 3, ParentId = p54, Order = 549 });


            // ==========================================================
            // 57 – GEÇMİŞ YILLAR KÂRLARI
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "57", Name = "Geçmiş Yıllar Kârları", Description = "Geçmiş Yıllar Kârları", Level = 2, ParentId = p5, Order = 57 });
            var p57 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "570", Name = "Geçmiş Yıllar Kârları", Description = "Geçmiş Yıllar Kârları", Level = 3, ParentId = p57, Order = 570 });


            // ==========================================================
            // 58 – GEÇMİŞ YILLAR ZARARLARI
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "58", Name = "Geçmiş Yıllar Zararları", Description = "Geçmiş Yıllar Zararları", Level = 2, ParentId = p5, Order = 58 });
            var p58 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "580", Name = "Geçmiş Yıl Zararları (-)", Description = "Geçmiş Yıl Zararları (-)", Level = 3, ParentId = p58, Order = 580 });


            // ==========================================================
            // 59 – DÖNEM NET KÂRI / ZARARI
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "59", Name = "Dönem Net Kârı (Zararı)", Description = "Dönem Net Kârı (Zararı)", Level = 2, ParentId = p5, Order = 59 });
            var p59 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "590", Name = "Dönem Net Kârı", Description = "Dönem Net Kârı", Level = 3, ParentId = p59, Order = 590 });
            nodes.Add(new AccountNode { Id = id++, Code = "591", Name = "Dönem Net Zararı (-)", Description = "Dönem Net Zararı (-)", Level = 3, ParentId = p59, Order = 591 });

            // ----------------------------------------------------------
            // 6 - GELİRLER (LEVEL 1)
            // ----------------------------------------------------------
            // ----------------------------------------------------------
            // 6 - GELİR TABLOSU HESAPLARI (LEVEL 1)
            // ----------------------------------------------------------
            nodes.Add(new AccountNode
            {
                Id = id,
                Code = "6",
                Name = "Gelir Tablosu Hesapları",
                Description = "Gelir Tablosu Hesapları",
                Level = 1,
                Order = 6
            });
            var p6 = id++;

            // ==========================================================
            // 60 – BRÜT SATIŞLAR
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "60", Name = "Brüt Satışlar", Description = "Brüt Satışlar", Level = 2, ParentId = p6, Order = 60 });
            var p60 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "600", Name = "Yurtiçi Satışlar", Description = "Yurtiçi Satışlar", Level = 3, ParentId = p60, Order = 600 });
            nodes.Add(new AccountNode { Id = id++, Code = "601", Name = "Yurtdışı Satışlar", Description = "Yurtdışı Satışlar", Level = 3, ParentId = p60, Order = 601 });
            nodes.Add(new AccountNode { Id = id++, Code = "602", Name = "Diğer Gelirler", Description = "Diğer Gelirler", Level = 3, ParentId = p60, Order = 602 });

            // ==========================================================
            // 61 – SATIŞ İNDİRİMLERİ (-)
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "61", Name = "Satış İndirimleri (-)", Description = "Satış İndirimleri (-)", Level = 2, ParentId = p6, Order = 61 });
            var p61 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "610", Name = "Satıştan İadeler (-)", Description = "Satıştan İadeler (-)", Level = 3, ParentId = p61, Order = 610 });
            nodes.Add(new AccountNode { Id = id++, Code = "611", Name = "Satış İskontoları (-)", Description = "Satış İskontoları (-)", Level = 3, ParentId = p61, Order = 611 });
            nodes.Add(new AccountNode { Id = id++, Code = "612", Name = "Diğer İndirimler (-)", Description = "Diğer İndirimler (-)", Level = 3, ParentId = p61, Order = 612 });

            // ==========================================================
            // 62 – SATIŞLARIN MALİYETİ (-)
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "62", Name = "Satışların Maliyeti (-)", Description = "Satışların Maliyeti (-)", Level = 2, ParentId = p6, Order = 62 });
            var p62 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "620", Name = "Satılan Mamuller Maliyeti (-)", Description = "Satılan Mamuller Maliyeti (-)", Level = 3, ParentId = p62, Order = 620 });
            nodes.Add(new AccountNode { Id = id++, Code = "621", Name = "Satılan Ticari Mallar Maliyeti (-)", Description = "Satılan Ticari Mallar Maliyeti (-)", Level = 3, ParentId = p62, Order = 621 });
            nodes.Add(new AccountNode { Id = id++, Code = "622", Name = "Satılan Hizmet Maliyeti (-)", Description = "Satılan Hizmet Maliyeti (-)", Level = 3, ParentId = p62, Order = 622 });
            nodes.Add(new AccountNode { Id = id++, Code = "623", Name = "Diğer Satışların Maliyeti (-)", Description = "Diğer Satışların Maliyeti (-)", Level = 3, ParentId = p62, Order = 623 });

            // ==========================================================
            // 63 – FAALİYET GİDERLERİ (-)
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "63", Name = "Faaliyet Giderleri (-)", Description = "Faaliyet Giderleri (-)", Level = 2, ParentId = p6, Order = 63 });
            var p63 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "630", Name = "Araştırma ve Geliştirme Giderleri (-)", Description = "Araştırma ve Geliştirme Giderleri (-)", Level = 3, ParentId = p63, Order = 630 });
            nodes.Add(new AccountNode { Id = id++, Code = "631", Name = "Pazarlama Satış ve Dağıtım Giderleri (-)", Description = "Pazarlama Satış ve Dağıtım Giderleri (-)", Level = 3, ParentId = p63, Order = 631 });
            nodes.Add(new AccountNode { Id = id++, Code = "632", Name = "Genel Yönetim Giderleri (-)", Description = "Genel Yönetim Giderleri (-)", Level = 3, ParentId = p63, Order = 632 });

            // ==========================================================
            // 64 – DİĞER FAALİYETLERDEN OLAĞAN GELİR VE KÂRLAR
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "64", Name = "Diğer Faaliyetlerden Olağan Gelir ve Kârlar", Description = "Diğer Faaliyetlerden Olağan Gelir ve Kârlar", Level = 2, ParentId = p6, Order = 64 });
            var p64 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "640", Name = "İştiraklerden Temettü Gelirleri", Description = "İştiraklerden Temettü Gelirleri", Level = 3, ParentId = p64, Order = 640 });
            nodes.Add(new AccountNode { Id = id++, Code = "641", Name = "Bağlı Ortaklıklardan Temettü Gelirleri", Description = "Bağlı Ortaklıklardan Temettü Gelirleri", Level = 3, ParentId = p64, Order = 641 });
            nodes.Add(new AccountNode { Id = id++, Code = "642", Name = "Faiz Gelirleri", Description = "Faiz Gelirleri", Level = 3, ParentId = p64, Order = 642 });
            nodes.Add(new AccountNode { Id = id++, Code = "643", Name = "Komisyon Gelirleri", Description = "Komisyon Gelirleri", Level = 3, ParentId = p64, Order = 643 });
            nodes.Add(new AccountNode { Id = id++, Code = "644", Name = "Konusu Kalmayan Karşılıklar", Description = "Konusu Kalmayan Karşılıklar", Level = 3, ParentId = p64, Order = 644 });
            nodes.Add(new AccountNode { Id = id++, Code = "645", Name = "Menkul Kıymet Satış Karları", Description = "Menkul Kıymet Satış Karları", Level = 3, ParentId = p64, Order = 645 });
            nodes.Add(new AccountNode { Id = id++, Code = "646", Name = "Kambiyo Karları", Description = "Kambiyo Karları", Level = 3, ParentId = p64, Order = 646 });
            nodes.Add(new AccountNode { Id = id++, Code = "647", Name = "Reeskont Faiz Gelirleri", Description = "Reeskont Faiz Gelirleri", Level = 3, ParentId = p64, Order = 647 });
            nodes.Add(new AccountNode { Id = id++, Code = "648", Name = "Enflasyon Düzeltmesi Karları", Description = "Enflasyon Düzeltmesi Karları", Level = 3, ParentId = p64, Order = 648 });
            nodes.Add(new AccountNode { Id = id++, Code = "649", Name = "Diğer Olağan Gelir ve Karlar", Description = "Diğer Olağan Gelir ve Karlar", Level = 3, ParentId = p64, Order = 649 });

            // ==========================================================
            // 65 – DİĞER FAALİYETLERDEN OLAĞAN GİDER VE ZARARLAR (-)
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "65", Name = "Diğer Faaliyetlerden Olağan Gider ve Zararlar (-)", Description = "Diğer Faaliyetlerden Olağan Gider ve Zararlar (-)", Level = 2, ParentId = p6, Order = 65 });
            var p65 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "653", Name = "Komisyon Giderleri (-)", Description = "Komisyon Giderleri (-)", Level = 3, ParentId = p65, Order = 653 });
            nodes.Add(new AccountNode { Id = id++, Code = "654", Name = "Karşılık Giderleri (-)", Description = "Karşılık Giderleri (-)", Level = 3, ParentId = p65, Order = 654 });
            nodes.Add(new AccountNode { Id = id++, Code = "655", Name = "Menkul Kıymet Satış Zararları (-)", Description = "Menkul Kıymet Satış Zararları (-)", Level = 3, ParentId = p65, Order = 655 });
            nodes.Add(new AccountNode { Id = id++, Code = "656", Name = "Kambiyo Zararları (-)", Description = "Kambiyo Zararları (-)", Level = 3, ParentId = p65, Order = 656 });
            nodes.Add(new AccountNode { Id = id++, Code = "657", Name = "Reeskont Faiz Giderleri (-)", Description = "Reeskont Faiz Giderleri (-)", Level = 3, ParentId = p65, Order = 657 });
            nodes.Add(new AccountNode { Id = id++, Code = "658", Name = "Enflasyon Düzeltmesi Zararları (-)", Description = "Enflasyon Düzeltmesi Zararları (-)", Level = 3, ParentId = p65, Order = 658 });
            nodes.Add(new AccountNode { Id = id++, Code = "659", Name = "Diğer Olağan Gider ve Zararlar (-)", Description = "Diğer Olağan Gider ve Zararlar (-)", Level = 3, ParentId = p65, Order = 659 });

            // ==========================================================
            // 66 – FİNANSMAN GİDERLERİ (-)
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "66", Name = "Finansman Giderleri (-)", Description = "Finansman Giderleri (-)", Level = 2, ParentId = p6, Order = 66 });
            var p66 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "660", Name = "Kısa Vadeli Borçlanma Giderleri (-)", Description = "Kısa Vadeli Borçlanma Giderleri (-)", Level = 3, ParentId = p66, Order = 660 });
            nodes.Add(new AccountNode { Id = id++, Code = "661", Name = "Uzun Vadeli Borçlanma Giderleri (-)", Description = "Uzun Vadeli Borçlanma Giderleri (-)", Level = 3, ParentId = p66, Order = 661 });

            // ==========================================================
            // 67 – OLAĞANDIŞI GELİR VE KÂRLAR
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "67", Name = "Olağandışı Gelir ve Karlar", Description = "Olağandışı Gelir ve Karlar", Level = 2, ParentId = p6, Order = 67 });
            var p67 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "671", Name = "Önceki Dönem Gelir ve Karları", Description = "Önceki Dönem Gelir ve Karları", Level = 3, ParentId = p67, Order = 671 });
            nodes.Add(new AccountNode { Id = id++, Code = "679", Name = "Diğer Olağandışı Gelir ve Karlar", Description = "Diğer Olağandışı Gelir ve Karlar", Level = 3, ParentId = p67, Order = 679 });

            // ==========================================================
            // 68 – OLAĞANDIŞI GİDER VE ZARARLAR (-)
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "68", Name = "Olağandışı Gider ve Zararlar (-)", Description = "Olağandışı Gider ve Zararlar (-)", Level = 2, ParentId = p6, Order = 68 });
            var p68 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "680", Name = "Çalışmayan Kısım Gider ve Zararları (-)", Description = "Çalışmayan Kısım Gider ve Zararları (-)", Level = 3, ParentId = p68, Order = 680 });
            nodes.Add(new AccountNode { Id = id++, Code = "681", Name = "Önceki Dönem Gider ve Zararları (-)", Description = "Önceki Dönem Gider ve Zararları (-)", Level = 3, ParentId = p68, Order = 681 });
            nodes.Add(new AccountNode { Id = id++, Code = "689", Name = "Diğer Olağandışı Gider ve Zararlar (-)", Description = "Diğer Olağandışı Gider ve Zararlar (-)", Level = 3, ParentId = p68, Order = 689 });

            // ==========================================================
            // 69 – DÖNEM NET KARI (ZARARI)
            // ==========================================================
            nodes.Add(new AccountNode { Id = id, Code = "69", Name = "Dönem Net Karı (Zararı)", Description = "Dönem Net Karı (Zararı)", Level = 2, ParentId = p6, Order = 69 });
            var p69 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "690", Name = "Dönem Karı veya Zararı", Description = "Dönem Karı veya Zararı", Level = 3, ParentId = p69, Order = 690 });
            nodes.Add(new AccountNode { Id = id++, Code = "691", Name = "Dönem Karı Vergi ve Diğer Yasal Yükümlülük Karşılıkları (-)", Description = "Dönem Karı Vergi ve Diğer Yasal Yükümlülük Karşılıkları (-)", Level = 3, ParentId = p69, Order = 691 });
            nodes.Add(new AccountNode { Id = id++, Code = "692", Name = "Dönem Net Karı veya Zararı", Description = "Dönem Net Karı veya Zararı", Level = 3, ParentId = p69, Order = 692 });
            nodes.Add(new AccountNode { Id = id++, Code = "697", Name = "Yıllara Yaygın İnşaat Enflasyon Düzeltme Hesabı", Description = "Yıllara Yaygın İnşaat Enflasyon Düzeltme Hesabı", Level = 3, ParentId = p69, Order = 697 });
            nodes.Add(new AccountNode { Id = id++, Code = "698", Name = "Enflasyon Düzeltme Hesabı", Description = "Enflasyon Düzeltme Hesabı", Level = 3, ParentId = p69, Order = 698 });

            // ----------------------------------------------------------
            // 7 - MALİYET HESAPLARI (7/A ve 7/B SEÇENEĞİ)
            // ----------------------------------------------------------
            nodes.Add(new AccountNode
            {
                Id = id,
                Code = "7",
                Name = "Maliyet Hesapları",
                Description = "Maliyet Hesapları (7/A ve 7/B Seçeneği)",
                Level = 1,
                Order = 7
            });
            var p7 = id++;

            // ==========================================================
            // 70 – MALİYET MUHASEBESİ BAĞLANTI HESAPLARI
            // ==========================================================
            nodes.Add(new AccountNode
            {
                Id = id,
                Code = "70",
                Name = "Maliyet Muhasebesi Bağlantı Hesapları",
                Description = "Maliyet Muhasebesi Bağlantı Hesapları",
                Level = 2,
                ParentId = p7,
                Order = 70
            });
            var p70 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "700", Name = "Maliyet Muhasebesi Bağlantı Hesabı", Description = "Maliyet Muhasebesi Bağlantı Hesabı", Level = 3, ParentId = p70, Order = 700 });
            nodes.Add(new AccountNode { Id = id++, Code = "701", Name = "Maliyet Muhasebesi Yansıtma Hesabı", Description = "Maliyet Muhasebesi Yansıtma Hesabı", Level = 3, ParentId = p70, Order = 701 });

            // ==========================================================
            // 71 – DİREKT İLKMADDE VE MALZEME GİDERLERİ
            // ==========================================================
            nodes.Add(new AccountNode
            {
                Id = id,
                Code = "71",
                Name = "Direkt İlk Madde ve Malzeme Giderleri",
                Description = "Direkt İlk Madde ve Malzeme Giderleri",
                Level = 2,
                ParentId = p7,
                Order = 71
            });
            var p71 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "710", Name = "Direkt İlk Madde ve Malzeme Giderleri", Description = "Direkt İlk Madde ve Malzeme Giderleri", Level = 3, ParentId = p71, Order = 710 });
            nodes.Add(new AccountNode { Id = id++, Code = "711", Name = "Direkt İlk Madde ve Malzeme Yansıtma Hesabı", Description = "Direkt İlk Madde ve Malzeme Yansıtma Hesabı", Level = 3, ParentId = p71, Order = 711 });
            nodes.Add(new AccountNode { Id = id++, Code = "712", Name = "Direkt İlk Madde ve Malzeme Fiyat Farkı", Description = "Direkt İlk Madde ve Malzeme Fiyat Farkı", Level = 3, ParentId = p71, Order = 712 });
            nodes.Add(new AccountNode { Id = id++, Code = "713", Name = "Direkt İlk Madde ve Malzeme Miktar Farkı", Description = "Direkt İlk Madde ve Malzeme Miktar Farkı", Level = 3, ParentId = p71, Order = 713 });

            // ==========================================================
            // 72 – DİREKT İŞÇİLİK GİDERLERİ
            // ==========================================================
            nodes.Add(new AccountNode
            {
                Id = id,
                Code = "72",
                Name = "Direkt İşçilik Giderleri",
                Description = "Direkt İşçilik Giderleri",
                Level = 2,
                ParentId = p7,
                Order = 72
            });
            var p72 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "720", Name = "Direkt İşçilik Giderleri", Description = "Direkt İşçilik Giderleri", Level = 3, ParentId = p72, Order = 720 });
            nodes.Add(new AccountNode { Id = id++, Code = "721", Name = "Direkt İşçilik Giderleri Yansıtma Hesabı", Description = "Direkt İşçilik Giderleri Yansıtma Hesabı", Level = 3, ParentId = p72, Order = 721 });
            nodes.Add(new AccountNode { Id = id++, Code = "722", Name = "Direkt İşçilik Ücret Farkları", Description = "Direkt İşçilik Ücret Farkları", Level = 3, ParentId = p72, Order = 722 });
            nodes.Add(new AccountNode { Id = id++, Code = "723", Name = "Direkt İşçilik Süre (Zaman) Farkları", Description = "Direkt İşçilik Süre (Zaman) Farkları", Level = 3, ParentId = p72, Order = 723 });

            // ==========================================================
            // 73 – GENEL ÜRETİM GİDERLERİ
            // ==========================================================
            nodes.Add(new AccountNode
            {
                Id = id,
                Code = "73",
                Name = "Genel Üretim Giderleri",
                Description = "Genel Üretim Giderleri",
                Level = 2,
                ParentId = p7,
                Order = 73
            });
            var p73 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "730", Name = "Genel Üretim Giderleri", Description = "Genel Üretim Giderleri", Level = 3, ParentId = p73, Order = 730 });
            nodes.Add(new AccountNode { Id = id++, Code = "731", Name = "Genel Üretim Giderleri Yansıtma Hesabı", Description = "Genel Üretim Giderleri Yansıtma Hesabı", Level = 3, ParentId = p73, Order = 731 });
            nodes.Add(new AccountNode { Id = id++, Code = "732", Name = "Genel Üretim Giderleri Bütçe Farkları", Description = "Genel Üretim Giderleri Bütçe Farkları", Level = 3, ParentId = p73, Order = 732 });
            nodes.Add(new AccountNode { Id = id++, Code = "733", Name = "Genel Üretim Giderleri Verimlilik Farkları", Description = "Genel Üretim Giderleri Verimlilik Farkları", Level = 3, ParentId = p73, Order = 733 });
            nodes.Add(new AccountNode { Id = id++, Code = "734", Name = "Genel Üretim Giderleri Kapasite Farkları", Description = "Genel Üretim Giderleri Kapasite Farkları", Level = 3, ParentId = p73, Order = 734 });

            // ==========================================================
            // 74 – HİZMET ÜRETİM MALİYETİ
            // ==========================================================
            nodes.Add(new AccountNode
            {
                Id = id,
                Code = "74",
                Name = "Hizmet Üretim Maliyeti",
                Description = "Hizmet Üretim Maliyeti",
                Level = 2,
                ParentId = p7,
                Order = 74
            });
            var p74 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "740", Name = "Hizmet Üretim Maliyeti", Description = "Hizmet Üretim Maliyeti", Level = 3, ParentId = p74, Order = 740 });
            nodes.Add(new AccountNode { Id = id++, Code = "741", Name = "Hizmet Üretim Maliyeti Yansıtma Hesabı", Description = "Hizmet Üretim Maliyeti Yansıtma Hesabı", Level = 3, ParentId = p74, Order = 741 });
            nodes.Add(new AccountNode { Id = id++, Code = "742", Name = "Hizmet Üretim Maliyeti Fark Hesapları", Description = "Hizmet Üretim Maliyeti Fark Hesapları", Level = 3, ParentId = p74, Order = 742 });

            // ==========================================================
            // 75 – ARAŞTIRMA VE GELİŞTİRME GİDERLERİ
            // ==========================================================
            nodes.Add(new AccountNode
            {
                Id = id,
                Code = "75",
                Name = "Araştırma ve Geliştirme Giderleri",
                Description = "Araştırma ve Geliştirme Giderleri",
                Level = 2,
                ParentId = p7,
                Order = 75
            });
            var p75 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "750", Name = "Araştırma ve Geliştirme Giderleri", Description = "Araştırma ve Geliştirme Giderleri", Level = 3, ParentId = p75, Order = 750 });
            nodes.Add(new AccountNode { Id = id++, Code = "751", Name = "Araştırma ve Geliştirme Giderleri Yansıtma Hesabı", Description = "Araştırma ve Geliştirme Giderleri Yansıtma Hesabı", Level = 3, ParentId = p75, Order = 751 });
            nodes.Add(new AccountNode { Id = id++, Code = "752", Name = "Araştırma ve Geliştirme Gider Farkları", Description = "Araştırma ve Geliştirme Gider Farkları", Level = 3, ParentId = p75, Order = 752 });

            // ==========================================================
            // 76 – PAZARLAMA SATIŞ VE DAĞITIM GİDERLERİ
            // ==========================================================
            nodes.Add(new AccountNode
            {
                Id = id,
                Code = "76",
                Name = "Pazarlama Satış ve Dağıtım Giderleri",
                Description = "Pazarlama Satış ve Dağıtım Giderleri",
                Level = 2,
                ParentId = p7,
                Order = 76
            });
            var p76 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "760", Name = "Pazarlama Satış ve Dağıtım Giderleri", Description = "Pazarlama Satış ve Dağıtım Giderleri", Level = 3, ParentId = p76, Order = 760 });
            nodes.Add(new AccountNode { Id = id++, Code = "761", Name = "Pazarlama Satış ve Dağıtım Giderleri Yansıtma Hesabı", Description = "Pazarlama Satış ve Dağıtım Giderleri Yansıtma Hesabı", Level = 3, ParentId = p76, Order = 761 });
            nodes.Add(new AccountNode { Id = id++, Code = "762", Name = "Pazarlama Satış ve Dağıtım Giderleri Fark Hesabı", Description = "Pazarlama Satış ve Dağıtım Giderleri Fark Hesabı", Level = 3, ParentId = p76, Order = 762 });

            // ==========================================================
            // 77 – GENEL YÖNETİM GİDERLERİ
            // ==========================================================
            nodes.Add(new AccountNode
            {
                Id = id,
                Code = "77",
                Name = "Genel Yönetim Giderleri",
                Description = "Genel Yönetim Giderleri",
                Level = 2,
                ParentId = p7,
                Order = 77
            });
            var p77 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "770", Name = "Genel Yönetim Giderleri", Description = "Genel Yönetim Giderleri", Level = 3, ParentId = p77, Order = 770 });
            nodes.Add(new AccountNode { Id = id++, Code = "771", Name = "Genel Yönetim Giderleri Yansıtma Hesabı", Description = "Genel Yönetim Giderleri Yansıtma Hesabı", Level = 3, ParentId = p77, Order = 771 });
            nodes.Add(new AccountNode { Id = id++, Code = "772", Name = "Genel Yönetim Gider Farkları Hesabı", Description = "Genel Yönetim Gider Farkları Hesabı", Level = 3, ParentId = p77, Order = 772 });

            // ==========================================================
            // 78 – FİNANSMAN GİDERLERİ
            // ==========================================================
            nodes.Add(new AccountNode
            {
                Id = id,
                Code = "78",
                Name = "Finansman Giderleri",
                Description = "Finansman Giderleri",
                Level = 2,
                ParentId = p7,
                Order = 78
            });
            var p78 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "780", Name = "Finansman Giderleri", Description = "Finansman Giderleri", Level = 3, ParentId = p78, Order = 780 });
            nodes.Add(new AccountNode { Id = id++, Code = "781", Name = "Finansman Giderleri Yansıtma Hesabı", Description = "Finansman Giderleri Yansıtma Hesabı", Level = 3, ParentId = p78, Order = 781 });
            nodes.Add(new AccountNode { Id = id++, Code = "782", Name = "Finansman Giderleri Fark Hesabı", Description = "Finansman Giderleri Fark Hesabı", Level = 3, ParentId = p78, Order = 782 });

            // ==========================================================
            // 79 – GİDER ÇEŞİTLERİ (7/B SEÇENEĞİ)
            // ==========================================================
            nodes.Add(new AccountNode
            {
                Id = id,
                Code = "79",
                Name = "Gider Çeşitleri (7/B Seçeneği)",
                Description = "Gider Çeşitleri (7/B Seçeneği)",
                Level = 2,
                ParentId = p7,
                Order = 79
            });
            var p79 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "790", Name = "İlk Madde ve Malzeme Giderleri", Description = "İlk Madde ve Malzeme Giderleri", Level = 3, ParentId = p79, Order = 790 });
            nodes.Add(new AccountNode { Id = id++, Code = "791", Name = "Memur Ücret ve Giderleri", Description = "Memur Ücret ve Giderleri", Level = 3, ParentId = p79, Order = 791 });
            nodes.Add(new AccountNode { Id = id++, Code = "792", Name = "İşçi Ücret ve Giderleri", Description = "İşçi Ücret ve Giderleri", Level = 3, ParentId = p79, Order = 792 });
            nodes.Add(new AccountNode { Id = id++, Code = "793", Name = "Dışarıdan Sağlanan Fayda ve Hizmetler", Description = "Dışarıdan Sağlanan Fayda ve Hizmetler", Level = 3, ParentId = p79, Order = 793 });
            nodes.Add(new AccountNode { Id = id++, Code = "794", Name = "Çeşitli Giderler", Description = "Çeşitli Giderler", Level = 3, ParentId = p79, Order = 794 });
            nodes.Add(new AccountNode { Id = id++, Code = "795", Name = "Vergi, Resim ve Harçlar", Description = "Vergi, Resim ve Harçlar", Level = 3, ParentId = p79, Order = 795 });
            nodes.Add(new AccountNode { Id = id++, Code = "796", Name = "Amortismanlar ve Tükenme Payları", Description = "Amortismanlar ve Tükenme Payları", Level = 3, ParentId = p79, Order = 796 });
            nodes.Add(new AccountNode { Id = id++, Code = "797", Name = "Finansman Giderleri", Description = "Finansman Giderleri", Level = 3, ParentId = p79, Order = 797 });
            nodes.Add(new AccountNode { Id = id++, Code = "798", Name = "Gider Çeşitleri Yansıtma Hesabı", Description = "Gider Çeşitleri Yansıtma Hesabı", Level = 3, ParentId = p79, Order = 798 });
            nodes.Add(new AccountNode { Id = id++, Code = "799", Name = "Üretim Maliyet Hesabı", Description = "Üretim Maliyet Hesabı", Level = 3, ParentId = p79, Order = 799 });


            // ----------------------------------------------------------
            // 8 - NAZIM HESAPLAR (LEVEL 1)
            // ----------------------------------------------------------
            nodes.Add(new AccountNode
            {
                Id = id,
                Code = "8",
                Name = "Nazım Hesaplar",
                Description = "Nazım Hesaplar",
                Level = 1,
                Order = 8
            });
            var p8 = id++;

            // ==========================================================
            // 80 – GELECEK AYLARA AİT GİDERLER / GELİRLER 
            // (Nazım hesap olarak takip edilenler)
            // ==========================================================
            nodes.Add(new AccountNode
            {
                Id = id,
                Code = "80",
                Name = "Gelecek Aylara Ait İşlemler",
                Description = "Gelecek Aylara Ait İşlemler",
                Level = 2,
                ParentId = p8,
                Order = 80
            });
            var p80 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "800", Name = "Gelecek Aylara Ait Giderler", Description = "Gelecek Aylara Ait Giderler", Level = 3, ParentId = p80, Order = 800 });
            nodes.Add(new AccountNode { Id = id++, Code = "801", Name = "Gelecek Aylara Ait Gelirler", Description = "Gelecek Aylara Ait Gelirler", Level = 3, ParentId = p80, Order = 801 });

            // ==========================================================
            // 81 – YANSITMA HESAPLARI (Nazım Amaçlı Kullanılanlar)
            // ==========================================================
            nodes.Add(new AccountNode
            {
                Id = id,
                Code = "81",
                Name = "Yansıtma Hesapları",
                Description = "Yansıtma Hesapları",
                Level = 2,
                ParentId = p8,
                Order = 81
            });
            var p81 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "810", Name = "Üretim Giderleri Yansıtma Hesabı", Description = "Üretim Giderleri Yansıtma Hesabı", Level = 3, ParentId = p81, Order = 810 });
            nodes.Add(new AccountNode { Id = id++, Code = "811", Name = "Genel Yönetim Giderleri Yansıtma Hesabı", Description = "Genel Yönetim Giderleri Yansıtma Hesabı", Level = 3, ParentId = p81, Order = 811 });
            nodes.Add(new AccountNode { Id = id++, Code = "812", Name = "Pazarlama Satış Dağıtım Giderleri Yansıtma Hesabı", Description = "Pazarlama Satış Dağıtım Giderleri Yansıtma Hesabı", Level = 3, ParentId = p81, Order = 812 });

            // ==========================================================
            // 89 – DİĞER NAZIM HESAPLAR
            // ==========================================================
            nodes.Add(new AccountNode
            {
                Id = id,
                Code = "89",
                Name = "Diğer Nazım Hesaplar",
                Description = "Diğer Nazım Hesaplar",
                Level = 2,
                ParentId = p8,
                Order = 89
            });
            var p89 = id++;

            nodes.Add(new AccountNode { Id = id++, Code = "890", Name = "Teminat Mektupları", Description = "Teminat Mektupları", Level = 3, ParentId = p89, Order = 890 });
            nodes.Add(new AccountNode { Id = id++, Code = "891", Name = "Verilen Garanti ve Kefaletler", Description = "Verilen Garanti ve Kefaletler", Level = 3, ParentId = p89, Order = 891 });
            nodes.Add(new AccountNode { Id = id++, Code = "892", Name = "Alınan Garanti ve Kefaletler", Description = "Alınan Garanti ve Kefaletler", Level = 3, ParentId = p89, Order = 892 });
            nodes.Add(new AccountNode { Id = id++, Code = "893", Name = "Emanet ve Vekalet Hesapları", Description = "Emanet ve Vekalet Hesapları", Level = 3, ParentId = p89, Order = 893 });
            nodes.Add(new AccountNode { Id = id++, Code = "899", Name = "Diğer Nazım Hesaplar", Description = "Diğer Nazım Hesaplar", Level = 3, ParentId = p89, Order = 899 });



            // ----------------------------
            // 9 - YÖNETİMSEL EK HESAPLAR
            // ----------------------------
            nodes.Add(new AccountNode
            {
                Id = id,
                Code = "9",
                Name = "Yönetimsel Hesaplar",
                Description = "Yönetimsel Hesaplar",
                Level = 1,
                Order = 900
            });
            var p9 = id++;

            // 90 Maliyet Muhasebesi
            nodes.Add(new AccountNode
            {
                Id = id,
                Code = "90",
                Name = "Maliyet Muhasebesi Hesapları",
                Description = "Maliyet Muhasebesi Hesapları",
                Level = 2,
                ParentId = p9,
                Order = 900
            });
            var p90 = id++;

            // 91 Bütçe
            nodes.Add(new AccountNode
            {
                Id = id,
                Code = "91",
                Name = "Bütçe Hesapları",
                Description = "Bütçe Hesapları",
                Level = 2,
                ParentId = p9,
                Order = 910
            });
            var p91 = id++;

            // 92 Yönetim Muhasebesi
            nodes.Add(new AccountNode
            {
                Id = id,
                Code = "92",
                Name = "Yönetim Muhasebesi Hesapları",
                Description = "Yönetim Muhasebesi Hesapları",
                Level = 2,
                ParentId = p9,
                Order = 920
            });
            var p92 = id++;

            // 93 Operasyon Hesapları
            nodes.Add(new AccountNode
            {
                Id = id,
                Code = "93",
                Name = "Operasyon Takip Hesapları",
                Description = "Operasyon Takip Hesapları",
                Level = 2,
                ParentId = p9,
                Order = 930
            });
            var p93 = id++;

            // 98 Evrak / İş Takip
            nodes.Add(new AccountNode
            {
                Id = id,
                Code = "98",
                Name = "Evrak Takip Hesapları",
                Description = "Evrak Takip Hesapları",
                Level = 2,
                ParentId = p9,
                Order = 980
            });
            var p98 = id++;

            // 99 Kapanış
            nodes.Add(new AccountNode
            {
                Id = id,
                Code = "99",
                Name = "Kapanış ve Envanter Hesapları",
                Description = "Kapanış ve Envanter Hesapları",
                Level = 2,
                ParentId = p9,
                Order = 990
            });
            var p99 = id++;

            b.Entity<AccountNode>().HasData(nodes);
        }
    }
}
