using CatalogService.Api.Features.Payroll.Configuration;
using CatalogService.Api.Features.Payroll.Dtos.Shared;
using MediatR;

namespace CatalogService.Api.Features.Payroll.Queries.GetPayrollLawTypes
{
    public class GetPayrollLawTypesQueryHandler : IRequestHandler<GetPayrollLawTypesQuery, List<PayrollLawTypeDto>>
    {
        public Task<List<PayrollLawTypeDto>> Handle(GetPayrollLawTypesQuery request, CancellationToken cancellationToken)
        {
            // 2026 öncesi yıllar dondurulmuş: liste aynı tablodan, ama ek girdi sorulmaz.
            var yeni = request.Year >= PayrollTesvikKurallari.YeniKurallarYili;

            var lawTypes = PayrollTesvikKurallari.YilIcin(request.Year)
                .Select(k => new PayrollLawTypeDto
                {
                    Code = k.Anahtar,
                    Label = k.Etiket,
                    Name = k.Ad,
                    SgkKodu = k.SgkKodu,
                    AltSecimBasligi = yeni ? k.AltSecimBasligi : null,
                    AltSecimler = yeni
                        ? k.Varyantlar.Select(v => new PayrollLawTypeAltSecimDto { Deger = v.Deger, Ad = v.Ad }).ToList()
                        : new(),
                    EgitimDurumuSorulur = yeni && k.Gv == GvTesvikTuru.EgitimeGore,
                    EngelZorunlu = yeni && k.EngelZorunlu,
                    EmekliyeUygulanmaz = yeni && k.EmekliyeUygulanmaz,
                    AyriSatirDestek = k.AyriSatirDestek,
                    OnayMetni = k.Hesap == TesvikHesapTuru.GunlukUcretDestegi ? k.OnayMetni : null,
                    AylikTutarSorulur = k.Hesap == TesvikHesapTuru.AylikTutarDestegi,
                    ManuelSorulur = k.Hesap == TesvikHesapTuru.Manuel,
                })
                .ToList();

            return Task.FromResult(lawTypes);
        }
    }
}
