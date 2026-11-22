using System.ComponentModel.DataAnnotations;

namespace Circulos.Api.Contracts;

public sealed record ErrorResponse(string Error);

public sealed class RegisterRequest
{
    [Required(ErrorMessage = "Informe o e-mail.")]
    [EmailAddress(ErrorMessage = "Informe um e-mail valido.")]
    [MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe a senha.")]
    [MinLength(8, ErrorMessage = "A senha precisa ter pelo menos 8 caracteres.")]
    [MaxLength(128)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o nome.")]
    [MinLength(2, ErrorMessage = "O nome precisa ter pelo menos 2 caracteres.")]
    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;
}

public sealed class LoginRequest
{
    [Required(ErrorMessage = "Informe o e-mail.")]
    [EmailAddress(ErrorMessage = "Informe um e-mail valido.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe a senha.")]
    public string Password { get; set; } = string.Empty;
}

public sealed record AuthUserDto(Guid Id, string Email, string Name);

public sealed record AuthResponse(string Token, AuthUserDto User);

public sealed class UpdateProfileRequest
{
    [Required(ErrorMessage = "Informe o nome.")]
    [MinLength(2, ErrorMessage = "O nome precisa ter pelo menos 2 caracteres.")]
    [MaxLength(120)]
    public string DisplayName { get; set; } = string.Empty;

    public DateOnly? BirthDate { get; set; }

    [MaxLength(40)]
    public string? Phone { get; set; }

    [MaxLength(256)]
    [EmailAddress(ErrorMessage = "Informe um e-mail de contato valido.")]
    public string? SecondaryEmail { get; set; }
}

public sealed record ProfileResponse(
    string DisplayName,
    DateOnly? BirthDate,
    string? Phone,
    string? SecondaryEmail,
    bool HasPhoto);

public sealed record DocumentResponse(
    Guid Id,
    string FileName,
    string ContentType,
    long SizeBytes,
    DateTime CreatedAt);
