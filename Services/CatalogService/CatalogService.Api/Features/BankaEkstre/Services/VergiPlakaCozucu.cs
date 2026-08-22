using System.Text.RegularExpressions;
using CatalogService.Api.Features.BankaEkstre.Domain;

namespace CatalogService.Api.Features.BankaEkstre.Services
{
    /// <summary>
    /// Vergi tahsilatı ve plaka anahtarı (madde 7). İkisi de "tek başına karar vermeyen,
    /// adayları daraltan" katmanlardır; sonuç tek adaya inmezse satır onaya düşer.
    /// </summary>
    public static class VergiPlakaCozucu
    {
        private static readonly TimeSpan ZamanAsimi = TimeSpan.FromMilliseconds(250);

        /// <summary>
        /// Vergi tahsilatı satırının imzası. İşlem tipi "Vergi Tahsilatı" / "Kambiyo
        /// Muameleleri Vergisi Tahsilatı" gibi yazımların hepsini kapsasın diye
        /// sadeleştirilmiş metinde aranır.
        /// </summary>
        private const string VergiImzasi = "VERGI";

        /// <summary>
        /// Metnin başındaki dört haneli vergi kodu: "0040/S.DAMGA V. Tahsilatı",
        /// "0033/0033/KUR.GEÇ.V Tahsilatı". Kod eğik çizgiden önce durur.
        /// </summary>
        private static readonly Regex VergiKoduDeseni = new(
            @"(?<![0-9])(\d{4})\s*/", RegexOptions.Compiled | RegexOptions.CultureInvariant, ZamanAsimi);

        /// <summary>
        /// Plaka: hesap planında boşluklu ("34 Mrp 081"), banka metninde bitişik
        /// ("Plaka:34MRP081", "34MRP471 Nolu plakanın") yazılıyor.
        /// </summary>
        private static readonly Regex PlakaDeseni = new(
            @"\b(\d{2}\s?[A-ZÇĞİÖŞÜa-zçğıöşü]{1,3}\s?\d{2,5})\b",
            RegexOptions.Compiled | RegexOptions.CultureInvariant, ZamanAsimi);

        /// <summary>
        /// Satır bir vergi tahsilatı mı? Öyleyse <b>unvan çıkarılmaz</b>: açıklamadaki
        /// "Soyadi/Unvani :PKF ADAY …" alanı hesap sahibinin kendi unvanı, karşı taraf değil.
        /// </summary>
        public static bool VergiSatiriMi(string? islemTipi)
            => Normalizasyon.MetinNormalize(islemTipi) is { Length: > 0 } sade &&
               Normalizasyon.IfadeVarMi(sade, VergiImzasi) &&
               Normalizasyon.IfadeVarMi(sade, "TAHSILATI");

        /// <summary>Metindeki vergi kodları (aynı satırda birden fazla geçebiliyor).</summary>
        public static List<string> VergiKodlari(string? metin)
        {
            var kodlar = new List<string>();
            if (string.IsNullOrWhiteSpace(metin)) return kodlar;

            try
            {
                foreach (Match m in VergiKoduDeseni.Matches(metin))
                {
                    var kod = m.Groups[1].Value;
                    if (!kodlar.Contains(kod, StringComparer.Ordinal)) kodlar.Add(kod);
                }
            }
            catch (RegexMatchTimeoutException)
            {
                // Patolojik metin tüm ekstreyi durdurmasın.
            }

            return kodlar;
        }

        /// <summary>Metindeki plakaların karşılaştırma anahtarları ("34MRP081").</summary>
        public static List<string> PlakaAnahtarlari(string? metin)
        {
            var anahtarlar = new List<string>();
            if (string.IsNullOrWhiteSpace(metin)) return anahtarlar;

            try
            {
                foreach (Match m in PlakaDeseni.Matches(metin))
                {
                    var anahtar = Normalizasyon.PlakaAnahtar(m.Groups[1].Value);

                    // "34MRP081" gibi bir plakada en az bir harf olmalı; salt rakam dizileri
                    // (sorgu numarası, referans) plaka değildir.
                    if (anahtar.Length < 5 || !anahtar.Any(char.IsLetter)) continue;
                    if (!anahtarlar.Contains(anahtar, StringComparer.Ordinal)) anahtarlar.Add(anahtar);
                }
            }
            catch (RegexMatchTimeoutException)
            {
            }

            return anahtarlar;
        }

        /// <summary>
        /// Vergi eşleme tablosundan uyan hesap kodları. Kod veya anahtar kelimeden
        /// <b>herhangi biri</b> tuttuğunda satır aday sayılır.
        /// </summary>
        public static List<VergiKoduEslemesi> VergiAdaylari(
            string? hamAciklama, IReadOnlyList<VergiKoduEslemesi> tablo)
        {
            var sonuc = new List<VergiKoduEslemesi>();
            if (tablo.Count == 0) return sonuc;

            var kodlar = VergiKodlari(hamAciklama);
            var metin = Normalizasyon.MetinNormalize(hamAciklama);

            foreach (var satir in tablo.Where(t => t.Aktif).OrderBy(t => t.Sira))
            {
                var kodTuttu = !string.IsNullOrWhiteSpace(satir.VergiKodu) &&
                               kodlar.Contains(satir.VergiKodu.Trim(), StringComparer.Ordinal);

                var kelimeTuttu = !string.IsNullOrWhiteSpace(satir.AnahtarKelime) &&
                                  Normalizasyon.IfadeVarMi(metin, Normalizasyon.MetinNormalize(satir.AnahtarKelime));

                if (kodTuttu || kelimeTuttu) sonuc.Add(satir);
            }

            return sonuc;
        }

        /// <summary>
        /// Metindeki plakayı adında taşıyan hesap planı kayıtları.
        ///
        /// Aynı plakanın birden fazla hesabı olabiliyor ("34 Mrp 081 Araç Kira Bedeli" /
        /// "34 Mrp 081 Araç Otopark Yakıt Vb."), o yüzden plaka <b>tek başına karar vermez</b>;
        /// adayları daraltır ve satır onaya düşer.
        /// </summary>
        public static List<HesapPlaniKaydi> PlakaAdaylari(
            string? hamAciklama, IReadOnlyList<HesapPlaniKaydi> plan)
        {
            var sonuc = new List<HesapPlaniKaydi>();

            var anahtarlar = PlakaAnahtarlari(hamAciklama);
            if (anahtarlar.Count == 0) return sonuc;

            foreach (var hesap in plan)
            {
                if (!hesap.Aktif) continue;

                var adAnahtar = Normalizasyon.PlakaAnahtar(hesap.Ad);
                if (adAnahtar.Length == 0) continue;

                if (anahtarlar.Any(a => adAnahtar.Contains(a, StringComparison.Ordinal)))
                    sonuc.Add(hesap);
            }

            return sonuc;
        }
    }
}
