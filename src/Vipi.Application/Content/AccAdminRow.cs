namespace Vipi.Application.Content;

/// <summary>Riga ACC (center area) per la pagina di gestione: codice, nome, militare, nascosto.</summary>
public sealed record AccAdminRow(int Id, string Code, string Name, bool IsMilitary, bool IsHidden);
