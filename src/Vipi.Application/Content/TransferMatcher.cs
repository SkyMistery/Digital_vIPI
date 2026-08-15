using System.Globalization;
using System.Text.RegularExpressions;
using Vipi.Application.Aor;
using Vipi.AuroraBridge.Contracts;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>Convenzione con cui si compone la stringa scritta nell'etichetta quota di Aurora.
/// Aurora accetta testo libero (piano §11.2), quindi è una scelta di leggibilità del tag, non un vincolo tecnico.</summary>
public enum AuroraLabelConvention
{
    /// <summary>Solo il numero: FL250 → «250», 5000 ft → «5000». Convenzione di default.</summary>
    Number,
    /// <summary>Prefisso esplicito: FL250 → «FL250»; i piedi restano numerici.</summary>
    FlPrefixed,
}

/// <summary>Parametri di risoluzione: convenzione dell'etichetta e quanti candidati restituire.</summary>
public sealed record TransferMatchOptions(
    AuroraLabelConvention Convention = AuroraLabelConvention.Number,
    int MaxCandidates = 8);

/// <summary>
/// Cuore deterministico del bridge Aurora: dato il contesto di un volo e i flussi di trasferimento della ACC,
/// produce i punti candidati ordinati, col livello pronto da scrivere e le ragioni della graduatoria.
/// Puro e senza IO: tutto ciò che serve arriva dai parametri (l'orchestrazione sta in TransferMatchService).
///
/// Non scarta mai un candidato in silenzio: ciò che non torna abbassa il punteggio e produce una ragione o
/// un avviso, perché il controllore deve poter capire — e smentire — la proposta.
/// </summary>
public static class TransferMatcher
{
    // Punteggi base per accoppiamento flusso↔volo.
    private const double BaseAirportKind = 1.00;   // il flusso è dell'aeroporto giusto e del tipo giusto
    private const double BaseOverflight = 0.60;    // flusso senza aeroporto (sorvolo)
    private const double BaseOtherKind = 0.50;     // Vfr/Other con aeroporto coerente

    // Contributi dei filtri.
    private const double CopInFixes = 0.30;
    private const double CopInRoute = 0.25;
    private const double CopAirwayRange = 0.20;
    private const double CopWildcard = 0.10;
    private const double CopMissing = -0.30;
    private const double ParityOk = 0.15;
    private const double ParityKo = -0.50;
    private const double RunwayOk = 0.20;
    private const double RunwayKo = -0.40;
    private const double ConditionUnknown = -0.05;
    private const double NextStationAgrees = 0.15;

    /// <summary>Somma dei contributi positivi massimi: serve a riportare il punteggio grezzo in 0..1 SENZA
    /// troncare. Troncare a 1 appiattirebbe i candidati forti fra loro, perdendo proprio la discriminazione
    /// che serve a metterli in fila (es. due punti sullo stesso CoP distinti solo dal next ATC impostato).</summary>
    private const double ScoreScale = BaseAirportKind + CopInFixes + ParityOk + RunwayOk + NextStationAgrees;

    private static readonly Regex AirwayRange = new(@"^([A-Z]{1,2})(\d{1,3})\s*-\s*([A-Z]{1,2})?(\d{1,3})$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex RunwayToken = new(@"\b(\d{2})([LCR])?\b", RegexOptions.Compiled);
    private static readonly Regex RouteToken = new(@"[A-Z0-9]{2,7}", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static TransferResolveResponse Match(
        TransferResolveRequest request,
        IReadOnlyList<TransferFlowRow> flows,
        Topology topology,
        IReadOnlySet<string> online,
        string? accCode,
        DateTimeOffset onlineAsOf,
        DateTimeOffset now,
        TransferMatchOptions? options = null)
    {
        var opt = options ?? new TransferMatchOptions();
        var response = new TransferResolveResponse
        {
            AsOf = now,
            OnlineAsOf = onlineAsOf,
            AccCode = accCode,
        };

        var owner = ResolveOwner(topology, request.OwnerCallsign);
        response.ResolvedOwner = owner;

        if (owner is null)
        {
            response.Warnings.Add($"Callsign «{request.OwnerCallsign}» non riconosciuto fra i settori noti: nessuna proposta.");
            return response;
        }

        // Chi copre cosa ORA. La postazione che chiede è online per definizione (sta controllando), anche se la
        // cache ATC è indietro di un poll: la aggiungo esplicitamente, altrimenti i suoi stessi flussi le sfuggono.
        var effectiveOnline = new HashSet<string>(online, StringComparer.OrdinalIgnoreCase) { owner };

        var mine = flows.Where(f => IsCoveredBy(f.OwningSectorCallsign, owner, topology, effectiveOnline)).ToList();
        if (mine.Count == 0)
        {
            response.Warnings.Add($"Nessun flusso di trasferimento per {owner}" +
                (accCode is null ? "." : $" nella ACC {accCode}."));
            return response;
        }

        var fixes = request.RouteFixes ?? new List<RouteFix>();
        var fixEto = fixes
            .Where(f => !string.IsNullOrWhiteSpace(f.Fix))
            .GroupBy(f => f.Fix.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Eto, StringComparer.OrdinalIgnoreCase);
        var routeTokens = Tokenize(request.Route);

        var candidates = new List<TransferCandidate>();
        foreach (var flow in mine)
        {
            var (baseScore, kindReason) = ScoreFlow(flow, request);
            if (baseScore <= 0) continue;

            foreach (var point in flow.Points)
            {
                var c = ScorePoint(flow, point, request, topology, online, fixEto, routeTokens, baseScore, kindReason, opt);
                candidates.Add(c);
            }
        }

        if (candidates.Count == 0)
        {
            response.Warnings.Add("Flussi presenti, ma nessun punto di trasferimento definito.");
            return response;
        }

        foreach (var w in CollectWarnings(candidates)) response.Warnings.Add(w);

        response.Candidates = candidates
            .OrderByDescending(c => c.Score)
            .ThenBy(c => c.CopEto ?? "9999", StringComparer.Ordinal)
            .ThenBy(c => c.FlowId).ThenBy(c => c.PointId)
            .Take(opt.MaxCandidates)
            .ToList();

        return response;
    }

    /// <summary>Riconosce il settore che sta chiedendo: match esatto, poi per segmento del callsign
    /// (stessa euristica di <see cref="TransferOnlineResolver"/>, che qui però va nella direzione opposta).</summary>
    private static string? ResolveOwner(Topology topology, string? callsign)
    {
        if (string.IsNullOrWhiteSpace(callsign)) return null;
        var cs = callsign.Trim();

        var exact = topology.Sectors.FirstOrDefault(s => s.Equals(cs, StringComparison.OrdinalIgnoreCase));
        if (exact is not null) return exact;

        // «LIRR_NE» ↔ «LIRR_NE_CTR»: confronto sui segmenti non vuoti, il più specifico vince.
        var segments = cs.Split('_', StringSplitOptions.RemoveEmptyEntries);
        return topology.Sectors
            .Where(s => segments.Length > 1 && s.StartsWith(string.Join('_', segments.Take(2)), StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(s => s.Length)
            .FirstOrDefault();
    }

    /// <summary>Vero se il flusso ricade su di me ORA: o è mio, o il suo proprietario è chiuso e sono il primo
    /// antenato online (top-down). Se un sotto-settore online lo assorbe, il flusso non è mio.</summary>
    private static bool IsCoveredBy(string flowOwner, string me, Topology topology, IReadOnlySet<string> online)
    {
        var chain = new List<string> { flowOwner };
        chain.AddRange(topology.Ancestors(flowOwner));
        var first = TransferOnlineResolver.FirstOnline(chain, online);
        return first is not null && first.Equals(me, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Accoppia il flusso al volo: aeroporto di partenza/arrivo, oppure sorvolo. 0 = non pertinente.</summary>
    private static (double Score, string Reason) ScoreFlow(TransferFlowRow flow, TransferResolveRequest req)
    {
        var icao = flow.AirportIcao;
        var isDep = Same(icao, req.Departure);
        var isArr = Same(icao, req.Arrival);

        return flow.Kind switch
        {
            TransferFlowKind.Departure when isDep => (BaseAirportKind, $"partenza da {icao}"),
            TransferFlowKind.Arrival when isArr => (BaseAirportKind, $"arrivo a {icao}"),
            TransferFlowKind.Overflight when string.IsNullOrWhiteSpace(icao) => (BaseOverflight, "sorvolo"),
            TransferFlowKind.Overflight when isDep || isArr => (BaseOverflight, $"sorvolo ({icao})"),
            TransferFlowKind.Vfr or TransferFlowKind.Other when isDep || isArr => (BaseOtherKind, $"flusso {flow.Kind} su {icao}"),
            _ => (0, ""),
        };
    }

    private static TransferCandidate ScorePoint(
        TransferFlowRow flow, TransferPointRow p, TransferResolveRequest req, Topology topology,
        IReadOnlySet<string> online, IReadOnlyDictionary<string, string?> fixEto, IReadOnlySet<string> routeTokens,
        double baseScore, string kindReason, TransferMatchOptions opt)
    {
        var reasons = new List<string> { kindReason };
        var score = baseScore;

        // --- CoP ---
        var (copScore, copReason, eto) = ScoreCop(p.Cop, fixEto, routeTokens);
        score += copScore;
        if (copReason is not null) reasons.Add(copReason);

        // --- parità (regola semicircolare, riferita al livello di crociera del volo) ---
        if (p.Parity != LevelParity.Any && req.CruiseLevel is int cruise)
        {
            var cruiseParity = (cruise / 10) % 2 == 0 ? LevelParity.Even : LevelParity.Odd;
            if (cruiseParity == p.Parity)
            {
                score += ParityOk;
                reasons.Add($"livello di crociera {LevelFormatting.ParityLabel(p.Parity)}");
            }
            else
            {
                score += ParityKo;
                reasons.Add($"riga per livelli {LevelFormatting.ParityLabel(p.Parity)}, il volo è a FL{cruise}");
            }
        }

        // --- condizioni operative ---
        var condition = EvaluateCondition(flow, p, req);
        score += condition.Match switch
        {
            "matched" => RunwayOk,
            "unmatched" => RunwayKo,
            "unknown" => ConditionUnknown,
            _ => 0,
        };
        if (condition.Match == "matched") reasons.Add($"condizione «{condition.Display}» soddisfatta");
        else if (condition.Match == "unmatched") reasons.Add($"condizione «{condition.Display}» NON soddisfatta");

        // --- ente successivo già impostato dal controllore in Aurora ---
        var (handler, handlerOnline) = ResolveHandler(p.NextSectorCallsign, topology, online);
        if (!string.IsNullOrWhiteSpace(req.NextStation) && !string.IsNullOrWhiteSpace(p.NextSectorCallsign) &&
            Same(req.NextStation, p.NextSectorCallsign))
        {
            score += NextStationAgrees;
            reasons.Add($"coincide col next ATC impostato ({req.NextStation})");
        }

        var (auroraValue, writable) = ComposeLabel(p, opt.Convention);

        return new TransferCandidate
        {
            FlowId = flow.Id,
            PointId = p.Id,
            FlowKind = flow.Kind.ToString(),
            AirportIcao = flow.AirportIcao,
            Cop = p.Cop,
            CopEto = eto,
            Level = new CandidateLevel
            {
                Value = p.LevelValue,
                Unit = p.LevelUnit.ToString(),
                Constraint = p.LevelConstraint.ToString(),
                Special = p.LevelSpecial,
                Parity = p.Parity.ToString(),
                VerticalState = p.VerticalState.ToString(),
                Text = p.LevelText,
                HandoffKind = p.HandoffKind.ToString(),
                HandoffLabel = p.HandoffLabel,
                TransferValue = p.HandoffLevelValue,
                TransferUnit = p.HandoffLevelValue is null ? null : p.HandoffLevelUnit.ToString(),
                TransferConstraint = p.HandoffLevelValue is null ? null : p.HandoffLevelConstraint.ToString(),
                TransferText = p.HandoffLevelText,
                Speed = p.SpeedText,
            },
            NextSectorCallsign = p.NextSectorCallsign,
            ResolvedHandler = handler,
            HandlerOnline = handlerOnline,
            Condition = condition,
            AuroraValue = auroraValue,
            Writable = writable,
            Score = Math.Round(Math.Clamp(score / ScoreScale, 0, 1), 3),
            Reasons = reasons.Where(r => !string.IsNullOrWhiteSpace(r)).ToList(),
        };
    }

    /// <summary>Confronta il CoP della vIPI con la rotta. Riconosce i fix, i jolly («ALL», «ALL to GR») e i
    /// range di aerovie («Y01-Y12»), che nei dati reali convivono nello stesso campo.</summary>
    private static (double Score, string? Reason, string? Eto) ScoreCop(
        string? cop, IReadOnlyDictionary<string, string?> fixEto, IReadOnlySet<string> routeTokens)
    {
        if (string.IsNullOrWhiteSpace(cop)) return (0, null, null);
        var value = cop.Trim();

        if (value.StartsWith("ALL", StringComparison.OrdinalIgnoreCase))
        {
            var qualifier = value.Length > 3 ? value[3..].Trim() : "";
            return qualifier.Length == 0
                ? (CopWildcard, "vale per tutti i punti", null)
                : (CopWildcard, $"jolly «{value}»: la destinazione va verificata a mano", null);
        }

        var range = AirwayRange.Match(value);
        if (range.Success)
        {
            var prefix = range.Groups[1].Value;
            var from = int.Parse(range.Groups[2].Value, CultureInfo.InvariantCulture);
            var to = int.Parse(range.Groups[4].Value, CultureInfo.InvariantCulture);
            var hit = routeTokens.FirstOrDefault(t => InAirwayRange(t, prefix, Math.Min(from, to), Math.Max(from, to)));
            return hit is not null
                ? (CopAirwayRange, $"aerovia {hit} nel range {value}", null)
                : (CopMissing, $"nessuna aerovia del range {value} in rotta", null);
        }

        if (fixEto.TryGetValue(value, out var eto))
            return (CopInFixes, eto is null or "-" ? $"CoP {value} in rotta" : $"CoP {value} in rotta (ETO {eto})", eto);

        if (routeTokens.Contains(value))
            return (CopInRoute, $"CoP {value} nella rotta del piano di volo", null);

        return (CopMissing, $"CoP {value} non trovato in rotta", null);
    }

    private static bool InAirwayRange(string token, string prefix, int from, int to)
    {
        if (!token.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
        var digits = token[prefix.Length..];
        return digits.Length > 0 && int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var n)
            && n >= from && n <= to;
    }

    /// <summary>Verifica le condizioni del punto. La pista si può controllare (Aurora dà <c>#CTRLRWY</c>);
    /// area e condizione personalizzata no, e restano dichiaratamente «unknown».</summary>
    private static CandidateCondition EvaluateCondition(TransferFlowRow flow, TransferPointRow p, TransferResolveRequest req)
    {
        var display = p.ConditionDisplay;
        if (string.IsNullOrWhiteSpace(display))
            return new CandidateCondition { Display = null, Match = "none" };

        var hasUnverifiable = !string.IsNullOrWhiteSpace(p.ConditionAreaLabel) || !string.IsNullOrWhiteSpace(p.ConditionCustomLabel);
        var wanted = string.IsNullOrWhiteSpace(p.ConditionLabel)
            ? new List<string>()
            : RunwayToken.Matches(p.ConditionLabel!).Select(m => m.Value.ToUpperInvariant()).Distinct().ToList();

        if (wanted.Count == 0 || string.IsNullOrWhiteSpace(flow.AirportIcao))
            return new CandidateCondition { Display = display, Match = "unknown" };

        if (req.RunwaysInUse is null || !TryGetRunways(req.RunwaysInUse, flow.AirportIcao!, out var cfg))
            return new CandidateCondition { Display = display, Match = "unknown" };

        var inUse = flow.Kind switch
        {
            TransferFlowKind.Arrival => cfg.Arrival,
            TransferFlowKind.Departure => cfg.Departure,
            _ => cfg.Arrival.Concat(cfg.Departure).ToList(),
        };
        var normalized = inUse.Where(r => !string.IsNullOrWhiteSpace(r)).Select(r => r.Trim().ToUpperInvariant()).ToList();
        if (normalized.Count == 0)
            return new CandidateCondition { Display = display, Match = "unknown" };

        var matched = wanted.Any(w => normalized.Contains(w));
        // Pista coerente ma con anche un'area/condizione libera: resta «unknown», non posso dichiararla soddisfatta.
        return new CandidateCondition
        {
            Display = display,
            Match = matched ? (hasUnverifiable ? "unknown" : "matched") : "unmatched",
        };
    }

    private static bool TryGetRunways(IDictionary<string, RunwayConfig> map, string icao, out RunwayConfig cfg)
    {
        foreach (var kv in map)
        {
            if (kv.Key.Equals(icao, StringComparison.OrdinalIgnoreCase) && kv.Value is not null)
            {
                cfg = kv.Value;
                return true;
            }
        }
        cfg = new RunwayConfig();
        return false;
    }

    private static (string Handler, bool Online) ResolveHandler(string? next, Topology topology, IReadOnlySet<string> online)
    {
        if (string.IsNullOrWhiteSpace(next)) return (TransferOnlineResolver.Unicom, false);
        var chain = new List<string> { next! };
        chain.AddRange(topology.Ancestors(next!));
        return TransferOnlineResolver.Resolve(chain, online);
    }

    /// <summary>
    /// Compone la stringa per <c>#LBALT</c>. I livelli «Special» sono scrivibili (Aurora accetta testo),
    /// ma vanno ripuliti: il «;» è il separatore del protocollo e non può entrare in un argomento.
    ///
    /// <para>Quando la riga distingue i due eventi (accordi ACC→APP), l'etichetta porta il livello <b>al
    /// trasferimento</b> e non quello autorizzato: nel tag il controllore scrive la quota che il traffico ha
    /// quando passa di mano — su una riga «autorizzato FL160, trasferito passando FL110» scrivere 160
    /// direbbe una cosa che non succede. Senza faccetta i due coincidono e non cambia niente.</para>
    /// </summary>
    private static (string? Value, bool Writable) ComposeLabel(TransferPointRow p, AuroraLabelConvention convention)
    {
        if (p.HandoffLevelValue is int h)
            return (Format(h, p.HandoffLevelUnit, convention), true);

        if (p.LevelConstraint == LevelConstraint.Special)
        {
            var special = Sanitize(p.LevelSpecial);
            return special is null ? (null, false) : (special, true);
        }

        if (p.LevelValue is not int v) return (null, false);

        return (Format(v, p.LevelUnit, convention), true);
    }

    private static string Format(int value, LevelUnit unit, AuroraLabelConvention convention) =>
        unit == LevelUnit.Fl && convention == AuroraLabelConvention.FlPrefixed
            ? $"FL{value}"
            : value.ToString(CultureInfo.InvariantCulture);

    private static string? Sanitize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var cleaned = text.Replace(';', ' ').Replace('\r', ' ').Replace('\n', ' ').Trim();
        if (cleaned.Length == 0) return null;
        return cleaned.Length <= 24 ? cleaned : cleaned[..24].Trim();
    }

    private static IEnumerable<string> CollectWarnings(IReadOnlyList<TransferCandidate> candidates)
    {
        if (candidates.Any(c => c.Condition.Match == "unknown"))
            yield return "Alcune condizioni (area attiva, condizioni personalizzate, pista non nota ad Aurora) non sono verificabili in automatico: controllale a vista.";
        if (candidates.All(c => !c.Writable))
            yield return "Nessun candidato ha un livello scrivibile: sono tutti testuali o senza valore.";
    }

    private static IReadOnlySet<string> Tokenize(string? route)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(route)) return set;
        foreach (Match m in RouteToken.Matches(route)) set.Add(m.Value.ToUpperInvariant());
        return set;
    }

    private static bool Same(string? a, string? b) =>
        !string.IsNullOrWhiteSpace(a) && !string.IsNullOrWhiteSpace(b) &&
        a!.Trim().Equals(b!.Trim(), StringComparison.OrdinalIgnoreCase);
}
