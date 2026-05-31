using CatalogService.Api.Features.Payroll.Entities;

namespace CatalogService.Api.Features.Payroll.Configuration
{
    /// <summary>
    /// SGK teşvik kanunları (PayrollLawType) için kod-içi konfigürasyon kaynağı.
    /// Liste DB'den değil bu sınıftan beslenir. AvailableFromYear / AvailableToYear
    /// null ise kanun her yıl listede görünür; aralık verilirse handler yıl filtresi uygular.
    /// </summary>
    public static class PayrollLawTypeConfigStore
    {
        public sealed record PayrollLawTypeDefinition(
            string Code,
            string Name,
            int DisplayOrder,
            int? AvailableFromYear = null,
            int? AvailableToYear = null,
            bool IsActive = true);

        /// <summary>
        /// Mevcut 19 kanunun aynısı; sıralama önceki DB seed'i ile birebir.
        /// Geçerlilik aralığı şimdilik açık (null/null) → 2024, 2025, 2026 hepsinde listede görünür.
        /// Gerçek mevzuat farklılıkları sonraki turda tek tek yıllandırılacak.
        /// </summary>
        public static readonly IReadOnlyList<PayrollLawTypeDefinition> All = new List<PayrollLawTypeDefinition>
        {
            new("00000", "Standart Çalışan",   DisplayOrder: 1),
            new("02828", "Sayılı Kanun",       DisplayOrder: 2),
            new("04691", "Teknopark Personeli", DisplayOrder: 3),
            new("05510", "Sayılı Kanun",       DisplayOrder: 4),
            new("05746", "Ar-Ge Personeli",    DisplayOrder: 5),
            new("06111", "Sayılı Kanun",       DisplayOrder: 6),
            new("06486", "Sayılı Kanun",       DisplayOrder: 7),
            new("06645", "Sayılı Kanun",       DisplayOrder: 8),
            new("14857", "Sayılı Kanun",       DisplayOrder: 9),
            new("15510", "Sayılı Kanun",       DisplayOrder: 10),
            new("15746", "Sayılı Kanun",       DisplayOrder: 11),
            new("16322", "Sayılı Kanun",       DisplayOrder: 12),
            new("25225", "Sayılı Kanun",       DisplayOrder: 13),
            new("25510", "Sayılı Kanun",       DisplayOrder: 14),
            new("26322", "Sayılı Kanun",       DisplayOrder: 15),
            new("46486", "Sayılı Kanun",       DisplayOrder: 16),
            new("55225", "Sayılı Kanun",       DisplayOrder: 17),
            new("56486", "Sayılı Kanun",       DisplayOrder: 18),
            new("66486", "Sayılı Kanun",       DisplayOrder: 19),
        };

        /// <summary>
        /// Verilen yıl için geçerli olan aktif kanunları DisplayOrder ile döner.
        /// Entity tipi (PayrollLawType) reused — handler'ın downstream şekli değişmesin.
        /// </summary>
        public static IReadOnlyList<PayrollLawType> GetForYear(int year)
        {
            return All
                .Where(x => x.IsActive)
                .Where(x => x.AvailableFromYear is null || year >= x.AvailableFromYear.Value)
                .Where(x => x.AvailableToYear is null || year <= x.AvailableToYear.Value)
                .OrderBy(x => x.DisplayOrder)
                .Select(x => new PayrollLawType
                {
                    Year = year,
                    Code = x.Code,
                    Name = x.Name,
                    IsActive = x.IsActive,
                    DisplayOrder = x.DisplayOrder
                })
                .ToList();
        }
    }
}
