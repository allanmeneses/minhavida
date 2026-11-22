namespace Circulos.Api.Domain;

public sealed class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public Profile Profile { get; set; } = null!;
    public ICollection<PersonalDocument> Documents { get; set; } = new List<PersonalDocument>();
}
