using System.Globalization;
using Vipi.Domain.Entities;

namespace Vipi.Application.Content;

/// <summary>
/// Una finestra di modifiche: dalla prima scrittura che ha segnalato, alla chiusura. È la <b>causa</b> delle
/// segnalazioni «da ripubblicare» che il giro apre subito dopo. Carta
/// <c>docs/feature/2026-09-23-da-fare-per-cambiamento.md</c> §4.
/// </summary>
/// <param name="DaUtc">La prima modifica della finestra.</param>
/// <param name="Famiglie">Che genere di dati è cambiato (<see cref="FamiglieDiModifica"/>), senza doppioni,
/// in ordine alfabetico: due finestre uguali si scrivono uguali.</param>
public sealed record FinestraDiModifiche(DateTime DaUtc, IReadOnlyList<string> Famiglie)
{
    /// <summary>La chiave della causa: il prefisso dice la natura, l'istante la identifica. Due finestre non
    /// cominciano mai nello stesso secondo, perché una si apre solo quando l'altra è stata presa.</summary>
    public string Chiave => "mod:" + DaUtc.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);

    /// <summary>Gli argomenti della frase: l'istante (ISO, UTC) e poi le famiglie. La UI li compone nella lingua
    /// di chi legge.</summary>
    public IReadOnlyList<string> Argomenti =>
        new[] { DaUtc.ToString("o", CultureInfo.InvariantCulture) }.Concat(Famiglie).ToList();
}

/// <summary>
/// Il raccoglitore di processo delle modifiche: le scritture segnalano, il giro della deriva prende.
///
/// <para>⚠️ <b>Singleton</b>, e senza database: lo chiama l'interceptor dei salvataggi da dentro
/// <c>SaveChanges</c>, dove una query sarebbe una seconda operazione sullo stesso contesto.</para>
/// </summary>
public interface IModificheInAttesa
{
    /// <summary>Qualcuno ha scritto dati di queste famiglie. Apre una finestra, o si aggiunge a quella aperta.</summary>
    void Segnala(IReadOnlyCollection<string> famiglie, DateTime quandoUtc);

    /// <summary>
    /// Aspetta che ci sia una finestra e che sia passata <paramref name="attesa"/> dalla sua <b>prima</b>
    /// modifica, poi la prende (e la chiude). ⚠️ Dalla prima e non dall'ultima: con modifiche continue un'attesa
    /// «della calma» non finirebbe mai, e la lista resterebbe indietro proprio quando si lavora di più.
    /// </summary>
    Task<FinestraDiModifiche> PrendiAsync(TimeSpan attesa, CancellationToken ct);
}

/// <inheritdoc cref="IModificheInAttesa"/>
public sealed class ModificheInAttesa : IModificheInAttesa
{
    private readonly object _lock = new();
    private DateTime? _da;
    private readonly SortedSet<string> _famiglie = new(StringComparer.Ordinal);
    private TaskCompletionSource _arrivata = Nuova();

    private static TaskCompletionSource Nuova() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Segnala(IReadOnlyCollection<string> famiglie, DateTime quandoUtc)
    {
        if (famiglie.Count == 0) return;
        lock (_lock)
        {
            _da ??= quandoUtc;
            foreach (var f in famiglie) _famiglie.Add(f);
            _arrivata.TrySetResult();
        }
    }

    public async Task<FinestraDiModifiche> PrendiAsync(TimeSpan attesa, CancellationToken ct)
    {
        while (true)
        {
            Task arrivata;
            DateTime? da;
            lock (_lock) { arrivata = _arrivata.Task; da = _da; }

            if (da is null)
            {
                await arrivata.WaitAsync(ct).ConfigureAwait(false);
                continue;
            }

            var resta = da.Value + attesa - DateTime.UtcNow;
            // ⚠️ Mai più della finestra: un istante di segnalazione nel futuro (orologio spostato, o un chiamante
            // che passa un'ora locale per UTC) farebbe aspettare ore. Costato un'ora e undici minuti di test il
            // 23 settembre 2026, con un istante fisso alle 21:04 «UTC» scritto alle 20 UTC.
            if (resta > attesa) resta = attesa;
            if (resta > TimeSpan.Zero) await Task.Delay(resta, ct).ConfigureAwait(false);

            lock (_lock)
            {
                if (_da is null) continue;   // presa da un altro (non succede con un consumatore solo, ma non si conta)
                var finestra = new FinestraDiModifiche(_da.Value, _famiglie.ToList());
                _da = null;
                _famiglie.Clear();
                _arrivata = Nuova();
                return finestra;
            }
        }
    }
}

/// <summary>
/// Da un'entità scritta alla sua <b>famiglia</b>, cioè a come la chiama chi lavora: «coordinamenti», non
/// <c>AgreementClause</c>. <c>null</c> = non è una modifica che possa cambiare un documento pubblicato, oppure è
/// una scrittura del giro stesso.
///
/// <para>⚠️ <b>Un elenco di ciò che conta, non di ciò che non conta.</b> Un tipo dimenticato qui fa il giro di
/// sempre, quello notturno: è la stessa lista di prima, solo in ritardo. Il contrario — escludere — farebbe
/// ripartire il giro a ogni battito di un lock o di una sessione ATC, e le segnalazioni che il giro scrive lo
/// rilancerebbero da sole.</para>
/// </summary>
public static class FamiglieDiModifica
{
    public const string Coordinamenti = "Coordinamenti";
    public const string Settori = "Settori";
    public const string Aeroporti = "Aeroporti";
    public const string Procedure = "Procedure";
    public const string Testo = "Testo";
    public const string Aree = "Aree";
    public const string SpaziAerei = "SpaziAerei";
    public const string Radioassistenze = "Radioassistenze";
    public const string Allegati = "Allegati";

    public static string? Di(object entita) => entita switch
    {
        CoordinationPoint or CoordinationAgreement or AgreementSection or AgreementAirport or AgreementClause
            => Coordinamenti,
        Acc or Sector or AccSector or AirportSector or UnificationRule or SectorFallback or CallsignAlias
            => Settori,
        AirportProcedure or SidFixAlias => Procedure,
        Airport or AirportTransitionLevel or AirportRunway or AirportRunwayRule or AirportLvpMinima
            or AirportFrequencyLink or AirportExtraSection => Aeroporti,
        Document or DocumentParty or DocumentSection or ContentBlock or SharedBlock or NavReference
            or DocumentProfile or DocumentUnion or DocumentUnionMember => Testo,
        SpecialArea or SpecialAreaCenter => Aree,
        AirspaceVolume or SectorAirspaceBinding or SectorShapePart => SpaziAerei,
        Navaid => Radioassistenze,
        Attachment or AttachmentVersion or MediaAsset => Allegati,
        _ => null,
    };
}
