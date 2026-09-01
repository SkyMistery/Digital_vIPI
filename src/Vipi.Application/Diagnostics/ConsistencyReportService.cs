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
        ISectorfileComparisonReport? sectorfile = null)
    {
        _repo = repo;
        _schema = schema;
        _admin = admin;
        _server = server;
        _startup = startup;
        _policy = policy;
        _sectorfile = sectorfile;
    }

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
            async () => Analyze(await _repo.LoadAsync(ct)), ct);
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
    public static IReadOnlyList<ConsistencyFinding> Analyze(ConsistencyDataset d)
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
}
