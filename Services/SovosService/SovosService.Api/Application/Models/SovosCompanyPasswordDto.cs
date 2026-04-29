using System.ComponentModel.DataAnnotations;

namespace SovosService.Api.Application.Models;

public sealed class SovosCompanyPasswordDto
{
    [Required, MinLength(4)]
    public string NewPassword { get; set; } = "";
}
