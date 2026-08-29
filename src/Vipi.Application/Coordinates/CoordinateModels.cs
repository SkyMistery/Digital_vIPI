namespace Vipi.Application.Coordinates;

/// <summary>Che cosa il lettore non è riuscito a fare, o ha fatto con una scelta che va dichiarata.</summary>
/// <remarks>⚠️ È un <b>codice</b>, non un messaggio: il testo lo scrive la UI, nelle due lingue. Un motore che
/// restituisce frasi le restituisce in una lingua sola.</remarks>
public enum CoordinateIssueKind
{
    /// <summary>La riga non contiene una coordinata riconoscibile.</summary>
    RigaNonLetta,

    /// <summary>Numero dispari di angoli: l'ultimo resta senza il suo compagno.</summary>
    AngoloSpaiato,

    /// <summary>Latitudine oltre 90° o longitudine oltre 180°.</summary>
    FuoriIntervallo,

    /// <summary>Il primo numero non poteva essere una latitudine: i due sono stati scambiati.</summary>
    LatLonScambiate,

    /// <summary>Catena di segmenti interrotta: la fine di una riga non è l'inizio della successiva.</summary>
    CatenaInterrotta,

    /// <summary>Poligono KML con buco: si è tenuto il contorno esterno.</summary>
    BucoScartato,

    /// <summary>L'ingresso supera il tetto di righe: il resto non è stato letto.</summary>
    TroppeRighe,

    /// <summary>Il file KML/KMZ non si è potuto aprire.</summary>
    FileNonLetto,
}

/// <summary>Una segnalazione, ancorata alla <paramref name="Riga"/> (1-based; 0 = riguarda tutto l'ingresso).</summary>
public sealed record CoordinateIssue(CoordinateIssueKind Kind, int Riga, string Testo, string? Dettaglio = null);

/// <summary>
/// Un'area letta: i suoi vertici in ordine, più ciò che l'ingresso sapeva dirci di lei.
/// <para><paramref name="Nome"/> = il 6° campo del sectorfile o il <c>&lt;name&gt;</c> del Placemark KML; null se
/// l'ingresso non lo dice. <paramref name="Tipo"/> = il 5° campo (<c>RESTRICT</c>, <c>COAST</c>…).
/// <paramref name="AnelloChiuso"/> = l'ultimo vertice tornava sul primo, e in tal caso <b>non</b> è ripetuto in
/// <paramref name="Punti"/>: il vertice di chiusura è una proprietà dell'anello, non un punto in più.</para>
/// </summary>
public sealed record CoordinateArea(
    string? Nome,
    IReadOnlyList<(double Lat, double Lon)> Punti,
    bool AnelloChiuso,
    string? Tipo = null,
    bool DaSegmenti = false);

/// <summary>
/// L'esito della lettura: le aree, le segnalazioni e i conti. ⚠️ <b>Le due cose viaggiano insieme</b>: un
/// convertitore che restituisce i punti e si tiene gli scarti è un convertitore che perde righe in silenzio.
/// </summary>
public sealed record CoordinateReadResult(
    IReadOnlyList<CoordinateArea> Aree,
    IReadOnlyList<CoordinateIssue> Segnalazioni,
    int RigheLette,
    int RigheTotali)
{
    public static CoordinateReadResult Vuoto { get; } =
        new(Array.Empty<CoordinateArea>(), Array.Empty<CoordinateIssue>(), 0, 0);

    public int PuntiTotali => Aree.Sum(a => a.Punti.Count);

    /// <summary>
    /// Tutti i punti cadono fuori da un riquadro largo attorno all'Italia. Non è un errore — l'attrezzo serve
    /// anche per un confinante — ma sbagliare un emisfero è l'errore più facile da fare e il più difficile da
    /// vedere su un elenco di numeri.
    /// </summary>
    public bool TuttoFuoriItalia =>
        PuntiTotali > 0 && Aree.SelectMany(a => a.Punti).All(p => !InItalia(p.Lat, p.Lon));

    private static bool InItalia(double lat, double lon) =>
        lat is >= 34.0 and <= 48.5 && lon is >= 5.0 and <= 20.0;
}
