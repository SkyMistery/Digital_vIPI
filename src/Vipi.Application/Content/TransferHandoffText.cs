using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>
/// Rende a parole la faccetta TRASFERIMENTO di una riga: dove passa il controllo (o le comunicazioni) e a che
/// livello. Funzione pura, testi presi dal template — quindi italiana nelle vIPI e inglese nelle vLOA senza
/// che il chiamante debba saperlo.
///
/// <para>Sta qui e non dentro il composer perché serve a <b>due</b> consumatori con esigenze diverse: la frase
/// («… al confine dell'AoR passando FL110 …») e le colonne della tabella, che vogliono gli stessi pezzi
/// separati. Ricopiare la mappatura tipo→parola nella derivazione avrebbe significato due posti da tenere
/// d'accordo, e uno dei due lo si aggiorna sempre dopo l'altro.</para>
/// </summary>
public static class TransferHandoffText
{
    /// <summary>Dove avviene il trasferimento, a parole. Vuoto per <c>Unspecified</c> e per i tipi che
    /// richiedono un'etichetta e non ce l'hanno — meglio muti che monchi.</summary>
    public static string Place(CoordinationSentenceTemplate tpl, TransferHandoffKind kind, string? label)
    {
        var l = (label ?? "").Trim();
        return kind switch
        {
            TransferHandoffKind.AorBoundary => tpl.Handoff.AorBoundary,
            TransferHandoffKind.Point when l.Length > 0 => tpl.Handoff.Point.Replace("{label}", l),
            TransferHandoffKind.Custom when l.Length > 0 => tpl.Handoff.Custom.Replace("{label}", l),
            _ => "",
        };
    }

    /// <summary>Livello al trasferimento a parole: «passando FL110», «a FL110 o inferiore». Vuoto senza valore.
    /// <para>«Passando» è la forma di riferimento perché al trasferimento il traffico ATTRAVERSA quel livello:
    /// è la differenza rispetto al livello autorizzato, che invece è un'assegnazione.</para></summary>
    public static string Level(CoordinationSentenceTemplate tpl, int? value, LevelUnit unit, LevelConstraint constraint)
    {
        if (value is not int v) return "";
        var body = unit == LevelUnit.Fl ? $"FL{v}" : $"{v} ft";
        var form = constraint switch
        {
            LevelConstraint.AtOrBelow => tpl.Handoff.LevelAtOrBelow,
            LevelConstraint.AtOrAbove => tpl.Handoff.LevelAtOrAbove,
            _ => tpl.Handoff.LevelPassing,
        };
        return form.Replace("{v}", body);
    }

    /// <summary>
    /// Il livello AUTORIZZATO per la colonna di tabella, con le parole della parità prese dal <b>template</b>:
    /// «FL260 (pari)» in una vIPI, «FL260 (even)» in una vLOA.
    ///
    /// <para>⚠️ È l'unica colonna che non passava di qui, e si vedeva: handoff, comunicazioni e velocità
    /// arrivavano dal template mentre il livello restava cablato in italiano — dentro una vLOA inglese usciva
    /// «FL260 (pari)». Il difetto era congelato nell'approvato della rete di caratterizzazione, che lo
    /// fotografava senza poterlo giudicare.</para>
    /// </summary>
    public static string ClearedLevel(CoordinationSentenceTemplate tpl, TransferPointRow p) =>
        LevelFormatting.Format(p.LevelValue, p.LevelUnit, p.LevelConstraint, p.LevelSpecial, p.Parity,
            p.VerticalState, new LevelFormatting.ParityWords(tpl.Level.ParityEven, tpl.Level.ParityOdd));

    /// <summary>Restrizione di velocità a parole: «a 250 kt o inferiore». Vuota se assente.</summary>
    public static string Speed(CoordinationSentenceTemplate tpl, int? value, SpeedConstraint constraint)
    {
        if (value is not int v) return "";
        var form = constraint switch
        {
            SpeedConstraint.AtOrBelow => tpl.Speed.AtOrBelow,
            SpeedConstraint.AtOrAbove => tpl.Speed.AtOrAbove,
            SpeedConstraint.Exact => tpl.Speed.Exact,
            _ => "",
        };
        return form.Length == 0 ? "" : form.Replace("{v}", v.ToString());
    }

    /// <summary>Dove passano le comunicazioni, <b>solo se altrove</b> rispetto al controllo: dirlo due volte
    /// allunga la frase (e la tabella) senza aggiungere niente.</summary>
    public static string CommsPlace(CoordinationSentenceTemplate tpl, TransferHandoffFacet f)
    {
        if (f.CommsKind == TransferHandoffKind.Unspecified) return "";
        var same = f.CommsKind == f.Kind
                   && string.Equals((f.CommsLabel ?? "").Trim(), (f.Label ?? "").Trim(),
                       StringComparison.OrdinalIgnoreCase);
        return same ? "" : Place(tpl, f.CommsKind, f.CommsLabel);
    }
}
