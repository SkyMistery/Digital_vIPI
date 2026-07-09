namespace Vipi.Application;

/// <summary>
/// Paesi confinanti da importare da IVAO per generare le vLOA (sezione "Neighbours"). Si scaricano
/// gli ACC/settori dei paesi elencati, si calcola quali confinano geometricamente con i settori della
/// divisione, e da lì si propongono le coppie ACC per cui creare una vLOA (es. LIRR↔LFMM). I dati NON
/// confinanti non entrano nel DB: restano solo i candidati adiacenti (o da risolvere a mano).
/// </summary>
public sealed class NeighboursOptions
{
    public const string SectionName = "Neighbours";

    /// <summary>CountryId IVAO dei paesi vicini (es. IT → ["FR","CH","AT","SI","HR","GR","MT","TN"]).</summary>
    public List<string> CountryIds { get; set; } = new();

    /// <summary>Soglia di adiacenza in miglia nautiche: due settori confinano se la distanza minima
    /// tra i loro bordi è inferiore a questo valore. Poligoni IVAO grezzi non combaciano al metro → margine.</summary>
    public double AdjacencyThresholdNm { get; set; } = 8.0;
}
