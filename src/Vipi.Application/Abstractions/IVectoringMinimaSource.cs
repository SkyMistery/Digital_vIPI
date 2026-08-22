namespace Vipi.Application.Abstractions;

/// <summary>
/// Etichetta di testo di una carta MRVA, così com'è scritta nel sectorfile. Il testo NON è un numero e non viene
/// interpretato: nei file reali vale <c>110</c> (centinaia di piedi), <c>1500</c> (piedi), ma anche <c>TRL</c>,
/// <c>NO MINIMA</c>, <c>80/TRL</c>, <c>*30/40</c>. Nessun campo del formato distingue le unità, quindi convertire
/// sarebbe indovinare: si riporta verbatim, dov'era.
/// </summary>
/// <param name="Text">Il testo disegnato (campo 5 della riga <c>L;</c>).</param>
/// <param name="Lat">Latitudine del punto in cui Aurora scrive l'etichetta, gradi decimali con segno.</param>
/// <param name="Lon">Longitudine, gradi decimali con segno.</param>
/// <param name="Color">Indice colore Aurora grezzo (campo 6): nei file italiani <c>8</c>, <c>7</c> o <c>6</c>.</param>
public sealed record MvaLabel(string Text, double Lat, double Lon, string? Color);

/// <summary>
/// Un tracciato della carta MRVA: la sequenza di vertici di un blocco <c>T;</c>. NON è per forza un'area — su 315
/// tracciati misurati nel sectorfile italiano 92 sono <b>aperti</b> (archi, linee di confine; <c>LINEA2</c> di
/// <c>lirs.mva</c> ha due soli punti). Si riportano come sono: chiuderli d'ufficio inventerebbe geometria.
/// </summary>
/// <param name="Name">Nome del gruppo (campo 2): <c>ZONA1</c>, <c>CERCHIO-PA</c>, <c>RR US0</c>, l'ICAO… non normalizzato.</param>
/// <param name="IsClosed">Vero se il primo e l'ultimo vertice coincidono: solo allora è un'area.</param>
/// <param name="Points">Vertici in ordine di disegno, gradi decimali con segno.</param>
public sealed record MvaShape(string Name, bool IsClosed, IReadOnlyList<(double Lat, double Lon)> Points);

/// <summary>
/// Il contenuto di UN file <c>.mva</c>: tracciati ed etichette, indipendenti fra loro. L'associazione
/// etichetta↔area <b>non è dichiarata dal formato</b> (in <c>liph.mva</c> le dieci <c>L;</c> stanno tutte in cima al
/// file, prima di qualsiasi vertice) e non viene dedotta: si disegna ciò che c'è, dove c'è, come fa Aurora.
/// </summary>
public sealed record MvaChart(IReadOnlyList<MvaShape> Shapes, IReadOnlyList<MvaLabel> Labels)
{
    /// <summary>Carta vuota: sorgente non configurata, file assente (404) o file senza contenuto utile.</summary>
    public static readonly MvaChart Empty = new(Array.Empty<MvaShape>(), Array.Empty<MvaLabel>());

    /// <summary>Vero se non c'è niente da disegnare — la sezione mostra «nessun dato» invece di una mappa vuota.</summary>
    public bool IsEmpty => Shapes.Count == 0 && Labels.Count == 0;
}

/// <summary>
/// Porta neutra: fornisce la carta delle minime di vettoramento (MRVA) dalla sorgente esterna (impl. GitHub/Aurora
/// in Infrastructure). Nel sectorfile le MRVA stanno in due famiglie, e <b>il nome del file è l'unica attribuzione
/// che esista</b>: <c>ENRMVA/{acc}.mva</c> per l'enroute di un ACC, <c>{icao}.mva</c> nella root per un APP. Dentro
/// il file non c'è nulla che leghi un'area a un settore — per questo si espone una carta per ente, non una tabella
/// per settore.
/// </summary>
public interface IVectoringMinimaSource
{
    /// <summary>Carta enroute di un ACC (<c>ENRMVA/{acc}.mva</c>). <see cref="MvaChart.Empty"/> se la sorgente non
    /// è configurata o il file non c'è.</summary>
    Task<MvaChart> GetAccChartAsync(string accCode, CancellationToken ct = default);

    /// <summary>Carta di un aeroporto (<c>{icao}.mva</c>). <see cref="MvaChart.Empty"/> se la sorgente non è
    /// configurata o il file non c'è: su 49 APP censiti, 25 non hanno il file — è un caso normale, non un errore.</summary>
    Task<MvaChart> GetAirportChartAsync(string icao, CancellationToken ct = default);
}
