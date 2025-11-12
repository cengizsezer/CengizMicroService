using CatalogService.Api.Features.AccountPlan;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Infrastructure.Context
{
    public static class AccountPlanSeed
    {
        public static void Seed(ModelBuilder b)
        {
            var id = 1;
            var nodes = new List<AccountNode>();

            // 1 Dönen Varlıklar
            nodes.Add(new AccountNode { Id = id++, Code = "1", Name = "Dönen Varlıklar", Level = 1, Order = 1 });

            // 10 Hazır Değerler
            nodes.Add(new AccountNode { Id = id, Code = "10", Name = "Hazır Değerler", Level = 2, ParentId = 1, Order = 10 }); var p10 = id++;
            nodes.Add(new AccountNode { Id = id++, Code = "100", Name = "Kasa", Level = 3, ParentId = p10, Order = 100, Description = "İşletmenin kasa mevcudu." });
            nodes.Add(new AccountNode { Id = id++, Code = "101", Name = "Alınan Çekler", Level = 3, ParentId = p10, Order = 101 });
            nodes.Add(new AccountNode { Id = id++, Code = "102", Name = "Bankalar", Level = 3, ParentId = p10, Order = 102 });
            nodes.Add(new AccountNode { Id = id++, Code = "103", Name = "Verilen Çekler ve Ödeme Emirleri (-)", Level = 3, ParentId = p10, Order = 103 });
            nodes.Add(new AccountNode { Id = id++, Code = "108", Name = "Diğer Hazır Değerler", Level = 3, ParentId = p10, Order = 108 });

            // 11 Menkul Kıymetler
            nodes.Add(new AccountNode { Id = id, Code = "11", Name = "Menkul Kıymetler", Level = 2, ParentId = 1, Order = 11 }); var p11 = id++;
            nodes.Add(new AccountNode { Id = id++, Code = "110", Name = "Hisse Senetleri", Level = 3, ParentId = p11, Order = 110 });
            nodes.Add(new AccountNode { Id = id++, Code = "111", Name = "Özel Kesim Tahvil, Senet ve Bonoları", Level = 3, ParentId = p11, Order = 111 });
            nodes.Add(new AccountNode { Id = id++, Code = "112", Name = "Kamu Kesimi Tahvil, Senet ve Bonoları", Level = 3, ParentId = p11, Order = 112 });
            nodes.Add(new AccountNode { Id = id++, Code = "118", Name = "Diğer Menkul Kıymetler", Level = 3, ParentId = p11, Order = 118 });
            nodes.Add(new AccountNode { Id = id++, Code = "119", Name = "Menkul Kıymetler Değer Düşüklüğü Karşılığı (-)", Level = 3, ParentId = p11, Order = 119 });

            // 12 Ticari Alacaklar (örnek)
            nodes.Add(new AccountNode { Id = id, Code = "12", Name = "Ticari Alacaklar", Level = 2, ParentId = 1, Order = 12 }); var p12 = id++;
            nodes.Add(new AccountNode { Id = id++, Code = "120", Name = "Alıcılar", Level = 3, ParentId = p12, Order = 120 });
            nodes.Add(new AccountNode { Id = id++, Code = "121", Name = "Alacak Senetleri", Level = 3, ParentId = p12, Order = 121 });
            nodes.Add(new AccountNode { Id = id++, Code = "127", Name = "Diğer Ticari Alacaklar", Level = 3, ParentId = p12, Order = 127 });
            nodes.Add(new AccountNode { Id = id++, Code = "128", Name = "Şüpheli Ticari Alacaklar", Level = 3, ParentId = p12, Order = 128 });
            nodes.Add(new AccountNode { Id = id++, Code = "129", Name = "Şüpheli Ticari Alacaklar Karşılığı (-)", Level = 3, ParentId = p12, Order = 129 });

            // Devamı: 13 Diğer Alacaklar, 15 Stoklar, 17/18/19 ... (aynı kalıp)
            b.Entity<AccountNode>().HasData(nodes);
        }
    }
}
