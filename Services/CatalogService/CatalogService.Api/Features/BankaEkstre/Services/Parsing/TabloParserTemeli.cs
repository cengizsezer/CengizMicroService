namespace CatalogService.Api.Features.BankaEkstre.Services.Parsing
{
    /// <summary>
    /// Kolon başlıklı, tek sayfalık ekstre dosyalarının ortak ayrıştırma iskeleti:
    /// dosyayı oku → başlık satırını isimle bul → tarih ve tutar okunan satırları topla.
    ///
    /// Bankaya özel olan yalnız üç şey: kolon tanımları, yönün nasıl bulunduğu ve işlem
    /// tipinin nereden geldiği. Geri kalan (dosya biçimi seçimi, başlık arama, tarih/tutar
    /// okuma, atlanan satır sayımı) burada tek yerde durur — üç bankada üç kez yazılsaydı
    /// ilk düzeltme ikisinde unutulurdu.
    ///
    /// <b>Vakıfbank ayrıştırıcısı bu iskelete taşınmadı</b>: çalışıyor ve gerçek dosyayla
    /// doğrulanmış durumda; taşımak kazanç değil risk olurdu.
    ///
    /// Ayrıştırıcılar durumsuz (singleton) olmak zorunda: dosyaya özel her sayaç
    /// <see cref="AyristirmaBaglami"/> içinde, çağrı yerelinde durur.
    /// </summary>
    public abstract class TabloParserTemeli : IEkstreParser
    {
        // Kolon anahtarları: tanım listesi ile okuma tarafı aynı adı kullansın diye sabit.
        protected const string KolonTarih = "tarih";
        protected const string KolonTutar = "tutar";
        protected const string KolonAciklama = "aciklama";
        protected const string KolonIslemTipi = "islemTipi";
        protected const string KolonKanal = "kanal";
        protected const string KolonBorcAlacak = "borcAlacak";
        protected const string KolonReferans = "referans";

        public abstract string ParserTipi { get; }
        public abstract string Ad { get; }

        /// <summary>Bankanın kolonları: aranacak başlık adları ve ölçülen sabit indeksler.</summary>
        protected abstract IReadOnlyList<KolonTanimi> Kolonlar { get; }

        /// <summary>Ölçülen veri başlangıcı (1 tabanlı Excel satır numarası).</summary>
        protected abstract int VarsayilanIlkVeriSatiri { get; }

        /// <summary>Dosyaya özel sayaçlar; özet uyarılar buradan üretilir.</summary>
        protected sealed class AyristirmaBaglami
        {
            public AyristirmaBaglami(EkstreParseSonuc sonuc) => Sonuc = sonuc;

            public EkstreParseSonuc Sonuc { get; }

            /// <summary>Borç/alacak kolonu ile tutarın işaretinin çeliştiği satır sayısı.</summary>
            public int YonCelismesi { get; set; }
        }

        public EkstreParseSonuc Ayristir(Stream dosya)
        {
            var sonuc = new EkstreParseSonuc();
            var baglam = new AyristirmaBaglami(sonuc);

            var tablo = EkstreTabloOkuyucu.Oku(dosya, sonuc);
            var (kolonlar, ilkVeriSatiri) = TabloBaslik.Bul(tablo, Kolonlar, VarsayilanIlkVeriSatiri, sonuc);

            sonuc.AciklamaKolonu = kolonlar[KolonAciklama];

            foreach (var satir in tablo.Satirlar)
            {
                if (satir.SatirNo < ilkVeriSatiri || satir.BosMu) continue;

                var tarihHucresi = satir.Hucre(kolonlar[KolonTarih]);
                var tutarHucresi = satir.Hucre(kolonlar[KolonTutar]);

                // Tarih veya tutar okunamıyorsa satır veri değildir (ara başlık, toplam, dipnot).
                if (!TabloDeger.Tarih(tarihHucresi, out var tarih) ||
                    !TabloDeger.Tutar(tutarHucresi, out var imzaliTutar))
                {
                    if (!tarihHucresi.BosMu || !tutarHucresi.BosMu) sonuc.AtlananSatir++;
                    continue;
                }

                var aciklama = satir.Hucre(kolonlar[KolonAciklama]).Metin;

                var ayrilan = new AyrilanSatir
                {
                    SiraNo = sonuc.Satirlar.Count + 1,
                    KaynakSatirNo = satir.SatirNo,
                    Tarih = tarih,
                    Tutar = Math.Abs(imzaliTutar),
                    HamAciklama = aciklama,
                    Kanal = Bos(satir.Hucre(kolonlar[KolonKanal]).Metin),
                    Referans = Bos(satir.Hucre(kolonlar[KolonReferans]).Metin),
                    // IBAN kolonu yok; açıklama metninden çıkarılır. Yalnız bilgi olarak
                    // saklanır (IBAN katmanı hesap bazında açılıyor).
                    KarsiIban = Normalizasyon.IbanBul(aciklama)
                };

                Doldur(satir, kolonlar, imzaliTutar, ayrilan, baglam);

                sonuc.Satirlar.Add(ayrilan);
            }

            Tamamla(baglam);

            if (sonuc.Satirlar.Count == 0)
                sonuc.Uyarilar.Add("Dosyada ayrıştırılabilir satır bulunamadı. Doğru banka/hesap tipi seçildi mi?");

            return sonuc;
        }

        /// <summary>
        /// Bankaya özel alanlar: yön ve işlem tipi. <paramref name="imzaliTutar"/> dosyadaki
        /// işaretiyle gelir; <see cref="AyrilanSatir.Tutar"/> mutlak değere çevrilmiş durumda.
        /// </summary>
        protected abstract void Doldur(
            TabloSatiri satir, KolonHaritasi kolonlar, decimal imzaliTutar,
            AyrilanSatir ayrilan, AyristirmaBaglami baglam);

        /// <summary>Dosya bittikten sonraki özet uyarılar.</summary>
        protected virtual void Tamamla(AyristirmaBaglami baglam)
        {
        }

        /// <summary>
        /// Borç/alacak kolonundan yön. Kolon boşsa tutarın işaretine düşülür.
        /// <c>B = borç = çıkan</c>, <c>A = alacak = giren</c>.
        ///
        /// İki sinyal çelişirse <b>kolon kazanır</b> ve çelişki sayılır: işaret kullanmayan
        /// bir ekstre biçiminde tüm satırlar "giren" okunur ve 120/329 kararı tamamen ters
        /// giderdi. Çelişkinin kendisi de sessiz kalmamalı — dosya beklenenden farklıdır.
        /// </summary>
        protected static Domain.Yon YonBul(decimal imzaliTutar, string? borcAlacak, AyristirmaBaglami baglam)
        {
            var isaretYonu = imzaliTutar < 0m ? Domain.Yon.Cikan : Domain.Yon.Giren;

            if (string.IsNullOrWhiteSpace(borcAlacak)) return isaretYonu;

            var ba = Normalizasyon.TurkceSadelestir(borcAlacak).Trim();

            Domain.Yon? kolonYonu = ba.StartsWith("B", StringComparison.Ordinal) ? Domain.Yon.Cikan
                                  : ba.StartsWith("A", StringComparison.Ordinal) ? Domain.Yon.Giren
                                  : null;

            if (kolonYonu is null) return isaretYonu;

            // İşaret ancak sıfırdan farklı bir tutarda anlamlı; sıfır tutarda çelişki sayılmaz.
            if (imzaliTutar != 0m && kolonYonu != isaretYonu) baglam.YonCelismesi++;

            return kolonYonu.Value;
        }

        protected static string? Bos(string? deger)
            => string.IsNullOrWhiteSpace(deger) ? null : deger.Trim();
    }
}
