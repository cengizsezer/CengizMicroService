using CatalogService.Api.Features.Payroll.Commands.CalculatePayroll;
using CatalogService.Api.Features.Payroll.Configuration;
using CatalogService.Api.Features.Payroll.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CatalogService.Api.Features.Payroll.Services
{
    /// <summary>Teşvik girdisi eksik/geçersiz — controller'larda 400 + okunur Türkçe mesaj olur.</summary>
    public sealed class PayrollGirdiHatasi : Exception
    {
        public PayrollGirdiHatasi(string mesaj) : base(mesaj) { }
    }

    /// <summary><see cref="PayrollGirdiHatasi"/>'nı 400 düz metne çevirir (iki bordro controller'ı).</summary>
    public sealed class PayrollGirdiHatasiFiltresiAttribute : ExceptionFilterAttribute
    {
        public override void OnException(ExceptionContext context)
        {
            if (context.Exception is not PayrollGirdiHatasi hata) return;
            context.Result = new ContentResult
            {
                StatusCode = StatusCodes.Status400BadRequest,
                Content = hata.Message,
                ContentType = "text/plain; charset=utf-8"
            };
            context.ExceptionHandled = true;
        }
    }

    /// <summary>
    /// Teşvik seçimine bağlı zorunlu girdiler. Handler çağırır — <c>CalculatePayrollCommandValidator</c>
    /// MediatR hattına bağlı olmadığı için (hiç çalışmıyor) bu kurallar oraya konmadı.
    /// İstemci aynı kontrolleri hesap öncesi kendisi de yapar; burası son kapı.
    /// </summary>
    public static class PayrollTesvikDogrulama
    {
        public static List<string> Hatalar(CalculatePayrollCommand c)
        {
            var hatalar = YolYemekHatalari(c);
            var kural = PayrollTesvikKurallari.Bul(c.LawCode);

            if (kural is null || !kural.YilIcinGecerli(c.Year))
            {
                hatalar.Add($"'{c.LawCode}' teşviki {c.Year} yılı için tanımlı değil.");
                return hatalar;
            }

            // Huzur hakkında teşvik uygulanmaz; ek girdi istenmez.
            if (c.EmployeeType == PayrollEmployeeType.Honorarium || c.Year < PayrollTesvikKurallari.YeniKurallarYili)
                return hatalar;

            if (kural.Varyantlar.Count > 0 && kural.Varyantlar.All(v => v.Deger != c.TesvikAltSecimi))
                hatalar.Add($"{kural.Ad} için {kural.AltSecimBasligi?.ToLowerInvariant() ?? "alt seçim"} seçilmeli.");

            if (kural.EngelZorunlu && c.DisabilityType == PayrollDisabilityType.None)
                hatalar.Add($"{kural.Ad} için Engel Durumu en az 1. derece olmalı.");

            switch (kural.Hesap)
            {
                case TesvikHesapTuru.GunlukUcretDestegi:
                    if (!c.TurizmKonaklamaOnayi)
                        hatalar.Add($"{kural.Ad} için \"{kural.OnayMetni}\" onayı gerekli.");
                    if (c.TurizmPrimGunu is < 1 or > 30)
                        hatalar.Add("Prim günü 1 ile 30 arasında olmalı.");
                    break;
                case TesvikHesapTuru.AylikTutarDestegi:
                    if (c.IsgDestegiTutari <= 0)
                        hatalar.Add($"{kural.Ad} için aylık tutar girilmeli.");
                    break;
                case TesvikHesapTuru.Manuel:
                    if (c.ManuelTesvikTuru is null || !Enum.IsDefined(c.ManuelTesvikTuru.Value))
                        hatalar.Add("Manuel teşvik için tür (oran / tutar) seçilmeli.");
                    if (c.ManuelTesvikDegeri <= 0)
                        hatalar.Add("Manuel teşvik için değer girilmeli.");
                    else if (c.ManuelTesvikTuru == PayrollManuelTesvikTuru.Oran && c.ManuelTesvikDegeri > 100)
                        hatalar.Add("Manuel teşvik oranı %100'ü aşamaz.");
                    break;
            }

            return hatalar;
        }

        /// <summary>Yol / yemek girdisi (null: kapalı, kontrol yok). Ekranda da aynı kurallar var.</summary>
        private static List<string> YolYemekHatalari(CalculatePayrollCommand c)
        {
            var hatalar = new List<string>();
            if (c.YolYemek is not { } y) return hatalar;

            if (c.EmployeeType == PayrollEmployeeType.Honorarium)
                hatalar.Add("Huzur hakkında yol / yemek uygulanmaz.");
            else if (c.Year < PayrollYanHakIstisnalari.IlkYil)
                hatalar.Add($"Yol / yemek yalnız {PayrollYanHakIstisnalari.IlkYil} ve sonrası için hesaplanır.");
            if (y.GunSayisi is < 1 or > 31)
                hatalar.Add("Yol / yemek için aylık gün sayısı 1 ile 31 arasında olmalı.");
            if (y.YemekGunlukTutar < 0 || y.YolGunlukTutar < 0)
                hatalar.Add("Yol / yemek günlük tutarı negatif olamaz.");
            if (!Enum.IsDefined(y.YemekOdemeSekli) || !Enum.IsDefined(y.YolOdemeSekli))
                hatalar.Add("Yol / yemek için ödeme şekli seçilmeli.");
            return hatalar;
        }
    }
}
