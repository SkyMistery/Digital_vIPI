using Vipi.Application.Abstractions;

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

    /// <param name="schema">
    /// Opzionale: se c'è, al report si aggiunge il drift fra modello EF e schema fisico. Sta qui e non in
    /// <see cref="Analyze"/> perché non è un'incongruenza di <i>dati</i> ma di <i>schema</i>, e perché Analyze deve
    /// restare una funzione pura sul dataset di dominio. Agganciandolo in questo punto — l'unico consumato sia da
    /// <c>/vsop/admin/diagnostica</c> sia dall'health check — entrambi lo mostrano senza modifiche a valle.
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
    public ConsistencyReportService(IConsistencyReportRepository repo, ISchemaDriftProbe? schema = null,
        Auth.IAdminCoverageService? admin = null, IServerSettingsProbe? server = null,
        IStartupMaintenanceReport? startup = null)
    {
        _repo = repo;
        _schema = schema;
        _admin = admin;
        _server = server;
        _startup = startup;
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
    private const string DoveAccordi = "/vsop/admin/trasferimenti";
    private const string DoveStruttura = "/vsop/admin/sectorstructure";

    /// <summary>
    /// L'elenco dei documenti, non l'editor del singolo. ⚠️ Scelta dichiarata: la riga porta il <i>titolo</i>
    /// del documento, non il suo Id, e la rotta dell'editor dipende dal tipo e dall'ACC — costruirla di qui
    /// vorrebbe dire portarsi dietro il registro delle rotte per documento (<c>IDocKindRoutes</c>) dentro
    /// l'analisi pura. Meglio un link vero a un passo di distanza che uno preciso e sbagliato.
    /// </summary>
    private const string DoveDocumenti = "/vsop/versioni";

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
        await Raccogli(findings, "incongruenze dei dati", ConsistencyArea.Dati,
            async () => Analyze(await _repo.LoadAsync(ct)), ct);
        if (_schema is not null)
            await Raccogli(findings, "drift di schema", ConsistencyArea.Schema, () => _schema.RunAsync(ct), ct);
        if (_admin is not null)
            await Raccogli(findings, "copertura admin", ConsistencyArea.Configurazione, () => _admin.RunAsync(ct), ct);
        if (_server is not null)
            await Raccogli(findings, "impostazioni del server", ConsistencyArea.Server, () => _server.RunAsync(ct), ct);
        // Non è una sonda: è già successo, all'avvio. Qui si legge soltanto — e può solo fallire se qualcuno
        // ci mettesse dentro dell'I/O, quindi passa dallo stesso cancello per non doverlo ricordare.
        if (_startup is not null)
            await Raccogli(findings, "manutenzioni d'avvio", ConsistencyArea.Avvio,
                () => Task.FromResult(_startup.Findings), ct);

        return findings;
    }

    /// <summary>
    /// Esegue un pezzo del report e ne accoda i rilievi; se lancia, accoda <b>il guasto</b> e prosegue.
    /// </summary>
    private static async Task Raccogli(List<ConsistencyFinding> findings, string pezzo, ConsistencyArea area,
        Func<Task<IReadOnlyList<ConsistencyFinding>>> esegui, CancellationToken ct)
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
                DetailArgs: new object[] { pezzo, ex.GetType().Name, ex.Message }));
        }
    }

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
                    DetailArgs: new object[] { refId }));
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
                    DetailArgs: new object[] { ident, t.ConditionLabel! }));
            }

            // 3) Area fantasma: l'area denormalizzata non corrisponde ad alcuna area speciale esistente.
            if (!string.IsNullOrWhiteSpace(t.ConditionAreaLabel) && !d.AreaNames.Contains(t.ConditionAreaLabel!.Trim()))
            {
                findings.Add(new ConsistencyFinding("Area fantasma", ConsistencySeverity.Warning, who,
                    $"Area «{t.ConditionAreaLabel}» non presente tra le aree speciali: rinominata o rimossa.",
                    ConsistencyArea.Dati, DoveAccordi,
                    CategoryKey: "Diag_Cat_AreaFantasma", DetailKey: "Diag_Msg_AreaFantasma",
                    DetailArgs: new object[] { t.ConditionAreaLabel! }));
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
                    DetailArgs: new object[] { p.ParentCallsign }));
            }
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
        return findings;
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
