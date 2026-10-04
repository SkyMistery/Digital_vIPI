using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Infrastructure.Persistence;

/// <summary>La release che il pubblico vede adesso per un bersaglio, senza il payload.</summary>
internal sealed record ReleaseTesta(int Id, ReleaseTargetType Type, string Key, int VersionNumber,
    string AiracCycle, DateTime EffectiveUtc, DateTime CreatedUtc, int CreatedByUserId, string? Note);

/// <summary>
/// Quale release è in vigore per ciascun bersaglio, in <b>una</b> query, con la stessa regola di
/// <c>EfReleaseRepository.GetEffectiveAsync</c>: non superata, efficace entro adesso, la più recente per data
/// efficace e poi per numero.
///
/// <para>🔴 <b>Perché esiste (T-042, revisione del 13 settembre 2026).</b> Ricerca e «Cosa è cambiato» leggevano
/// la versione <b>corrente</b> del documento, mentre la pagina pubblica serve lo <b>snapshot</b> della release in
/// vigore. Dopo un «Pubblica questa versione» con la release al ciclo successivo, la pagina mostrava ancora la
/// v4 e la ricerca anonima citava testi e sezioni della v5. Il gate controllava solo che una release ci fosse.</para>
/// </summary>
internal static class ReleaseInVigore
{
    public static async Task<Dictionary<(ReleaseTargetType, string), ReleaseTesta>> TesteAsync(
        VipiDbContext db, IReadOnlyCollection<(ReleaseTargetType Type, string Key)> bersagli, DateTime adesso,
        CancellationToken ct)
    {
        var risultato = new Dictionary<(ReleaseTargetType, string), ReleaseTesta>();
        if (bersagli.Count == 0) return risultato;

        var tipi = bersagli.Select(b => b.Type).Distinct().ToList();
        var chiavi = bersagli.Select(b => b.Key).Distinct().ToList();
        var voluti = bersagli.ToHashSet();

        var righe = await db.DocReleases.AsNoTracking()
            .Where(r => tipi.Contains(r.TargetType) && chiavi.Contains(r.TargetKey)
                        && r.Status != ReleaseStatus.Superseded && r.ReleaseEffectiveUtc <= adesso)
            .Select(r => new ReleaseTesta(r.Id, r.TargetType, r.TargetKey, r.VersionNumber, r.ReleaseAiracCycle,
                r.ReleaseEffectiveUtc, r.CreatedUtc, r.CreatedByUserId, r.Note))
            .ToListAsync(ct);

        foreach (var g in righe.GroupBy(r => (r.Type, r.Key)))
        {
            if (!voluti.Contains(g.Key)) continue;
            risultato[g.Key] = g.OrderByDescending(r => r.EffectiveUtc).ThenByDescending(r => r.VersionNumber).First();
        }
        return risultato;
    }

    /// <summary>Il documento congelato di un payload, o null se il payload non è leggibile (vuoto, di un'altra forma).</summary>
    public static RawDocument? Documento(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson)) return null;
        try { return JsonSerializer.Deserialize<DocReleasePayload>(payloadJson)?.Doc; }
        catch (JsonException) { return null; }
    }
}

/// <summary>
/// Il testo cercabile di una release, già estratto dallo snapshot. Si tiene in memoria per <b>id e istante di
/// creazione</b>: una release non cambia mai dopo che è nata, quindi la sua voce non scade — si butta quando la
/// release smette di essere in vigore.
///
/// <para>⚠️ Il costo che evita è quello che la ricerca aveva già pagato una volta (11 agosto 2026): leggere a ogni
/// tasto l'intero contenuto pubblicato. Qui i payload si leggono dal database <b>solo</b> per le release che
/// l'indice non ha ancora visto, cioè una volta per pubblicazione.</para>
/// </summary>
public sealed class IndiceDelleRelease
{
    /// <summary>Un blocco ha fino a due testi (Body e BodyJson): un risultato per blocco, col primo che combacia.</summary>
    /// <param name="Livello">0 = sezione di primo livello, 1 e oltre = sotto-sezione: decide l'ordine dei risultati.</param>
    /// <param name="Chiave">La chiave di catalogo (<c>sids</c>, <c>stars</c>…): la ricerca ci aggancia le procedure.</param>
    internal sealed record Sezione(int Id, string Titolo, string Percorso, IReadOnlyList<(string? Primo, string? Secondo)> Testi,
        int Livello = 0, string Chiave = "");
    internal sealed record Voce(string Titolo, IReadOnlyList<Sezione> Sezioni, int Blocchi, int TutteLeSezioni);

    private readonly ConcurrentDictionary<(int Id, DateTime Creata), Voce> _voci = new();

    /// <summary>Quante release tiene (diagnostica e test).</summary>
    public int Voci => _voci.Count;

    internal async Task<Voce?> VoceAsync(VipiDbContext db, ReleaseTesta testa, CancellationToken ct)
    {
        var chiave = (testa.Id, testa.CreatedUtc);
        if (_voci.TryGetValue(chiave, out var nota)) return nota;

        var payload = await db.DocReleases.AsNoTracking().Where(r => r.Id == testa.Id)
            .Select(r => r.PayloadJson).FirstOrDefaultAsync(ct);
        if (ReleaseInVigore.Documento(payload) is not { } doc) return null;

        var voce = Costruisci(doc);
        _voci[chiave] = voce;
        return voce;
    }

    /// <summary>Via le voci delle release che non sono più in vigore: l'indice non cresce con la storia.</summary>
    internal void TieniSolo(IEnumerable<ReleaseTesta> inVigore)
    {
        var tenute = inVigore.Select(t => (t.Id, t.CreatedUtc)).ToHashSet();
        foreach (var k in _voci.Keys)
            if (!tenute.Contains(k)) _voci.TryRemove(k, out _);
    }

    internal static Voce Costruisci(RawDocument doc)
    {
        var sezioni = new List<Sezione>();
        var blocchi = 0;
        var tutte = 0;

        void Scendi(RawSection s, string padre, bool nascosta, int livello)
        {
            tutte++;
            blocchi += s.Blocks.Count;
            var percorso = padre.Length == 0 ? s.Title : $"{padre} › {s.Title}";
            // Una sezione nascosta si porta via il proprio sottoalbero, nel documento come nell'indice.
            var fuori = nascosta || s.IsHidden;
            if (!fuori)
                sezioni.Add(new Sezione(s.Id, s.Title, percorso, s.Blocks.Select(TestiDi).ToList(), livello, s.SectionKey ?? ""));
            foreach (var figlio in s.Children) Scendi(figlio, percorso, fuori, livello + 1);
        }

        foreach (var r in doc.Roots) Scendi(r, "", nascosta: false, livello: 0);
        return new Voce(doc.Title, sezioni, blocchi, tutte);
    }

    /// <summary>
    /// Il testo cercabile di un blocco. Un'immagine ha per testo alternativo e didascalia (il JSON porta lo sha),
    /// un allegato titolo e nota (il JSON porta lo slug): né lo sha né lo slug devono pescare risultati, né
    /// finire in un estratto.
    /// <para>Una SID citata (§A73) si indicizza col suo ultimo nome visto, non come <c>[[SID …]]</c>: il codice
    /// del riferimento non deve finire in un estratto. ⚠️ Due limiti, noti e accettati: il nome di OGGI qui non si
    /// sa senza una query per scalo, e l'indice non ne fa — se la SID è stata aggiornata, la si trova col nome di
    /// quando è stata citata; ed è il CODICE (<c>BANA8A</c>), non il nome completo che il lettore vede
    /// (<c>BANAV 8A</c>): cercando quest'ultimo il testo non si trova.</para>
    /// </summary>
    private static (string? Primo, string? Secondo) TestiDi(RawBlock b) => b.Format switch
    {
        BlockFormat.Image => (MediaRef.TextOf(b.BodyJson, b.Body), null),
        BlockFormat.Attachment => (AttachmentRef.TextOf(b.BodyJson, b.Body), null),
        _ => (Leggibile(Riferimenti.Sostituisci(b.Body, null)), Leggibile(Riferimenti.Sostituisci(b.BodyJson, null))),
    };

    /// <summary>
    /// Un testo che è JSON (i blocchi strutturati: tabelle, aeroporti alternati, radioaiuti…) diventa i suoi soli
    /// VALORI di testo, separati da « · »: «LICA · Lamezia Terme · LMT». Le chiavi, le parentesi e i numeri nudi
    /// non sono testo che qualcuno legge.
    ///
    /// <para>🔴 Committente, 30 settembre 2026: cercando «LICA» un risultato mostrava
    /// <c>{"icao":"LICA","name":"Lamezia Terme","navaids":[{"code":"LMT"…</c>. Il JSON finiva nell'indice così
    /// com'era, e con lui nell'estratto; cercare «name» o «icao» pescava ogni blocco strutturato del sito.</para>
    ///
    /// <para>Un testo che non comincia con <c>{</c> o <c>[</c>, o che non si legge come JSON, resta com'è.</para>
    /// </summary>
    /// <remarks>Le chiavi interne (<c>"Key":"cfg:bbbb0611"</c> dei gruppi e delle configurazioni, id, sha) non
    /// sono testo del documento: si saltano per nome, o cercare «cfg» pescherebbe ogni configurazione.</remarks>
    private static readonly HashSet<string> CampiTecnici = new(StringComparer.OrdinalIgnoreCase)
    {
        "Key", "Id", "Slug", "Sha", "MediaId", "SectionKey",
    };

    internal static string? Leggibile(string? testo)
    {
        if (string.IsNullOrWhiteSpace(testo)) return testo;
        var t = testo.TrimStart();
        if (t[0] != '{' && t[0] != '[') return testo;

        try
        {
            using var json = System.Text.Json.JsonDocument.Parse(t);
            var parti = new List<string>();
            void Visita(System.Text.Json.JsonElement e)
            {
                switch (e.ValueKind)
                {
                    case System.Text.Json.JsonValueKind.Object:
                        foreach (var p in e.EnumerateObject())
                            if (!CampiTecnici.Contains(p.Name)) Visita(p.Value);
                        break;
                    case System.Text.Json.JsonValueKind.Array:
                        foreach (var x in e.EnumerateArray()) Visita(x);
                        break;
                    case System.Text.Json.JsonValueKind.String:
                        var s = e.GetString();
                        if (!string.IsNullOrWhiteSpace(s)) parti.Add(s.Trim());
                        break;
                }
            }
            Visita(json.RootElement);
            // Senza doppioni: una configurazione ripete il callsign in «Open» e in «OpenCallsigns».
            return parti.Count == 0 ? null : string.Join(" · ", parti.Distinct(StringComparer.Ordinal));
        }
        catch (System.Text.Json.JsonException)
        {
            return testo;
        }
    }
}
