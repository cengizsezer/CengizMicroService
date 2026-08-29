namespace CatalogService.Api.Features.Ajanlar.Services
{
    /// <summary>
    /// Ajan sürümünün sunucunun beklediği asgari sürümü karşılayıp karşılamadığı.
    ///
    /// Saf fonksiyon: metinden <see cref="Version"/> üretip karşılaştırır. Metinsel
    /// karşılaştırma yapılmadı — "1.10.0" metin olarak "1.9.0"dan küçüktür ve tam da
    /// sürüm ondanla geçtiğinde, yani en kritik anda, yanlış yanıt verirdi.
    /// </summary>
    public static class SurumKontrolu
    {
        public static bool Uygun(string? ajanSurumu, string? asgariSurum, out string mesaj)
        {
            if (!Version.TryParse((ajanSurumu ?? string.Empty).Trim(), out var ajan))
            {
                mesaj = $"Ajan sürümü okunamadı ('{ajanSurumu}'). Beklenen biçim: 1.0.0";
                return false;
            }

            // Asgari sürüm yapılandırmadan geliyor; bozuk yazılmışsa kimseyi dışarıda
            // bırakmaktansa kontrolü atlıyoruz — yanlış bir yapılandırma satırı bütün
            // ofisi bağlantısız bırakmasın.
            if (!Version.TryParse((asgariSurum ?? string.Empty).Trim(), out var asgari))
            {
                mesaj = string.Empty;
                return true;
            }

            if (ajan < asgari)
            {
                mesaj = $"Ajan sürümü {ajan} desteklenmiyor; en az {asgari} gerekiyor. " +
                        "Lütfen PkfRobot'u güncelleyin.";
                return false;
            }

            mesaj = string.Empty;
            return true;
        }
    }
}
