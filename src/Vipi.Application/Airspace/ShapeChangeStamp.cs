namespace Vipi.Application.Airspace;

/// <summary>
/// Il <b>gettone</b> che dice «le forme dei settori sono cambiate»: un istante, condiviso da tutto il
/// processo, che si alza ogni volta che qualcuno aggancia, sgancia o riscrive dei pezzi.
///
/// <para><b>Perché serve.</b> L'attribuzione del traffico tiene i volumi in memoria per un'ora
/// (<c>AtcTrafficRecorder.CatalogTtl</c>): i cataloghi cambiano una volta al giorno e rileggerli a ogni giro
/// sarebbe uno spreco. Ma un <b>aggancio</b> non è un giro d'import: è una persona che preme un tasto e si
/// aspetta di vedere l'effetto. Senza questo gettone, «da adesso conta il CTR e non il monoblocco» entrerebbe
/// in vigore fra zero e sessanta minuti — un comportamento che nessuno può spiegare a chi lo guarda.</para>
///
/// <para>⚠️ <b>È un gettone, non un evento.</b> Non c'è nessuna sottoscrizione da ricordarsi di disdire e
/// nessun ordine di chiamata da rispettare: chi tiene una cache confronta il proprio istante di lettura con
/// <see cref="LastChangeUtc"/> e, se è più vecchio, rilegge. Un lettore che non lo consulta si comporta
/// esattamente come prima.</para>
///
/// <para>⚠️ È di <b>processo</b> (singleton), quindi vale per il poller che gira in questa istanza. In un
/// domani a più istanze varrebbe solo per la propria: allora il posto giusto sarebbe una colonna, non un
/// campo in memoria. Oggi l'host è uno — <c>docs/refactor/15-shape-del-settore-una-porta-sola.md</c> §3g.</para>
/// </summary>
public sealed class ShapeChangeStamp
{
    private long _ticks;

    /// <summary>L'ultimo cambio noto, o <see cref="DateTimeOffset.MinValue"/> se non ne è mai stato segnato uno.</summary>
    public DateTimeOffset LastChangeUtc =>
        new(Interlocked.Read(ref _ticks), TimeSpan.Zero);

    /// <summary>Segna che le forme sono cambiate <b>adesso</b>. Lo chiamano le porte che scrivono, non i motori.</summary>
    public void Touch(DateTimeOffset? now = null) =>
        Interlocked.Exchange(ref _ticks, (now ?? DateTimeOffset.UtcNow).UtcTicks);

    /// <summary>
    /// Vero se una cache letta a <paramref name="lettaIl"/> è **vecchia**: qualcuno ha cambiato le forme dopo.
    /// </summary>
    public bool IsStale(DateTimeOffset lettaIl) => LastChangeUtc > lettaIl;
}
