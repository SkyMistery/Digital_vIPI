namespace Vipi.Domain.Entities;

/// <summary>
/// Una carta MRVA del sectorfile (<c>ENRMVA/{acc}.mva</c>, <c>{icao}.mva</c>) come l'abbiamo vista l'ultima volta,
/// e quella <b>in vigore</b> se è cambiata.
///
/// <para>🔴 U-037 (revisione totale 3): le carte si leggevano dal repository al momento, e una carta rivista per il
/// ciclo prossimo — il sectorfile lo scriviamo in anticipo — entrava subito anche nelle release del ciclo in corso.
/// Qui si ricorda il testo di prima: una carta cambiata entra <b>dal ciclo successivo</b>, e fino ad allora una
/// release congela quella in vigore. È la regola delle aree di settore (<c>AccSector.ShapeAiracCycle</c>), su un
/// file invece che su un poligono.</para>
///
/// <para>Si tiene il <b>testo</b> del file e non la carta letta: il parser può cambiare, il file no — ed è il file
/// quello che la divisione ha scritto.</para>
/// </summary>
public class MvaChartState
{
    public int Id { get; set; }

    /// <summary>Il percorso nel sectorfile, come lo chiede il provider: <c>ENRMVA/lirr.mva</c>, <c>lirn.mva</c>.</summary>
    public string Path { get; set; } = default!;

    /// <summary>Il testo del file com'è adesso nel sectorfile.</summary>
    public string Text { get; set; } = default!;

    /// <summary>Il testo in vigore mentre <see cref="Text"/> aspetta <see cref="AiracCycle"/>. Null = <see cref="Text"/>
    /// è già in vigore.</summary>
    public string? TextInForce { get; set; }

    /// <summary>Il ciclo AIRAC (YYNN) dal quale <see cref="Text"/> entra in vigore. Null = è già in vigore.</summary>
    public string? AiracCycle { get; set; }

    /// <summary>Quando il testo è cambiato l'ultima volta.</summary>
    public DateTime UpdatedUtc { get; set; }
}
