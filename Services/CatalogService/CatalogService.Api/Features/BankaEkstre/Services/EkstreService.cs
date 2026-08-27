using System.Text.Json;
using CatalogService.Api.Features.BankaEkstre.Domain;
using CatalogService.Api.Features.BankaEkstre.Kapsam;
using CatalogService.Api.Features.BankaEkstre.Dtos;
using CatalogService.Api.Features.BankaEkstre.Services.Parsing;
using CatalogService.Api.Infrastructure.Auth;
using CatalogService.Api.Infrastructure.Context;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;

namespace CatalogService.Api.Features.BankaEkstre.Services
{
    /// <summary>Üretilen xlsx dosyası (düzeltilmiş ekstre veya analiz dökümü).</summary>
    public record EkstreDosyasi(string DosyaAdi, byte[] Icerik);

    public interface IEkstreService
    {
        Task<EkstreYuklemeDto> YukleAsync(int bankaHesabiId, Stream dosya, string dosyaAdi, CancellationToken ct = default);
        Task<List<EkstreYuklemeDto>> GetYuklemelerAsync(CancellationToken ct = default);
        Task<EkstreYuklemeDto?> GetYuklemeAsync(int id, CancellationToken ct = default);
        /// <param name="kategoriId">
        /// Dolu ise yalnız o işlem kategorisine düşen satırlar döner. Kategori satıra
        /// yazılmıyor, hesap kodunun ana grubundan okunuyor (bkz. <see cref="KategoriCozucu"/>).
        /// </param>
        Task<List<EkstreSatirDto>?> GetSatirlarAsync(int ekstreId, SatirDurum? durum, int? kategoriId = null,
                                                     CancellationToken ct = default);
        /// <summary>
        /// Satırı onaylar. <paramref name="kisiYonlendir"/> true ise satırdaki kişi için
        /// kalıcı bir <see cref="KisiYonlendirme"/> kaydı da oluşturulur (onay ekranındaki
        /// "bu kişiyi hep bu hesaba yönlendir" kısayolu).
        /// </summary>
        Task<EkstreSatirDto?> OnaylaAsync(int satirId, string hesapKodu, bool kisiYonlendir = false,
                                          CancellationToken ct = default);
        Task<EkstreSatirDto?> DigerBankadaAsync(int satirId, CancellationToken ct = default);
        Task<DisaAktarimSonucDto?> DisaAktarAsync(int ekstreId, CancellationToken ct = default);
        Task<EkstreDosyasi?> DuzeltilmisEkstreAsync(int ekstreId, CancellationToken ct = default);

        /// <summary>
        /// Analiz dökümü: satırların <b>tamamı</b>, durumu ne olursa olsun. Sistemin ne
        /// önerdiğini onaydan önce incelemek için; ORKA'ya yüklenmez.
        /// </summary>
        Task<EkstreDosyasi?> AnalizDokumuAsync(int ekstreId, CancellationToken ct = default);
        Task<bool> SilAsync(int ekstreId, CancellationToken ct = default);
    }

    /// <summary>
    /// Ekstre yükleme, satır işleme, onay ve dışa aktarım. Yükleme anında her satır için
    /// açıklama üretilir ve karşı hesap katmanlı olarak çözülür; belirsiz kalan satırlar
    /// onaya düşer. Onaylar <see cref="HesapEslesmesi"/> tablosuna yazılır ve bir sonraki
    /// yüklemede geçmiş onay katmanından çözülür.
    /// </summary>
    public class EkstreService : IEkstreService
    {
        private readonly CatalogContext _db;
        private readonly IEkstreParserSecici _parserSecici;
        private readonly IUnvanCikarici _unvanCikarici;
        private readonly IAciklamaUretici _aciklamaUretici;
        private readonly IHesapEslestirici _eslestirici;
        private readonly IHesapEslesmeService _ogrenme;
        private readonly IHttpCurrentUser _kullanici;
        private readonly IBankaFirmaKapsami _kapsam;

        public EkstreService(
            CatalogContext db,
            IEkstreParserSecici parserSecici,
            IUnvanCikarici unvanCikarici,
            IAciklamaUretici aciklamaUretici,
            IHesapEslestirici eslestirici,
            IHesapEslesmeService ogrenme,
            IHttpCurrentUser kullanici,
            IBankaFirmaKapsami kapsam)
        {
            _db = db;
            _parserSecici = parserSecici;
            _unvanCikarici = unvanCikarici;
            _aciklamaUretici = aciklamaUretici;
            _eslestirici = eslestirici;
            _ogrenme = ogrenme;
            _kullanici = kullanici;
            _kapsam = kapsam;
        }

        // ---- Firma kapsami ----
        // Modulun hicbir sorgusu gorunmez bir filtreye guvenmez; kapsam burada tek yerde
        // tanimlanir ve her sorgu bu ifadelerden birinden gecer.

        private IQueryable<BankaHesabi> Hesaplar
            => _db.EkstreBankaHesaplari.Where(h => h.FirmaId == _kapsam.FirmaId);

        private IQueryable<EkstreYukleme> Yuklemeler
            => _db.EkstreYuklemeler.Where(y => y.FirmaId == _kapsam.FirmaId);

        /// <summary>Satirin kendi FirmaId alani yok; kapsamini bagli oldugu yuklemeden alir.</summary>
        private IQueryable<EkstreSatiri> Satirlar
            => _db.EkstreSatirlari.Where(s => _db.EkstreYuklemeler
                                                 .Any(y => y.Id == s.EkstreYuklemeId && y.FirmaId == _kapsam.FirmaId));

        private IQueryable<HesapPlaniKaydi> Plan
            => _db.EkstreHesapPlani.Where(h => h.FirmaId == _kapsam.FirmaId);

        // ---- Yükleme ve işleme ----

        public async Task<EkstreYuklemeDto> YukleAsync(int bankaHesabiId, Stream dosya, string dosyaAdi, CancellationToken ct = default)
        {
            var hesap = await Hesaplar.FirstOrDefaultAsync(h => h.Id == bankaHesabiId, ct)
                        ?? throw new BankaEkstreKuralException(nameof(bankaHesabiId), "Banka hesabı bulunamadı.");

            // Ayrıştırıcısı olmayan hesap bilerek tanımlanmış olabilir (vadeli, süpürme,
            // blokaj): kayıt defterinde durur ve eşleştirmede kullanılır, ama ekstre kabul etmez.
            if (string.IsNullOrWhiteSpace(hesap.ParserTipi))
                throw new BankaEkstreKuralException(nameof(hesap.ParserTipi),
                    $"'{hesap.BankaAdi}' hesabında ayrıştırıcı seçili değil; bu hesaba ekstre " +
                    "yüklenemez. Hesap yalnız karşı hesap olarak kullanılıyor. Ekstre yüklenecekse " +
                    "Tanımlar > Banka hesapları'ndan ayrıştırıcı seçin.");

            var parser = _parserSecici.Sec(hesap.ParserTipi)
                         ?? throw new BankaEkstreKuralException(nameof(hesap.ParserTipi),
                             $"'{hesap.ParserTipi}' için ayrıştırıcı tanımlı değil.");

            // Kaynak dosya saklanır: dışa aktarımın birinci parçası orijinal yapıdaki
            // dosyanın açıklama kolonunu değiştirerek üretiliyor.
            var icerik = await BaytlariOkuAsync(dosya, ct);

            using var okumaAkisi = new MemoryStream(icerik, writable: false);
            var ayristirma = parser.Ayristir(okumaAkisi);

            var yukleme = new EkstreYukleme
            {
                FirmaId = _kapsam.FirmaId,
                BankaHesabiId = hesap.Id,
                DosyaAdi = Normalizasyon.Kirp(dosyaAdi, 260),
                YuklemeTarihi = DateTime.Now,
                DonemBaslangic = ayristirma.DonemBaslangic,
                DonemBitis = ayristirma.DonemBitis,
                SatirSayisi = ayristirma.Satirlar.Count,
                Durum = ayristirma.Satirlar.Count == 0 ? YuklemeDurum.Hatali : YuklemeDurum.Tamamlandi,
                Uyarilar = ayristirma.Uyarilar.Count == 0 ? null : string.Join(Environment.NewLine, ayristirma.Uyarilar),
                DosyaIcerik = icerik,
                AciklamaKolonu = ayristirma.AciklamaKolonu
            };

            if (ayristirma.AtlananSatir > 0)
                yukleme.Uyarilar = (yukleme.Uyarilar is null ? string.Empty : yukleme.Uyarilar + Environment.NewLine)
                                   + $"{ayristirma.AtlananSatir} satır tarih/tutar okunamadığı için atlandı.";

            _db.EkstreYuklemeler.Add(yukleme);
            await _db.SaveChangesAsync(ct);

            // Hesap sahibinin tüm yazımları eşleştirme verisinin parçası: unvan çıkarma da,
            // benzersiz önek indeksinin süzülmesi de aynı kimliği kullanır.
            var hesapSahibi = await HesapSahibiKimligiBulAsync(hesap, ct);
            var veri = await EslestirmeVerisiYukleAsync(hesap, hesapSahibi, ct);
            var sablonlar = await SablonlariYukleAsync(hesap.ParserTipi, ct);
            var desenler = await DesenleriYukleAsync(hesap.ParserTipi, ct);

            foreach (var ayrilan in ayristirma.Satirlar)
            {
                var satir = SatirOlustur(yukleme.Id, ayrilan, sablonlar, desenler, veri);
                _db.EkstreSatirlari.Add(satir);
            }

            await _db.SaveChangesAsync(ct);

            return (await GetYuklemeAsync(yukleme.Id, ct))!;
        }

        /// <summary>Tek satırın açıklama üretimi + katmanlı eşleştirmesi.</summary>
        private EkstreSatiri SatirOlustur(
            int yuklemeId,
            AyrilanSatir ayrilan,
            IReadOnlyList<AciklamaSablonu> sablonlar,
            IReadOnlyList<UnvanDeseni> desenler,
            EslestirmeVerisi veri)
        {
            var karsiIban = KarsiIbanSec(ayrilan, veri);

            var baglam = new SatirBaglami
            {
                IslemTipi = ayrilan.IslemTipi,
                HamAciklama = ayrilan.HamAciklama,
                Yon = ayrilan.Yon,
                KarsiIban = karsiIban,
                KarsiVkn = ayrilan.KarsiVkn
            };

            // Unvan çıkarmadan önce açıklama kapsamlı sabit kurala bakılır: personel avansı
            // satırlarında açıklamadaki isim bir cari değil, ödeme yapılan kişidir. Çıkarılsaydı
            // unvan benzerliği katmanı onu 120/329 altında bir cariye eşlerdi.
            var aciklamaKurali = _eslestirici.AciklamaKuraliBul(baglam, veri);

            // Kural yalnız ana grubu veriyorsa (personel/iş avansı → 195, 196) unvan yine
            // çıkarılır: kişi bir cari değil ama muavini o grubun içinde bu adla aranır.
            // Ölçümde giden FAST satırlarının 195'e düşen çoğunluğu ("masraf ödemesi")
            // aksi hâlde kişi adı hiç okunmadan onay kuyruğuna kalıyordu.
            // Vergi tahsilatı satırlarında unvan hiç çıkarılmaz: açıklamadaki
            // "Soyadi/Unvani :PKF ADAY …" hesap sahibinin kendi unvanı, karşı taraf değil.
            // Karşı hesabı vergi kodu / anahtar kelime / plaka belirler.
            var vergiSatiri = VergiPlakaCozucu.VergiSatiriMi(ayrilan.IslemTipi);

            var unvanCikarilsin = !vergiSatiri &&
                                  aciklamaKurali is null or { UnvanCikarilsin: true } or { AltHesapGerekli: true };

            if (unvanCikarilsin)
            {
                var unvan = _unvanCikarici.Cikar(ayrilan.HamAciklama, desenler, veri.HesapSahibi);
                baglam.Unvan = unvan.Unvan;

                // Karşı taraf olarak hesap sahibinin kendisi çıktı: satır kendi hesapları
                // arası bir transfer. Banka kayıt defteri katmanı bu bayrakla da açılır.
                baglam.HesapSahibiElendi = unvan.HesapSahibiElendi;

                // Yalnız hesap sahibinin kendi adı yakalandıysa karşı taraf bilinmiyor demektir.
                // İşlem tipi anahtarına düşülmez; satır onaya kalır.
                baglam.AnahtarUretilmesin = unvan.Unvan is null && unvan.HesapSahibiElendi;
            }

            // Unvan çıkarılmış olsa da kural "bu bir cari değil" diyorsa öğrenme anahtarı
            // üretilmez: anahtar kişi adına ya da işlem tipine düşerse ilk onaydan sonra
            // ilgisiz satırlar da aynı hesaba çözülürdü.
            if (aciklamaKurali is { UnvanCikarilsin: false } || vergiSatiri)
                baglam.AnahtarUretilmesin = true;

            baglam.Sablon = _aciklamaUretici.SablonBul(ayrilan.IslemTipi, sablonlar, ayrilan.HamAciklama);

            // Bankalar arası hareketlerde açıklamada unvan yerine banka adı geçer;
            // bu yüzden banka, açıklama üretiminden önce bulunur.
            baglam.BankaAdi = _eslestirici.BankaBul(baglam, veri)?.BankaAdi;

            var aciklama = _aciklamaUretici.Uret(baglam);
            var eslestirme = _eslestirici.Coz(baglam, veri);
            var cekirdek = HesapEslestirici.AnahtarCekirdek(baglam);

            return new EkstreSatiri
            {
                EkstreYuklemeId = yuklemeId,
                SiraNo = ayrilan.SiraNo,
                KaynakSatirNo = ayrilan.KaynakSatirNo,
                Tarih = ayrilan.Tarih,
                Yon = ayrilan.Yon,
                Tutar = ayrilan.Tutar,
                IslemTipi = Normalizasyon.Kirp(ayrilan.IslemTipi, 150),
                HamAciklama = ayrilan.HamAciklama,
                KarsiIban = karsiIban,
                KarsiVkn = ayrilan.KarsiVkn,
                Kanal = Normalizasyon.Kirp(ayrilan.Kanal, 100) is { Length: > 0 } k ? k : null,
                UretilenAciklama = aciklama,
                CikarilanUnvan = baglam.Unvan,
                AnahtarCekirdek = Normalizasyon.Kirp(cekirdek, 200) is { Length: > 0 } c ? c : null,
                AyirtEdiciEk = eslestirme.AyirtEdiciEk,
                OnerilenHesapKodu = eslestirme.HesapKodu,
                OnerilenHesapAdi = eslestirme.HesapAdi,
                GuvenSkoru = Math.Round(eslestirme.Guven, 4),
                KaynakKatman = eslestirme.Katman,
                IkinciAdayKodu = eslestirme.IkinciAdayKodu,
                IkinciAdayAdi = eslestirme.IkinciAdayAdi,
                IkinciAdaySkoru = eslestirme.IkinciAdaySkoru is decimal s ? Math.Round(s, 4) : null,
                Adaylar = AdaylariYaz(eslestirme.Adaylar),
                BelirsizlikAnahtari = eslestirme.BelirsizlikAnahtari,
                AdayKumesiOzeti = eslestirme.AdayKumesiOzeti,
                Durum = eslestirme.Durum
            };
        }

        /// <summary>
        /// Satırın karşı IBAN'ı. Ayrıştırıcı metindeki <b>ilk</b> IBAN'ı veriyor; virman ve
        /// döviz alış/satış satırlarında ilk IBAN ekstrenin kendi hesabı oluyor ("… TR40 …
        /// nolu hesabından TR80 … nolu hesabına … döviz alış"). Kendi IBAN'ı karşı taraf
        /// değildir: elenir ve sıradaki IBAN'a bakılır.
        ///
        /// Hesabın IBAN'ı Tanımlar'da boşsa eleme yapılamaz; ayrıştırıcının bulduğu değer
        /// olduğu gibi kalır.
        /// </summary>
        private static string? KarsiIbanSec(AyrilanSatir ayrilan, EslestirmeVerisi veri)
        {
            var kendi = veri.IslenenIbanAnahtari;
            if (kendi.Length == 0) return ayrilan.KarsiIban;

            foreach (var iban in Normalizasyon.IbanlariBul(ayrilan.HamAciklama))
                if (Normalizasyon.IbanAnahtar(iban) != kendi) return iban;

            return Normalizasyon.IbanAnahtar(ayrilan.KarsiIban) == kendi ? null : ayrilan.KarsiIban;
        }

        // ---- Listeleme ----

        public async Task<List<EkstreYuklemeDto>> GetYuklemelerAsync(CancellationToken ct = default)
        {
            var yuklemeler = await Yuklemeler.AsNoTracking()
                .Include(y => y.BankaHesabi)
                .OrderByDescending(y => y.Id)
                .Select(y => new
                {
                    Yukleme = y,
                    // Dosya içeriği (megabaytlarca) listeye çekilmesin; yalnız varlığı gerekiyor.
                    KaynakDosyaVar = y.DosyaIcerik != null
                })
                .ToListAsync(ct);

            if (yuklemeler.Count == 0) return new();

            var idler = yuklemeler.Select(y => y.Yukleme.Id).ToList();
            var sayaclar = await SayaclariYukleAsync(idler, ct);

            return yuklemeler
                .Select(y => Esle(y.Yukleme,
                                  sayaclar.TryGetValue(y.Yukleme.Id, out var s) ? s : new EkstreSayaclariDto(),
                                  y.KaynakDosyaVar))
                .ToList();
        }

        public async Task<EkstreYuklemeDto?> GetYuklemeAsync(int id, CancellationToken ct = default)
        {
            var yukleme = await Yuklemeler.AsNoTracking()
                .Include(y => y.BankaHesabi)
                .Where(y => y.Id == id)
                .Select(y => new { Yukleme = y, KaynakDosyaVar = y.DosyaIcerik != null })
                .FirstOrDefaultAsync(ct);

            if (yukleme is null) return null;

            var sayaclar = await SayaclariYukleAsync(new[] { id }, ct);
            return Esle(yukleme.Yukleme,
                        sayaclar.TryGetValue(id, out var s) ? s : new EkstreSayaclariDto(),
                        yukleme.KaynakDosyaVar);
        }

        public async Task<List<EkstreSatirDto>?> GetSatirlarAsync(int ekstreId, SatirDurum? durum,
                                                                   int? kategoriId = null,
                                                                   CancellationToken ct = default)
        {
            if (!await Yuklemeler.AnyAsync(y => y.Id == ekstreId, ct)) return null;

            var sorgu = Satirlar.AsNoTracking().Where(s => s.EkstreYuklemeId == ekstreId);
            if (durum is SatirDurum d) sorgu = sorgu.Where(s => s.Durum == d);

            var satirlar = await sorgu.OrderBy(s => s.SiraNo).ToListAsync(ct);
            var cozucu = await KategoriCozucuKurAsync(ct);

            var dtolar = satirlar.Select(s => Esle(s, cozucu)).ToList();

            // Süzme bellekte: kategori satırda saklanmıyor, hesap kodundan türetiliyor.
            // Bir yüklemenin satır sayısı birkaç yüz olduğu için tarama maliyeti önemsiz.
            return kategoriId is int id
                ? dtolar.Where(d => d.IslemKategorisiId == id).ToList()
                : dtolar;
        }

        /// <summary>
        /// Kategori çözücüsü: ana grup → kategori indeksi. Kategori tablosu global ve
        /// yirmi satır civarında; istek başına bir kez okunur.
        /// </summary>
        private async Task<KategoriCozucu> KategoriCozucuKurAsync(CancellationToken ct)
            => KategoriCozucu.Kur(await _db.EkstreIslemKategorileri.AsNoTracking().ToListAsync(ct));

        // ---- Onay ve öğrenme ----

        /// <summary>
        /// Satırı onaylar ve öğrenme kaydını yazar. Kullanıcı önerilenden farklı bir kod
        /// seçtiyse öğrenme kaydı da güncellenir — sadece satır değil; aksi hâlde hata
        /// gelecek ay geri gelirdi.
        ///
        /// Hesap planında olmayan kod **kabul edilir** (ORKA'da yeni açılmış olabilir) ama
        /// öğrenilmez: doğrulanmamış kod kalıcılaşmasın.
        /// </summary>
        public async Task<EkstreSatirDto?> OnaylaAsync(int satirId, string hesapKodu, bool kisiYonlendir = false,
                                                      CancellationToken ct = default)
        {
            var satir = await SatirGetirAsync(satirId, ct);
            if (satir is null) return null;

            var kod = Normalizasyon.HesapKoduNormalize(hesapKodu);
            if (kod.Length == 0)
                throw new BankaEkstreKuralException(nameof(hesapKodu), "Hesap kodu boş olamaz.");

            var plan = await Plan.AsNoTracking().FirstOrDefaultAsync(h => h.Kod == kod, ct);
            var planDolu = await Plan.AnyAsync(ct);
            var bilinmeyenKod = plan is null && planDolu;

            satir.OnaylananHesapKodu = kod;
            satir.OnaylananHesapAdi = plan?.Ad;
            satir.OnayTarihi = DateTime.Now;
            satir.OnaylayanKullanici = Normalizasyon.Kirp(_kullanici.UserName ?? _kullanici.UserId, 100);
            satir.Durum = SatirDurum.Onaylandi;
            satir.KaynakKatman = KaynakKatman.Kullanici;

            if (!bilinmeyenKod)
                await _ogrenme.OgrenAsync(satir, kod, plan?.Ad, ct);

            var yonlendirmeUyarisi = kisiYonlendir
                ? await KisiYonlendirmesiYazAsync(satir, kod, plan?.Ad, ct)
                : null;

            await _db.SaveChangesAsync(ct);

            var dto = Esle(satir, await KategoriCozucuKurAsync(ct));
            if (bilinmeyenKod)
                dto.Uyari = $"'{kod}' hesap planında yok — ORKA'da yeni açıldıysa hesap planını güncelleyin. " +
                            "Kod kaydedildi ama öğrenilmedi.";

            if (yonlendirmeUyarisi is not null)
                dto.Uyari = dto.Uyari is null ? yonlendirmeUyarisi : dto.Uyari + " " + yonlendirmeUyarisi;

            return dto;
        }

        /// <summary>
        /// Onay ekranındaki "bu kişiyi hep bu hesaba yönlendir" kısayolu. Kullanıcının
        /// Tanımlar ekranına gidip aynı ismi elle yazmasına gerek kalmasın diye kayıt
        /// buradan oluşturulur; yön, onaylanan satırın yönünden gelir.
        ///
        /// İsim satırın <b>çıkarılan unvanından</b> alınır: satırda kişi adı okunamamışsa
        /// yönlendirme yazılamaz (uyarı döner). Aynı isim + yön için kayıt zaten varsa
        /// üzerine yazılır — kullanıcı fikrini değiştirmiş demektir.
        ///
        /// Kaydetmez; çağıran <c>SaveChangesAsync</c> ile birlikte yazar.
        /// </summary>
        private async Task<string?> KisiYonlendirmesiYazAsync(EkstreSatiri satir, string kod, string? ad, CancellationToken ct)
        {
            var cekirdek = Normalizasyon.Cekirdek(satir.CikarilanUnvan);
            if (cekirdek.Length == 0)
                return "Bu satırda kişi adı okunamadığı için yönlendirme oluşturulmadı; " +
                       "Tanımlar > Kişi yönlendirmeleri'nden elle ekleyebilirsiniz.";

            cekirdek = Normalizasyon.Kirp(cekirdek, 200);
            var yon = satir.Yon == Yon.Giren ? YonlendirmeYonu.Giren : YonlendirmeYonu.Cikan;

            var mevcut = await _db.EkstreKisiYonlendirmeleri
                .FirstOrDefaultAsync(k => k.FirmaId == _kapsam.FirmaId && k.IsimCekirdegi == cekirdek && k.Yon == yon, ct);

            if (mevcut is null)
            {
                mevcut = new KisiYonlendirme { FirmaId = _kapsam.FirmaId, IsimCekirdegi = cekirdek, Yon = yon };
                _db.EkstreKisiYonlendirmeleri.Add(mevcut);
            }

            mevcut.Isim = Normalizasyon.Kirp(satir.CikarilanUnvan, 200);
            mevcut.HesapKodu = kod;
            mevcut.HesapAdi = ad;
            mevcut.Aktif = true;

            return null;
        }

        /// <summary>
        /// Bankalar arası transferin karşı bacağı başka bankanın ekstresinde işlendiyse
        /// satır dışa aktarımdan düşer. Öğrenme kaydı yazılmaz — bu bir hesap kararı değil.
        /// </summary>
        public async Task<EkstreSatirDto?> DigerBankadaAsync(int satirId, CancellationToken ct = default)
        {
            var satir = await SatirGetirAsync(satirId, ct);
            if (satir is null) return null;

            satir.Durum = SatirDurum.DigerBankada;
            satir.OnayTarihi = DateTime.Now;
            satir.OnaylayanKullanici = Normalizasyon.Kirp(_kullanici.UserName ?? _kullanici.UserId, 100);

            await _db.SaveChangesAsync(ct);
            return Esle(satir, await KategoriCozucuKurAsync(ct));
        }

        // ---- Dışa aktarım ----

        /// <summary>
        /// Dışa aktarımın ikinci parçası: karşı hesap kodu listesi. Çözülemedi veya onay
        /// bekleyen satır varsa üretilmez — eksik listeyle ORKA'ya gitmenin anlamı yok.
        /// </summary>
        public async Task<DisaAktarimSonucDto?> DisaAktarAsync(int ekstreId, CancellationToken ct = default)
        {
            var yukleme = await Yuklemeler.AsNoTracking()
                .Include(y => y.BankaHesabi)
                .Where(y => y.Id == ekstreId)
                .Select(y => new { Yukleme = y, KaynakDosyaVar = y.DosyaIcerik != null })
                .FirstOrDefaultAsync(ct);

            if (yukleme is null) return null;

            var satirlar = await AktarilacakSatirlarAsync(ekstreId, ct);
            var aktarilacak = OrkayaGidenSatirlar(satirlar);
            var bankaKodu = yukleme.Yukleme.BankaHesabi?.OrkaHesapKodu ?? string.Empty;

            return new DisaAktarimSonucDto
            {
                EkstreId = ekstreId,
                DosyaAdi = yukleme.Yukleme.DosyaAdi,
                SatirSayisi = aktarilacak.Count,
                DigerBankadaAtlanan = satirlar.Count - aktarilacak.Count,
                // Düzeltilmiş ekstre artık kaynak dosyadan üretilmiyor, sıfırdan yazılıyor;
                // kaynak dosya saklanmamış olsa da hazır (KARARLAR §82).
                DuzeltilmisEkstreHazir = true,
                Satirlar = aktarilacak.Select(s => new OrkaSatirDto
                {
                    SiraNo = s.SiraNo,
                    Tarih = s.Tarih,
                    // Robotun satır doğrulaması açıklamaya bakıyor; çıkarılmaz.
                    Aciklama = s.UretilenAciklama ?? string.Empty,
                    Yon = s.Yon,
                    Tutar = s.Tutar,
                    KarsiHesapKodu = s.EtkinHesapKodu ?? string.Empty,
                    HesapAdi = s.OnaylananHesapAdi ?? s.OnerilenHesapAdi,
                    BankaHesapKodu = bankaKodu
                }).ToList()
            };
        }

        // ORKA Veri Transferi'nin beklediği dört kolon (1 tabanlı).
        private const int KolonTarih = 1;
        private const int KolonAciklama = 2;
        private const int KolonGiren = 3;
        private const int KolonCikan = 4;

        /// <summary>Tutar hücrelerinin görünüm biçimi; hücrenin kendisi sayısaldır.</summary>
        private const string TutarBicimi = "#,##0.00";

        /// <summary>
        /// Dışa aktarımın birinci parçası: ORKA Veri Transferi ekranının beklediği
        /// <b>dört kolonlu sade dosya</b> — <c>Tarih | Açıklama | Giren | Çıkan</c>.
        /// Başlık 1. satırda, veri 2. satırdan başlar; hesap künyesi bloğu yoktur.
        ///
        /// <b>Neden orijinal dosya artık kopyalanmıyor?</b> Önceki sürüm kaynak xlsx'i açıp
        /// yalnız açıklama kolonunu değiştiriyordu; çıktı bankanın 17 kolonu ve 6 satırlık
        /// künye bloğuyla geliyordu. ORKA bu yapıyı okumuyor. Üstelik kaynak dosyayı açıp
        /// yeniden kaydetmek dosyayı bozuyordu: üretilen dosya ClosedXML ile bile yeniden
        /// açılamıyor (stil tablosu round-trip'te tutarsızlaşıyor), ORKA da satırların
        /// yalnız bir kısmını okuyordu. Dosya artık sıfırdan üretiliyor; kaynak dosyaya ve
        /// <c>AciklamaKolonu</c> bilgisine hiç ihtiyaç yok (bkz. KARARLAR §82).
        ///
        /// <b>Satır kümesi kod listesiyle birebir aynı</b> (<see cref="OrkayaGidenSatirlar"/>):
        /// robot kod listesini ORKA gridine satır sırasına göre yazıyor; iki dosyanın satır
        /// sayısı veya sırası ayrışırsa kodlar yanlış satırlara gider.
        ///
        /// Tutarlar <b>sayısal hücre</b> olarak yazılır (metin hücreyi ORKA yanlış
        /// ayrıştırabiliyor); yönüne göre yalnız bir kolon dolar, diğeri boş kalır. Tutar
        /// veritabanında her zaman pozitiftir, işaret <see cref="Yon"/> alanındadır.
        /// </summary>
        public async Task<EkstreDosyasi?> DuzeltilmisEkstreAsync(int ekstreId, CancellationToken ct = default)
        {
            var yukleme = await Yuklemeler.AsNoTracking().FirstOrDefaultAsync(y => y.Id == ekstreId, ct);
            if (yukleme is null) return null;

            var satirlar = OrkayaGidenSatirlar(await AktarilacakSatirlarAsync(ekstreId, ct));

            using var kitap = new XLWorkbook();
            var sayfa = kitap.Worksheets.Add("Ekstre");

            sayfa.Cell(1, KolonTarih).Value = "Tarih";
            sayfa.Cell(1, KolonAciklama).Value = "Açıklama";
            sayfa.Cell(1, KolonGiren).Value = "Giren";
            sayfa.Cell(1, KolonCikan).Value = "Çıkan";
            sayfa.Row(1).Style.Font.Bold = true;

            var satirNo = 2;
            foreach (var satir in satirlar)
            {
                sayfa.Cell(satirNo, KolonTarih).Value = satir.Tarih;
                sayfa.Cell(satirNo, KolonTarih).Style.DateFormat.Format = "dd.MM.yyyy";

                sayfa.Cell(satirNo, KolonAciklama).Value =
                    Normalizasyon.Kirp(satir.UretilenAciklama, AciklamaUretici.EnFazlaUzunluk);

                var tutarKolonu = satir.Yon == Yon.Giren ? KolonGiren : KolonCikan;
                sayfa.Cell(satirNo, tutarKolonu).Value = satir.Tutar;
                sayfa.Cell(satirNo, tutarKolonu).Style.NumberFormat.Format = TutarBicimi;

                satirNo++;
            }

            sayfa.ColumnsUsed().AdjustToContents(1, 60);

            using var cikti = new MemoryStream();
            kitap.SaveAs(cikti);

            var ad = Path.GetFileNameWithoutExtension(yukleme.DosyaAdi);
            return new EkstreDosyasi($"{ad}-duzeltilmis.xlsx", cikti.ToArray());
        }

        /// <summary>
        /// Analiz dökümü (yeni). "Kod listesi" ve "Düzeltilmiş ekstre" onay bekleyen veya
        /// çözülemeyen satır varken üretilmiyor — doğru kural, korunuyor. Ama sistemin ne
        /// önerdiğini <b>onaydan önce</b> görebilmek gerekiyor: bu döküm durumdan bağımsız
        /// olarak tüm satırları verir.
        ///
        /// Dosya ORKA'ya yüklenmez; kolonları da onun için değil, inceleme için seçildi
        /// (hangi katman ne önerdi, kaç aday vardı, satır hangi durumda kaldı).
        /// </summary>
        public async Task<EkstreDosyasi?> AnalizDokumuAsync(int ekstreId, CancellationToken ct = default)
        {
            var yukleme = await Yuklemeler.AsNoTracking()
                .FirstOrDefaultAsync(y => y.Id == ekstreId, ct);

            if (yukleme is null) return null;

            var satirlar = await Satirlar.AsNoTracking()
                .Where(s => s.EkstreYuklemeId == ekstreId)
                .OrderBy(s => s.SiraNo)
                .ToListAsync(ct);

            using var kitap = new XLWorkbook();
            var sayfa = kitap.Worksheets.Add("Analiz");

            var basliklar = new[]
            {
                "SiraNo", "Tarih", "Yon", "Tutar", "HamAciklama", "UretilenAciklama",
                "OnerilenHesapKodu", "OnerilenHesapAdi", "GuvenSkoru", "KaynakKatman",
                "Durum", "AdaySayisi"
            };

            for (var i = 0; i < basliklar.Length; i++)
            {
                sayfa.Cell(1, i + 1).Value = basliklar[i];
                sayfa.Cell(1, i + 1).Style.Font.Bold = true;
            }

            var satirNo = 2;
            foreach (var satir in satirlar)
            {
                // Onaylanan kod varsa dışa aktarıma giden odur; analiz de onu göstermeli.
                sayfa.Cell(satirNo, 1).Value = satir.SiraNo;
                sayfa.Cell(satirNo, 2).Value = satir.Tarih;
                sayfa.Cell(satirNo, 2).Style.DateFormat.Format = "dd.MM.yyyy";
                sayfa.Cell(satirNo, 3).Value = satir.Yon == Yon.Giren ? "Giren" : "Çıkan";
                sayfa.Cell(satirNo, 4).Value = satir.Tutar;
                sayfa.Cell(satirNo, 5).Value = satir.HamAciklama;
                sayfa.Cell(satirNo, 6).Value = satir.UretilenAciklama ?? string.Empty;
                sayfa.Cell(satirNo, 7).Value = satir.EtkinHesapKodu ?? string.Empty;
                sayfa.Cell(satirNo, 8).Value = satir.OnaylananHesapAdi ?? satir.OnerilenHesapAdi ?? string.Empty;
                sayfa.Cell(satirNo, 9).Value = satir.GuvenSkoru;
                sayfa.Cell(satirNo, 10).Value = satir.KaynakKatman.ToString();
                sayfa.Cell(satirNo, 11).Value = satir.Durum.ToString();
                sayfa.Cell(satirNo, 12).Value = AdaylariOku(satir.Adaylar).Count;
                satirNo++;
            }

            sayfa.ColumnsUsed().AdjustToContents(1, 60);

            using var cikti = new MemoryStream();
            kitap.SaveAs(cikti);

            var dosyaAdi = Path.GetFileNameWithoutExtension(yukleme.DosyaAdi);
            return new EkstreDosyasi($"{dosyaAdi}-analiz.xlsx", cikti.ToArray());
        }

        public async Task<bool> SilAsync(int ekstreId, CancellationToken ct = default)
        {
            var yukleme = await Yuklemeler.FirstOrDefaultAsync(y => y.Id == ekstreId, ct);
            if (yukleme is null) return false;

            // Satırlar cascade ile gider; öğrenilen kayıtlar kalır (bilgi kaybolmasın).
            _db.EkstreYuklemeler.Remove(yukleme);
            await _db.SaveChangesAsync(ct);
            return true;
        }

        // ---- Yardımcılar ----

        /// <summary>
        /// ORKA'ya giden <b>ortak</b> satır kümesi: kod listesi ve düzeltilmiş ekstre
        /// aynı satırları aynı sırayla içermek zorunda. Robot kod listesini ORKA gridine
        /// satır sırasına göre yazdığı için iki çıktının ayrışması kodları yanlış satırlara
        /// yazdırır (bkz. KARARLAR §82).
        ///
        /// Sıra dosyadaki sıradır (<see cref="EkstreSatiri.SiraNo"/>); "diğer bankada"
        /// işaretli satırlar iki çıktıdan da düşer (§61).
        /// </summary>
        private static List<EkstreSatiri> OrkayaGidenSatirlar(IEnumerable<EkstreSatiri> satirlar)
            => satirlar
                .Where(s => s.Durum != SatirDurum.DigerBankada)
                .OrderBy(s => s.SiraNo)
                .ToList();

        /// <summary>Dışa aktarıma girecek satırlar; eksik satır varsa 400'e karşılık gelen kural hatası.</summary>
        private async Task<List<EkstreSatiri>> AktarilacakSatirlarAsync(int ekstreId, CancellationToken ct)
        {
            var satirlar = await Satirlar.AsNoTracking()
                .Where(s => s.EkstreYuklemeId == ekstreId)
                .OrderBy(s => s.SiraNo)
                .ToListAsync(ct);

            var eksik = satirlar.Count(s => s.Durum is SatirDurum.OnayBekliyor or SatirDurum.Cozulemedi);
            if (eksik > 0)
                throw new BankaEkstreKuralException("satirlar",
                    $"{eksik} satır hâlâ çözülmemiş (onay bekleyen veya çözülemeyen). " +
                    "Eksik listeyle dışa aktarım yapılmaz; önce onay ekranını tamamlayın.");

            return satirlar;
        }

        private static async Task<byte[]> BaytlariOkuAsync(Stream dosya, CancellationToken ct)
        {
            if (dosya is MemoryStream hazir) return hazir.ToArray();

            using var bellek = new MemoryStream();
            if (dosya.CanSeek) dosya.Position = 0;
            await dosya.CopyToAsync(bellek, ct);
            return bellek.ToArray();
        }

        /// <summary>
        /// Satiri, bagli oldugu yukleme secili firmaya aitse getirir. Satirin kendi FirmaId
        /// alani yok; baska firmanin satir id'si gonderilirse null doner.
        /// </summary>
        private Task<EkstreSatiri?> SatirGetirAsync(int satirId, CancellationToken ct)
            => Satirlar.Where(s => s.Id == satirId).FirstOrDefaultAsync(ct);

        /// <summary>
        /// Hesap sahibinin kimliği: ana unvan + takma adlar. Firma bazlı ve tek kez girilir:
        /// ekstresi işlenen hesapta boşsa aynı firmanın dolu olan başka bir hesabından okunur.
        /// Böylece kullanıcı her banka hesabına ayrı ayrı yazmak zorunda kalmaz.
        /// </summary>
        private async Task<HesapSahibiKimligi> HesapSahibiKimligiBulAsync(BankaHesabi hesap, CancellationToken ct)
        {
            if (!string.IsNullOrWhiteSpace(hesap.HesapSahibiUnvani))
                return HesapSahibiKimligi.Kur(hesap.HesapSahibiUnvani, hesap.HesapSahibiTakmaAdlari);

            var digeri = await Hesaplar.AsNoTracking()
                .Where(h => h.HesapSahibiUnvani != null && h.HesapSahibiUnvani != string.Empty)
                .OrderBy(h => h.Id)
                .Select(h => new { h.HesapSahibiUnvani, h.HesapSahibiTakmaAdlari })
                .FirstOrDefaultAsync(ct);

            // Takma adlar ekstresi işlenen hesapta da tanımlı olabilir; ikisi birleştirilir.
            return HesapSahibiKimligi.Kur(
                HesapSahibiKimligi.Ayikla(digeri?.HesapSahibiUnvani)
                    .Concat(HesapSahibiKimligi.Ayikla(digeri?.HesapSahibiTakmaAdlari))
                    .Concat(HesapSahibiKimligi.Ayikla(hesap.HesapSahibiTakmaAdlari)));
        }

        private async Task<EslestirmeVerisi> EslestirmeVerisiYukleAsync(
            BankaHesabi hesap, HesapSahibiKimligi hesapSahibi, CancellationToken ct)
            => new()
            {
                Eslesmeler = await _db.EkstreHesapEslesmeleri.AsNoTracking()
                                        .Where(e => e.FirmaId == _kapsam.FirmaId).ToListAsync(ct),
                BankaHesaplari = await Hesaplar.AsNoTracking().ToListAsync(ct),
                SabitKurallar = await SabitKurallariYukleAsync(hesap.ParserTipi, ct),
                HesapPlani = await Plan.AsNoTracking().Where(h => h.Aktif).ToListAsync(ct),
                // Vergi kodlari GLOBAL (bkz. KARARLAR 70): kodun anlami firmadan firmaya degismez.
                VergiKodlari = await _db.EkstreVergiKodlari.AsNoTracking().Where(v => v.Aktif)
                                        .OrderBy(v => v.Sira).ToListAsync(ct),
                KisiYonlendirmeleri = await _db.EkstreKisiYonlendirmeleri.AsNoTracking()
                                        .Where(k => k.FirmaId == _kapsam.FirmaId && k.Aktif).ToListAsync(ct),
                IslenenBankaHesabiId = hesap.Id,
                IbanKatmaniAktif = hesap.IbanKatmaniAktif,
                VknKatmaniAktif = hesap.VknKatmaniAktif,
                HesapSahibi = hesapSahibi
            };

        // Üç yapılandırma tablosu da aynı ayrıştırıcı sözleşmesini kullanır: ParserTipi
        // BOŞ ise kayıt tüm bankalarda geçerlidir, doluysa yalnız o bankada. Vakıfbank'a
        // özel bir desen Ziraat ekstresinde çalışmamalı; ortak kurallar ise (banka
        // masrafı, komisyon) her bankada tek satırla tanımlanabilmeli.
        //
        // Sıra eşitse BANKAYA ÖZEL kayıt önce denenir: aynı sıradaki genel kural, o banka
        // için özellikle yazılmış kaydı gölgelememeli. Tüketiciler listeyi Sira'ya göre
        // yeniden sıralarken LINQ OrderBy kararlı olduğu için bu ikincil sıra korunur.
        private Task<List<AciklamaSablonu>> SablonlariYukleAsync(string parserTipi, CancellationToken ct)
            => _db.EkstreAciklamaSablonlari.AsNoTracking()
                .Where(s => (s.ParserTipi == "" || s.ParserTipi == parserTipi) && s.Aktif)
                .OrderBy(s => s.Sira).ThenBy(s => s.ParserTipi == "" ? 1 : 0).ThenBy(s => s.Id)
                .ToListAsync(ct);

        private Task<List<UnvanDeseni>> DesenleriYukleAsync(string parserTipi, CancellationToken ct)
            => _db.EkstreUnvanDesenleri.AsNoTracking()
                .Where(d => (d.ParserTipi == "" || d.ParserTipi == parserTipi) && d.Aktif)
                .OrderBy(d => d.Sira).ThenBy(d => d.ParserTipi == "" ? 1 : 0).ThenBy(d => d.Id)
                .ToListAsync(ct);

        /// <summary>
        /// Sabit kurallar da bankaya göre süzülür. Eskiden tablonun tamamı yükleniyordu:
        /// tek banka varken farkı yoktu, ikinci banka eklendiğinde Vakıfbank'ın kuralları
        /// diğer bankanın ekstresinde de çalışırdı.
        /// </summary>
        private Task<List<SabitKural>> SabitKurallariYukleAsync(string? parserTipi, CancellationToken ct)
        {
            var tip = parserTipi ?? string.Empty;

            return _db.EkstreSabitKurallar.AsNoTracking()
                .Where(k => k.ParserTipi == "" || k.ParserTipi == tip)
                .OrderBy(k => k.Sira).ThenBy(k => k.ParserTipi == "" ? 1 : 0).ThenBy(k => k.Id)
                .ToListAsync(ct);
        }

        private async Task<Dictionary<int, EkstreSayaclariDto>> SayaclariYukleAsync(IReadOnlyCollection<int> ekstreIdler, CancellationToken ct)
        {
            var ham = await Satirlar.AsNoTracking()
                .Where(s => ekstreIdler.Contains(s.EkstreYuklemeId))
                .GroupBy(s => new { s.EkstreYuklemeId, s.Durum })
                .Select(g => new { g.Key.EkstreYuklemeId, g.Key.Durum, Adet = g.Count() })
                .ToListAsync(ct);

            var sonuc = new Dictionary<int, EkstreSayaclariDto>();
            foreach (var satir in ham)
            {
                if (!sonuc.TryGetValue(satir.EkstreYuklemeId, out var sayac))
                    sonuc[satir.EkstreYuklemeId] = sayac = new EkstreSayaclariDto();

                sayac.Toplam += satir.Adet;
                switch (satir.Durum)
                {
                    case SatirDurum.Otomatik: sayac.Otomatik += satir.Adet; break;
                    case SatirDurum.OnayBekliyor: sayac.OnayBekleyen += satir.Adet; break;
                    case SatirDurum.Onaylandi: sayac.Onaylanan += satir.Adet; break;
                    case SatirDurum.Cozulemedi: sayac.Cozulemeyen += satir.Adet; break;
                    case SatirDurum.DigerBankada: sayac.DigerBankada += satir.Adet; break;
                }
            }

            return sonuc;
        }

        // ---- Aday listesi (JSON) ----

        private static readonly JsonSerializerOptions AdaySecenekleri = new(JsonSerializerDefaults.Web);

        /// <summary>
        /// Aday listesi JSON'u. Eskiden yalnız <b>birden fazla</b> aday saklanıyordu; tek
        /// aday kaybolduğu için "kural grubu dışında birebir bulunan tek kişi"
        /// (<c>331 02 Abdulkadir Sayıcı</c>) onay ekranında hiç görünmüyordu. Artık tek
        /// aday da yazılır; boş liste yine null.
        /// </summary>
        private static string? AdaylariYaz(IReadOnlyList<AdayKayit> adaylar)
            => adaylar.Count == 0 ? null : JsonSerializer.Serialize(adaylar, AdaySecenekleri);

        private static List<AdayDto> AdaylariOku(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new();

            try
            {
                var kayitlar = JsonSerializer.Deserialize<List<AdayKayit>>(json, AdaySecenekleri);
                return kayitlar?.Select(a => new AdayDto { Kod = a.Kod, Ad = a.Ad, Skor = Math.Round(a.Skor, 4) }).ToList()
                       ?? new List<AdayDto>();
            }
            catch (JsonException)
            {
                // Bozuk kayıt tüm listeyi düşürmesin; onay ekranı iki adayla devam eder.
                return new();
            }
        }

        private static EkstreYuklemeDto Esle(EkstreYukleme y, EkstreSayaclariDto sayaclar, bool kaynakDosyaVar) => new()
        {
            Id = y.Id,
            BankaHesabiId = y.BankaHesabiId,
            BankaAdi = y.BankaHesabi?.BankaAdi ?? string.Empty,
            DosyaAdi = y.DosyaAdi,
            YuklemeTarihi = y.YuklemeTarihi,
            DonemBaslangic = y.DonemBaslangic,
            DonemBitis = y.DonemBitis,
            SatirSayisi = y.SatirSayisi,
            Durum = y.Durum,
            Uyarilar = y.Uyarilar,
            Sayaclar = sayaclar,
            KaynakDosyaVar = kaynakDosyaVar
        };

        /// <summary>
        /// Satır DTO'su. Kategori etiketi <paramref name="cozucu"/> verildiğinde doldurulur;
        /// tek satır dönen uçlarda (onay, diğer bankada) çözücü ayrıca kurulur.
        /// </summary>
        private static EkstreSatirDto Esle(EkstreSatiri s, KategoriCozucu? cozucu = null) => new()
        {
            Id = s.Id,
            SiraNo = s.SiraNo,
            Tarih = s.Tarih,
            Yon = s.Yon,
            Tutar = s.Tutar,
            IslemTipi = s.IslemTipi,
            HamAciklama = s.HamAciklama,
            KarsiIban = s.KarsiIban,
            KarsiVkn = s.KarsiVkn,
            Kanal = s.Kanal,
            UretilenAciklama = s.UretilenAciklama,
            CikarilanUnvan = s.CikarilanUnvan,
            OnerilenHesapKodu = s.OnerilenHesapKodu,
            OnerilenHesapAdi = s.OnerilenHesapAdi,
            GuvenSkoru = s.GuvenSkoru,
            KaynakKatman = s.KaynakKatman,
            IkinciAdayKodu = s.IkinciAdayKodu,
            IkinciAdayAdi = s.IkinciAdayAdi,
            IkinciAdaySkoru = s.IkinciAdaySkoru,
            Adaylar = AdaylariOku(s.Adaylar),
            IslemKategorisiId = (cozucu ?? KategoriCozucu.Bos).Coz(s.OnaylananHesapKodu ?? s.OnerilenHesapKodu).Id,
            IslemKategorisiAdi = (cozucu ?? KategoriCozucu.Bos).Coz(s.OnaylananHesapKodu ?? s.OnerilenHesapKodu).Ad,
            OnaylananHesapKodu = s.OnaylananHesapKodu,
            OnaylananHesapAdi = s.OnaylananHesapAdi,
            Durum = s.Durum,
            AnahtarCekirdek = s.AnahtarCekirdek,
            AyirtEdiciEk = s.AyirtEdiciEk,
            BelirsizlikAnahtari = s.BelirsizlikAnahtari
        };
    }
}
