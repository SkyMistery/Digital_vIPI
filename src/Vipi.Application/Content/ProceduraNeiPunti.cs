using System.Text.RegularExpressions;
using Vipi.Domain;

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
    [GeneratedRegex(@"^[A-Z]{2,5} ?[0-9][A-Z]$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
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

    /// <summary>L'input di salvataggio con il luogo che vale davvero: nel database va il dato già giusto.</summary>
    public static AgreementClauseInput Normalizza(AgreementClauseInput i)
    {
        var k = Consegna(i.HandoffKind, i.Cops);
        return k == i.HandoffKind ? i : i with { HandoffKind = k, HandoffLevelConstraint = LevelConstraint.Exact };
    }
}
