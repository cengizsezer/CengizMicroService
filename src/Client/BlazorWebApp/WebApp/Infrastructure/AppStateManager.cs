using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebApp.Infrastructure
{
    public class AppStateManager
    {
        public event Action<ComponentBase, string> StateChanged;
        public string SelectedFirmId { get; set; } = string.Empty;
        public string SelectedFirmName { get; set; } = string.Empty;

        public void UpdateCart(ComponentBase component)
        {
            StateChanged?.Invoke(component, "updatebasket");
        }

        public void LoginChanged(ComponentBase component)
        {
            StateChanged?.Invoke(component, "login");
        }

       

        public void FirmChanged(ComponentBase component)
        {
            StateChanged?.Invoke(component, "firmchanged");
        }

    }
}