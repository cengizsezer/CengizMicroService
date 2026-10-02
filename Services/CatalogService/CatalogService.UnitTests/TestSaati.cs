namespace CatalogService.UnitTests
{
    /// <summary>
    /// Sabit "bugün" veren saat (Prompt 7B: servisler <c>DateTime.Today</c> yerine <see cref="TimeProvider"/>
    /// alıyor). Öğlen UTC: yerel saat dilimi ±12 saat içinde olduğu sürece <c>GetLocalNow().Date</c> aynı gündür.
    /// </summary>
    public static class TestSaati
    {
        public static TimeProvider Gun(DateTime gun) => new SabitSaat(new DateTimeOffset(gun.Date.AddHours(12), TimeSpan.Zero));

        private sealed class SabitSaat(DateTimeOffset an) : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => an;
        }
    }
}
