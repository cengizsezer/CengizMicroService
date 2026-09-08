namespace PkfRobot.UnitTests.Core;

/// <summary>
/// <see cref="PkfRobot.Core.Klavye"/>'nin surucusu ve varsayilan beklemesi
/// STATIK. Iki test sinifi da onlari kendi kurulumunda degistirip
/// <c>Dispose</c>'ta geri aliyor; xUnit sinif koleksiyonlarini PARALEL
/// calistirdigi icin biri digerinin surucusunu ortada sifirlayabiliyordu ve
/// test aralikli olarak bos olay listesi goruyordu.
///
/// Ayni koleksiyona konulan siniflar sirayla calisir. Cozum statigi kaldirmak
/// olurdu ama <c>Klavye</c> uretim kodunda bilerek statik (adim motoru ona
/// ornek tasimadan erisiyor); testleri seri yapmak, uretim tasarimini test
/// ugruna degistirmekten ucuz.
/// </summary>
[CollectionDefinition(Ad, DisableParallelization = true)]
public class KlavyeKoleksiyonu
{
    public const string Ad = "Klavye statigi";
}
