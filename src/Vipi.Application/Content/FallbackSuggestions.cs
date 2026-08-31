namespace Vipi.Application.Content;

/// <summary>
/// Un settore come serve alla proposta: chi è e che fascia di quota occupa. Le quote nulle sono aperte
/// (<c>Base</c> null = dal suolo, <c>Top</c> null = illimitato).
/// </summary>
/// <param name="Callsign">Callsign del settore.</param>
/// <param name="BaseFeet">Piede complessivo della sua forma, in piedi.</param>
/// <param name="TopFeet">Tetto complessivo della sua forma, in piedi.</param>
public readonly record struct SectorBand(string Callsign, int? BaseFeet, int? TopFeet);

/// <summary>Una riga che la geometria propone all'admin. Non è ancora una riga di ripiego: va confermata.</summary>
/// <param name="TargetCallsign">Il sostituto proposto.</param>
/// <param name="BaseFeet">Piede della fascia proposta: l'INTERSEZIONE fra le due bande.</param>
/// <param name="TopFeet">Tetto della fascia proposta.</param>
/// <param name="OverlapFeet">Quanta quota condividono davvero, in piedi. Ordina le proposte.</param>
public readonly record struct FallbackSuggestion(string TargetCallsign, int? BaseFeet, int? TopFeet, int OverlapFeet);

/// <summary>
/// La parte «B» della carta: <b>la geometria propone, l'admin conferma</b>. Non gira mai a runtime — sta
/// nell'editor Struttura e riempie la tabella che poi la ricaduta legge.
///
/// <para>⚠️ <b>Si accoppia per BANDA, non per sovrapposizione in pianta.</b> ES5 e WS5 sono affiancati, non
/// impilati: in pianta non si toccano mai, e una regola basata sull'area comune non li accosterebbe mai. È lo
/// <i>strato</i> che li rende sostituti l'uno dell'altro — quando il settore alto d'est chiude, il suo cielo
/// va all'altro settore alto, non al basso che gli sta sotto.</para>
///
/// <para><b>Chi resta fuori.</b> Il settore stesso e i suoi antenati: gli antenati sono già la coda della
/// catena, riproporli sarebbe scrivere a mano ciò che il sistema fa da sé. E chi non condivide un piede di
/// quota: a quel punto non è un sostituto, è un altro spazio aereo.</para>
///
/// <para>Puro e deterministico, nessun I/O. Carta
/// <c>docs/feature/2026-08-31-ricaduta-verticale-e-cicli.md</c> §2.</para>
/// </summary>
public static class FallbackSuggestions
{
    /// <summary>Tetto convenzionale quando la banda è aperta in alto: serve solo a MISURARE la sovrapposizione.</summary>
    private const int TettoAperto = 66_000;

    /// <summary>
    /// I sostituti proposti per <paramref name="sector"/>, dal più sovrapposto in quota al meno.
    /// </summary>
    /// <param name="sector">Il settore per cui si propone.</param>
    /// <param name="bands">Le bande di tutti i settori candidati, il settore stesso compreso o no.</param>
    /// <param name="ancestors">I suoi antenati: esclusi, perché sono già la coda della catena.</param>
    public static IReadOnlyList<FallbackSuggestion> For(
        string sector,
        IReadOnlyList<SectorBand> bands,
        IReadOnlySet<string> ancestors)
    {
        var mia = bands.FirstOrDefault(b => b.Callsign.Equals(sector, StringComparison.OrdinalIgnoreCase));
        if (mia.Callsign is null) return Array.Empty<FallbackSuggestion>();

        var proposte = new List<FallbackSuggestion>();
        foreach (var b in bands)
        {
            if (b.Callsign.Equals(sector, StringComparison.OrdinalIgnoreCase)) continue;
            if (ancestors.Contains(b.Callsign)) continue;

            var piede = PiedeIntersezione(mia.BaseFeet, b.BaseFeet);
            var tetto = TettoIntersezione(mia.TopFeet, b.TopFeet);
            var quanto = (tetto ?? TettoAperto) - (piede ?? 0);
            if (quanto <= 0) continue;                      // nemmeno un piede in comune: non è un sostituto

            proposte.Add(new FallbackSuggestion(b.Callsign, piede, tetto, quanto));
        }

        return proposte
            .OrderByDescending(p => p.OverlapFeet)
            .ThenBy(p => p.TargetCallsign, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Banda complessiva di una forma a pezzi (UNIONE): il piede più basso e il tetto più alto.
    ///
    /// <para>⚠️ Qui <c>null</c> è <b>assorbente</b>, ed è il contrario di quel che fa nell'intersezione: un
    /// solo pezzo aperto verso il basso apre verso il basso tutta la banda. Le due regole stanno in quattro
    /// funzioni separate apposta — scriverne due e riusarle in tutti e due i versi è il modo in cui questo
    /// errore torna, silenzioso, in una proposta che sembra plausibile.</para>
    /// </summary>
    public static SectorBand BandOf(string callsign, IEnumerable<(int? BaseFeet, int? TopFeet)> parts)
    {
        int? piede = null, tetto = null;
        var primo = true;
        foreach (var (b, t) in parts)
        {
            piede = primo ? b : PiedeUnione(piede, b);
            tetto = primo ? t : TettoUnione(tetto, t);
            primo = false;
        }
        return new SectorBand(callsign, piede, tetto);
    }

    // --- Intersezione: null = illimitato, quindi NON vince (l'altro estremo è più stretto) ---
    private static int? PiedeIntersezione(int? a, int? b) => a is null ? b : b is null ? a : Math.Max(a.Value, b.Value);
    private static int? TettoIntersezione(int? a, int? b) => a is null ? b : b is null ? a : Math.Min(a.Value, b.Value);

    // --- Unione: null = illimitato, quindi VINCE (la banda risultante è aperta da quel lato) ---
    private static int? PiedeUnione(int? a, int? b) => a is null || b is null ? null : Math.Min(a.Value, b.Value);
    private static int? TettoUnione(int? a, int? b) => a is null || b is null ? null : Math.Max(a.Value, b.Value);
}
