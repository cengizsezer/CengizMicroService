using CatalogService.Api.Features.Muhasebe.Domain;

namespace CatalogService.Api.Features.Muhasebe.Services
{
    /// <summary>
    /// Çözümlenmiş kod maskesi. Segment uzunlukları ve ayraç firma (tenant) bazlıdır;
    /// kod üretme ve doğrulama mantığının tamamı bu sınıftan okur, hiçbir yerde
    /// "3-2-2-4" sabit yazılmaz.
    /// <para>
    /// İlk segment (varsayılan 3) ağacın üst seviyelerini tanımlar: sınıf → grup → kebir.
    /// Bu seviyelerin her biri 1 hanedir ve ayraç kullanmaz ("1", "10", "102").
    /// Sonraki segmentler muavin seviyeleridir ve her biri ayraçla eklenir
    /// ("102.01", "102.01.01", "102.01.01.0046").
    /// </para>
    /// </summary>
    public sealed class MaskeBilgisi
    {
        public const string VarsayilanSegmentUzunluk = "3,2,2,4";
        public const string VarsayilanAyrac = ".";

        private readonly int[] _segmentler;

        private MaskeBilgisi(int[] segmentler, string ayrac)
        {
            _segmentler = segmentler;
            Ayrac = ayrac;
        }

        /// <summary>Segmentleri ayıran karakter, ör. ".".</summary>
        public string Ayrac { get; }

        /// <summary>
        /// Kebirin bulunduğu seviye = ilk segmentin hane sayısı (varsayılan 3).
        /// Bu seviyeye kadar olan düğümler 1 hanelidir.
        /// </summary>
        public byte KebirSeviyesi => (byte)_segmentler[0];

        /// <summary>Maskenin izin verdiği en derin seviye.</summary>
        public byte MaksimumSeviye => (byte)(KebirSeviyesi + _segmentler.Length - 1);

        /// <summary>Firma maskesini çözümler; kayıt yoksa varsayılan maskeyi kullanır.</summary>
        public static MaskeBilgisi Coz(KodMaskesi? maske)
        {
            var ham = string.IsNullOrWhiteSpace(maske?.SegmentUzunluk)
                ? VarsayilanSegmentUzunluk
                : maske!.SegmentUzunluk;

            var ayrac = string.IsNullOrEmpty(maske?.Ayrac) ? VarsayilanAyrac : maske!.Ayrac;

            var segmentler = ham
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(p => int.TryParse(p, out var n) ? n : 0)
                .ToArray();

            if (segmentler.Length == 0 || segmentler.Any(n => n < 1))
                throw new MuhasebeKuralException("kodMaskesi",
                    "Firmanın hesap kodu maskesi geçersiz. Maskeyi \"3,2,2,4\" biçiminde düzeltin.");

            return new MaskeBilgisi(segmentler, ayrac);
        }

        /// <summary>Verilen seviyedeki düğümün kendi segmentinin hane sayısı.</summary>
        public int SegmentUzunlugu(byte seviye)
        {
            if (seviye < 1 || seviye > MaksimumSeviye)
                throw new MuhasebeKuralException("segment",
                    $"Hesap planı en fazla {MaksimumSeviye} seviye derinliğinde olabilir.");

            return seviye <= KebirSeviyesi ? 1 : _segmentler[seviye - KebirSeviyesi];
        }

        /// <summary>Verilen seviyedeki düğümün kodu, üst hesabın koduna ayraçla mı eklenir?</summary>
        public bool AyracKullanir(byte seviye) => seviye > KebirSeviyesi;

        /// <summary>Seviyeden hesap türü: 1 sınıf, aradakiler grup, kebir seviyesi kebir, altı muavin.</summary>
        public HesapTuru TuruBul(byte seviye)
        {
            if (seviye > KebirSeviyesi) return HesapTuru.Muavin;
            if (seviye == KebirSeviyesi) return HesapTuru.Kebir;
            return seviye == 1 ? HesapTuru.Sinif : HesapTuru.Grup;
        }

        /// <summary>
        /// Kök sınıf rakamından bakiye karakteri: 1–2 Aktif, 3–5 Pasif,
        /// 6 Gelir, 7 Gider, 8 Maliyet, 9 Nazım. Yalnızca kök hesap açılırken kullanılır;
        /// alt hesaplar karakteri üst hesaptan miras alır (iş kuralı 5).
        /// </summary>
        public static HesapKarakter KokKarakter(char kokRakam) => kokRakam switch
        {
            '1' or '2' => HesapKarakter.Aktif,
            '3' or '4' or '5' => HesapKarakter.Pasif,
            '6' => HesapKarakter.Gelir,
            '7' => HesapKarakter.Gider,
            '8' => HesapKarakter.Maliyet,
            '9' => HesapKarakter.Nazim,
            _ => HesapKarakter.Aktif
        };
    }
}
