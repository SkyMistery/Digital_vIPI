using Vipi.Application.Abstractions;
using Vipi.Domain.Services;

namespace Vipi.Application.Content;

/// <summary>
/// <b>Da quale ciclo AIRAC è in vigore una SID appena prelevata.</b> Funzione pura: la parte che decide è
/// quella che sbagliava, e qui si verifica senza rete e senza database. Carta
/// <c>docs/feature/2026-09-02-il-ciclo-entrante.md</c> §AW2.
///
/// <para><b>Il difetto che chiude.</b> Il valore scritto in <c>SourceAiracCycle</c> era
/// <c>GetCycle(DateTime.UtcNow)</c> — <i>il ciclo in cui è capitato di girare</i> — e la riga diventava
/// pubblica al ciclo <b>dopo</b>. Ma il giro è ogni 24 ore, con ritardo d'avvio e ritentativi: quando cade è
/// un dettaglio d'esercizio, non un fatto sui dati. La stessa riga prendeva due destini a seconda dell'ora:</para>
/// <list type="bullet">
///   <item>sectorfile aggiornato l'1 settembre, giro che passa il 2 → pubblica il 3 settembre;</item>
///   <item>lo stesso file, giro che passa il 3 alle 02:00 → pubblica il <b>1º ottobre</b>.</item>
/// </list>
/// <para>Un mese di ritardo deciso da un ritentativo slittato, e <b>muto</b>.</para>
///
/// <para><b>La sorgente lo dichiara, e nessuno lo chiedeva.</b> Il sectorfile Aurora tiene un
/// <c>CHANGELOG/&lt;ciclo&gt;.txt</c> per ogni AIRAC — misurato il 2 settembre 2026, il più alto era
/// <c>2608.txt</c>, che si apre con «AIRAC A2608 IN VIGORE DAL 06/08/2026», la stessa data che calcola
/// <c>AiracService</c>. Quando la sorgente dichiara il ciclo non c'è più niente da indovinare: il contenuto
/// vale <b>da quel ciclo</b>, non «dal prossimo, chissà quale».</para>
/// </summary>
public static class SidStampCycle
{
    /// <summary>
    /// Il ciclo <b>dal quale</b> la riga è in vigore, in tre gradini dichiarati.
    ///
    /// <list type="number">
    ///   <item><b>Il ciclo dichiarato</b> dalla sorgente. È un fatto scritto da chi pubblica i dati, e non
    ///     dipende né dall'ora in cui passiamo né da quanto siamo stati fermi.</item>
    ///   <item>Il ciclo <b>successivo</b> a quello in cui la sorgente è cambiata l'ultima volta. È il vecchio
    ///     buffer di un ciclo, ma ancorato a <i>quando i dati si sono mossi</i> invece che a quando li abbiamo
    ///     guardati.</item>
    ///   <item>Il ciclo <b>successivo</b> all'ultimo giro riuscito, e in mancanza anche di quello, a adesso —
    ///     cioè esattamente il comportamento di prima di questa carta.</item>
    /// </list>
    ///
    /// <para>⚠️ <b>I ripieghi sbagliano per eccesso di fretta, ed è voluto.</b> Al gradino 3, se l'ultimo giro
    /// riuscito è di tre giorni fa e nel frattempo il ciclo è girato, si parte dal ciclo <b>vecchio</b> e la
    /// riga esce <b>prima</b>. Il cambiamento era osservabile in quella finestra e noi non abbiamo guardato:
    /// il ritardo è nostro e non deve diventare un ritardo del dato. Il verso opposto — nasconderla per un
    /// mese — è il difetto che questa funzione chiude. Chi vuole trattenerne una ha <c>ForcePublished</c>
    /// per riga.</para>
    ///
    /// <para>⚠️ <b>Un ciclo dichiarato illeggibile non ferma niente</b>: <c>EffectiveUtcForCycle</c> solleva
    /// su una stringa che non è <c>YYNN</c>, e un import non deve cadere perché qualcuno ha rinominato un
    /// file di changelog. Si scende al gradino dopo.</para>
    /// </summary>
    public static string Scegli(
        IAiracService airac, DateTime adessoUtc, SidSourceRelease sorgente, DateTime? ultimoGiroRiuscitoUtc)
    {
        if (Leggibile(airac, sorgente.DeclaredCycle) is string dichiarato) return dichiarato;

        var quando = sorgente.LastChangedUtc ?? ultimoGiroRiuscitoUtc ?? adessoUtc;
        if (quando > adessoUtc) quando = adessoUtc;   // orologi storti: non si timbra un futuro
        return Successivo(airac, quando);
    }

    /// <summary>Il ciclo che comincia subito dopo quello in vigore a <paramref name="quandoUtc"/>.</summary>
    private static string Successivo(IAiracService airac, DateTime quandoUtc)
    {
        var cicli = airac.NextCycles(quandoUtc, 2);
        return cicli.Count > 1 ? cicli[1].Cycle : airac.GetCycle(quandoUtc);
    }

    /// <summary>Il ciclo dichiarato, se è un <c>YYNN</c> che il calendario AIRAC sa leggere; altrimenti null.</summary>
    private static string? Leggibile(IAiracService airac, string? ciclo)
    {
        if (string.IsNullOrWhiteSpace(ciclo)) return null;
        try { _ = airac.EffectiveUtcForCycle(ciclo); return ciclo.Trim(); }
        catch (ArgumentException) { return null; }
    }
}
