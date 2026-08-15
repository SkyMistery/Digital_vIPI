namespace Vipi.Domain;

/// <summary>
/// Il livello letto da un testo: l'esito di <see cref="LevelFormatting.Parse"/>, cioè le dimensioni che
/// <see cref="LevelFormatting.Format"/> aveva messo in parole.
/// </summary>
/// <param name="Value">Valore numerico; <c>null</c> quando il testo non ne porta uno (vuoto, «—», testo libero).</param>
/// <param name="Unit">Unità del valore. Senza valore resta il predefinito e non significa nulla.</param>
/// <param name="Constraint">Vincolo: <c>Special</c> quando il testo non è un livello ma una frase.</param>
/// <param name="Special">Il testo libero, quando <paramref name="Constraint"/> è <c>Special</c>.</param>
/// <param name="Parity">Regola semicircolare letta dal suffisso «(pari)» / «(dispari)».</param>
/// <param name="VerticalState">Stato verticale letto dalla freccia «↑» / «↓».</param>
public readonly record struct ParsedLevel(
    int? Value,
    LevelUnit Unit,
    LevelConstraint Constraint,
    string? Special,
    LevelParity Parity,
    TransferVerticalState VerticalState);

/// <summary>Formattazione condivisa del livello di un punto di trasferimento (editor + resa documento).</summary>
public static class LevelFormatting
{
    /// <summary>
    /// Il numero più alto che ha senso leggere come livello di volo. Serve a decidere che unità ha un numero
    /// scritto <b>nudo</b> nell'editor in cella: «190» è FL190, «3000» sono piedi. Non è una stima — sopra
    /// FL660 non si vola, e un numero di quattro cifre in una casella di livello è un'altitudine.
    /// </summary>
    private const int MaxFlightLevel = 660;

    /// <summary>Le parità che lasciano un suffisso nel testo (<c>Any</c> non ne lascia): i soli casi da rileggere.</summary>
    private static readonly LevelParity[] WrittenParities = { LevelParity.Even, LevelParity.Odd };


    /// <summary>Rende il livello come testo: «FL130-», «FL280+ ↑ (dispari)», «2500 ft (pari)», «per aerovia» o «—».
    /// Il vincolo di livello è reso col segno «+» (≥) / «-» (≤); lo stato verticale con la freccia «↑» (salita) /
    /// «↓» (discesa); la parità (regola semicircolare) è appesa fra parentesi quando non è <see cref="LevelParity.Any"/>.</summary>
    public static string Format(int? value, LevelUnit unit, LevelConstraint constraint, string? special,
        LevelParity parity = LevelParity.Any, TransferVerticalState verticalState = TransferVerticalState.Unspecified) =>
        AppendParity(AppendState(Body(value, unit, constraint, special), verticalState), parity);

    private static string Body(int? value, LevelUnit unit, LevelConstraint constraint, string? special)
    {
        if (constraint == LevelConstraint.Special)
            return string.IsNullOrWhiteSpace(special) ? "—" : special.Trim();
        if (value is not int v) return "—";

        var body = unit == LevelUnit.Fl ? $"FL{v}" : $"{v} ft";
        // Segno del vincolo di livello (NON è lo stato verticale): «+» = a/o sopra, «-» = a/o sotto.
        return constraint switch
        {
            LevelConstraint.AtOrAbove => body + "+",
            LevelConstraint.AtOrBelow => body + "-",
            _ => body,
        };
    }

    // Freccia dello stato verticale (dimensione indipendente dal vincolo): «↑» salita, «↓» discesa.
    // Stabile/non specificato non aggiungono simbolo.
    private static string AppendState(string body, TransferVerticalState state) => state switch
    {
        TransferVerticalState.Climbing => $"{body} ↑",
        TransferVerticalState.Descending => $"{body} ↓",
        _ => body,
    };

    /// <summary>Livello AL TRASFERIMENTO come testo di tabella: «FL110», «FL110-», «3000 ft». Stringa VUOTA quando
    /// non c'è un valore — la cella è opzionale, e chi la rende decide il segnaposto. Niente parità né freccia:
    /// quelle qualificano il livello autorizzato e la crociera, non l'istante del trasferimento.</summary>
    public static string FormatHandoffLevel(int? value, LevelUnit unit, LevelConstraint constraint) =>
        value is int ? Body(value, unit, constraint, special: null) : "";

    /// <summary>Restrizione di velocità come testo di tabella: «≤250 kt», «≥250 kt», «250 kt». Vuota se assente.</summary>
    public static string FormatSpeed(int? value, SpeedConstraint constraint)
    {
        if (value is not int v || constraint == SpeedConstraint.Unspecified) return "";
        var sign = constraint switch
        {
            SpeedConstraint.AtOrBelow => "≤",
            SpeedConstraint.AtOrAbove => "≥",
            _ => "",
        };
        return $"{sign}{v} kt";
    }

    /// <summary>Etichetta italiana della parità: «pari» / «dispari» / "" per Any.</summary>
    public static string ParityLabel(LevelParity parity) => parity switch
    {
        LevelParity.Even => "pari",
        LevelParity.Odd => "dispari",
        _ => "",
    };

    private static string AppendParity(string body, LevelParity parity) =>
        parity == LevelParity.Any ? body : $"{body} ({ParityLabel(parity)})";

    /// <summary>
    /// Legge un livello scritto a mano: l'inverso di <see cref="Format"/>, per l'editing in cella della tabella
    /// trasferimenti — la colonna mostra <c>Format(...)</c>, e chi ci scrive dentro si aspetta che valga la stessa
    /// scrittura.
    /// <para><b>Non può fallire, e non deve.</b> Ciò che non è riconoscibile come livello diventa
    /// <see cref="LevelConstraint.Special"/>, cioè il testo libero — che è già una forma prevista dal modello:
    /// «per aerovia» è un livello valido, non un errore di battitura.</para>
    /// <para>La proprietà che la lega a <see cref="Format"/> è un round-trip <b>sul testo</b>:
    /// <c>Format(Parse(s)) == s</c> per ogni <c>s</c> che <c>Format</c> sappia produrre.</para>
    /// <para>⚠️ Round-trip sul testo, non sui campi, e la differenza ha un caso solo:
    /// <see cref="TransferVerticalState.Level"/> non lascia segno nel testo (nessuna freccia), quindi rileggendolo
    /// torna <c>Unspecified</c>. Chi salva una cella deve **conservare** lo stato verticale che la cella non
    /// mostra: una casella non può cambiare ciò che non fa vedere.</para>
    /// </summary>
    public static ParsedLevel Parse(string? text)
    {
        var s = (text ?? string.Empty).Trim();

        // 1) La parità è un suffisso fra parentesi, e Format la mette per ultima: si toglie per prima.
        var parity = LevelParity.Any;
        foreach (var p in WrittenParities)
        {
            var suffix = $"({ParityLabel(p)})";
            if (!s.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) continue;
            parity = p;
            s = s[..^suffix.Length].TrimEnd();
            break;
        }

        // 2) Poi la freccia dello stato verticale.
        var state = TransferVerticalState.Unspecified;
        if (s.EndsWith('↑')) { state = TransferVerticalState.Climbing; s = s[..^1].TrimEnd(); }
        else if (s.EndsWith('↓')) { state = TransferVerticalState.Descending; s = s[..^1].TrimEnd(); }

        // 3) Quel che resta è il corpo. Vuoto o trattino = nessun livello (la cella svuotata).
        if (s.Length == 0 || s is "—" or "–")
            return new ParsedLevel(null, LevelUnit.Fl, LevelConstraint.Exact, null, parity, state);

        // Il corpo GREZZO va conservato: se non è un livello diventa testo libero, e il testo libero può
        // legittimamente finire con «-» («FL100/FL200-») — che qui sarebbe scambiato per il segno del vincolo.
        var body = s;
        var constraint = LevelConstraint.Exact;
        if (body.EndsWith('+')) { constraint = LevelConstraint.AtOrAbove; body = body[..^1].TrimEnd(); }
        else if (body.EndsWith('-')) { constraint = LevelConstraint.AtOrBelow; body = body[..^1].TrimEnd(); }

        if (TryReadValue(body, out var value, out var unit))
            return new ParsedLevel(value, unit, constraint, null, parity, state);

        // Non è un livello: è una frase. Torna il corpo com'era, segno compreso.
        return new ParsedLevel(null, LevelUnit.Fl, LevelConstraint.Special, s, parity, state);
    }

    /// <summary>Il numero e la sua unità: «FL190», «2500 ft», o un numero nudo (→ <see cref="MaxFlightLevel"/>).</summary>
    private static bool TryReadValue(string body, out int value, out LevelUnit unit)
    {
        value = 0;
        unit = LevelUnit.Fl;
        if (body.Length == 0) return false;

        if (body.StartsWith("FL", StringComparison.OrdinalIgnoreCase))
            return int.TryParse(body[2..].Trim(), out value);

        var feet = body.EndsWith("ft", StringComparison.OrdinalIgnoreCase);
        if (feet) { body = body[..^2].TrimEnd(); unit = LevelUnit.Feet; }

        if (!int.TryParse(body, out value)) return false;
        // Numero nudo: l'unità la decide l'ordine di grandezza, non un'ipotesi.
        if (!feet && value > MaxFlightLevel) unit = LevelUnit.Feet;
        return true;
    }

    /// <summary>Rende un <see cref="ParsedLevel"/>: la metà che chiude il round-trip con <see cref="Parse"/>.</summary>
    public static string Format(ParsedLevel level) =>
        Format(level.Value, level.Unit, level.Constraint, level.Special, level.Parity, level.VerticalState);
}
