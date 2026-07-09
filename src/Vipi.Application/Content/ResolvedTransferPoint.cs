namespace Vipi.Application.Content;

/// <summary>Esito della risoluzione live di un punto (vista operativa): chi prende davvero il traffico ora.</summary>
public sealed class ResolvedTransferPoint
{
    public required TransferPointRow Point { get; init; }
    /// <summary>Callsign del ricevente risolto (primo settore online risalendo la gerarchia), oppure «UNICOM».</summary>
    public required string ResolvedHandler { get; init; }
    public required bool IsOnline { get; init; }
}
