namespace Vipi.AuroraBridge.Core;

/// <summary>Esito di una scrittura dell'etichetta quota.</summary>
public sealed record WriteResult(bool Ok, string? Error)
{
    public static readonly WriteResult Success = new(true, null);
    public static WriteResult Fail(string error) => new(false, error);
}

/// <summary>
/// I comandi di Aurora che servono al bridge, tipizzati. Sopra <see cref="AuroraClient"/> (che parla il
/// protocollo) e sotto l'orchestratore (che decide quando chiedere cosa).
/// </summary>
public sealed class AuroraSession
{
    private readonly AuroraClient _client;

    public AuroraSession(AuroraClient client) => _client = client;

    public bool IsConnected => _client.IsConnected;

    /// <summary>Callsign della postazione connessa (<c>#CONN</c>). Null se Aurora non è connessa alla rete.</summary>
    public async Task<string?> GetConnectedCallsignAsync(CancellationToken ct = default)
    {
        var r = await _client.SendAsync("#CONN", ct).ConfigureAwait(false);
        return r.Ok ? First(r) : null;
    }

    /// <summary>Callsign del traffico selezionato (<c>#SELTFC</c>), null se non c'è selezione.</summary>
    public async Task<string?> GetSelectedTrafficAsync(CancellationToken ct = default)
    {
        var r = await _client.SendAsync("#SELTFC", ct).ConfigureAwait(false);
        return r.Ok ? First(r) : null;
    }

    public async Task<FlightPlanRecord?> GetFlightPlanAsync(string callsign, CancellationToken ct = default)
    {
        var r = await _client.SendAsync("#FP", ct, callsign).ConfigureAwait(false);
        return r.Ok ? AuroraRecords.ParseFlightPlan(r.Fields) : null;
    }

    public async Task<TrafficPositionRecord?> GetPositionAsync(string callsign, CancellationToken ct = default)
    {
        var r = await _client.SendAsync("#TRPOS", ct, callsign).ConfigureAwait(false);
        return r.Ok ? AuroraRecords.ParseTrafficPosition(r.Fields) : null;
    }

    /// <summary>Fix della rotta con l'ETO (<c>#TRPATHL</c>): è la fonte migliore per cercare il CoP, perché
    /// contiene la rotta come l'ha risolta Aurora e in ordine di sorvolo. Vuota se il traffico è al suolo.</summary>
    public async Task<IReadOnlyList<(string Fix, string? Eto)>> GetRoutePathAsync(string callsign, CancellationToken ct = default)
    {
        var r = await _client.SendAsync("#TRPATHL", ct, callsign).ConfigureAwait(false);
        return r.Ok ? AuroraRecords.ParseTrafficPath(r.Fields) : Array.Empty<(string, string?)>();
    }

    /// <summary>Piste in uso degli aeroporti controllati (<c>#CTRLRWY</c>). Alimenta le condizioni dei punti.
    /// Nota: <c>#ATIS</c> NON è una fonte affidabile (su una posizione ACC torna quasi vuota).</summary>
    public async Task<IReadOnlyList<RunwayConfiguration>> GetControlledRunwaysAsync(CancellationToken ct = default)
    {
        var r = await _client.SendAsync("#CTRLRWY", ct).ConfigureAwait(false);
        return r.Ok ? AuroraRecords.ParseControlledRunways(r.Fields) : Array.Empty<RunwayConfiguration>();
    }

    public async Task<IReadOnlyList<string>> GetTrafficInRangeAsync(CancellationToken ct = default)
    {
        var r = await _client.SendAsync("#TR", ct).ConfigureAwait(false);
        return r.Ok ? AuroraRecords.ParseList(r.Fields) : Array.Empty<string>();
    }

    /// <summary>
    /// Scrive l'etichetta quota (<c>#LBALT</c>). Unico punto del tool che modifica qualcosa in Aurora, e viene
    /// chiamato SOLO su azione esplicita dell'utente.
    ///
    /// Due limiti scoperti in F0, entrambi tradotti qui in messaggi comprensibili: il traffico dev'essere
    /// **assunto** (altrimenti «Traffic not assumed.»), e il valore non può contenere «;».
    /// </summary>
    public async Task<WriteResult> SetAltitudeLabelAsync(string callsign, string? value, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(callsign)) return WriteResult.Fail("Nessun traffico selezionato.");
        if (value is not null && value.Contains(';')) return WriteResult.Fail("Il valore non può contenere «;».");

        var r = await _client.SendAsync("#LBALT", ct, callsign, value ?? "").ConfigureAwait(false);
        if (r.Ok) return WriteResult.Success;

        var error = r.Error ?? "errore sconosciuto";
        if (error.Contains("not assumed", StringComparison.OrdinalIgnoreCase))
            error = $"{callsign} non è assunto: Aurora consente di scrivere l'etichetta solo sul traffico assunto.";

        return WriteResult.Fail(error);
    }

    /// <summary>Cancella l'etichetta quota: <c>#LBALT</c> con argomento vuoto (verificato in F0).</summary>
    public Task<WriteResult> ClearAltitudeLabelAsync(string callsign, CancellationToken ct = default) =>
        SetAltitudeLabelAsync(callsign, "", ct);

    private static string? First(AuroraResponse r) =>
        r.Fields.Count > 0 && !string.IsNullOrWhiteSpace(r.Fields[0]) ? r.Fields[0].Trim() : null;
}
