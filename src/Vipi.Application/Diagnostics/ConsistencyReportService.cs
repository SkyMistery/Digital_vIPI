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

    public async Task<IReadOnlyList<ConsistencyFinding>> RunAsync(CancellationToken ct = default)
    {
        var findings = Analyze(await _repo.LoadAsync(ct)).ToList();

        if (_schema is not null) findings.AddRange(await _schema.RunAsync(ct));
        if (_admin is not null) findings.AddRange(await _admin.RunAsync(ct));
        if (_server is not null) findings.AddRange(await _server.RunAsync(ct));
        // Non è una sonda: è già successo, all'avvio. Qui si legge soltanto.
        if (_startup is not null) findings.AddRange(_startup.Findings);

        return findings;
    }

    // Logica pura (nessuna dipendenza da EF): il dataset è già in memoria ⇒ testabile con fixture.
    public static IReadOnlyList<ConsistencyFinding> Analyze(ConsistencyDataset d)
    {
        var findings = new List<ConsistencyFinding>();

        foreach (var t in d.TransferConditions)
        {
            var who = $"TransferPoint #{t.PointId} ({t.AccCode}, CoP {t.Cop})";

            // 1) Pista orfana: soft-ref valorizzato ma la pista non esiste più.
            if (t.ConditionRefId is int refId && !d.RunwayIdents.ContainsKey(refId))
            {
                findings.Add(new ConsistencyFinding("Pista orfana", ConsistencySeverity.Error, who,
                    $"ConditionRefId={refId} non corrisponde a nessuna pista: rimossa o re-importata con altro Id."));
            }
            // 2) Label divergente: la pista esiste ma il suo ident non compare più nell'etichetta denormalizzata.
            else if (t.ConditionRefId is int okId
                     && d.RunwayIdents.TryGetValue(okId, out var ident)
                     && !string.IsNullOrWhiteSpace(t.ConditionLabel)
                     && !t.ConditionLabel!.Contains(ident, StringComparison.OrdinalIgnoreCase))
            {
                findings.Add(new ConsistencyFinding("Label pista divergente", ConsistencySeverity.Warning, who,
                    $"La pista referenziata è ora «{ident}» ma l'etichetta salvata è «{t.ConditionLabel}»: rinominata dopo il salvataggio."));
            }

            // 3) Area fantasma: l'area denormalizzata non corrisponde ad alcuna area speciale esistente.
            if (!string.IsNullOrWhiteSpace(t.ConditionAreaLabel) && !d.AreaNames.Contains(t.ConditionAreaLabel!.Trim()))
            {
                findings.Add(new ConsistencyFinding("Area fantasma", ConsistencySeverity.Warning, who,
                    $"Area «{t.ConditionAreaLabel}» non presente tra le aree speciali: rinominata o rimossa."));
            }
        }

        // 4) Gerarchia dangling: un padre di copertura per callsign che non risolve ad alcun nodo dei cataloghi.
        foreach (var p in d.ParentRefs)
        {
            if (!d.ValidCallsigns.Contains(p.ParentCallsign))
            {
                findings.Add(new ConsistencyFinding("Gerarchia dangling", ConsistencySeverity.Error,
                    $"{p.Kind} {p.Reference}",
                    $"ParentCallsign «{p.ParentCallsign}» non esiste nei cataloghi: catena di copertura interrotta."));
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
                "dall'import; nel documento restano citate ma non vengono mostrate."));
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
                    "esplicita callsign↔postazione.");
            }
        }
    }
}
