using System.Text.RegularExpressions;
using Vipi.Domain;
using Vipi.Domain.Entities;

namespace Vipi.Application.Content;

/// <summary>
/// Una SID o una STAR scritta fra i punti di una clausola di trasferimento (21 settembre 2026).
///
/// <para>Una procedura non è un punto: il traffico non «passa su BANAV 9A», è <b>autorizzato via</b> BANAV 9A. La
/// frase deve quindi essere quella con il participio (<see cref="CoordinationSentenceTemplate.TemplateCleared"/>),
/// e quella frase la sceglie la faccetta trasferimento: quando la clausola non ne dice una, il luogo di
/// trasferimento diventa <see cref="TransferHandoffKind.AorBoundary"/> — il caso di chi consegna un traffico in
/// procedura.</para>
///
/// <para>⚠️ La regola sta in TRE porte e non in una sola, perché sono tre strade diverse verso la stessa frase:
/// il salvataggio (<see cref="Normalizza"/>, il dato giusto nel database), la frase composta
/// (<c>CoordinationSentences.Compose</c>: anteprima dell'editor, vIPI, vLOA) e la riga espansa
/// (<c>AgreementExpansion</c>: la colonna «trasferimento» della tabella e il ponte). Una clausola salvata prima del
/// giorno in cui la regola esiste, o arrivata da un import, deve dire la stessa cosa.</para>
///
/// <para>Il nome si riconosce dalla FORMA, non da un catalogo: 2–5 lettere, uno spazio facoltativo, una cifra e una
/// lettera — <c>BANAV 9A</c>, <c>BANA9A</c>, <c>ELB 1A</c>. Un fix o una radioassistenza non contiene mai cifre, e
/// la frase non può aspettare una tabella SID per sapere come si dice: la compongono anche la vLOA e il ponte, che
/// le tabelle non le hanno.</para>
/// </summary>
public static partial class ProceduraNeiPunti
{
    // Fino a SETTE lettere come il pezzo di `RiferimentiProcedura` (`SALENTO5A`): un fix ne ha al più cinque e
    // nessuna cifra, quindi allargare non prende nessun punto per una procedura.
    [GeneratedRegex(@"^[A-Z]{2,7} ?[0-9][A-Z]$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Forma();

    /// <summary>Il singolo punto è il nome di una SID o di una STAR.</summary>
    public static bool E(string? punto) => Forma().IsMatch((punto ?? "").Trim());

    /// <summary>Fra i punti della clausola (elenco separato da virgole) ce n'è almeno una procedura.</summary>
    public static bool Contiene(string? punti) => CopList.Parse(punti).Any(E);

    /// <summary>Il luogo di trasferimento che vale davvero: con una procedura e nessuna scelta, il confine dell'AoR.
    /// Una scelta scritta (punto, testo, confine) resta com'è.</summary>
    public static TransferHandoffKind Consegna(TransferHandoffKind scritto, string? punti) =>
        scritto == TransferHandoffKind.Unspecified && Contiene(punti) ? TransferHandoffKind.AorBoundary : scritto;

    /// <summary>La faccetta con il luogo che vale davvero. Stessa istanza se non cambia niente.</summary>
    public static TransferHandoffFacet Faccetta(TransferHandoffFacet f, string? punti)
    {
        var k = Consegna(f.Kind, punti);
        // Il vincolo di livello di una faccetta mai usata è il valore di riposo della colonna, non una scelta: si
        // riparte da «passando», come fa il form quando apre la faccetta.
        return k == f.Kind ? f : f with { Kind = k, LevelConstraint = LevelConstraint.Exact };
    }

    // ---- Il nome di OGGI (21 settembre 2026, chiesto dal committente) ----
    //
    // Una SID o una STAR in un trasferimento segue l'archivio come nelle tabelle degli aeroporti e nelle
    // citazioni: scritta «BANA9A» esce «BANAV 9A»; rinominata la procedura in BANA1A, esce «BANAV 1A» senza
    // toccare la clausola. Il meccanismo è quello delle citazioni (`NomiProcedura`: radice del nome, il nome
    // scritto vince se è ancora vivo) — non una seconda regola.
    // ⚠️ Nel database resta quel che è stato scritto: come il riferimento nel testo, è l'ultimo nome visto, e
    // una procedura che non si trova più esce così com'è.

    /// <summary>In quale verso si cerca una procedura: gli arrivi arrivano per STAR, le partenze partono per SID;
    /// gli altri flussi non lo dicono, e si prova prima la SID poi la STAR.</summary>
    public static IReadOnlyList<ProcedureKind> Versi(TransferFlowKind kind) => kind switch
    {
        TransferFlowKind.Arrival => new[] { ProcedureKind.Star },
        TransferFlowKind.Departure => new[] { ProcedureKind.Sid },
        _ => new[] { ProcedureKind.Sid, ProcedureKind.Star },
    };

    /// <summary>Le tabelle da leggere per gli accordi dati: una per verso e scalo, solo dove una clausola scrive
    /// una procedura. La via breve è la regola: quasi nessun accordo ne ha, e allora non si legge niente.</summary>
    public static IReadOnlySet<(ProcedureKind Kind, string Icao)> TabelleCitate(IEnumerable<AgreementRow> accordi)
    {
        var tabelle = new HashSet<(ProcedureKind, string)>();
        foreach (var s in accordi.SelectMany(a => a.Sections))
        {
            if (s.Airports.Count == 0 || !s.Clauses.Any(c => Contiene(c.Cops))) continue;
            foreach (var apt in s.Airports)
                foreach (var verso in Versi(s.Kind))
                    tabelle.Add((verso, RiferimentiProcedura.Norm(apt.Icao)));
        }
        return tabelle;
    }

    /// <summary>I punti con ogni procedura scritta col suo nome di oggi, cercata negli scali della sezione nel
    /// verso del flusso. Un punto che non è una procedura, o che non si trova, resta com'è.</summary>
    public static string Risolvi(string? punti, IEnumerable<string> scali, TransferFlowKind kind, NomiProcedura nomi)
    {
        if (!Contiene(punti)) return punti ?? "";
        var elenco = scali.ToList();
        return CopList.Format(CopList.Parse(punti).Select(p =>
        {
            if (!E(p)) return p;
            foreach (var verso in Versi(kind))
                foreach (var icao in elenco)
                    if (nomi.NomeDelPunto(verso, icao, p) is { } oggi) return oggi;
            return p;
        }));
    }

    /// <summary>Gli accordi con i nomi di oggi nei punti. Stesse istanze dove non cambia niente.</summary>
    public static IReadOnlyList<AgreementRow> ConNomiDiOggi(IReadOnlyList<AgreementRow> accordi, NomiProcedura nomi) =>
        accordi.Select(a => !a.Sections.Any(s => s.Clauses.Any(c => Contiene(c.Cops))) ? a : a with
        {
            Sections = a.Sections.Select(s => s.Airports.Count == 0 || !s.Clauses.Any(c => Contiene(c.Cops)) ? s : s with
            {
                Clauses = s.Clauses.Select(c => Contiene(c.Cops)
                    ? c with { Cops = Risolvi(c.Cops, s.Airports.OrderBy(x => x.Order).Select(x => x.Icao), s.Kind, nomi) }
                    : c).ToList(),
            }).ToList(),
        }).ToList();

    /// <summary>L'input di salvataggio con il luogo che vale davvero: nel database va il dato già giusto.</summary>
    public static AgreementClauseInput Normalizza(AgreementClauseInput i)
    {
        var k = Consegna(i.HandoffKind, i.Cops);
        return k == i.HandoffKind ? i : i with { HandoffKind = k, HandoffLevelConstraint = LevelConstraint.Exact };
    }
}
