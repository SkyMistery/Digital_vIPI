namespace Vipi.Application.Content;

/// <summary>
/// Il traffico militare e i suoi enti. Carta <c>docs/feature/2026-09-24-mil-solo-traffico-militare.md</c>.
///
/// <para>La regola del committente (24 settembre 2026, vale per tutti i MIL_CTR): <b>i militari controllano solo il
/// traffico militare</b>. Un MIL_CTR può essere aperto anche col padre chiuso, ma vede soltanto i trasferimenti
/// scritti verso di lui — mai quelli scritti per un ente civile — e in più <b>assorbe gli APP militari suoi
/// fratelli</b>: gli APP degli scali «Solo militare» che hanno il suo stesso padre.</para>
/// </summary>
public static class RipiegoMilitare
{
    /// <summary>
    /// Il callsign è di un ente militare: <c>MIL</c> in un pezzo di mezzo (<c>LIMM_MIL_CTR</c>, <c>LIEE_MIL_APP</c>).
    /// ⚠️ Stessa regola di <see cref="AccFamigliaAorRegola"/>, e non una seconda: due modi di dire «è militare»
    /// finirebbero per dare due risposte.
    /// </summary>
    public static bool Militare(string? callsign) => AccFamigliaAorRegola.Di(callsign) == FamigliaAor.Mil;

    /// <summary>Un MIL_CTR: militare, e d'area.</summary>
    public static bool MilCtr(string? callsign) =>
        Militare(callsign) && callsign!.Trim().EndsWith("_CTR", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Per ogni APP militare, il MIL_CTR suo fratello (stesso padre), se c'è.
    /// </summary>
    /// <param name="appMilitari">Gli APP degli scali «Solo militare», col loro padre.</param>
    /// <param name="settori">Tutti i settori attivi, col loro padre: i MIL_CTR si cercano qui.</param>
    /// <remarks>⚠️ <b>Solo lo stesso padre</b> (decisione D2): un APP militare più in basso nell'albero segue
    /// l'albero civile come prima. Due MIL_CTR sotto lo stesso padre non ci sono nei dati; se capitasse, vince il
    /// primo in ordine di nome, e il risultato è deterministico.</remarks>
    public static IReadOnlyDictionary<string, string> Fratelli(
        IEnumerable<(string Callsign, string? Padre)> appMilitari,
        IEnumerable<(string Callsign, string? Padre)> settori)
    {
        var milPerPadre = settori
            .Where(s => MilCtr(s.Callsign) && !string.IsNullOrWhiteSpace(s.Padre))
            .GroupBy(s => s.Padre!.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Select(s => s.Callsign.Trim())
                .OrderBy(c => c, StringComparer.OrdinalIgnoreCase).First(), StringComparer.OrdinalIgnoreCase);

        var esito = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (app, padre) in appMilitari)
            if (!string.IsNullOrWhiteSpace(padre) && milPerPadre.TryGetValue(padre.Trim(), out var mil))
                esito[app.Trim()] = mil;
        return esito;
    }

    /// <summary>
    /// Le righe di ripiego dichiarate, con in più quella <b>automatica</b> di ogni APP militare verso il suo MIL_CTR.
    ///
    /// <para>⚠️ In <b>coda</b> alle righe scritte a mano, e senza fascia: chi ha scritto un ripiego per quell'APP
    /// l'ha voluto, e passa prima. Nella camminata di <see cref="FallbackChain"/> il padre sta allo stesso passo ma
    /// <b>dopo</b> le righe, quindi l'ordine è quello deciso (D3): righe scritte → MIL_CTR → padre civile.</para>
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<FallbackRow>> ConAutomatiche(
        IReadOnlyDictionary<string, IReadOnlyList<FallbackRow>> dichiarate,
        IReadOnlyDictionary<string, string> fratelli)
    {
        if (fratelli.Count == 0) return dichiarate;
        var esito = new Dictionary<string, IReadOnlyList<FallbackRow>>(dichiarate, StringComparer.OrdinalIgnoreCase);
        foreach (var (app, mil) in fratelli)
        {
            var righe = esito.TryGetValue(app, out var r) ? r.ToList() : new List<FallbackRow>();
            if (righe.Any(x => string.Equals(x.TargetCallsign, mil, StringComparison.OrdinalIgnoreCase))) continue;
            righe.Add(new FallbackRow(mil, null, null, Automatica: true));
            esito[app] = righe;
        }
        return esito;
    }
}
