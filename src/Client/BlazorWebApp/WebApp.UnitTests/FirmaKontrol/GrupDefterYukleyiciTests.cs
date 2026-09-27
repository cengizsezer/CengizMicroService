using System.Diagnostics;
using System.Reflection;
using WebApp.Application.Services;
using WebApp.Application.Services.FirmaKontrol;
using WebApp.Application.Services.Interfaces;
using WebApp.Domain.Models.FirmaKontrol;
using WebApp.Shared.Dto.FirmaKontrol;

namespace WebApp.UnitTests.FirmaKontrol
{
    /// <summary>
    /// Grup görünümü yüklemesi: paralel okuma, firma başına zaman sınırı, aşamalı teslim ve
    /// mizanı olmayan firmada ağır zincirin çalışmaması.
    /// </summary>
    public class GrupDefterYukleyiciTests
    {
        private const int BuFirma = 3;
        private const int AdayFirma = 7;
        private const int EkipFirma = 8;

        private static readonly TimeSpan KisaSinir = TimeSpan.FromMilliseconds(300);

        private static HesapDugumu Gelir(string kod, decimal tutar) =>
            HesapDugumu.Olustur(kod, "Satışlar", 0m, tutar, -tutar)!;

        private static readonly List<HesapDugumu> StratejiAgaci = new()
        {
            Gelir("600", 21_458_746.99m),
            Gelir("600 1", 21_458_746.99m),
            Gelir("600 1 20", 12_194_054.99m),
            Gelir("600 1 21", 9_264_692.00m),
            Gelir("601", 1_228_856.20m),
            Gelir("610", -666_790.50m),
        };

        private static EtiketBoyutuDto Boyut() => new()
        {
            Id = 1,
            KapsamHesaplari = "600,601,610",
            Degerler = new()
            {
                new() { Id = 10, Ad = "Kurumsal Strateji", Sira = 1, FirmaId = BuFirma },
                new() { Id = 11, Ad = "PKF Aday", Sira = 2, FirmaId = AdayFirma },
                new() { Id = 12, Ad = "PKF Ekip", Sira = 3, FirmaId = EkipFirma },
            },
            Kurallar = new()
            {
                new() { Id = 1, Sira = 1, EslesmeTipi = EtiketEslesmeTipi.KodDeseni, Desen = "600 1 20*", DegerId = 10 },
                new() { Id = 2, Sira = 2, EslesmeTipi = EtiketEslesmeTipi.KodDeseni, Desen = "600 1 21*", DegerId = 11 },
            }
        };

        /// <summary>Hafif ağaç okumasının firma bazındaki davranışı.</summary>
        private sealed class Senaryo
        {
            public Func<int, CancellationToken, Task<HesapAgaciSonucu>> Hafif { get; init; } =
                (_, _) => Task.FromResult(new HesapAgaciSonucu(HesapAgaciDurumu.MizanYuklenmedi, Array.Empty<HesapDugumu>()));

            public int TamHidrasyonCagrisi;
            public int DigerFirmaBoyutCagrisi;

            public IFirmaKontrolService FirmaKontrol() => Sahte<IFirmaKontrolService>.Olustur((m, a) => m.Name switch
            {
                nameof(IFirmaKontrolService.GetHesapAgaciSonucuAsync) => KendiAgaci((int)a![0]!),
                nameof(IFirmaKontrolService.GetHesapAgaciHafifAsync) => Hafif((int)a![0]!, (CancellationToken)a[2]!),
                _ => throw new NotSupportedException(m.Name)
            });

            public IEtiketApiClient EtiketApi() => Sahte<IEtiketApiClient>.Olustur((m, a) =>
            {
                if (m.Name != nameof(IEtiketApiClient.GetBoyutlarAsync)) throw new NotSupportedException(m.Name);
                Interlocked.Increment(ref DigerFirmaBoyutCagrisi);
                return Task.FromResult(new List<EtiketBoyutuDto> { Boyut() });
            });

            private Task<HesapAgaciSonucu> KendiAgaci(int firmaId)
            {
                // Tam hidrasyon yolu yalnızca bakılan firma için kullanılabilir.
                if (firmaId != BuFirma) Interlocked.Increment(ref TamHidrasyonCagrisi);
                return Task.FromResult(new HesapAgaciSonucu(HesapAgaciDurumu.Yuklu, StratejiAgaci));
            }
        }

        private static Task<List<GrupGorunumu.Defter>> Yukle(Senaryo s, IReadOnlyList<int> firmalar, List<GrupGorunumu.Defter> gelisSirasi) =>
            GrupDefterYukleyici.YukleAsync(
                s.FirmaKontrol(), s.EtiketApi(), BuFirma, Boyut(), firmalar,
                id => $"Firma {id}",
                d => { lock (gelisSirasi) gelisSirasi.Add(d); },
                KisaSinir);

        [Fact]
        public async Task Takilan_firma_zaman_sinirinda_ulasilamadi_olur_kendi_satiri_once_gelir()
        {
            var s = new Senaryo
            {
                // İptale HİÇ uymayan, asla bitmeyen çağrı: sınır yine de kesin işlemeli.
                Hafif = (_, _) => new TaskCompletionSource<HesapAgaciSonucu>().Task
            };
            var gelis = new List<GrupGorunumu.Defter>();

            var sure = Stopwatch.StartNew();
            var defterler = await Yukle(s, new[] { BuFirma, AdayFirma }, gelis);
            sure.Stop();

            Assert.True(sure.Elapsed < TimeSpan.FromSeconds(3), $"Sonsuz bekleme: {sure.ElapsedMilliseconds} ms");

            // Aşamalı: bakılan firma takılan firmayı beklemeden teslim edildi.
            Assert.Equal(BuFirma, gelis[0].FirmaId);
            Assert.True(gelis[0].Yuklu);
            Assert.Equal(12_194_054.99m, gelis[0].Dagilim!.DegerToplamlari[10]);
            Assert.Equal(9_264_692.00m, gelis[0].Dagilim!.DegerToplamlari[11]);
            Assert.Equal(562_065.70m, gelis[0].Dagilim!.Atanmamis);
            Assert.Equal(22_020_812.69m, gelis[0].AnaHesapToplami);

            var aday = defterler.Single(d => d.FirmaId == AdayFirma);
            Assert.Equal(HesapAgaciDurumu.Ulasilamadi, aday.Durum);
            Assert.Contains("yanıt gelmedi", aday.Not);

            // Matris takılan satıra rağmen tutarlı çizilir.
            var m = GrupGorunumu.MatrisOlustur(defterler, Boyut().Degerler);
            Assert.False(m.CiftSayimVar);
            Assert.Equal(22_020_812.69m, m.SatirToplamlariToplami);
        }

        [Fact]
        public async Task Mizani_olmayan_firma_hizlica_duser_agir_zincir_ve_kural_cagrisi_calismaz()
        {
            var s = new Senaryo();   // hafif okuma: MizanYuklenmedi
            var gelis = new List<GrupGorunumu.Defter>();

            var defterler = await Yukle(s, new[] { BuFirma, AdayFirma }, gelis);

            Assert.Equal(HesapAgaciDurumu.MizanYuklenmedi, defterler.Single(d => d.FirmaId == AdayFirma).Durum);
            Assert.Equal(0, s.TamHidrasyonCagrisi);      // diğer firma için tam hidrasyon yok
            Assert.Equal(0, s.DigerFirmaBoyutCagrisi);   // ağaç yoksa kurallar hiç sorulmaz
        }

        [Fact]
        public async Task Diger_firmalar_paralel_okunur()
        {
            var s = new Senaryo
            {
                Hafif = async (_, ct) =>
                {
                    await Task.Delay(200, ct);
                    return new HesapAgaciSonucu(HesapAgaciDurumu.MizanYuklenmedi, Array.Empty<HesapDugumu>());
                }
            };

            var sure = Stopwatch.StartNew();
            await Yukle(s, new[] { BuFirma, AdayFirma, EkipFirma }, new List<GrupGorunumu.Defter>());
            sure.Stop();

            // Sırayla olsaydı ≥ 400 ms; paralelde ~200 ms.
            Assert.True(sure.ElapsedMilliseconds < 380, $"Sıralı okuma şüphesi: {sure.ElapsedMilliseconds} ms");
        }
    }

    /// <summary>Mock kütüphanesi olmadan arayüz sahtesi (yerleşik DispatchProxy).</summary>
    public class Sahte<T> : DispatchProxy where T : class
    {
        private Func<MethodInfo, object?[]?, object?> _isleyici = (m, _) => throw new NotSupportedException(m.Name);

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => _isleyici(targetMethod!, args);

        public static T Olustur(Func<MethodInfo, object?[]?, object?> isleyici)
        {
            var vekil = Create<T, Sahte<T>>();
            ((Sahte<T>)(object)vekil)._isleyici = isleyici;
            return vekil;
        }
    }
}
