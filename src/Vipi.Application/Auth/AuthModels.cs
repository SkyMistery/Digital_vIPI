namespace Vipi.Application.Auth;

/// <summary>Riga di concessione editing per l'elenco admin.</summary>
public sealed class GrantRow
{
    public required int Id { get; init; }
    public required int UserId { get; init; }
    public string? DisplayName { get; init; }
    public required string FirCode { get; init; }
    public required int GrantedByUserId { get; init; }
    public required DateTime GrantedAtUtc { get; init; }
}
