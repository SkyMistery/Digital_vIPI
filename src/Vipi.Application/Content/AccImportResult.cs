namespace Vipi.Application.Content;

/// <summary>Esito dell'import ACC + settori ATC dalla sorgente.</summary>
public sealed record AccImportResult(int AccsCreated, int AccsUpdated, int SubcentersCreated, int SubcentersUpdated);
