using System.Globalization;
using CatalogService.Api.Features.FirmaKontrol.Dtos;
using ClosedXML.Excel;

namespace CatalogService.Api.Features.FirmaKontrol.Services
{
    /// <summary>
    /// Beyanname formatına yakın tek sayfalık .xlsx üretir: kalem kodu, ad, kanun maddesi,
    /// tutar ve kullanıcı notu. Ara toplam ve matrah satırları vurguludur.
    /// </summary>
    public static class VergiBeyannameExcel
    {
        private static readonly CultureInfo Kultur = new("tr-TR");
        private const string TutarBicimi = "#,##0.00";

        private static readonly XLColor BaslikArka = XLColor.FromHtml("#E2E8F0");
        private static readonly XLColor AraToplamArka = XLColor.FromHtml("#F7FAFC");
        private static readonly XLColor MatrahArka = XLColor.FromHtml("#EBF8FF");
        private static readonly XLColor SonucArka = XLColor.FromHtml("#F0FFF4");
        private static readonly XLColor BolumArka = XLColor.FromHtml("#EDF2F7");

        public static byte[] Olustur(VergiBeyannameDto beyanname, string firmaUnvani)
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add($"KV {beyanname.DonemYil}");

            var s = beyanname.Sonuc;
            var satir = 1;

            // ── Başlık ──
            ws.Cell(satir, 1).Value = "KURUMLAR VERGİSİ BEYANNAMESİ";
            ws.Range(satir, 1, satir, 5).Merge().Style.Font.SetBold().Font.SetFontSize(14);
            satir++;

            ws.Cell(satir, 1).Value = firmaUnvani;
            ws.Range(satir, 1, satir, 5).Merge().Style.Font.SetFontSize(11);
            satir++;

            ws.Cell(satir, 1).Value = $"Dönem: {beyanname.DonemYil} · Kurumlar vergisi oranı: %{beyanname.KvOrani.ToString("0.##", Kultur)}";
            ws.Range(satir, 1, satir, 5).Merge().Style.Font.SetItalic().Font.SetFontColor(XLColor.Gray);
            satir += 2;

            // ── Kolon başlıkları ──
            string[] basliklar = { "Kod", "Kalem", "Kanun maddesi", "Tutar", "Not" };
            for (var i = 0; i < basliklar.Length; i++)
            {
                var hucre = ws.Cell(satir, i + 1);
                hucre.Value = basliklar[i];
                hucre.Style.Font.SetBold().Fill.SetBackgroundColor(BaslikArka);
                hucre.Style.Border.SetBottomBorder(XLBorderStyleValues.Thin);
            }
            satir++;

            // ── 1. Ticari kâr ──
            satir = AraToplamYaz(ws, satir, "690", "Ticari bilanço kârı / zararı", s.TicariKar, AraToplamArka);

            // ── 2. İlaveler ──
            satir = BolumBasligiYaz(ws, satir, "İLAVELER (+)");
            satir = BolumBasligiYaz(ws, satir, "  Matrahı artıran KKEG", ikincil: true);
            satir = KalemleriYaz(ws, satir, s.Ilaveler.Where(x => x.MatrahiArtirir));
            satir = BolumBasligiYaz(ws, satir, "  İstisnaya ilişkin KKEG (matraha net etkisi yok)", ikincil: true);
            satir = KalemleriYaz(ws, satir, s.Ilaveler.Where(x => !x.MatrahiArtirir));

            satir = AraToplamYaz(ws, satir, "", "Ham ilave toplamı (beyannameye yazılan)", s.IlaveHamToplam, AraToplamArka);
            satir = AraToplamYaz(ws, satir, "", "Matraha etki eden ilave kısmı", s.IlaveMatrahaEtkiEden, AraToplamArka);
            satir = AraToplamYaz(ws, satir, "", "KÂR VE İLAVELER TOPLAMI", s.KarVeIlavelerToplami, AraToplamArka);

            // ── 3. Zarar olsa dahi indirilecek ──
            satir = BolumBasligiYaz(ws, satir, "ZARAR OLSA DAHİ İNDİRİLECEK İSTİSNA VE İNDİRİMLER (−)");
            satir = KalemleriYaz(ws, satir, s.ZararOlsaDahiIndirimler, efektifKullan: true);
            satir = AraToplamYaz(ws, satir, "", "Toplam", s.ZararOlsaDahiToplam, AraToplamArka);
            satir = AraToplamYaz(ws, satir, "", "KÂR / ZARAR", s.KarZarar, AraToplamArka);

            // ── 4. Geçmiş yıl zararları ──
            satir = BolumBasligiYaz(ws, satir, "GEÇMİŞ YIL ZARARLARI (−)");
            foreach (var z in s.ZararMahsuplari.OrderBy(z => z.ZararYili))
            {
                ws.Cell(satir, 1).Value = z.ZararYili.ToString();
                ws.Cell(satir, 2).Value = $"{z.ZararYili} yılı zararı";
                ws.Cell(satir, 3).Value = z.MahsupEdilebilir ? "KVK 9/1-a" : "mahsup edilemez";
                TutarYaz(ws, satir, 4, z.MahsupEdilen);

                var not = new List<string>();
                if (z.ZararTutari != z.MahsupEdilen) not.Add($"devreden {Bicim(z.DevredenTutar)}");
                if (!string.IsNullOrWhiteSpace(z.Uyari)) not.Add(z.Uyari!);
                ws.Cell(satir, 5).Value = string.Join(" · ", not);

                satir++;
            }
            satir = AraToplamYaz(ws, satir, "", "Mahsup toplamı", s.ZararMahsupToplami, AraToplamArka);

            // ── 5. Kazanç varsa indirilecek ──
            satir = BolumBasligiYaz(ws, satir, "KAZANCIN BULUNMASI HÂLİNDE İNDİRİLECEK İNDİRİMLER (−)");
            satir = KalemleriYaz(ws, satir, s.KazancVarsaIndirimler, efektifKullan: true);
            satir = AraToplamYaz(ws, satir, "", "Toplam", s.KazancVarsaToplam, AraToplamArka);

            // ── 6. Matrah ──
            satir = AraToplamYaz(ws, satir, "", "MATRAH", s.Matrah, MatrahArka);

            // ── 7. Vergi ──
            satir = BolumBasligiYaz(ws, satir, "VERGİ HESABI");
            satir = AraToplamYaz(ws, satir, "", "Normal hesaplanan kurumlar vergisi", s.NormalVergi, AraToplamArka);

            if (s.AsgariKvHesaplandi)
            {
                satir = AraToplamYaz(ws, satir, "", $"Yurt içi asgari kurumlar vergisi (32/C) — matrah {Bicim(s.AsgariMatrah)}",
                    s.AsgariVergi, AraToplamArka);
                ws.Cell(satir - 1, 5).Value = s.AsgariUygulandi ? "uygulanan" : "uygulanmadı (normal vergi yüksek)";
            }

            satir = AraToplamYaz(ws, satir, "", "HESAPLANAN KURUMLAR VERGİSİ", s.HesaplananVergi, MatrahArka);

            // ── 8. Mahsuplar ──
            satir = BolumBasligiYaz(ws, satir, "MAHSUPLAR (−)");
            satir = KalemleriYaz(ws, satir, s.Mahsuplar);
            satir = AraToplamYaz(ws, satir, "", "Mahsup toplamı", s.MahsupToplami, AraToplamArka);

            // ── 9. Sonuç ──
            var sonucBaslik = s.OdenecekVergi >= 0 ? "ÖDENECEK KURUMLAR VERGİSİ" : "İADE / MAHSUP EDİLECEK";
            satir = AraToplamYaz(ws, satir, "", sonucBaslik, Math.Abs(s.OdenecekVergi), SonucArka);

            // ── Uyarılar ──
            if (s.Uyarilar.Count > 0)
            {
                satir++;
                satir = BolumBasligiYaz(ws, satir, "UYARILAR");
                foreach (var u in s.Uyarilar)
                {
                    ws.Cell(satir, 2).Value = u.KalemKodu is null ? u.Mesaj : $"[{u.KalemKodu}] {u.Mesaj}";
                    ws.Range(satir, 2, satir, 5).Merge().Style.Font.SetFontColor(XLColor.FromHtml("#742A2A"));
                    satir++;
                }
            }

            if (!string.IsNullOrWhiteSpace(beyanname.Notlar))
            {
                satir++;
                ws.Cell(satir, 1).Value = "Notlar";
                ws.Cell(satir, 1).Style.Font.SetBold();
                ws.Cell(satir + 1, 1).Value = beyanname.Notlar;
                ws.Range(satir + 1, 1, satir + 1, 5).Merge().Style.Alignment.SetWrapText();
            }

            ws.Column(1).Width = 12;
            ws.Column(2).Width = 58;
            ws.Column(3).Width = 20;
            ws.Column(4).Width = 18;
            ws.Column(5).Width = 45;
            ws.SheetView.FreezeRows(5);

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        private static int BolumBasligiYaz(IXLWorksheet ws, int satir, string baslik, bool ikincil = false)
        {
            var hucre = ws.Cell(satir, 1);
            hucre.Value = baslik;
            ws.Range(satir, 1, satir, 5).Merge();
            hucre.Style.Font.SetBold(!ikincil).Font.SetItalic(ikincil);
            if (!ikincil) hucre.Style.Fill.SetBackgroundColor(BolumArka);
            return satir + 1;
        }

        /// <summary>
        /// Kalem satırları. Grup 2 ve 3'te efektif tutar yazılır: Grup 2'de ilişkili KKEG ile
        /// büyütülmüş, Grup 3'te üst sınır ve kalan kazanç sonrası uygulanabilen tutardır.
        /// </summary>
        private static int KalemleriYaz(IXLWorksheet ws, int satir, IEnumerable<VergiSonucSatirDto> kalemler, bool efektifKullan = false)
        {
            // Beyannameye tutarı olan kalemler yazılır; boş kalemler çıktıyı şişirmesin.
            foreach (var k in kalemler.Where(k => k.GirilenTutar != 0m || k.EfektifTutar != 0m).OrderBy(k => k.SiraNo).ThenBy(k => k.Kod))
            {
                ws.Cell(satir, 1).Value = k.Kod;
                ws.Cell(satir, 2).Value = k.Ad;
                ws.Cell(satir, 3).Value = k.KanunMaddesi ?? string.Empty;
                TutarYaz(ws, satir, 4, efektifKullan ? k.EfektifTutar : k.GirilenTutar);

                var not = new List<string>();
                if (!string.IsNullOrWhiteSpace(k.Aciklama)) not.Add(k.Aciklama!);
                if (k.IliskiliKkeg > 0) not.Add($"girilen {Bicim(k.GirilenTutar)} + ilişkili KKEG {Bicim(k.IliskiliKkeg)}");
                if (k.SinirAsimi > 0) not.Add($"üst sınır aşımı {Bicim(k.SinirAsimi)}");
                if (k.DevredenTutar > 0) not.Add($"devreden {Bicim(k.DevredenTutar)}");
                if (k.YananTutar > 0) not.Add($"indirilemeyen (yanan) {Bicim(k.YananTutar)}");

                ws.Cell(satir, 5).Value = string.Join(" · ", not);
                satir++;
            }

            return satir;
        }

        private static int AraToplamYaz(IXLWorksheet ws, int satir, string kod, string ad, decimal tutar, XLColor arka)
        {
            ws.Cell(satir, 1).Value = kod;
            ws.Cell(satir, 2).Value = ad;
            TutarYaz(ws, satir, 4, tutar);

            var aralik = ws.Range(satir, 1, satir, 5);
            aralik.Style.Font.SetBold().Fill.SetBackgroundColor(arka);
            aralik.Style.Border.SetTopBorder(XLBorderStyleValues.Thin);

            return satir + 1;
        }

        private static void TutarYaz(IXLWorksheet ws, int satir, int kolon, decimal tutar)
        {
            var hucre = ws.Cell(satir, kolon);
            hucre.Value = tutar;
            hucre.Style.NumberFormat.SetFormat(TutarBicimi);
            hucre.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);
        }

        private static string Bicim(decimal tutar) => tutar.ToString("N2", Kultur);
    }
}
