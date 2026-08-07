namespace Vipi.Application.Content;

/// <summary>
/// Riga ACC (center area) per la pagina di gestione: codice, nome, militare, nascosto, estero, più lo stato delle
/// aree regolamentate (abilitate all'import periodico + quante ne ha oggi in archivio).
/// </summary>
public sealed record AccAdminRow(
    int Id, string Code, string Name, bool IsMilitary, bool IsHidden,
    bool IsForeign = false, bool SpecialAreasEnabled = true, int SpecialAreaCount = 0);
