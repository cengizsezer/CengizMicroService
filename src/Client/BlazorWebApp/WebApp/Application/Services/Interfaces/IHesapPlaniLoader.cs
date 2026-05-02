using WebApp.Domain.Models.FirmaKontrol;

namespace WebApp.Application.Services.Interfaces
{
    public interface IHesapPlaniLoader
    {
        Task<HesapPlani> LoadAsync();
    }
}
