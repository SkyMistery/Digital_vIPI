using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>Che cosa si sta per eliminare.</summary>
public enum DeletionTargetKind
{
    Sector,
    Airport,
    Acc,
    Document,
}

/// <summary>L'indirizzo di ciò che si elimina: un tipo e la sua chiave.</summary>
public sealed record DeletionTarget(DeletionTargetKind Kind, int Id = 0, string? Code = null)
{
    public static DeletionTarget Sector(int id) => new(DeletionTargetKind.Sector, id);

    /// <summary>Il settore per callsign: è così che lo conosce l'albero della Struttura, fatto di righe di catalogo.</summary>
    public static DeletionTarget SectorByCallsign(string callsign) => new(DeletionTargetKind.Sector, 0, callsign);
    public static DeletionTarget Airport(int id) => new(DeletionTargetKind.Airport, id);
    public static DeletionTarget Document(int id) => new(DeletionTargetKind.Document, id);
    public static DeletionTarget Acc(string code) => new(DeletionTargetKind.Acc, 0, code);
}

/// <summary>Chi trattiene, in una frase, col posto dove si risolve (<c>null</c> = non c'è una pagina sola).</summary>
public sealed record DeletionBlocker(string Testo, string? Href = null);

/// <summary>
/// Il piano: <b>cosa muore</b>, <b>cosa si sposta</b>, <b>cosa resta da ripubblicare</b> e <b>chi blocca</b>.
/// È ciò che la finestra mostra prima di chiedere conferma, ed è anche ciò che l'esecuzione ricalcola: fra
/// lo schermo e il clic passa del tempo, e un altro amministratore può aver cambiato le carte.
/// </summary>
public sealed record DeletionPlan(
    DeletionTarget Bersaglio,
    string Titolo,
    IReadOnlyList<string> Muore,
    IReadOnlyList<string> SiSposta,
    IReadOnlyList<string> DaRivedere,
    IReadOnlyList<DeletionBlocker> Blocca,
    DeletionActions Azioni)
{
    public bool Eliminabile => Blocca.Count == 0;
}

/// <summary>
/// Il piano tradotto in mosse, per chi lo esegue. Nasce dalle stesse regole che hanno scritto le frasi del
/// piano, così non ci sono due verità: la finestra promette esattamente ciò che la transazione farà.
/// </summary>
public sealed record DeletionActions(
    IReadOnlyList<int> SettoriDaEliminare,
    IReadOnlyList<int> FigliDaRiappendere,
    int? NuovoPadreDeiFigli,
    IReadOnlyList<int> PartiDaEliminare,
    IReadOnlyList<int> BlocchiDaEliminare,
    IReadOnlyList<int> BlocchiDaSganciare,
    IReadOnlyList<int> DocumentiDaMarcare,
    IReadOnlyList<string> CallsignDiCatalogoDaTogliere,
    int? AeroportoDaEliminare = null,
    string? AccDaEliminare = null,
    int? DocumentoDaEliminare = null)
{
    public static readonly DeletionActions Nessuna = new(
        Array.Empty<int>(), Array.Empty<int>(), null, Array.Empty<int>(), Array.Empty<int>(),
        Array.Empty<int>(), Array.Empty<int>(), Array.Empty<string>());
}

/// <summary>Un blocco di contenuto che cita il settore, e in che veste lo cita.</summary>
/// <param name="Scope">Il blocco è <i>riferito</i> al settore: senza, resta prosa senza destinatario.</param>
/// <param name="Estremo">Il settore è un capo di un <i>da→to</i>: senza, il blocco non è incompleto, è FALSO.</param>
public sealed record BlockRefFacts(int BlockId, string Sezione, bool Scope, bool Estremo);

/// <summary>Un documento che cita il settore, e con quale forza.</summary>
/// <param name="AncoraQui">Il documento è appeso a <b>questo</b> settore (<c>Sector.DocumentId</c>).</param>
/// <param name="Parti">Id delle <c>DocumentParty</c> che puntano al settore (vLOA).</param>
/// <param name="RestaAncorato">
/// Dopo la rimozione il documento ha ancora un aggancio: un altro settore, l'aeroporto, o un'altra parte.
/// È la differenza fra «sganciare e segnalare» e «fermarsi».
/// </param>
public sealed record DocRefFacts(
    int DocumentId, string Titolo, bool AncoraQui, IReadOnlyList<int> Parti,
    IReadOnlyList<BlockRefFacts> Blocchi, bool RestaAncorato);

/// <summary>Un figlio del settore che si sta eliminando.</summary>
public sealed record ChildFacts(int SectorId, string Callsign);

/// <summary>Un accordo di coordinamento che ha il settore su uno dei due lati.</summary>
public sealed record AgreementFacts(int AgreementId, string Etichetta, string? Href);

/// <summary>Tutto ciò che serve a decidere se e come un settore si può eliminare.</summary>
public sealed record SectorFacts(
    int SectorId, string Callsign, string Name, string AccCode,
    SectorType Type, SectorKind Kind,
    int? AirportId, string? AirportIcao, int? ParentSectorId, string? ParentCallsign,
    bool IsProjected, bool CatalogoManuale, DateTime? ImportedAtUtc,
    IReadOnlyList<ChildFacts> Figli,
    IReadOnlyList<DocRefFacts> Documenti,
    IReadOnlyList<AgreementFacts> Accordi);

/// <summary>Tutto ciò che serve a decidere se e come un aeroporto si può eliminare.</summary>
public sealed record AirportFacts(
    int AirportId, string Icao, string Name, string AccCode,
    DateTime? LastSeenAtUtc, int? DocumentId, string? DocumentTitolo,
    IReadOnlyList<SectorFacts> Settori);

/// <summary>Tutto ciò che serve a decidere se e come una ACC si può eliminare.</summary>
public sealed record AccFacts(
    string Code, string Name, DateTime? ImportedAtUtc, int Settori, int Aeroporti);

/// <summary>Tutto ciò che serve a decidere se e come un documento si può eliminare.</summary>
public sealed record DocumentFacts(
    int DocumentId, string Titolo, DocumentType Tipo, bool Pubblicato,
    int Release, IReadOnlyList<string> SettoriCheLoPerdono, string? AeroportoCheLoPerde);

/// <summary>
/// Le <b>politiche di protezione</b>, in un posto solo e senza IO: dai fatti esce il piano. Le regole sono
/// quelle decise il 26 agosto 2026, carta <c>docs/feature/2026-08-26-eliminare-con-le-protezioni.md</c> §2.
///
/// <list type="number">
/// <item><b>D1 gerarchia</b> — i figli passano al nonno; se la vittima è radice diventano radici.</item>
/// <item><b>D2/D3/D4 documenti</b> — se il documento resta ancorato altrove si sgancia il riferimento e il
/// documento si marca <i>da rivedere</i>; se il settore è il suo ultimo aggancio ci si ferma e si dice di
/// eliminare prima il documento.</item>
/// <item><b>D4 blocchi, le tre vie</b> — un blocco che cita il settore come <i>estremo</i> di un da→a
/// diventerebbe <b>falso</b>: si elimina. Uno che lo cita solo come <i>ambito</i> resta, sganciato, e il
/// documento va riletto. Se il documento perde l'ultimo aggancio, si blocca.</item>
/// <item><b>D5 accordi</b> — bloccano sempre: un accordo senza un lato non è un accordo, ma è anche una
/// scelta editoriale a due, e la cancella chi l'ha scritta.</item>
/// <item><b>D6 torre</b> — TWR/I_TWR cade solo insieme all'intero aeroporto.</item>
/// <item><b>D7 settori d'aeroporto</b> — DEL/GND/APP si eliminano da soli; con lo scalo muoiono tutti.</item>
/// <item><b>D8 sorgente</b> — si elimina solo ciò che la sorgente non manda da due giri
/// (<see cref="SogliaEliminazione"/>).</item>
/// </list>
/// </summary>
public static class DeletionRules
{
    /// <summary>Il piano per un settore eliminato <b>da solo</b>.</summary>
    public static DeletionPlan PerSettore(SectorFacts f, DateTime? penultimoGiro) =>
        PerSettore(f, penultimoGiro, dentroLoScalo: false);

    /// <param name="dentroLoScalo">
    /// Vero quando il settore cade come parte dell'eliminazione del suo aeroporto: solo allora la torre può
    /// andarsene (D6), e solo allora il documento <b>dell'aeroporto</b> non conta come aggancio perduto,
    /// perché lo si sta valutando a parte.
    /// </param>
    public static DeletionPlan PerSettore(SectorFacts f, DateTime? penultimoGiro, bool dentroLoScalo)
    {
        var muore = new List<string> { $"il settore {f.Callsign} ({f.Name})" };
        var sposta = new List<string>();
        var rivedere = new List<string>();
        var blocca = new List<DeletionBlocker>();

        // D1 — i figli al nonno. Non è un blocco: è un UPDATE prima del DELETE.
        foreach (var c in f.Figli)
            sposta.Add(f.ParentCallsign is { } nonno
                ? $"{c.Callsign} passa sotto {nonno}"
                : $"{c.Callsign} diventa radice");

        // D6 — la torre cade solo con lo scalo.
        if (!dentroLoScalo && f.Type is SectorType.Twr or SectorType.ITwr && f.AirportId is not null)
            blocca.Add(new DeletionBlocker(
                $"{f.Callsign} è la torre di {f.AirportIcao}: una torre si elimina solo insieme all'intero aeroporto",
                f.AirportIcao is { } icao ? $"/services/vsop/{f.AccCode.ToLowerInvariant()}/airports/editor?icao={icao}" : null));

        // D8 — la sorgente deve tacere da due giri. I settori aggiunti a mano non la riguardano.
        if (f.IsProjected && !f.CatalogoManuale
            && !SogliaEliminazione.Consentita(f.ImportedAtUtc, penultimoGiro, isManual: false))
            blocca.Add(new DeletionBlocker(
                $"{f.Callsign} non si può eliminare: " +
                SogliaEliminazione.MotivoDelRifiuto(f.ImportedAtUtc, penultimoGiro, isManual: false)));

        // D5 — gli accordi bloccano sempre, e si dice quali.
        foreach (var a in f.Accordi)
            blocca.Add(new DeletionBlocker($"elimina prima l'accordo di coordinamento «{a.Etichetta}»", a.Href));

        // D2/D3/D4 — un documento per volta.
        var parti = new List<int>();
        var blocchiVia = new List<int>();
        var blocchiSganciati = new List<int>();
        var daMarcare = new List<int>();

        foreach (var d in f.Documenti)
        {
            if (!d.RestaAncorato)
            {
                blocca.Add(new DeletionBlocker(
                    $"elimina prima il documento «{d.Titolo}»: {f.Callsign} è il suo ultimo aggancio",
                    "/services/vsop/versions"));
                continue;
            }

            var pezzi = new List<string>();
            if (d.AncoraQui) pezzi.Add("perde questo settore");
            if (d.Parti.Count > 0)
            {
                parti.AddRange(d.Parti);
                pezzi.Add(d.Parti.Count == 1 ? "perde una parte" : $"perde {d.Parti.Count} parti");
            }

            var estremi = d.Blocchi.Where(b => b.Estremo).ToList();
            var soloAmbito = d.Blocchi.Where(b => !b.Estremo && b.Scope).ToList();
            if (estremi.Count > 0)
            {
                blocchiVia.AddRange(estremi.Select(b => b.BlockId));
                muore.Add(estremi.Count == 1
                    ? $"un blocco di «{d.Titolo}» che raccontava un passaggio da o verso {f.Callsign}"
                    : $"{estremi.Count} blocchi di «{d.Titolo}» che raccontavano passaggi da o verso {f.Callsign}");
            }
            if (soloAmbito.Count > 0)
            {
                blocchiSganciati.AddRange(soloAmbito.Select(b => b.BlockId));
                pezzi.Add(soloAmbito.Count == 1
                    ? "un blocco resta senza il settore a cui era riferito"
                    : $"{soloAmbito.Count} blocchi restano senza il settore a cui erano riferiti");
            }

            if (pezzi.Count > 0 || estremi.Count > 0)
            {
                daMarcare.Add(d.DocumentId);
                rivedere.Add($"«{d.Titolo}» — {string.Join(", ", pezzi.Count > 0 ? pezzi : new List<string> { "va riletto" })}");
            }
        }

        var azioni = new DeletionActions(
            SettoriDaEliminare: new[] { f.SectorId },
            FigliDaRiappendere: f.Figli.Select(c => c.SectorId).ToList(),
            NuovoPadreDeiFigli: f.ParentSectorId,
            PartiDaEliminare: parti,
            BlocchiDaEliminare: blocchiVia,
            BlocchiDaSganciare: blocchiSganciati,
            DocumentiDaMarcare: daMarcare,
            CallsignDiCatalogoDaTogliere: f.IsProjected ? new[] { f.Callsign } : Array.Empty<string>());

        return new DeletionPlan(DeletionTarget.Sector(f.SectorId), f.Callsign,
            muore, sposta, rivedere, blocca, azioni);
    }

    /// <summary>
    /// Il piano per un aeroporto: lo scalo più <b>tutti</b> i suoi settori (D7), ciascuno con le proprie
    /// protezioni. Il documento dello scalo non cade con lui: è un bersaglio a parte, e finché c'è blocca.
    /// </summary>
    public static DeletionPlan PerAeroporto(AirportFacts f, DateTime? penultimoGiroAeroporti,
        DateTime? penultimoGiroSettori)
    {
        var muore = new List<string> { $"l'aeroporto {f.Icao} ({f.Name})" };
        var sposta = new List<string>();
        var rivedere = new List<string>();
        var blocca = new List<DeletionBlocker>();

        // D8 sullo scalo: la sorgente non deve nominarlo da due giri.
        if (!SogliaEliminazione.Consentita(f.LastSeenAtUtc, penultimoGiroAeroporti, isManual: false))
            blocca.Add(new DeletionBlocker(
                $"{f.Icao} non si può eliminare: " +
                SogliaEliminazione.MotivoDelRifiuto(f.LastSeenAtUtc, penultimoGiroAeroporti, isManual: false)));

        // Il documento dello scalo: si elimina prima, a mano. Non lo si porta via di straforo.
        if (f.DocumentId is not null)
            blocca.Add(new DeletionBlocker(
                $"elimina prima il documento «{f.DocumentTitolo}»: è la vIPI di {f.Icao}",
                "/services/vsop/versions"));

        var settori = new List<int>();
        var figli = new List<int>();
        var parti = new List<int>();
        var blocchiVia = new List<int>();
        var blocchiSganciati = new List<int>();
        var daMarcare = new List<int>();
        var callsign = new List<string>();
        int? nuovoPadre = null;

        foreach (var s in f.Settori)
        {
            var p = PerSettore(s, penultimoGiroSettori, dentroLoScalo: true);
            muore.AddRange(p.Muore);
            sposta.AddRange(p.SiSposta);
            rivedere.AddRange(p.DaRivedere);
            blocca.AddRange(p.Blocca);

            settori.AddRange(p.Azioni.SettoriDaEliminare);
            // ⚠️ I figli di un settore dello scalo NON sono per forza settori dello scalo: un APP può avere
            // sotto di sé la torre di un altro campo. Il riaggancio al nonno resta quello del singolo padre.
            figli.AddRange(p.Azioni.FigliDaRiappendere.Where(id => !settori.Contains(id)));
            nuovoPadre ??= p.Azioni.NuovoPadreDeiFigli;
            parti.AddRange(p.Azioni.PartiDaEliminare);
            blocchiVia.AddRange(p.Azioni.BlocchiDaEliminare);
            blocchiSganciati.AddRange(p.Azioni.BlocchiDaSganciare);
            daMarcare.AddRange(p.Azioni.DocumentiDaMarcare);
            callsign.AddRange(p.Azioni.CallsignDiCatalogoDaTogliere);
        }

        var azioni = new DeletionActions(
            SettoriDaEliminare: settori,
            FigliDaRiappendere: figli.Distinct().ToList(),
            NuovoPadreDeiFigli: nuovoPadre,
            PartiDaEliminare: parti.Distinct().ToList(),
            BlocchiDaEliminare: blocchiVia.Distinct().ToList(),
            BlocchiDaSganciare: blocchiSganciati.Distinct().ToList(),
            DocumentiDaMarcare: daMarcare.Distinct().ToList(),
            CallsignDiCatalogoDaTogliere: callsign.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            AeroportoDaEliminare: f.AirportId);

        return new DeletionPlan(DeletionTarget.Airport(f.AirportId), f.Icao,
            muore, sposta, rivedere, blocca, azioni);
    }

    /// <summary>
    /// Il piano per una ACC. ⚠️ Qui <b>non</b> c'è cascata: una ACC porta interi aeroporti e decine di
    /// settori, e un tasto che li porta via tutti insieme è una perdita che nessuna finestra di conferma
    /// rende reversibile. La politica è «svuotala prima», e l'elenco dice quanto manca.
    /// </summary>
    public static DeletionPlan PerAcc(AccFacts f, DateTime? penultimoGiroAcc)
    {
        var blocca = new List<DeletionBlocker>();

        if (!SogliaEliminazione.Consentita(f.ImportedAtUtc, penultimoGiroAcc, isManual: false))
            blocca.Add(new DeletionBlocker(
                $"{f.Code} non si può eliminare: " +
                SogliaEliminazione.MotivoDelRifiuto(f.ImportedAtUtc, penultimoGiroAcc, isManual: false)));

        if (f.Settori > 0)
            blocca.Add(new DeletionBlocker(
                f.Settori == 1
                    ? $"{f.Code} ha ancora un settore: eliminalo prima"
                    : $"{f.Code} ha ancora {f.Settori} settori: eliminali prima",
                "/services/vsop/admin/sector-structure"));

        if (f.Aeroporti > 0)
            blocca.Add(new DeletionBlocker(
                f.Aeroporti == 1
                    ? $"{f.Code} ha ancora un aeroporto: eliminalo o spostalo prima"
                    : $"{f.Code} ha ancora {f.Aeroporti} aeroporti: eliminali o spostali prima",
                "/services/vsop/admin/airports"));

        return new DeletionPlan(DeletionTarget.Acc(f.Code), f.Code,
            new[] { $"la ACC {f.Code} ({f.Name})" }, Array.Empty<string>(), Array.Empty<string>(), blocca,
            DeletionActions.Nessuna with { AccDaEliminare = f.Code });
    }

    /// <summary>
    /// Il piano per un documento. Un documento è <b>nostro</b>: nessuna sorgente lo rivendica e niente lo
    /// blocca. Quel che serve è che chi conferma veda cosa perde — le release e chi resta senza pagina.
    /// </summary>
    public static DeletionPlan PerDocumento(DocumentFacts f)
    {
        var muore = new List<string> { $"il documento «{f.Titolo}»" };
        if (f.Release > 0)
            muore.Add(f.Release == 1 ? "la sua pubblicazione" : $"le sue {f.Release} pubblicazioni");
        foreach (var s in f.SettoriCheLoPerdono) muore.Add($"il legame con il settore {s}");
        if (f.AeroportoCheLoPerde is { } icao) muore.Add($"il legame con l'aeroporto {icao}");

        return new DeletionPlan(DeletionTarget.Document(f.DocumentId), f.Titolo,
            muore, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<DeletionBlocker>(),
            DeletionActions.Nessuna with { DocumentoDaEliminare = f.DocumentId });
    }
}
