namespace Vipi.Application.Weather;

/// <summary>
/// Vento decodificato. <see cref="Variable"/>=VRB (direzione non significativa). Velocità/raffica in kt.
///
/// <para>⚠️ <b>Qui non c'è più <c>Label</c></b>, e non è una semplificazione: l'unica sua parte con una lingua
/// era «Calmo», scritto in italiano dentro lo strato Application — dove non si sa né in che lingua guarda chi
/// legge né in quale un documento è bloccato. L'etichetta la compone <c>Vipi.Ui.Shared.WxText.Wind</c>, che
/// quelle due cose le sa. Toglierla invece di lasciarla «quasi giusta» è voluto: un <c>Label</c> neutro solo
/// finché il vento non è calmo è una trappola che si vede una volta l'anno, in italiano dentro una pagina
/// inglese.</para>
/// </summary>
/// <param name="VarFromDeg">
/// Estremi del <b>settore di variabilità</b> (gruppo <c>200V280</c>), quando il bollettino lo dichiara.
/// <para>⚠️ Sono <b>in coda e con un default</b> perché arrivano da un token <i>diverso</i> da quello del vento:
/// chi costruisce un vento a mano — e i banchi di prova lo fanno — non deve doversene ricordare.</para>
/// <para>⚠️ Il settore può <b>scavalcare il nord</b> (<c>340V020</c>): la larghezza è
/// <c>(VarTo - VarFrom + 360) % 360</c>, mai una sottrazione secca.</para>
/// </param>
public sealed record ParsedWind(int? DirectionDeg, bool Variable, int SpeedKt, int? GustKt, bool Calm,
    int? VarFromDeg = null, int? VarToDeg = null);

/// <summary>Il valore RVR è esatto, oppure fuori scala in alto (<c>P2000</c>) o in basso (<c>M0050</c>).</summary>
public enum RvrModifier { Exact, Above, Below }

/// <summary>Tendenza dichiarata dal suffisso RVR: <c>U</c>=in salita, <c>D</c>=in discesa, <c>N</c>=stabile.</summary>
public enum RvrTendency { None, Up, Down, Steady }

/// <summary>
/// Un gruppo RVR (<c>R16R/0350U</c>): portata visuale di pista, in metri, per una testata.
///
/// <para>⚠️ <see cref="Modifier"/> non è decorazione: <c>P2000</c> vuol dire «oltre il fondo scala», e trattarlo
/// come 2000 farebbe entrare un valore misurato dove c'è solo un limite dello strumento. Chi calcola una media
/// o confronta con un minimo deve guardarlo.</para>
/// </summary>
public sealed record RunwayVisualRange(string Runway, int ValueM, RvrModifier Modifier, RvrTendency Tendency);

/// <summary>Intensità di un gruppo di tempo presente: <c>-</c>=leggero, <c>+</c>=forte, <c>VC</c>=in prossimità.</summary>
public enum WxIntensity { Moderate, Light, Heavy, Vicinity }

/// <summary>
/// Un gruppo di tempo presente decodificato nei suoi <b>codici</b> (<c>SHRA</c> → <c>[SH, RA]</c>), mai in
/// parole: le parole hanno una lingua, e questo record attraversa anche il confine SSR→isola serializzato.
/// </summary>
/// <param name="Raw">Il token com'era nel bollettino (es. <c>-SHRA</c>), per diagnosi e per la stringa grezza.</param>
/// <param name="Codes">I codici a due lettere <b>nell'ordine del token</b>: l'ordine è informazione (<c>FZRA</c> ≠ <c>RAFZ</c>).</param>
public sealed record WeatherGroup(string Raw, WxIntensity Intensity, IReadOnlyList<string> Codes);

/// <summary>Strato di nubi: copertura (FEW/SCT/BKN/OVC) + base in piedi + eventuale tipo (CB/TCU).</summary>
public sealed record CloudLayer(string Cover, int BaseFt, string? Type)
{
    public string Label => $"{Cover} {BaseFt}{(Type is null ? "" : " " + Type)}";
}

/// <summary>METAR decodificato. Campi null = non presenti/non riconosciuti (token grezzo resta nel raw).</summary>
/// <param name="Visibility">La visibilità <b>già scritta</b> per chi legge (">10 km", "1200 m").</param>
/// <param name="VisibilityMeters">
/// La stessa visibilità in <b>metri</b>, per chi deve confrontarla con una soglia.
/// <para>⚠️ Esiste perché <see cref="Visibility"/> è una <i>frase</i>, e un minimo LVP non si confronta con una
/// frase: rileggerne i numeri con una regex sarebbe un secondo parser dentro il primo. CAVOK e <c>9999</c>
/// valgono 10 000 — è il fondo scala del bollettino, non una misura.</para>
/// </param>
/// <param name="Rvr">I gruppi RVR nell'ordine del bollettino; vuoto = la stazione non li manda.</param>
/// <param name="VerticalVisibilityFt">Visibilità verticale (<c>VV002</c> → 200 ft): c'è quando il cielo è invisibile.</param>
public sealed record ParsedMetar(
    string Raw,
    string? Station,
    string? TimeRaw,
    ParsedWind? Wind,
    string? Visibility,
    IReadOnlyList<CloudLayer> Clouds,
    IReadOnlyList<WeatherGroup> Weather,
    int? QnhHpa,
    int? TempC,
    int? DewpointC,
    string? Trend,
    bool HasRain,
    bool HasSnow,
    int? VisibilityMeters = null,
    IReadOnlyList<RunwayVisualRange>? Rvr = null,
    int? VerticalVisibilityFt = null)
{
    public string CloudsLabel => Clouds.Count == 0 ? "—" : string.Join(" · ", Clouds.Select(c => c.Label));

    /// <summary>I gruppi RVR, mai null: chi cicla non deve difendersi da un elenco assente.</summary>
    public IReadOnlyList<RunwayVisualRange> RvrGroups => Rvr ?? Array.Empty<RunwayVisualRange>();

    /// <summary>
    /// Il <b>soffitto</b> in piedi: la base dello strato più basso che copre (BKN o OVC), oppure la visibilità
    /// verticale quando il cielo non si vede.
    ///
    /// <para>⚠️ FEW e SCT <b>non fanno soffitto</b> — è la definizione, non una semplificazione: un cielo
    /// «sparso a 200 piedi» non chiude l'aeroporto, uno «coperto a 200 piedi» sì. Null = nessuno strato
    /// coprente, che è diverso da «soffitto zero».</para>
    /// </summary>
    public int? CeilingFt
    {
        get
        {
            var coprenti = Clouds.Where(c => c.Cover is "BKN" or "OVC").Select(c => c.BaseFt).ToList();
            if (coprenti.Count == 0) return VerticalVisibilityFt;
            var min = coprenti.Min();
            return VerticalVisibilityFt is int vv && vv < min ? vv : min;
        }
    }
}

/// <summary>Tipo di gruppo di variazione TAF.</summary>
public enum TafChangeKind { Base, Becmg, Tempo, From, Prob }

/// <summary>Segmento TAF (Base o variazione). <see cref="PeriodRaw"/> = periodo grezzo (es. "1918/1920").</summary>
public sealed record TafSegment(
    TafChangeKind Kind,
    string? PeriodRaw,
    int? Probability,
    ParsedWind? Wind,
    string? Visibility,
    IReadOnlyList<CloudLayer> Clouds,
    IReadOnlyList<WeatherGroup> Weather,
    string Raw);

/// <summary>TAF decodificato: stazione, periodo di validità grezzo, segmenti (Base + variazioni).</summary>
public sealed record ParsedTaf(
    string Raw,
    string? Station,
    string? ValidityRaw,
    IReadOnlyList<TafSegment> Segments);

/// <summary>
/// Formatta i periodi TAF grezzi (ddHH) in forma leggibile "DD-MM HH:MM UTC" per la parte SPIEGATA (non la stringa
/// grezza). Il mese non è nel TAF → dedotto dalla data di riferimento (ora corrente) gestendo il cambio mese.
/// </summary>
public static class TafPeriod
{
    /// <summary>"2112/2212" → "21-07 12:00 → 22-07 12:00 UTC"; "2112" (FM/PROB) → "21-07 12:00 UTC". Null/illeggibile ⇒ grezzo.</summary>
    public static string? Format(string? period, DateTime referenceUtc)
    {
        if (string.IsNullOrWhiteSpace(period)) return period;
        var slash = period.IndexOf('/');
        if (slash > 0)
        {
            var a = FormatPoint(period[..slash], referenceUtc);
            var b = FormatPoint(period[(slash + 1)..], referenceUtc);
            return a is null || b is null ? period : $"{a} → {b} UTC";
        }
        var p = FormatPoint(period, referenceUtc);
        return p is null ? period : $"{p} UTC";
    }

    // ddHH → "DD-MM HH:MM". Ora TAF ammette 24 = fine giornata (→ 00:00 del giorno dopo).
    private static string? FormatPoint(string ddhh, DateTime referenceUtc)
    {
        if (ddhh.Length != 4 || !int.TryParse(ddhh[..2], out var day) || !int.TryParse(ddhh[2..], out var hour)
            || day is < 1 or > 31 || hour is < 0 or > 24)
            return null;
        if (ResolveDate(day, referenceUtc) is not DateTime date) return null;
        var when = date.AddHours(hour);   // hour 24 → 00:00 giorno successivo
        return $"{when:dd-MM} {when:HH:mm}";
    }

    // Sceglie l'anno/mese in cui cade il giorno-del-mese indicato, più vicino alla data di riferimento (gestisce fine mese).
    private static DateTime? ResolveDate(int day, DateTime reference)
    {
        var refDate = reference.Date;
        foreach (var off in new[] { 0, 1, -1 })
        {
            var m = refDate.AddMonths(off);
            if (day <= DateTime.DaysInMonth(m.Year, m.Month))
            {
                var cand = new DateTime(m.Year, m.Month, day, 0, 0, 0, DateTimeKind.Utc);
                if (Math.Abs((cand - refDate).TotalDays) <= 20) return cand;
            }
        }
        var clamped = Math.Min(day, DateTime.DaysInMonth(reference.Year, reference.Month));
        return new DateTime(reference.Year, reference.Month, clamped, 0, 0, 0, DateTimeKind.Utc);
    }
}
