namespace Vipi.Application.Abstractions;

/// <summary>
/// Dove stanno i punti che si possono scrivere in un CoP, in una fotografia sola: nome → posizione.
///
/// <para><b>Perché una fotografia e non una domanda per punto.</b> Chi la usa risolve <b>molti</b> punti
/// nello stesso giro — una vista live di trasferimenti ne ha decine — e ognuno costerebbe un giro di I/O.
/// La fotografia si prende una volta per richiesta e poi è pura, che è anche la sola forma in cui la può
/// ricevere un risolutore senza I/O.</para>
///
/// <para>⚠️ <b>Non è una terza anagrafica.</b> Mette insieme le due che ci sono già e basta: il
/// <b>catalogo punti</b> del sectorfile (<see cref="INavaidSource"/>, che porta i <i>fix</i> — e sono la
/// maggioranza dei CoP veri) e l'<b>anagrafica delle radioassistenze</b> (<see cref="INavaidCatalog"/>, che
/// porta VOR e NDB e le coordinate <b>scritte a mano</b>). Niente tabella nuova: i fix restano fuori
/// dall'anagrafica per la decisione già presa in <c>NavaidImporter</c> — tremila punti di riporto
/// renderebbero inservibile la tendina da cui si sceglie una radioassistenza.</para>
/// </summary>
public interface ICopPositions
{
    /// <summary>La fotografia di adesso. Vuota è un caso <b>normale</b> (sorgente non configurata o muta):
    /// chi la usa perde una risposta, non si rompe.</summary>
    Task<CopPositions> GetAsync(CancellationToken ct = default);
}

/// <summary>La fotografia: nome del punto → posizione. Immutabile, pura, senza I/O.</summary>
public sealed class CopPositions
{
    /// <summary>Nessun punto collocabile. Vedi <see cref="ICopPositions.GetAsync"/>: è un caso normale.</summary>
    public static readonly CopPositions Empty = new(Array.Empty<(string, double, double)>());

    private readonly Dictionary<string, (double Lat, double Lon)> _punti;

    /// <summary>
    /// ⚠️ <b>Vince la PRIMA occorrenza di un nome</b>, quindi l'ordine con cui il chiamante accoda decide chi
    /// prevale fra due omonimi. È l'ordine giusto per due ragioni misurate: <b>diciassette</b> codici stanno
    /// sia fra i VHF sia fra gli NDB (<c>MMP</c> è uno di quelli, e le due posizioni distano una dozzina di
    /// metri: per collocare un CoP sono la stessa cosa), e la coordinata scritta <b>a mano</b> in anagrafica
    /// deve poter scavalcare quella della sorgente.
    /// </summary>
    public CopPositions(IEnumerable<(string Name, double Lat, double Lon)> punti)
    {
        _punti = new Dictionary<string, (double, double)>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, lat, lon) in punti)
        {
            var n = (name ?? "").Trim();
            if (n.Length == 0) continue;
            if (!_punti.ContainsKey(n)) _punti[n] = (lat, lon);
        }
    }

    /// <summary>Quanti punti sa collocare. Serve alla diagnostica, non alla risoluzione.</summary>
    public int Count => _punti.Count;

    /// <summary>
    /// Dove sta un punto. Falso se il nome non c'è — e «non c'è» comprende sia il nome sconosciuto sia il
    /// token che un punto non è (<c>Y01-Y12</c>, <c>ALL</c>): per chi deve collocarlo sono la stessa cosa.
    /// A distinguerli, per la frase da mostrare, è <c>NavaidCheck.IsCheckable</c>.
    /// </summary>
    public bool TryGet(string? cop, out (double Lat, double Lon) point)
    {
        point = default;
        var n = (cop ?? "").Trim();
        return n.Length != 0 && _punti.TryGetValue(n, out point);
    }
}
