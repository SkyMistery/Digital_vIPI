using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>Che cosa si sta per eliminare.</summary>
public enum DeletionTargetKind
{
    Sector,
    Airport,
    Acc,
    Document,

    /// <summary>Un candidato confinante: la coppia ACC nostro ↔ ACC estero da cui nasce una vLOA.</summary>
    Neighbour,

    /// <summary>Un'area regolamentata del catalogo (per id IVAO).</summary>
    Area,
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
    public static DeletionTarget Neighbour(int id) => new(DeletionTargetKind.Neighbour, id);
    public static DeletionTarget Area(string ivaoId) => new(DeletionTargetKind.Area, 0, ivaoId);
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
    DeletionActions Azioni,
    IReadOnlyList<string>? Note = null)
{
    public bool Eliminabile => Blocca.Count == 0;

    /// <summary>
    /// Quel che <b>non</b> muore e <b>non</b> si sposta, ma va saputo prima di premere: un riferimento che
    /// resterà a puntare nel vuoto, una riga che sopravvive da un'altra parte. Non è un blocco — è la
    /// differenza fra una conseguenza scelta e una scoperta dopo.
    /// </summary>
    public IReadOnlyList<string> Avvisi => Note ?? Array.Empty<string>();
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
    IReadOnlyList<CatalogReparent> RiaggancioDiCatalogo,
    int? AeroportoDaEliminare = null,
    string? AccDaEliminare = null,
    int? DocumentoDaEliminare = null,
    int? CandidatoDaEliminare = null,
    string? AreaDaEliminare = null)
{
    public static readonly DeletionActions Nessuna = new(
        Array.Empty<int>(), Array.Empty<int>(), null, Array.Empty<int>(), Array.Empty<int>(),
        Array.Empty<int>(), Array.Empty<int>(), Array.Empty<string>(), Array.Empty<CatalogReparent>());
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

/// <summary>Un figlio del settore che si sta eliminando, nella proiezione.</summary>
public sealed record ChildFacts(int SectorId, string Callsign);

/// <summary>
/// Una riga che si appende al settore <b>per callsign</b>: un'altra riga di catalogo, o un aeroporto.
///
/// <para>⚠️ Non è la stessa cosa dei figli della proiezione, ed è la differenza che conta. Il contenimento
/// vive nel <b>catalogo</b> (<c>ParentCallsign</c>) e la proiezione lo ricalcola da lì a ogni sync: se si
/// riappendesse solo il <c>Sector</c>, il giro successivo tornerebbe a leggere un padre che non esiste più,
/// non lo troverebbe, e il figlio diventerebbe <b>radice</b>. La promessa «i figli passano al nonno»
/// durerebbe meno di un giorno.</para>
///
/// <para>E comprende righe che la proiezione <b>non conosce affatto</b>: un settore nascosto, un aeroporto
/// (che è una foglia dell'albero, non un settore), una riga di catalogo che nessun <c>Sector</c> specchia.</para>
/// </summary>
/// <param name="Dove">In quale tabella sta: serve a chi esegue, non a chi legge.</param>
public sealed record CatalogChildFacts(string Callsign, CatalogChildKind Dove);

/// <summary>Le tre tabelle che portano un <c>ParentCallsign</c>.</summary>
public enum CatalogChildKind { AccSector, AirportSector, Airport }

/// <summary>Una riga di catalogo da riappendere: da chi muore, al primo antenato che sopravvive.</summary>
public sealed record CatalogReparent(string Figlio, CatalogChildKind Dove, string? NuovoPadre);

/// <summary>Un accordo di coordinamento che ha il settore su uno dei due lati.</summary>
public sealed record AgreementFacts(int AgreementId, string Etichetta, string? Href);

/// <summary>Tutto ciò che serve a decidere se e come un settore si può eliminare.</summary>
public sealed record SectorFacts(
    int SectorId, string Callsign, string Name, string AccCode,
    SectorType Type, SectorKind Kind,
    int? AirportId, string? AirportIcao, int? ParentSectorId, string? ParentCallsign,
    bool IsProjected, bool CatalogoManuale, DateTime? ImportedAtUtc,
    IReadOnlyList<ChildFacts> Figli,
    IReadOnlyList<CatalogChildFacts> FigliDiCatalogo,
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
/// <param name="Incarichi">
/// Titoli degli incarichi editoriali che puntano a questo documento. ⚠️ Il legame è <b>debole</b>
/// (<c>TargetType</c> + <c>TargetKey</c>, senza chiave esterna): eliminando il documento l'incarico resta,
/// con la sua etichetta vecchia e senza più un collegamento che apra qualcosa.
/// </param>
public sealed record DocumentFacts(
    int DocumentId, string Titolo, DocumentType Tipo, bool Pubblicato,
    int Release, IReadOnlyList<string> SettoriCheLoPerdono, string? AeroportoCheLoPerde,
    IReadOnlyList<string>? Incarichi = null);

/// <summary>Tutto ciò che serve a decidere di un candidato confinante.</summary>
/// <param name="SettoreEsteroPresente">Il settore estero materializzato dalla conferma esiste ancora: non
/// muore col candidato, e chi conferma deve saperlo.</param>
public sealed record NeighbourFacts(
    int Id, string HomeAccCode, string ForeignAccCode, string ForeignAccName,
    string ForeignRootCallsign, bool Confermato, int? VloaDocumentId, string? VloaTitolo,
    bool SettoreEsteroPresente);

/// <summary>Tutto ciò che serve a decidere di un'area regolamentata.</summary>
/// <param name="Enti">Quanti ACC la elencano: l'area è di tutti, non di chi la sta guardando.</param>
/// <param name="Documenti">I documenti che la citano: resteranno da rivedere. ⚠️ Con l'<b>Id</b>, non il solo
/// titolo — nell'archivio vero due documenti diversi possono chiamarsi allo stesso modo (misurato: due
/// «vIPI Roma»), e due righe identiche in una finestra sembrano un difetto della finestra.</param>
public sealed record AreaFacts(
    string IvaoId, string Nome, int Enti, IReadOnlyList<AffectedDoc> Documenti);

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
        //
        // ⚠️ Si tocca il CATALOGO, non solo la proiezione. Il contenimento vive in `ParentCallsign` e la
        // proiezione lo ricalcola da lì a ogni sync: riappendere il solo `Sector` sarebbe una promessa che
        // dura fino a stanotte, e poi i figli diventerebbero radici senza che nessuno l'abbia chiesto.
        var nonnoCs = f.ParentCallsign;
        foreach (var c in f.Figli)
            sposta.Add(nonnoCs is { } n1 ? $"{c.Callsign} passa sotto {n1}" : $"{c.Callsign} diventa radice");

        var riaggancio = new List<CatalogReparent>();
        foreach (var c in f.FigliDiCatalogo)
        {
            riaggancio.Add(new CatalogReparent(c.Callsign, c.Dove, nonnoCs));
            // Chi è già stato nominato come figlio della proiezione non si ripete: è la stessa cosa vista
            // da due parti, e a schermo sarebbe una riga doppia.
            if (f.Figli.Any(x => string.Equals(x.Callsign, c.Callsign, StringComparison.OrdinalIgnoreCase))) continue;
            sposta.Add(nonnoCs is { } n2 ? $"{c.Callsign} passa sotto {n2}" : $"{c.Callsign} diventa radice");
        }

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
                    $"elimina prima il documento «{d.Titolo}»: {f.Callsign} è il suo ultimo aggancio " +
                    "(in Documenti, o in «Da sistemare» se lì non compare)",
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
            CallsignDiCatalogoDaTogliere: f.IsProjected ? new[] { f.Callsign } : Array.Empty<string>(),
            RiaggancioDiCatalogo: riaggancio);

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
        var riaggancio = new List<CatalogReparent>();
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
            riaggancio.AddRange(p.Azioni.RiaggancioDiCatalogo);
        }

        // ⚠️ Dentro una cascata il «nonno» può essere a sua volta in lista: la torre pende dall'APP, e nello
        // scalo muoiono tutti e due. Riappendere a un callsign che sta per sparire rifarebbe il buco che
        // questo riaggancio esiste per evitare — quindi si risale finché non si trova qualcuno che resta.
        var morituri = f.Settori.Select(x => x.Callsign).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var padreDi = f.Settori.ToDictionary(x => x.Callsign, x => x.ParentCallsign, StringComparer.OrdinalIgnoreCase);
        string? PrimoSuperstite(string? cs)
        {
            var visti = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (cs is not null && morituri.Contains(cs) && visti.Add(cs))
                cs = padreDi.TryGetValue(cs, out var su) ? su : null;
            return cs;
        }
        riaggancio = riaggancio
            .Where(r => !morituri.Contains(r.Figlio))     // chi muore non ha bisogno di un padre nuovo
            .Select(r => r with { NuovoPadre = PrimoSuperstite(r.NuovoPadre) })
            .ToList();

        var azioni = new DeletionActions(
            SettoriDaEliminare: settori,
            FigliDaRiappendere: figli.Distinct().ToList(),
            NuovoPadreDeiFigli: nuovoPadre,
            PartiDaEliminare: parti.Distinct().ToList(),
            BlocchiDaEliminare: blocchiVia.Distinct().ToList(),
            BlocchiDaSganciare: blocchiSganciati.Distinct().ToList(),
            DocumentiDaMarcare: daMarcare.Distinct().ToList(),
            CallsignDiCatalogoDaTogliere: callsign.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            RiaggancioDiCatalogo: riaggancio,
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

        // ⚠️ Gli incarichi puntano al documento per (tipo, chiave), senza chiave esterna: non si rompe
        // niente e nessuno se ne accorge. Restano nell'elenco col titolo di prima e senza collegamento —
        // «aggiorna la vIPI Roma» per una vIPI Roma che non c'è più.
        var note = new List<string>();
        var incarichi = f.Incarichi ?? Array.Empty<string>();
        if (incarichi.Count > 0)
            note.Add(incarichi.Count == 1
                ? $"un incarico resterà senza documento: «{incarichi[0]}» — l'elenco lo mostrerà ancora, ma il collegamento non aprirà più niente"
                : $"{incarichi.Count} incarichi resteranno senza documento ({string.Join(", ", incarichi.Take(3).Select(t => $"«{t}»"))}{(incarichi.Count > 3 ? ", …" : "")}): l'elenco li mostrerà ancora, ma il collegamento non aprirà più niente");

        return new DeletionPlan(DeletionTarget.Document(f.DocumentId), f.Titolo,
            muore, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<DeletionBlocker>(),
            DeletionActions.Nessuna with { DocumentoDaEliminare = f.DocumentId }, note);
    }

    /// <summary>
    /// Il piano per un <b>candidato confinante</b>: la coppia ACC nostro ↔ ACC estero da cui nasce una vLOA.
    ///
    /// <para>Blocca finché c'è la vLOA — stessa regola di D2: il documento si elimina prima, a mano. Quel
    /// che invece <b>non</b> muore col candidato è il settore estero materializzato dalla conferma: è una
    /// riga di catalogo a sé, e si toglie dalla Struttura come qualsiasi altro settore.</para>
    /// </summary>
    public static DeletionPlan PerConfinante(NeighbourFacts f)
    {
        var muore = new List<string> { $"il candidato confinante {f.HomeAccCode} ↔ {f.ForeignAccCode} ({f.ForeignAccName})" };
        var blocca = new List<DeletionBlocker>();
        var note = new List<string>();

        if (f.VloaDocumentId is not null)
            blocca.Add(new DeletionBlocker(
                $"elimina prima la vLOA «{f.VloaTitolo}»: è nata da questo candidato",
                "/services/vsop/versions"));

        if (f.SettoreEsteroPresente)
            note.Add($"il settore estero {f.ForeignRootCallsign} resta in archivio: si elimina dalla Struttura, non da qui");

        if (f.Confermato)
            note.Add("era un confinante CONFERMATO: al prossimo giro dei confinanti la coppia può ricomparire fra i candidati in attesa");

        return new DeletionPlan(DeletionTarget.Neighbour(f.Id), $"{f.HomeAccCode} ↔ {f.ForeignAccCode}",
            muore, Array.Empty<string>(), Array.Empty<string>(), blocca,
            DeletionActions.Nessuna with { CandidatoDaEliminare = f.Id }, note);
    }

    /// <summary>
    /// Il piano per un'<b>area regolamentata</b>. Non la blocca niente: è una riga di catalogo, e nessun
    /// vincolo del database la trattiene. Ma i documenti che la citano restano a nominare un'area che non
    /// esiste più — e per questo si marcano da rivedere, esattamente come quando è l'import a potarla.
    ///
    /// <para>⚠️ Si elimina l'<b>area</b>, non il legame con un ente: se più ACC la elencano, sparisce per
    /// tutti. È scritto nel piano perché non lo si scopra dopo.</para>
    /// </summary>
    public static DeletionPlan PerArea(AreaFacts f)
    {
        var muore = new List<string> { $"l'area regolamentata «{f.Nome}» ({f.IvaoId})" };
        if (f.Enti > 0)
            muore.Add(f.Enti == 1 ? "il legame con l'ente che la elenca" : $"i legami con i {f.Enti} enti che la elencano");

        // Il numero del documento compare SOLO quando il titolo si ripete: sempre sarebbe rumore, mai
        // renderebbe due righe gemelle indistinguibili.
        var omonimi = f.Documenti.GroupBy(d => d.Title, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1).Select(g => g.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rivedere = f.Documenti
            .Select(d => omonimi.Contains(d.Title)
                ? $"«{d.Title}» (documento {d.Id}) — cita un'area che non esisterà più"
                : $"«{d.Title}» — cita un'area che non esisterà più")
            .ToList();

        var note = new List<string>();
        if (f.Enti > 1)
            note.Add($"la elencano {f.Enti} enti: sparisce per tutti, non solo per quello da cui stai guardando");
        note.Add("se la sorgente la rimanda, il prossimo import la ricrea");

        return new DeletionPlan(DeletionTarget.Area(f.IvaoId), f.Nome,
            muore, Array.Empty<string>(), rivedere, Array.Empty<DeletionBlocker>(),
            DeletionActions.Nessuna with { AreaDaEliminare = f.IvaoId }, note);
    }
}
