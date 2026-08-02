using CatalogService.Api.Features.FirmaKontrol.Domain;

namespace CatalogService.UnitTests.FirmaKontrol
{
    /// <summary>
    /// Motor testleri için küçük kalem katalogu. Gerçek seed'in kısaltılmış hâli:
    /// her gruptan, testlerin ihtiyacı olan davranışı temsil eden kalemler.
    /// </summary>
    public static class VergiTestKatalogu
    {
        // Sabit Id'ler: testlerde tutar bağlarken kullanılır.
        public const int KkegCeza = 1;          // matrahı artıran KKEG
        public const int KkegTeknopark = 2;     // istisnaya ilişkin KKEG -> IstisnaTeknopark
        public const int KkegBagsiz = 3;        // istisnaya ilişkin ama bağlı istisnası yok
        public const int IstisnaIstirak = 10;   // Grup 2, asgari matrahtan düşer
        public const int IstisnaTeknopark = 11; // Grup 2, asgari matrahtan düşer
        public const int IstisnaDiger = 12;     // Grup 2, asgari matrahtan düşmez
        public const int IndirimBagis = 20;     // Grup 3, %5 üst sınır, devretmez
        public const int IndirimArge = 21;      // Grup 3, sınırsız, devreder
        public const int MahsupGecici = 30;     // Grup 4

        public static List<VergiKalemi> Olustur() => new()
        {
            new VergiKalemi
            {
                Id = KkegCeza, Kod = "KKEG-03", Ad = "Vergi cezaları", Grup = VergiKalemGrubu.Kkeg,
                KanunMaddesi = "KVK 11/1-d", SiraNo = 1, SistemKalemi = true, Aktif = true
            },
            new VergiKalemi
            {
                Id = KkegTeknopark, Kod = "KKEGI-01", Ad = "Teknopark faaliyetine ilişkin KKEG",
                Grup = VergiKalemGrubu.Kkeg, IstisnayaIliskinMi = true, BagliIstisnaKalemiId = IstisnaTeknopark,
                SiraNo = 30, SistemKalemi = true, Aktif = true
            },
            new VergiKalemi
            {
                Id = KkegBagsiz, Kod = "KKEGI-05", Ad = "Diğer istisna kazançlara ilişkin KKEG",
                Grup = VergiKalemGrubu.Kkeg, IstisnayaIliskinMi = true, BagliIstisnaKalemiId = null,
                SiraNo = 34, SistemKalemi = true, Aktif = true
            },
            new VergiKalemi
            {
                Id = IstisnaIstirak, Kod = "IST-01", Ad = "İştirak kazançları istisnası",
                Grup = VergiKalemGrubu.ZararOlsaDahi, AsgariMatrahtanDuser = true,
                SiraNo = 1, SistemKalemi = true, Aktif = true
            },
            new VergiKalemi
            {
                Id = IstisnaTeknopark, Kod = "IST-17", Ad = "Teknoloji geliştirme bölgesi kazanç istisnası",
                Grup = VergiKalemGrubu.ZararOlsaDahi, AsgariMatrahtanDuser = true,
                SiraNo = 17, SistemKalemi = true, Aktif = true
            },
            new VergiKalemi
            {
                Id = IstisnaDiger, Kod = "IST-20", Ad = "Diğer indirim ve istisnalar",
                Grup = VergiKalemGrubu.ZararOlsaDahi, AsgariMatrahtanDuser = false,
                SiraNo = 20, SistemKalemi = true, Aktif = true
            },
            new VergiKalemi
            {
                Id = IndirimBagis, Kod = "IND-05", Ad = "Bağış ve yardımlar (genel)",
                Grup = VergiKalemGrubu.KazancVarsa, UstSinirTuru = UstSinirTuru.KurumKazanciYuzdesi,
                UstSinirDeger = 5m, DevredebilirMi = false, SiraNo = 5, SistemKalemi = true, Aktif = true
            },
            new VergiKalemi
            {
                Id = IndirimArge, Kod = "IND-01", Ad = "Ar-Ge indirimi",
                Grup = VergiKalemGrubu.KazancVarsa, DevredebilirMi = true, AsgariMatrahtanDuser = true,
                SiraNo = 1, SistemKalemi = true, Aktif = true
            },
            new VergiKalemi
            {
                Id = MahsupGecici, Kod = "MAH-03", Ad = "Ödenen geçici vergi",
                Grup = VergiKalemGrubu.Mahsup, SiraNo = 3, SistemKalemi = true, Aktif = true
            }
        };
    }
}
