using System.IO.Compression;
using System.Security;
using System.Text;
using WebApp.Application.Services;
using WebApp.Application.Services.Interfaces;

namespace WebApp.UnitTests.FirmaKontrol
{
    /// <summary>
    /// Mizan Excel okuyucusu sütunları BAŞLIK ADIYLA bulur. ORKA'nın iki biçimi (döviz sütunlu /
    /// dövizsiz, bir sola kaymış) aynı hesaba aynı bakiyeyi vermeli; döviz sütunu asla bakiye
    /// sayılmaz; zorunlu başlık eksikse sabit sıraya düşülmez.
    /// </summary>
    public class ExcelMizanParserTests
    {
        private static readonly object?[] DovizliBaslik =
        {
            "Hesap Kodu", "Hesap Adı", null, "Döviz Borç Bakiye", "Döviz Alacak Bakiye",
            "TL Borç Bakiye", "TL Alacak Bakiye", "Bakiye"
        };

        private static readonly object?[] DovizsizBaslik =
        {
            "Hesap Kodu", "Hesap Adı", "TL Borç Toplam", "TL Alacak Toplam",
            "TL Borç Bakiye", "TL Alacak Bakiye", "Bakiye"
        };

        private static MizanParseResult Oku(params object?[][] satirlar) => OkuSayfalar(satirlar);

        private static MizanParseResult OkuSayfalar(params object?[][][] sayfalar)
        {
            using var ms = TestXlsx.Olustur(sayfalar);
            return new ExcelMizanParser().ParseAsync(ms).GetAwaiter().GetResult();
        }

        private static decimal? Bakiye(MizanParseResult r, string kod) =>
            r.Rows.Single(x => x.Kod == kod).CariDonem;

        [Fact]
        public void Doviz_sutunlu_bicimde_bakiye_TL_sutunlarindan_okunur_doviz_asla()
        {
            var r = Oku(
                DovizliBaslik,
                new object?[] { "100", "KASA", null, 999m, null, 1000m, null, 1000m },
                new object?[] { "100 1", "TL Kasa", null, 999m, null, 1000m, null, 1000m },
                new object?[] { "320", "SATICILAR", null, null, 777m, null, 1000m, -1000m });

            Assert.Empty(r.Errors);
            Assert.Equal(1000m, Bakiye(r, "100"));
            Assert.Equal(-1000m, Bakiye(r, "320"));

            var kasa = r.HesapDugumleri.Single(d => d.Kod == "100");
            Assert.Equal(1000m, kasa.BorcBakiye);
            Assert.Equal(0m, kasa.AlacakBakiye);
            Assert.Empty(r.Uyarilar);
        }

        [Fact]
        public void Dovizsiz_bicim_bir_sola_kaymis_olsa_da_ayni_sonucu_verir()
        {
            // Eski okuyucu F/G'yi sabit okuyordu: bu biçimde F = TL Alacak Bakiye, G = Bakiye.
            var r = Oku(
                DovizsizBaslik,
                new object?[] { "100", "KASA", 5000m, 4000m, 1000m, null, 1000m },
                new object?[] { "320", "SATICILAR", 200m, 1200m, null, 1000m, -1000m });

            Assert.Empty(r.Errors);
            Assert.Equal(1000m, Bakiye(r, "100"));
            Assert.Equal(-1000m, Bakiye(r, "320"));
            Assert.Empty(r.Uyarilar);
        }

        [Fact]
        public void Baslik_ucuncu_satirda_olabilir()
        {
            var r = Oku(
                new object?[] { "LIME TEKSTİL A.Ş. MİZANI" },
                new object?[] { "01.01.2026 - 30.09.2026" },
                DovizsizBaslik,
                new object?[] { "100", "KASA", 10m, 0m, 10m, null, 10m },
                new object?[] { "500", "SERMAYE", 0m, 10m, null, 10m, -10m });

            Assert.Empty(r.Errors);
            Assert.Equal(2, r.Rows.Count);
            Assert.Equal(10m, Bakiye(r, "100"));
        }

        [Fact]
        public void Baslik_harf_turkce_karakter_bosluk_ve_noktalamaya_duyarsiz()
        {
            // LIME/ORKA çıktısındaki gibi büyük harf ve "-Sİ" ekli adlar.
            var r = Oku(
                new object?[] { "HESAP KODU", "HESAP ADI", null, "BORÇ", "ALACAK", "BORÇ  BAKİYESİ:", "alacak bakiyesi", "Bakiye" },
                new object?[] { "102", "BANKALAR", null, 300m, 100m, 200m, null, 200m },
                new object?[] { "331", "ORTAKLARA BORÇLAR", null, 0m, 200m, null, 200m, -200m });

            Assert.Empty(r.Errors);
            Assert.Equal(200m, Bakiye(r, "102"));
            Assert.Equal(-200m, Bakiye(r, "331"));
        }

        [Fact]
        public void Zorunlu_baslik_eksikse_acik_hata_doner_sabit_siraya_dusmez()
        {
            var r = Oku(
                new object?[] { "Hesap Kodu", "Hesap Adı", "Borç", "Alacak", "Bakiye" },
                new object?[] { "100", "KASA", 10m, 0m, 10m });

            var hata = Assert.Single(r.Errors);
            Assert.Contains("Borç bakiye", hata);
            Assert.Contains("Alacak bakiye", hata);
            Assert.Contains("Bulunan başlıklar", hata);
            Assert.Contains("C: Borç", hata);
            Assert.Empty(r.Rows);
        }

        [Fact]
        public void Hesap_Kodu_basligi_yoksa_hata_doner()
        {
            var r = Oku(
                new object?[] { "Kod", "Ad", "Borç Bakiye", "Alacak Bakiye" },
                new object?[] { "100", "KASA", 10m, 0m });

            var hata = Assert.Single(r.Errors);
            Assert.Contains("Hesap Kodu", hata);
            Assert.Empty(r.Rows);
        }

        [Fact]
        public void Dengesiz_mizan_okunur_ama_uyari_doner()
        {
            var r = Oku(
                DovizsizBaslik,
                new object?[] { "100", "KASA", 1000m, 0m, 1000m, null, 1000m },
                new object?[] { "320", "SATICILAR", 0m, 900m, null, 900m, -900m });

            Assert.Empty(r.Errors);
            Assert.Equal(2, r.Rows.Count);
            var uyari = Assert.Single(r.Uyarilar);
            Assert.Contains("dengesiz", uyari);
            Assert.Contains("100,00", uyari); // fark
        }

        [Fact]
        public void Denge_yalniz_ana_hesaplardan_alt_kirilimlar_cift_sayilmaz()
        {
            var r = Oku(
                DovizsizBaslik,
                new object?[] { "100", "KASA", 1000m, 0m, 1000m, null, 1000m },
                new object?[] { "100 1", "TL", 1000m, 0m, 1000m, null, 1000m },
                new object?[] { "100 1 01", "Merkez", 1000m, 0m, 1000m, null, 1000m },
                new object?[] { "500", "SERMAYE", 0m, 1000m, null, 1000m, -1000m });

            Assert.Empty(r.Uyarilar);
            Assert.Equal(2, r.Rows.Count);
        }

        [Fact]
        public void Genel_toplam_satiri_bos_sutun_kaymasina_ragmen_yakalanir()
        {
            // PROGROUP çıktısı: E sütunu boş, son satırda kod sütununda sayfa sayısı ("233" —
            // hesap planında gerçek bir kod). Eski okuyucu toplamı D/E'den okuduğu için bu
            // satırı 233 hesabı sanıyordu.
            var r = Oku(
                new object?[] { "Hesap Kodu", "Hesap Adı", "TL Borç Toplam", "TL Alacak Toplam", null, "TL Borç Bakiye", "TL Alacak Bakiye", "Bakiye" },
                new object?[] { "102", "BANKALAR", 500m, 100m, null, 400m, null, 400m },
                new object?[] { "500", "SERMAYE", 0m, 400m, null, null, 400m, -400m },
                new object?[] { "233", null, 500m, 500m, null, 400m, 400m, 0m });

            Assert.DoesNotContain(r.Rows, x => x.Kod == "233");
            Assert.Contains(r.AtlananSatirlar, a => a.Kod == "233" && a.Sebep == AtlamaSebebi.MizanToplamSatiri);
            Assert.Empty(r.Uyarilar);
        }

        [Fact]
        public void Adi_dolu_kapanmis_hesap_toplam_satiri_sayilmaz_sifir_okunur()
        {
            // LIME çıktısı: 120 ALICILAR borç toplamı = alacak toplamı, bakiye hücreleri boş.
            var r = Oku(
                new object?[] { "HESAP KODU", "HESAP ADI", null, "BORÇ", "ALACAK", "BORÇ BAKİYESİ", "ALACAK BAKİYESİ", "Bakiye" },
                new object?[] { "102", "BANKALAR", null, 300m, 100m, 200m, null, 200m },
                new object?[] { "120", "ALICILAR", null, 500m, 500m, null, null, 0m },
                new object?[] { "500", "SERMAYE", null, 0m, 200m, null, 200m, -200m });

            Assert.Equal(0m, Bakiye(r, "120"));
            Assert.DoesNotContain(r.AtlananSatirlar, a => a.Kod == "120");
            Assert.Empty(r.Uyarilar);
        }

        [Fact]
        public void Adi_bos_esit_satir_toplam_satiridir()
        {
            var r = Oku(
                DovizsizBaslik,
                new object?[] { "102", "BANKALAR", 500m, 100m, 400m, null, 400m },
                new object?[] { "500", "SERMAYE", 0m, 400m, null, 400m, -400m },
                new object?[] { "186", null, 500m, 500m, 400m, 400m, 0m });

            Assert.DoesNotContain(r.Rows, x => x.Kod == "186");
            Assert.Contains(r.AtlananSatirlar, a => a.Kod == "186" && a.Sebep == AtlamaSebebi.MizanToplamSatiri);
        }

        [Fact]
        public void Mizan_ilk_sayfada_degilse_sonraki_sayfada_aranir()
        {
            var r = OkuSayfalar(
                new[]
                {
                    new object?[] { "Firma Ünvanı", "Fatura No" },
                    new object?[] { "X A.Ş.", "F1" }
                },
                new[]
                {
                    new object?[] { "Hesap Kodu", "Hesap Adı", "TL Borç Toplam", "TL Alacak Toplam", "TL Borç Bakiye", "TL Alacak Bakiye" },
                    new object?[] { "191", "İNDİRİLECEK KDV", 50m, 0m, 50m, null },
                    new object?[] { "391", "HESAPLANAN KDV", 0m, 50m, null, 50m }
                });

            Assert.Empty(r.Errors);
            Assert.Equal(50m, Bakiye(r, "191"));
            Assert.Equal(-50m, Bakiye(r, "391"));
        }

        [Fact]
        public void Bos_bakiye_hucreleri_sifirdir_okunamayan_metin_atlanir()
        {
            // ORKA kapanmış hesapta iki bakiyeyi de boş bırakır → 0. Hücrede metin varsa okunamaz.
            var r = Oku(
                DovizliBaslik,
                new object?[] { "100", "KASA", null, null, null, 10m, null, 10m },
                new object?[] { "120", "ALICILAR", null, null, null, null, null, 0m },
                new object?[] { "320", "SATICILAR", null, null, null, "yok", 10m, -10m },
                new object?[] { "500", "SERMAYE", null, null, null, null, 10m, -10m });

            Assert.Equal(0m, Bakiye(r, "120"));
            Assert.DoesNotContain(r.Rows, x => x.Kod == "320");
            Assert.Contains(r.AtlananSatirlar, a => a.Kod == "320" && a.Sebep == AtlamaSebebi.BakiyeOkunamadi);
        }

        [Fact]
        public void Basit_bicim_Hesap_Kodu_ve_Bakiye_basliklariyla_okunur()
        {
            var r = Oku(
                new object?[] { "Hesap Kodu", "Bakiye" },
                new object?[] { "100", 250m },
                new object?[] { "600", -250m });

            Assert.Empty(r.Errors);
            Assert.Equal(250m, Bakiye(r, "100"));
            Assert.Equal(-250m, Bakiye(r, "600"));
            Assert.Empty(r.Uyarilar);
        }

        [Theory]
        [InlineData("  TL Borç  Bakiyesi: ", "tl borc bakiyesi")]
        [InlineData("HESAP KODU", "hesap kodu")]
        [InlineData("Hesap Adı", "hesap adi")]
        [InlineData("BORÇ BAKİYESİ", "borc bakiyesi")]
        [InlineData("Alacak-Bakiye", "alacak bakiye")]
        [InlineData("ŞĞÜÖÇİı", "sguocii")]
        public void Normalize_turkce_ve_noktalamayi_duzler(string girdi, string beklenen) =>
            Assert.Equal(beklenen, MizanBasliklari.Normalize(girdi));

        /// <summary>
        /// Paket eklemeden en küçük xlsx: hücreler inline string ya da sayı. ExcelDataReader
        /// bunu gerçek bir Excel dosyası gibi okur.
        /// </summary>
        private static class TestXlsx
        {
            public static MemoryStream Olustur(object?[][][] sayfalar)
            {
                var ms = new MemoryStream();
                using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
                {
                    void Yaz(string ad, string icerik)
                    {
                        using var w = new StreamWriter(zip.CreateEntry(ad).Open(), new UTF8Encoding(false));
                        w.Write(icerik);
                    }

                    var sayfaTipleri = string.Concat(Enumerable.Range(1, sayfalar.Length).Select(i =>
                        $"<Override PartName=\"/xl/worksheets/sheet{i}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>"));
                    Yaz("[Content_Types].xml",
                        "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                        "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
                        "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
                        "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
                        sayfaTipleri + "</Types>");
                    Yaz("_rels/.rels",
                        "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                        "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
                    Yaz("xl/workbook.xml",
                        "<?xml version=\"1.0\" encoding=\"UTF-8\"?><workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets>" +
                        string.Concat(Enumerable.Range(1, sayfalar.Length).Select(i => $"<sheet name=\"Sayfa{i}\" sheetId=\"{i}\" r:id=\"rId{i}\"/>")) +
                        "</sheets></workbook>");
                    Yaz("xl/_rels/workbook.xml.rels",
                        "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                        string.Concat(Enumerable.Range(1, sayfalar.Length).Select(i =>
                            $"<Relationship Id=\"rId{i}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet{i}.xml\"/>")) +
                        "</Relationships>");

                    for (var s = 0; s < sayfalar.Length; s++)
                        Yaz($"xl/worksheets/sheet{s + 1}.xml", Sayfa(sayfalar[s]));
                }
                ms.Position = 0;
                return ms;
            }

            private static string Sayfa(object?[][] satirlar)
            {
                var sb = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>");
                for (var r = 0; r < satirlar.Length; r++)
                {
                    sb.Append($"<row r=\"{r + 1}\">");
                    for (var c = 0; c < satirlar[r].Length; c++)
                    {
                        var ref_ = MizanBasliklari.SutunHarfi(c) + (r + 1);
                        switch (satirlar[r][c])
                        {
                            case null: break;
                            case decimal d:
                                sb.Append($"<c r=\"{ref_}\"><v>{d.ToString(System.Globalization.CultureInfo.InvariantCulture)}</v></c>");
                                break;
                            case var v:
                                sb.Append($"<c r=\"{ref_}\" t=\"inlineStr\"><is><t xml:space=\"preserve\">{SecurityElement.Escape(v.ToString())}</t></is></c>");
                                break;
                        }
                    }
                    sb.Append("</row>");
                }
                sb.Append("</sheetData></worksheet>");
                return sb.ToString();
            }
        }
    }
}
