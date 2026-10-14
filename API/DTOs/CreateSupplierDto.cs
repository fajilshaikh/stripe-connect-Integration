using System.ComponentModel.DataAnnotations;

namespace API.DTOs;

public class CreateSupplierDto
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}
