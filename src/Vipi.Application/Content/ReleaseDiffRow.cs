namespace Vipi.Application.Content;

/// <summary>Una voce del riepilogo differenze di una release: cosa cambia rispetto alla versione in vigore.</summary>
public sealed record ReleaseDiffRow(string Label, string Change, string? Detail);   // Change: Aggiunta|Rimossa|Modificata
