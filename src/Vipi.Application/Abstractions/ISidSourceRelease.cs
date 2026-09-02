namespace Vipi.Application.Abstractions;

/// <summary>
/// <b>Che cosa dice la sorgente delle SID di sé stessa</b>: a quale ciclo AIRAC appartiene il contenuto che
/// stiamo scaricando, e quando è cambiato l'ultima volta. Carta
/// <c>docs/feature/2026-09-02-il-ciclo-entrante.md</c> §AW2.
/// </summary>
/// <param name="DeclaredCycle">
/// Il ciclo <b>dichiarato</b> dalla sorgente, formato <c>YYNN</c>, o <c>null</c> se non lo dichiara.
/// <para>⚠️ È il dato buono, e per un anno non ci si era accorti che <b>esiste</b>: il sectorfile Aurora
/// tiene un <c>CHANGELOG/&lt;ciclo&gt;.txt</c> per ogni AIRAC, e il file più alto <i>è</i> il ciclo del
/// contenuto pubblicato. Fino a qui il ciclo si <b>indovinava</b> dall'ora in cui era passato un job.</para>
/// </param>
/// <param name="LastChangedUtc">Quando la sorgente è cambiata l'ultima volta, o <c>null</c>. Ripiego: serve
/// solo se il ciclo dichiarato non si riesce a leggere.</param>
public sealed record SidSourceRelease(string? DeclaredCycle, DateTime? LastChangedUtc)
{
    /// <summary>La sorgente non ha saputo dire niente di sé. Non è un guasto: si scende ai ripieghi.</summary>
    public static SidSourceRelease Muta { get; } = new(null, null);
}

/// <summary>
/// Porta di lettura di <see cref="SidSourceRelease"/>.
///
/// <para><b>Perché una porta e non una chiamata dentro il provider.</b> <c>ISidProvider</c> risponde «quali
/// SID ci sono per questo ICAO», e questa è una domanda sulla <i>sorgente nel suo insieme</i>: la risposta
/// non dipende dall'aeroporto, e la fanno l'import e chi racconta lo stato dei giri. Tenerla lì dentro
/// vorrebbe dire chiedere le SID di uno scalo per sapere una cosa che non ne riguarda nessuno.</para>
///
/// <para>⚠️ <b>Chi la implementa non deve mai sollevare.</b> Non sapere che cosa dice la sorgente di sé non
/// è un motivo per non importare: si torna <see cref="SidSourceRelease.Muta"/> e chi chiama scende al
/// gradino dopo. Un import che fallisce perché una API di contorno ha dato 403 sarebbe un danno molto più
/// grande della domanda a cui non si è saputo rispondere.</para>
/// </summary>
public interface ISidSourceRelease
{
    /// <summary>Che cosa dichiara la sorgente. Mai un'eccezione: al peggio <see cref="SidSourceRelease.Muta"/>.</summary>
    Task<SidSourceRelease> ReadAsync(CancellationToken ct = default);
}
