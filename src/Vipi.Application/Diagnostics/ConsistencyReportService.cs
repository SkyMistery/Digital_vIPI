using Vipi.Application.Abstractions;
using Vipi.Domain;

namespace Vipi.Application.Diagnostics;

/// <summary>
/// Report di consistenza dei soft-ref (Fase 2, audit 22 lug): le etichette denormalizzate e i riferimenti
/// per callsign non hanno FK (scelta deliberata: sopravvivono agli snapshot pubblicati e ai rename config),
/// quindi possono divergere dalla fonte. Questo servizio li <b>rileva</b> — non li corregge, non li vincola.
/// </summary>
public interface IConsistencyReportService
{
    Task<IReadOnlyList<ConsistencyFinding>> RunAsync(CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class ConsistencyReportService : IConsistencyReportService
{
    private readonly IConsistencyReportRepository _repo;
    private readonly ISchemaDriftProbe? _schema;
    private readonly Auth.IAdminCoverageService? _admin;
    private readonly IServerSettingsProbe? _server;
    private readonly IStartupMaintenanceReport? _startup;
    private readonly IImportPolicyStore? _policy;
    private readonly ISectorfileComparisonReport? _sectorfile;

    /// <param name="schema">
    /// Opzionale: se c'è, al report si aggiunge il drift fra modello EF e schema fisico. Sta qui e non in
    /// <see cref="Analyze"/> perché non è un'incongruenza di <i>dati</i> ma di <i>schema</i>, e perché Analyze deve
    /// restare una funzione pura sul dataset di dominio. Agganciandolo in questo punto — l'unico consumato sia da
    /// <c>/services/vsop/admin/diagnostics</c> sia dall'health check — entrambi lo mostrano senza modifiche a valle.
    /// </param>
    /// <param name="admin">
    /// Opzionale, come <paramref name="schema"/> e per la stessa ragione: non è un'incongruenza di <i>dati</i>
    /// ma di <b>configurazione</b> — se nessuno degli staff code osservati vale admin, in produzione nessuno
    /// può editare e non lo si rimedia da dentro. Agganciato qui perché è l'unico punto letto sia dalla
    /// diagnostica sia dall'health check.
    /// </param>
    /// <param name="server">
    /// Opzionale, e non è un'incongruenza di dati né di schema ma delle <b>impostazioni del server di
    /// database</b> — <c>sql_mode</c> e <c>max_allowed_packet</c>, che l'applicazione assume e non può
    /// imporre. Agganciato qui per la stessa ragione degli altri: è il punto letto sia dalla diagnostica sia
    /// dall'health check.
    /// </param>
    /// <param name="startup">
    /// Opzionale: i guasti delle manutenzioni d'avvio non critiche. Quelle passate ora catturano gli errori
    /// e lasciano proseguire l'avvio (un guasto lì, con <c>Restart=always</c>, era un ciclo di riavvii);
    /// perché «proseguire» non diventi «nessuno lo sa», il guasto esce di qui.
    /// </param>
    /// <param name="policy">
    /// Opzionale: il <b>regime di scrittura</b> dell'applicazione — quali categorie la sorgente può
    /// sovrascrivere. Non è un dato editoriale né una configurazione di file: è una riga sola in archivio, e
    /// se sparisce l'applicazione torna a «tutto da sorgente» <b>in silenzio</b>. Agganciato qui per la
    /// ragione degli altri: è il punto letto sia dalla diagnostica sia dall'health check.
    /// </param>
    /// <param name="sectorfile">
    /// Opzionale: la fotografia dell'ultimo confronto fra i cataloghi IVAO e il <b>sectorfile Aurora</b>.
    /// Come <paramref name="startup"/> non è una sonda — il confronto è già successo, per conto suo, e qui si
    /// legge soltanto: fa I/O di rete, e questo report lo legge anche <c>/vsop/health</c>, che è anonimo.
    /// </param>
    public ConsistencyReportService(IConsistencyReportRepository repo, ISchemaDriftProbe? schema = null,
        Auth.IAdminCoverageService? admin = null, IServerSettingsProbe? server = null,
        IStartupMaintenanceReport? startup = null, IImportPolicyStore? policy = null,
        ISectorfileComparisonReport? sectorfile = null,
        Content.IImportOverviewService? giri = null,
        Abstractions.ICopPositions? punti = null,
        Abstractions.ISectorVolumeCatalog? volumi = null,
        Abstractions.ITopologyProvider? topologia = null)
    {
        _repo = repo;
        _schema = schema;
        _admin = admin;
        _server = server;
        _startup = startup;
        _policy = policy;
        _sectorfile = sectorfile;
        _giri = giri;
        _punti = punti;
        _volumi = volumi;
        _topologia = topologia;
    }

    /// <summary>Volumi e topologia: servono alla scala di risalita. Opzionali, come tutto il resto qui.</summary>
    private readonly Abstractions.ISectorVolumeCatalog? _volumi;
    private readonly Abstractions.ITopologyProvider? _topologia;

    /// <summary>Dove stanno i punti dei CoP. Opzionale: senza, il rilievo sui CoP non si fa.</summary>
    private readonly Abstractions.ICopPositions? _punti;

    /// <summary>Lo stato dei giri periodici. Opzionale: senza, il report e' quello di prima.</summary>
    private readonly Content.IImportOverviewService? _giri;

    /// <summary>
    /// Categoria dei guasti delle sonde stesse. ⚠️ Non è un dettaglio interno: se una sonda non ha risposto,
    /// «zero rilievi in quell'area» **non** significa «va tutto bene», e chi legge deve saperlo.
    /// </summary>
    public const string CategoriaSondaRotta = "Sonda non riuscita";

    /// <summary>
    /// Dove si va a riparare, per famiglia di rilievo. Sta qui e non nella pagina perché è chi produce il
    /// rilievo a sapere dove si ripara — vedi <see cref="ConsistencyFinding.Where"/>.
    /// </summary>
    private const string DoveAccordi = "/services/vsop/admin/transfers";
    private const string DoveStruttura = "/services/vsop/admin/sector-structure";
    private const string DoveSorgenti = "/services/vsop/admin/sources";

    /// <summary>
    /// L'elenco dei documenti, non l'editor del singolo. ⚠️ Scelta dichiarata: la riga porta il <i>titolo</i>
    /// del documento, non il suo Id, e la rotta dell'editor dipende dal tipo e dall'ACC — costruirla di qui
    /// vorrebbe dire portarsi dietro il registro delle rotte per documento (<c>IDocKindRoutes</c>) dentro
    /// l'analisi pura. Meglio un link vero a un passo di distanza che uno preciso e sbagliato.
    /// </summary>
    private const string DoveDocumenti = "/services/vsop/versions";

    public async Task<IReadOnlyList<ConsistencyFinding>> RunAsync(CancellationToken ct = default)
    {
        var findings = new List<ConsistencyFinding>();

        // ⚠️ Ogni pezzo nel proprio try, e il guasto diventa un RILIEVO invece di travolgere il resto.
        //
        // Prima erano cinque chiamate in fila senza protezione, e le conseguenze erano due — la seconda
        // peggiore: (1) una sonda che lancia (il server MySQL che non risponde, la connessione caduta sotto
        // la sonda di drift) uccideva il circuito Blazor della pagina, che è proprio la pagina dove si va a
        // capire cosa non va; (2) anche prendendo l'eccezione più in alto, il guasto di UNA sonda cancellava
        // il lavoro di tutte le altre — un problema del server di database nascondeva una pista orfana che
        // `Analyze` aveva già trovato.
        //
        // È la lezione di `StartupMaintenanceReport`, che sta in questa stessa cartella e che questo servizio
        // consuma: «un guasto non deve uccidere il giro, ma non deve nemmeno restare zitto». Non era
        // applicata alle sonde di chi quel registro lo legge.
        // ⚠️ Il guasto eredita l'AREA del pezzo che non è riuscito: è l'area di cui il report non sa più dire
        // niente, ed è la sola cosa che rende quel rilievo utile a chi guarda i conteggi per area.
        await Raccogli(findings, "incongruenze dei dati", "Diag_Pezzo_Dati", ConsistencyArea.Dati,
            async () => Analyze(await _repo.LoadAsync(ct),
                _punti is null ? null : await _punti.GetAsync(ct),
                await ContestoDelRinvioAsync(ct)), ct);
        if (_schema is not null)
            await Raccogli(findings, "drift di schema", "Diag_Pezzo_Schema", ConsistencyArea.Schema, () => _schema.RunAsync(ct), ct);
        if (_admin is not null)
            await Raccogli(findings, "copertura admin", "Diag_Pezzo_Admin", ConsistencyArea.Configurazione, () => _admin.RunAsync(ct), ct);
        if (_server is not null)
            await Raccogli(findings, "impostazioni del server", "Diag_Pezzo_Server", ConsistencyArea.Server, () => _server.RunAsync(ct), ct);
        // Non è una sonda: è già successo, all'avvio. Qui si legge soltanto — e può solo fallire se qualcuno
        // ci mettesse dentro dell'I/O, quindi passa dallo stesso cancello per non doverlo ricordare.
        if (_startup is not null)
            await Raccogli(findings, "manutenzioni d'avvio", "Diag_Pezzo_Avvio", ConsistencyArea.Avvio,
                () => Task.FromResult(_startup.Findings), ct);
        if (_policy is not null)
            await Raccogli(findings, "policy di import", "Diag_Pezzo_Policy", ConsistencyArea.Dati,
                async () => PolicyDiImport(await _policy.GetInfoAsync(ct)), ct);
        // Come le manutenzioni d'avvio: qui NON si confronta, si legge la fotografia che il giro periodico ha
        // gia' preso. Passa dallo stesso cancello per non doversi ricordare che non fa I/O.
        if (_sectorfile is not null)
            await Raccogli(findings, "coerenza col sectorfile", "Diag_Pezzo_Sectorfile", ConsistencyArea.Sectorfile,
                () => Task.FromResult(_sectorfile.Findings), ct);
        if (_giri is not null)
            await Raccogli(findings, "giri periodici", "Diag_Pezzo_Giri", ConsistencyArea.Avvio,
                async () => GiriFermi(await _giri.ListAsync(ct)), ct);

        return findings;
    }

    /// <summary>
    /// Esegue un pezzo del report e ne accoda i rilievi; se lancia, accoda <b>il guasto</b> e prosegue.
    /// </summary>
    private static async Task Raccogli(List<ConsistencyFinding> findings, string pezzo, string pezzoKey,
        ConsistencyArea area, Func<Task<IReadOnlyList<ConsistencyFinding>>> esegui, CancellationToken ct)
    {
        try
        {
            findings.AddRange(await esegui());
        }
        // ⚠️ Prima di `catch (Exception)`: la richiesta annullata non è un guasto della sonda, ed è l'unica
        // eccezione che non va trasformata in un rilievo (nessuno lo leggerebbe: la risposta non parte).
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            findings.Add(new ConsistencyFinding(CategoriaSondaRotta, ConsistencySeverity.Error, pezzo,
                $"Il controllo «{pezzo}» non è andato a buon fine ({ex.GetType().Name}: {ex.Message}). " +
                "Gli altri controlli sono stati eseguiti lo stesso, ma di quest'area il report non sa dire " +
                "niente: l'assenza di rilievi qui non vuol dire che vada tutto bene.", area,
                CategoryKey: "Diag_Cat_SondaRotta", DetailKey: "Diag_Msg_SondaRotta",
                // ⚠️ Il nome del pezzo NON entra negli argomenti: sta già nella colonna del bersaglio, e da
                // lì lo traduce il narratore. Ripetuto qui compariva grezzo — in italiano dentro una frase
                // inglese — perché un argomento è un valore, non una chiave.
                DetailArgs: new object[] { ex.GetType().Name, ex.Message },
                EntityKey: pezzoKey));
        }
    }

    /// <summary>
    /// Il regime di scrittura in vigore, quando <b>non l'ha deciso nessuno</b>. Funzione pura sul solo
    /// <see cref="ImportPolicyInfo"/>: il fatto è già tutto lì.
    ///
    /// <para>Due rilievi diversi perché sono due guasti diversi. <b>Riga assente</b>: una <c>DELETE</c> sulla
    /// tabella riporta l'applicazione a «la sorgente scrive tutto», e il primo giro dopo sovrascrive TA e
    /// piste messe a mano — la riga è <b>una sola</b> in tutto il database, quindi non è un caso teorico.
    /// <b>Riga mai decisa con qualcosa di manuale</b>: quei <c>false</c> vengono dal default di una colonna,
    /// non da una scelta (è la storia di <c>ImportSids</c>, nato spento su un DB già popolato), e un import
    /// fermo da mesi è indistinguibile da una scelta dell'amministratore.</para>
    /// </summary>
    /// <summary>
    /// I giri periodici che <b>non stanno girando</b>: <c>ImportHealth.Ferma</c> vuol dire che l'ultimo
    /// successo e' piu' vecchio di <b>due</b> cadenze, cioe' che almeno un giro e' stato saltato.
    ///
    /// <para>🔴 <b>Perche' esiste.</b> Quel segnale c'era gia', ma si vedeva <b>solo aprendo la pagina
    /// Sorgenti</b>. La Diagnostica — che e' la pagina che si apre per chiedere «c'e' qualcosa che non
    /// va?» — degli import non sapeva niente: il 2 settembre 2026 diceva <b>«Avvio 0»</b> mentre meta' dei
    /// giri periodici non partiva. Uno zero che rassicura sul contrario di quel che succede e' peggio di
    /// nessun numero.</para>
    ///
    /// <para>⚠️ <b>Area <c>Avvio</c>, e la scelta e' deliberata</b> (l'enum non ha un default apposta):
    /// «l'istanza gira, ma non e' partita intera» descrive letteralmente questo caso, e il destinatario e'
    /// lo stesso — chi guarda il processo e l'hosting, non chi apre un editor. Su Plesk+Passenger la causa
    /// tipica non e' un errore ma un <b>processo spento per inattivita'</b> prima che il giro arrivasse al
    /// suo ritardo d'avvio: nessun errore da nessuna parte, e infatti <c>UltimoErrore</c> e' nullo.</para>
    ///
    /// <para>⚠️ Solo <c>Ferma</c>: <c>InErrore</c> ha gia' il suo messaggio con la causa vera, e
    /// duplicarlo qui direbbe due volte la stessa cosa in due aree diverse.</para>
    /// </summary>
    public static IReadOnlyList<ConsistencyFinding> GiriFermi(IReadOnlyList<Content.ImportOverviewRow> righe)
    {
        var fermi = righe.Where(r => r.Stato == Content.ImportHealth.Ferma).ToList();
        if (fermi.Count == 0) return Array.Empty<ConsistencyFinding>();

        var oggi = DateTime.UtcNow;
        return fermi.Select(r =>
        {
            var giorni = r.UltimoSuccessoUtc is DateTime u ? (int)Math.Floor((oggi - u).TotalDays) : -1;
            var quando = giorni < 0 ? "mai" : giorni == 0 ? "oggi" : $"{giorni} giorni fa";
            return new ConsistencyFinding("Giro periodico fermo", ConsistencySeverity.Warning,
                r.StateKey,
                $"L'ultimo giro riuscito e' {quando}, oltre il doppio della sua cadenza. Non c'e' un errore " +
                "registrato: quando succede senza errore, di solito il processo si e' spento prima che il " +
                "giro arrivasse al suo ritardo d'avvio. Si guarda in `diagnostica/avvii.txt` se il processo " +
                "vive abbastanza.",
                ConsistencyArea.Avvio, DoveSorgenti,
                CategoryKey: "Diag_Cat_GiroFermo", DetailKey: "Diag_Msg_GiroFermo",
                DetailArgs: new object[] { quando },
                EntityKey: null);
        }).ToList();
    }

    public static IReadOnlyList<ConsistencyFinding> PolicyDiImport(ImportPolicyInfo info)
    {
        if (!info.RigaPresente)
        {
            return new[]
            {
                new ConsistencyFinding("Policy di import assente", ConsistencySeverity.Warning,
                    "Policy di import",
                    "La riga della policy non c'è: vale il default «tutto da sorgente», e nessuno l'ha scelto. " +
                    "Se qualche categoria era manuale, il prossimo giro di import la sovrascrive senza dirlo. " +
                    "Si chiude salvando la policy voluta dalla pagina Sorgenti, anche identica a quella che si vede.",
                    ConsistencyArea.Dati, DoveSorgenti,
                    CategoryKey: "Diag_Cat_PolicyAssente", DetailKey: "Diag_Msg_PolicyAssente",
                    EntityKey: "Diag_Ent_PolicyImport"),
            };
        }

        if (!info.MaiDecisa) return Array.Empty<ConsistencyFinding>();

        // Solo se qualcosa è davvero manuale: una policy tutta «da sorgente» e mai toccata è il default
        // dichiarato del prodotto, non un'anomalia da mostrare a ogni apertura della pagina.
        var manuali = Enum.GetValues<ImportCategory>().Where(c => !info.Policy.IsImported(c))
            .Select(c => c.ToString()).ToArray();
        if (manuali.Length == 0) return Array.Empty<ConsistencyFinding>();

        var elenco = string.Join(", ", manuali);
        return new[]
        {
            new ConsistencyFinding("Policy di import mai decisa", ConsistencySeverity.Warning,
                "Policy di import",
                $"Queste categorie risultano manuali senza che nessuno l'abbia scelto: {elenco}. " +
                "Il valore viene dal default della colonna, quindi un import fermo da mesi qui è " +
                "indistinguibile da una decisione. Si chiude salvando la policy dalla pagina Sorgenti.",
                ConsistencyArea.Dati, DoveSorgenti,
                CategoryKey: "Diag_Cat_PolicyMaiDecisa", DetailKey: "Diag_Msg_PolicyMaiDecisa",
                DetailArgs: new object[] { elenco },
                EntityKey: "Diag_Ent_PolicyImport"),
        };
    }

    /// <summary>Come si nomina una clausola a video: numero, ACC e punti. Un posto solo, perché tre rilievi
    /// diversi parlano della stessa clausola e devono chiamarla allo stesso modo.</summary>
    private static object[] ArgomentiClausola(TransferConditionRow t) =>
        new object[] { t.ClauseId, t.AccCode, t.Points };

    // Logica pura (nessuna dipendenza da EF): il dataset è già in memoria ⇒ testabile con fixture.
    /// <param name="punti">
    /// Dove stanno i punti scrivibili in un CoP. <b>Facoltativo</b>: senza, il rilievo «CoP senza posizione»
    /// non si fa — e non si fa <b>in silenzio</b> di proposito, perche' un catalogo assente non prova che i
    /// punti manchino. E' la stessa regola di <c>NavaidCheck</c>: a catalogo vuoto NIENTE e' sconosciuto.
    /// </param>
    /// <param name="rinvio">
    /// Volumi, punti e topologia, per percorrere la <b>scala di risalita</b> di ogni punto. <b>Facoltativo</b>:
    /// senza, il rilievo «trasferimento senza ripiego» non si fa — e non si fa in silenzio, perché senza i
    /// volumi non si potrebbe distinguere «non ha ripieghi» da «non lo so».
    /// </param>
    public static IReadOnlyList<ConsistencyFinding> Analyze(ConsistencyDataset d,
        Abstractions.CopPositions? punti = null, Content.CoverageFallbackContext? rinvio = null)
    {
        var findings = new List<ConsistencyFinding>();

        foreach (var t in d.TransferConditions)
        {
            var who = $"Clausola #{t.ClauseId} ({t.AccCode}, punti {t.Points})";

            // 1) Pista orfana: soft-ref valorizzato ma la pista non esiste più.
            if (t.ConditionRefId is int refId && !d.RunwayIdents.ContainsKey(refId))
            {
                findings.Add(new ConsistencyFinding("Pista orfana", ConsistencySeverity.Error, who,
                    $"ConditionRefId={refId} non corrisponde a nessuna pista: rimossa o re-importata con altro Id.",
                    ConsistencyArea.Dati, DoveAccordi,
                    CategoryKey: "Diag_Cat_PistaOrfana", DetailKey: "Diag_Msg_PistaOrfana",
                    DetailArgs: new object[] { refId },
                    EntityKey: "Diag_Ent_Clausola", EntityArgs: ArgomentiClausola(t)));
            }
            // 2) Label divergente: la pista esiste ma il suo ident non compare più nell'etichetta denormalizzata.
            else if (t.ConditionRefId is int okId
                     && d.RunwayIdents.TryGetValue(okId, out var ident)
                     && !string.IsNullOrWhiteSpace(t.ConditionLabel)
                     && !t.ConditionLabel!.Contains(ident, StringComparison.OrdinalIgnoreCase))
            {
                findings.Add(new ConsistencyFinding("Label pista divergente", ConsistencySeverity.Warning, who,
                    $"La pista referenziata è ora «{ident}» ma l'etichetta salvata è «{t.ConditionLabel}»: rinominata dopo il salvataggio.",
                    ConsistencyArea.Dati, DoveAccordi,
                    CategoryKey: "Diag_Cat_LabelPista", DetailKey: "Diag_Msg_LabelPista",
                    DetailArgs: new object[] { ident, t.ConditionLabel! },
                    EntityKey: "Diag_Ent_Clausola", EntityArgs: ArgomentiClausola(t)));
            }

            // 3) Area fantasma: l'area denormalizzata non corrisponde ad alcuna area speciale esistente.
            if (!string.IsNullOrWhiteSpace(t.ConditionAreaLabel) && !d.AreaNames.Contains(t.ConditionAreaLabel!.Trim()))
            {
                findings.Add(new ConsistencyFinding("Area fantasma", ConsistencySeverity.Warning, who,
                    $"Area «{t.ConditionAreaLabel}» non presente tra le aree speciali: rinominata o rimossa.",
                    ConsistencyArea.Dati, DoveAccordi,
                    CategoryKey: "Diag_Cat_AreaFantasma", DetailKey: "Diag_Msg_AreaFantasma",
                    DetailArgs: new object[] { t.ConditionAreaLabel! },
                    EntityKey: "Diag_Ent_Clausola", EntityArgs: ArgomentiClausola(t)));
            }
        }

        // 4) Gerarchia dangling: un padre di copertura per callsign che non risolve ad alcun nodo dei cataloghi.
        foreach (var p in d.ParentRefs)
        {
            if (!d.ValidCallsigns.Contains(p.ParentCallsign))
            {
                findings.Add(new ConsistencyFinding("Gerarchia dangling", ConsistencySeverity.Error,
                    $"{p.Kind} {p.Reference}",
                    $"ParentCallsign «{p.ParentCallsign}» non esiste nei cataloghi: catena di copertura interrotta.",
                    ConsistencyArea.Dati, DoveStruttura,
                    CategoryKey: "Diag_Cat_GerarchiaDangling", DetailKey: "Diag_Msg_GerarchiaDangling",
                    DetailArgs: new object[] { p.ParentCallsign },
                    EntityKey: p.KindKey, EntityArgs: new object[] { p.Reference }));
            }
        }

        // 4-bis) Gerarchia ciclica: un settore è antenato di sé stesso nell'albero EFFETTIVO.
        //
        // ⚠️ È la rete, non la guardia. La guardia sta in `EfHierarchyEditingService` e impedisce di crearne
        // uno dall'interfaccia; qui si prendono quelli che entrano da tutte le altre porte — import, seed, DB
        // toccato a mano, il riaggancio dell'eliminazione, la rinomina — che padri li scrivono senza chiedere
        // niente a nessuno. Serve perché un anello NON si manifesta come un errore: tutti i lettori hanno una
        // guardia sui nodi già visti, quindi la catena di ricaduta si tronca in silenzio dove l'anello si
        // richiude, e il traffico finisce su un antenato arbitrario senza che una riga di log lo dica.
        foreach (var anello in Aor.HierarchyRules.FindAllCycles(d.EffectiveParents))
        {
            var percorso = string.Join(" → ", anello) + " → " + anello[0];
            findings.Add(new ConsistencyFinding("Gerarchia ciclica", ConsistencySeverity.Error,
                anello[0],
                $"Il settore è antenato di sé stesso ({percorso}): ogni catena di ricaduta che ci passa si interrompe qui.",
                ConsistencyArea.Dati, DoveStruttura,
                CategoryKey: "Diag_Cat_GerarchiaCiclica", DetailKey: "Diag_Msg_GerarchiaCiclica",
                DetailArgs: new object[] { percorso }));
        }

        // 4-ter) Lo stesso callsign in TUTTI E DUE i cataloghi: l'albero effettivo ne tiene uno solo.
        //
        // ⚠️ Non è un errore d'ingresso e non lo può diventare: l'unicità di ComposePosition è garantita
        // dentro ciascuna tabella da un indice, le tabelle sono due, e niente vieta la stessa chiave in
        // tutte e due. Fino al 7 settembre 2026 la seconda riga sovrascriveva la prima in silenzio: il
        // settore perdeva il padre vero e la ricaduta finiva su un ente sbagliato o su UNICOM — visibile
        // solo a valle, come «una gerarchia che non è quella che ho scritto» (R-014).
        foreach (var d2 in d.HierarchyDuplicates)
        {
            var tenuto = d2.PadreTenuto ?? "—";
            var scartato = d2.PadreScartato ?? "—";
            findings.Add(new ConsistencyFinding("Callsign in due cataloghi", ConsistencySeverity.Error,
                d2.Callsign,
                $"Lo stesso callsign è nel catalogo ACC e in quello d'aeroporto: l'albero di copertura tiene " +
                $"il padre «{tenuto}» e scarta «{scartato}». Va tolto da uno dei due.",
                ConsistencyArea.Dati, DoveStruttura,
                CategoryKey: "Diag_Cat_CallsignDueCataloghi", DetailKey: "Diag_Msg_CallsignDueCataloghi",
                DetailArgs: new object[] { tenuto, scartato }));
        }

        // 5) Area regolamentata dangling: un id salvato in una sezione «regulated» che non è più nei cataloghi.
        //    Il prune dell'import cancella le aree sparite dalla sorgente, ma la selezione salvata nel documento le
        //    cita ancora: il viewer le salta in silenzio (SpecialAreaProjection) e l'area sparisce senza dirlo.
        foreach (var r in d.RegulatedRefs)
        {
            var sel = Content.RegulatedSelectionJson.Parse(r.Json);
            // Le aree del proprio ACC in automatico non sono id salvati ma la lista viva: non possono essere dangling.
            var missing = sel.OwnIds.Concat(sel.ExtraIds)
                .Where(id => !string.IsNullOrWhiteSpace(id) && !d.SpecialAreaIds.Contains(id.Trim()))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (missing.Count == 0) continue;

            findings.Add(new ConsistencyFinding("Area regolamentata dangling", ConsistencySeverity.Warning,
                $"{r.Kind} {r.Reference}",
                $"Aree selezionate non più presenti: {string.Join(", ", missing)}. Rimosse dalla sorgente e potate " +
                "dall'import; nel documento restano citate ma non vengono mostrate.",
                ConsistencyArea.Dati, DoveDocumenti,
                CategoryKey: "Diag_Cat_AreaRegDangling", DetailKey: "Diag_Msg_AreaRegDangling",
                DetailArgs: new object[] { string.Join(", ", missing) }));
        }

        // 4-quater) La ricaduta di un settore NON copre il cielo che quel settore occupa.
        //
        // 🔴 Un settore chiuso manda il traffico a chi, a quella quota, non ha niente — e non succede niente
        // di visibile: la ricaduta RIESCE, verso l'ente sbagliato. Due forme dello stesso difetto, e sono
        // tutt'e due vere in produzione:
        //   · `LIMM_MIL_CTR` e' SFC-UNL ma pende da `LIMM_WS2_CTR`, che si ferma a FL325: un settore
        //     SOVRAPPOSTO non e' il sottoalbero di nessuno, e il suo padre e' una bugia strutturale;
        //   · `LIMM_ES5_CTR` sta FL325-UNL e pende da `LIMM_ES2_CTR`, che a quella quota non c'e': regge
        //     solo per UNA riga dichiarata, e chi la cancella non vede nessun errore.
        // Carta docs/feature/2026-09-10-rinvio-geometrico.md, Parte 8.
        findings.AddRange(RicadutaCheNonCopreLaQuota(d));

        // 4-quinquies) L'albero PROIETTATO e quello dei CATALOGHI dicono due cose diverse.
        //
        // ⚠️ La proiezione nasce dai cataloghi, quindi devono coincidere — ma sono due letture, e chi le usa
        // e' diverso: la ricaduta legge i cataloghi, la geometria (`EfSectorVolumeCatalog`, e con lei le
        // statistiche e il rinvio) legge la proiezione. Se divergono, alla stessa domanda si ottengono due
        // risposte a seconda di chi la fa — ed e' il difetto «due alberi» che questa base di codice ha gia'
        // pagato una volta.
        foreach (var cs in d.EffectiveParents.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            if (!d.ProjectedParents.TryGetValue(cs, out var proiettato)) continue;   // non proiettato: e' un altro rilievo
            var catalogo = d.EffectiveParents[cs];
            if (string.Equals(catalogo ?? "", proiettato ?? "", StringComparison.OrdinalIgnoreCase)) continue;

            var a = catalogo ?? "—";
            var b = proiettato ?? "—";
            findings.Add(new ConsistencyFinding("Albero proiettato divergente", ConsistencySeverity.Error, cs,
                $"I cataloghi dicono che il padre e' «{a}», la proiezione dice «{b}»: ricaduta e copertura rispondono due cose diverse.",
                ConsistencyArea.Dati, DoveStruttura,
                CategoryKey: "Diag_Cat_AlberoDivergente", DetailKey: "Diag_Msg_AlberoDivergente",
                DetailArgs: new object[] { a, b },
                EntityKey: "Diag_Ent_Settore", EntityArgs: new object[] { cs }));
        }

        // 4-septies) I trasferimenti che, chiuso il ricevente, non hanno NESSUNO.
        //
        // ⚠️ Non e' «CoP senza posizione» con altre parole: quello dice che il rinvio non potra' rispondere,
        // questo dice che la CATENA non porta da nessuna parte — ed e' vero anche su un punto collocato
        // benissimo, per esempio quando il ricevente e' una radice senza ripieghi (in produzione
        // `LIRR_MIL_CTR` lo e'). Il pannello della Parte 10 serve a chi ha un sospetto; questo serve a non
        // doverne avere uno.
        if (rinvio is not null && d.TransferLadders.Count > 0)
        {
            var perAcc = new SortedDictionary<string, SortedSet<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in d.TransferLadders)
            {
                if (string.IsNullOrWhiteSpace(t.NextSectorCallsign)) continue;

                var scala = Content.RisalitaScala.Costruisci(
                    t.NextSectorCallsign!, t.Cop, t.LevelFeet, t.OwningSectorCallsign, rinvio);
                if (!Content.RisalitaScala.FinisceSubitoSuUnicom(scala)) continue;

                // ⚠️ «Finisce su UNICOM» da solo NON è un difetto, ed è la lezione più importante di questo
                // rilievo: sopra un ACC non c'è niente per costruzione, quindi la radice di Brindisi e le
                // radici estere finiscono su UNICOM ed è giusto così. Misurato al primo giro dal vivo:
                // otto riceventi segnalati su LIBB, e sei erano ACC esteri. Un avviso che grida su dati
                // corretti si impara a ignorare, e allora smette di servire anche quando ha ragione.
                //
                // Il difetto è un altro: quel punto lo copre QUALCUN ALTRO, e la catena non ci arriva. È il
                // caso di `LIRR_MIL_CTR`, sovrapposto ai civili di Roma e però radice.
                var chiAltro = rinvio.Con(SenzaDiLui(rinvio.TuttiISettori, t.NextSectorCallsign!))
                    .Risolvi(t.Cop, t.LevelFeet, t.OwningSectorCallsign, t.NextSectorCallsign);
                if (chiAltro.Outcome != Content.CoverageFallbackOutcome.Resolved) continue;

                if (!perAcc.TryGetValue(t.AccCode, out var elenco))
                    perAcc[t.AccCode] = elenco = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
                elenco.Add($"{t.NextSectorCallsign} → {chiAltro.TargetCallsign}");
            }

            foreach (var (acc, riceventi) in perAcc)
            {
                var nomi = string.Join(", ", riceventi);
                findings.Add(new ConsistencyFinding("Trasferimento senza ripiego", ConsistencySeverity.Error, acc,
                    $"Chiuso il ricevente il traffico va su UNICOM, ma quel punto lo copre qualcun altro: manca un ripiego ({nomi}).",
                    ConsistencyArea.Dati, DoveAccordi,
                    CategoryKey: "Diag_Cat_TrasferimentoSenzaRipiego", DetailKey: "Diag_Msg_TrasferimentoSenzaRipiego",
                    DetailArgs: new object[] { nomi },
                    EntityKey: "Diag_Ent_Acc", EntityArgs: new object[] { acc }));
            }
        }

        // 4-sexies) I CoP che un rinvio non potra' mai collocare.
        //
        // ⚠️ Dice QUANTO E' CIECO il rinvio prima di accenderlo, invece di scoprirlo un punto alla volta. E
        // distingue due cose che si somigliano e non lo sono: `Y01-Y12` NON e' un punto — e' un tratto di
        // aerovie, la risposta va scritta a mano e non c'e' niente da aggiustare — mentre un nome di cinque
        // lettere che nessun catalogo colloca e' un dato che manca, e si apre una coordinata in anagrafica.
        // Qui si segnala solo il secondo. Carta 2026-09-10-rinvio-geometrico.md, Parti 5 e 8.
        if (punti is not null && punti.Count > 0)
        {
            // ⚠️ Si guardano TUTTI i punti, non le sole clausole con una condizione: quel filtro è giusto per
            // il controllo delle piste e sarebbe una vista parziale qui. Se l'elenco dei punti non c'è (chi
            // monta il report senza gli accordi) si ripiega sulle condizioni, che è meglio di niente.
            var sorgente = d.TransferLadders.Count > 0
                ? d.TransferLadders.Select(t => (t.AccCode, Punti: t.Cop))
                : d.TransferConditions.Select(t => (t.AccCode, Punti: t.Points));

            var senzaPosizione = new SortedDictionary<string, SortedSet<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in sorgente)
                foreach (var token in Content.CopList.Parse(t.Punti))
                {
                    if (!Content.NavaidCheck.IsCheckable(token)) continue;   // non e' un punto: non e' un difetto
                    if (punti.TryGet(token, out _)) continue;

                    if (!senzaPosizione.TryGetValue(t.AccCode, out var elenco))
                        senzaPosizione[t.AccCode] = elenco = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
                    elenco.Add(token.Trim().ToUpperInvariant());
                }

            foreach (var (acc, elenco) in senzaPosizione)
            {
                var nomi = string.Join(", ", elenco);
                findings.Add(new ConsistencyFinding("CoP senza posizione", ConsistencySeverity.Warning, acc,
                    $"{elenco.Count} punti usati negli accordi non stanno in nessun catalogo ({nomi}): su di loro il ripiego «copertura del punto» non puo' rispondere.",
                    ConsistencyArea.Dati, DoveAccordi,
                    CategoryKey: "Diag_Cat_CopSenzaPosizione", DetailKey: "Diag_Msg_CopSenzaPosizione",
                    DetailArgs: new object[] { elenco.Count, nomi },
                    EntityKey: "Diag_Ent_Acc", EntityArgs: new object[] { acc }));
            }
        }

        findings.AddRange(CallsignAmbigui(d.ValidCallsigns));
        findings.AddRange(ShapeDiSorgente(d.SectorShapes));
        return findings;
    }

    /// <summary>Suffissi che un volume di spazio aereo ce l'hanno: per gli altri la shape non è attesa.</summary>
    private static readonly HashSet<string> ConVolume =
        new(StringComparer.OrdinalIgnoreCase) { "CTR", "FSS", "TWR", "APP", "DEP" };

    /// <summary>
    /// Quel che non va nelle shape che arrivano dalla sorgente. Tre cose, tutte con la stessa conseguenza —
    /// <b>il traffico non si attribuisce</b> — e nessuna riparabile da dentro l'applicazione.
    ///
    /// <para><b>Perché questo controllo esiste.</b> Il 24 agosto 2026 <c>LIRR_TS_CTR</c> è risultato non
    /// attribuire <b>mai</b> niente: la sua shape arriva da IVAO col contorno ripetuto due volte, e col test
    /// pari/dispari un anello doppio si annulla. Se n'è accorto un occhio umano guardando una vista 3D. Senza
    /// una riga che lo dica, un settore muto resta muto per mesi: le sue ore ci sono, il suo traffico è zero,
    /// e zero somiglia molto a «non è passato nessuno».</para>
    ///
    /// <para>⚠️ Si legge il JSON <b>grezzo</b>, non i punti già interpretati: <c>ParsePoints</c> ripara al
    /// volo, quindi chi guarda il risultato non vede più l'anomalia che deve raccontare.</para>
    /// </summary>
    private static IEnumerable<ConsistencyFinding> ShapeDiSorgente(IReadOnlyList<SectorShapeRow> shapes)
    {
        foreach (var s in shapes)
        {
            var grezzi = Aor.PolygonGeometry.PuntiGrezzi(s.RawPolygon);

            if (grezzi.Count == 0)
            {
                // DEL/GND/ATIS non hanno un volume: per loro l'assenza è la normalità, non un rilievo.
                if (s.Position is null || !ConVolume.Contains(s.Position.Trim())) continue;

                yield return new ConsistencyFinding("Settore senza poligono", ConsistencySeverity.Warning,
                    $"{s.Kind} {s.Callsign}",
                    "La sorgente non espone una shape per questo settore: non compare nelle mappe e non può " +
                    "attribuire traffico. Le sue ore restano contate, i suoi movimenti saranno sempre zero.",
                    ConsistencyArea.Sorgente, DoveStruttura,
                    CategoryKey: "Diag_Cat_ShapeAssente", DetailKey: "Diag_Msg_ShapeAssente");
                continue;
            }

            var copie = Aor.PolygonGeometry.CopieDellAnello(grezzi);
            if (copie > 1)
            {
                yield return new ConsistencyFinding("Contorno ripetuto", ConsistencySeverity.Warning,
                    $"{s.Kind} {s.Callsign}",
                    $"La shape di sorgente contiene lo stesso anello {copie} volte ({grezzi.Count} punti). " +
                    "L'applicazione lo ripara in lettura; senza quella correzione il settore non conterrebbe " +
                    "nulla e il suo traffico sarebbe sempre zero.",
                    ConsistencyArea.Sorgente, DoveStruttura,
                    CategoryKey: "Diag_Cat_ContornoRipetuto", DetailKey: "Diag_Msg_ContornoRipetuto",
                    DetailArgs: new object[] { copie, grezzi.Count });
                continue;
            }

            if (s.IsSynthetic)
            {
                yield return new ConsistencyFinding("Shape sintetica", ConsistencySeverity.Warning,
                    $"{s.Kind} {s.Callsign}",
                    "La sorgente non dà il poligono di questa torre: si usa un cerchio di 5 NM. Il traffico " +
                    "attribuito qui è una stima, non una misura.",
                    ConsistencyArea.Sorgente, DoveStruttura,
                    CategoryKey: "Diag_Cat_ShapeSintetica", DetailKey: "Diag_Msg_ShapeSintetica");
            }
        }
    }

    /// <summary>
    /// Callsign che si confondono fra loro nella risoluzione live del ricevente.
    ///
    /// <para><b>Perché esiste.</b> <see cref="Content.TransferOnlineResolver"/> non confronta i callsign solo per
    /// uguaglianza: accetta anche il candidato che sia un <i>segmento</i> del callsign online o una sua
    /// sottostringa lunga. Serve a far risalire la copertura (un ACC online copre i suoi settori), ma se due
    /// callsign del catalogo si assomigliano abbastanza, un settore online ne fa apparire online un altro — e al
    /// controllore comparirebbe un consegnatario che non c'è.</para>
    ///
    /// <para><b>Misurato prima di decidere</b> (9 agosto 2026): sui 313 callsign reali le coppie che collidono
    /// sono <b>zero</b>, perché nessun callsign è privo di underscore e nessuno è contenuto in un altro — quindi
    /// nella pratica l'euristica si riduce al match esatto. Da qui la scelta di <b>non</b> introdurre una tabella
    /// di mapping esplicita (voce E1): sarebbe manutenzione in più a parità di comportamento. Questa regola è la
    /// sentinella che rende revocabile quella scelta: se un domani nasce un settore che collide, si vede qui
    /// invece che in frequenza.</para>
    ///
    /// <para>Il confronto <b>riusa il resolver</b> invece di ricopiarne le regole: se l'euristica cambia, questa
    /// diagnosi cambia con lei.</para>
    /// </summary>
    private static IEnumerable<ConsistencyFinding> CallsignAmbigui(IReadOnlySet<string> callsigns)
    {
        var elenco = callsigns.Where(c => !string.IsNullOrWhiteSpace(c)).OrderBy(c => c, StringComparer.Ordinal).ToList();
        var uno = new HashSet<string>(1, StringComparer.OrdinalIgnoreCase);

        foreach (var candidato in elenco)
        {
            foreach (var altro in elenco)
            {
                if (string.Equals(candidato, altro, StringComparison.OrdinalIgnoreCase)) continue;

                uno.Clear();
                uno.Add(altro);
                if (Content.TransferOnlineResolver.FirstOnline(new[] { candidato }, uno) is null) continue;

                yield return new ConsistencyFinding("Callsign ambiguo (risoluzione live)", ConsistencySeverity.Warning,
                    candidato,
                    $"Con «{altro}» online, «{candidato}» risulterebbe online anche se non lo è: i due callsign si " +
                    "confondono nella risalita della copertura. Rinominare uno dei due, o introdurre una tabella " +
                    "esplicita callsign↔postazione.", ConsistencyArea.Dati, DoveStruttura,
                    CategoryKey: "Diag_Cat_CallsignAmbiguo", DetailKey: "Diag_Msg_CallsignAmbiguo",
                    DetailArgs: new object[] { altro, candidato });
            }
        }
    }

    /// <summary>
    /// I settori la cui <b>catena di ricaduta</b> non contiene nessuno che copra il loro stesso piede.
    ///
    /// <para>Si guarda il piede e non tutta la banda di proposito: un settore alto che ricade su uno basso
    /// perde <b>tutto</b> il suo cielo, ed e' il caso che si vuole raccontare. Un ripiego che copre il piede
    /// ma non il tetto e' una divisione legittima, non un difetto.</para>
    ///
    /// <para>⚠️ I <b>rinvii</b> qui contano come «copre»: il loro bersaglio dipende dal punto, e questo
    /// report i punti non li ha. Segnalarli direbbe il falso proprio sulle righe scritte per riparare questo
    /// difetto.</para>
    /// </summary>
    private static IEnumerable<ConsistencyFinding> RicadutaCheNonCopreLaQuota(ConsistencyDataset d)
    {
        if (d.SectorBands.Count == 0) yield break;

        var bande = new Dictionary<string, (int Bottom, int Top)>(StringComparer.OrdinalIgnoreCase);
        foreach (var b in d.SectorBands)
            bande[b.Callsign] = Aor.AorFlBand.Normalize(b.LowerLimit, b.UpperLimit);

        foreach (var b in d.SectorBands.OrderBy(x => x.Callsign, StringComparer.OrdinalIgnoreCase))
        {
            var (piede, _) = bande[b.Callsign];
            if (piede <= Aor.AorFlBand.Ground) continue;   // parte da terra: qualunque ripiego lo tocca

            // La catena alla QUOTA DEL PIEDE, che e' la quota piu' bassa che questo settore possiede.
            var piediDelPiede = piede * 100;
            var catena = Content.FallbackChain.Candidates(
                b.Callsign, piediDelPiede, d.Fallbacks,
                cs => d.EffectiveParents.TryGetValue(cs, out var p) ? p : null);

            var copre = false;
            var rinvio = d.Fallbacks.TryGetValue(b.Callsign, out var righe)
                         && righe.Any(r => r.Kind == Domain.FallbackTargetKind.Coverage && r.AppliesAt(piediDelPiede));

            foreach (var c in catena.Skip(1))
            {
                if (!bande.TryGetValue(c, out var banda)) { copre = true; break; }   // non lo so: non accuso
                if (banda.Bottom <= piede && piede < banda.Top) { copre = true; break; }
            }

            if (copre || rinvio || catena.Count <= 1) continue;

            var quota = $"FL{piede}";
            yield return new ConsistencyFinding("Ricaduta che non copre la quota", ConsistencySeverity.Error,
                b.Callsign,
                $"Il settore parte da {quota}, ma nessuno della sua catena di ripiego ha qualcosa a quella quota: chiuso lui, il traffico va a chi non ce l'ha.",
                ConsistencyArea.Dati, DoveStruttura,
                CategoryKey: "Diag_Cat_RicadutaScoperta", DetailKey: "Diag_Msg_RicadutaScoperta",
                DetailArgs: new object[] { quota },
                EntityKey: "Diag_Ent_Settore", EntityArgs: new object[] { b.Callsign });
        }
    }


    /// <summary>
    /// Il contesto del rinvio con <b>tutti aperti</b>: è la domanda strutturale «come risalirebbe», non «chi
    /// c'è adesso». ⚠️ Senza volumi, punti o topologia torna <c>null</c> e il rilievo della scala non si fa.
    /// </summary>
    private async Task<Content.CoverageFallbackContext?> ContestoDelRinvioAsync(CancellationToken ct)
    {
        if (_volumi is null || _punti is null || _topologia is null) return null;

        var settori = await _volumi.GetAllAsync(ct);
        if (settori.Count == 0) return null;

        var tutti = new HashSet<string>(settori.Select(s => s.Callsign), StringComparer.OrdinalIgnoreCase);
        return Content.CoverageFallbackContext.Da(
            await _topologia.BuildGlobalAsync(ct), settori, tutti, await _punti.GetAsync(ct));
    }


    /// <summary>L'insieme dei settori senza uno: serve a chiedere «e se questo non ci fosse, chi lo copre?».</summary>
    private static IReadOnlySet<string> SenzaDiLui(IReadOnlySet<string> tutti, string escluso) =>
        new HashSet<string>(tutti.Where(c => !string.Equals(c, escluso, StringComparison.OrdinalIgnoreCase)),
            StringComparer.OrdinalIgnoreCase);

}
