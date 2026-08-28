using Vipi.Application.Abstractions;
using Vipi.Application.Content;
using Vipi.Domain;

namespace Vipi.MilSopLoader;

/// <summary>Che cosa è successo (o succederebbe) a una sezione.</summary>
public sealed record RigaPiano(string Chiave, string Titolo, int BlocchiDaScrivere, string Esito);

/// <summary>
/// Scrive un SOP trascritto nelle sezioni del documento militare di un campo.
///
/// <para>
/// ⚠️ <b>Non tocca una sezione che ha già contenuto.</b> Il documento è di chi lo redige: se qualcuno ci ha
/// già scritto dentro, questo strumento si ferma e lo dice. Ripassarci sopra è la via più rapida per
/// cancellare il lavoro di una persona senza che nessuno se ne accorga.
/// </para>
/// </summary>
public sealed class Caricatore
{
    private readonly IEditingRepository _editing;
    private readonly IMilitaryDocumentService _militari;
    private readonly int _autore;

    public Caricatore(IEditingRepository editing, IMilitaryDocumentService militari, int autore)
    {
        _editing = editing;
        _militari = militari;
        _autore = autore;
    }

    /// <summary>Prepara il piano; con <paramref name="applica"/> lo esegue.</summary>
    public async Task<(int DocumentId, IReadOnlyList<RigaPiano> Piano)> EseguiAsync(
        SopTrascritto sop, bool applica, CancellationToken ct = default)
    {
        // Il documento: se non c'è lo crea, come farebbe il tasto dell'elenco. Idempotente.
        var docId = await _militari.CreaAsync(sop.Icao, ct);
        var doc = await _editing.LoadForEditAsync(docId, ct)
            ?? throw new InvalidOperationException($"documento {docId} non caricabile.");

        var perChiave = Appiattisci(doc.Sections)
            .ToDictionary(s => s.SectionKey, StringComparer.OrdinalIgnoreCase);

        var piano = new List<RigaPiano>();

        foreach (var sezione in sop.Sezioni)
        {
            if (!perChiave.TryGetValue(sezione.Chiave, out var target))
            {
                // Il profilo non ha quella chiave: è un errore di trascrizione, non un caso da ignorare.
                piano.Add(new RigaPiano(sezione.Chiave, "—", sezione.Blocchi.Count,
                    "SALTATA: la chiave non esiste nel profilo AirportMil"));
                continue;
            }

            // ⚠️ Il blocco SEGNAPOSTO delle sezioni rese dalla pagina non conta come contenuto: nasce vuoto
            // alla creazione del documento, e scambiarlo per lavoro di qualcuno bloccherebbe proprio le due
            // sezioni che hanno più da dire (frequenze e piste).
            var suoi = target.Blocks.Where(b => !Vuoto(b)).ToList();
            if (suoi.Count > 0)
            {
                piano.Add(new RigaPiano(sezione.Chiave, target.Title, sezione.Blocchi.Count,
                    $"SALTATA: ha già {suoi.Count} blocchi con contenuto"));
                continue;
            }

            var postilla = sop.SenzaContenuto.TryGetValue(sezione.Chiave, out var manca)
                ? $" — INCOMPLETA, {manca}"
                : "";
            piano.Add(new RigaPiano(sezione.Chiave, target.Title, sezione.Blocchi.Count,
                (applica ? "scritta" : "da scrivere") + postilla));

            if (!applica) continue;

            foreach (var blocco in sezione.Blocchi)
            {
                var id = await _editing.AddBlockAsync(target.Id, blocco.Formato,
                    BlockTier.Extended, BlockVisibility.Always, ct);
                await _editing.UpdateBlockAsync(id, new BlockEdit
                {
                    Tier = BlockTier.Extended,
                    Visibility = BlockVisibility.Always,
                    CalloutKind = blocco.Avviso,
                    Body = blocco.Testo,
                    BodyJson = blocco.Json,
                }, ct);
            }

            if (sezione.Destinatario != SectionAudience.Both)
                await _editing.SetSectionAudienceAsync(target.Id, sezione.Destinatario, ct);
        }

        // Le sezioni del profilo che questa trascrizione non tocca. ⚠️ Il motivo NON è uno solo, e
        // appiattirli in «vuota» sarebbe la cosa peggiore che questo rendiconto possa fare: una sezione
        // vuota perché è un contenitore, una vuota perché la scheda la disegna la pagina e una vuota perché
        // qualcuno se l'è dimenticata si assomigliano solo a chi non guarda.
        foreach (var s in Appiattisci(doc.Sections))
        {
            if (sop.Sezioni.Any(x => string.Equals(x.Chiave, s.SectionKey, StringComparison.OrdinalIgnoreCase)))
                continue;

            // Contenitore e «resa dalla pagina» li dice il CATALOGO, non una lista scritta a mano qui:
            // così una sezione aggiunta al profilo domani è classificata bene senza toccare lo strumento.
            var motivo =
                sop.SenzaContenuto.TryGetValue(s.SectionKey, out var m) ? $"vuota — {m}"
                : s.Children.Count > 0 ? "vuota — è un contenitore: il contenuto sta nelle sue sotto-sezioni"
                : SectionCatalog.IsHostRendered(SectionProfile.AirportMil, s.SectionKey)
                    ? "vuota — la scheda la disegna la pagina, e l'originale non ha altro da aggiungere"
                    : "vuota — l'originale non ha questa sezione";
            piano.Add(new RigaPiano(s.SectionKey, s.Title, 0, motivo));
        }

        // Le sezioni che su questo campo non esistono: si nascondono. È indipendente dal contenuto, quindi
        // si applica anche a un documento già caricato — ed è idempotente.
        foreach (var chiave in sop.DaNascondere)
        {
            if (!perChiave.TryGetValue(chiave, out var s)) continue;
            if (s.IsHidden) continue;
            piano.Add(new RigaPiano(chiave, s.Title, 0,
                applica ? "NASCOSTA: l'originale non ha questa procedura" : "da nascondere: l'originale non ha questa procedura"));
            if (applica) await _editing.SetSectionHiddenAsync(s.Id, true, ct);
        }

        return (docId, piano);
    }

    /// <summary>Un blocco senza corpo: il segnaposto delle sezioni rese dalla pagina.</summary>
    private static bool Vuoto(EditableBlock b) =>
        string.IsNullOrWhiteSpace(b.Body) && string.IsNullOrWhiteSpace(b.BodyJson);

    private static IEnumerable<EditableSection> Appiattisci(IEnumerable<EditableSection> sezioni) =>
        sezioni.SelectMany(s => new[] { s }.Concat(Appiattisci(s.Children)));
}
