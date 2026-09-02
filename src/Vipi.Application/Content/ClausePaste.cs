using System;
using System.Collections.Generic;
using System.Linq;
using Vipi.Application.Import;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <summary>Una riga incollata, letta: la clausola che ne esce, oppure il perché non ne esce niente.</summary>
/// <param name="Line">Il numero di riga nel testo incollato (1-based): serve a dire DOVE, non solo COSA.</param>
/// <param name="Raw">La riga com'era, per mostrarla accanto all'esito.</param>
/// <param name="Clause">La clausola letta, o <c>null</c> se la riga non si legge.</param>
/// <param name="Receiver">Il ricevente scritto sulla riga, se c'era: <b>non</b> entra nella clausola — è del
/// lato B dell'accordo — ma serve a dire che righe con riceventi diversi sono accordi diversi.</param>
/// <param name="Error">Perché la riga non si legge; <c>null</c> se si legge.</param>
public sealed record PastedClause(int Line, string Raw, AgreementClauseInput? Clause, string? Receiver, string? Error)
{
    public bool Ok => Clause is not null;
}

/// <summary>
/// Legge una tabella **incollata** da una LoA o da un IPI e la trasforma in clausole.
///
/// <para><b>Perché esiste.</b> Riempire l'Italia a mano significa ridigitare tabelle che esistono già, scritte
/// da qualcun altro, in un PDF. Il costo per riga è tre campi e due clic; moltiplicato per le righe di un ACC
/// è il motivo per cui gli accordi non vengono scritti.</para>
///
/// <para><b>Non salva niente.</b> Restituisce l'esito riga per riga — quella letta, quella no e perché — e chi
/// chiama lo mostra prima di scrivere. Un incolla che salvasse direttamente metterebbe in archivio la propria
/// interpretazione di un testo che nessuno ha riletto: e l'interpretazione di una tabella copiata da un PDF
/// sbaglia, non «potrebbe sbagliare».</para>
///
/// <para><b>Il livello lo rilegge <see cref="LevelFormatting.Parse"/></b>, che è l'inverso esatto di
/// <c>Format</c> — la stessa sintassi che si scrive nella cella e si legge in tabella. Una seconda grammatica
/// per il testo incollato sarebbe una seconda cosa da tenere d'accordo con la prima.</para>
/// </summary>
public static class ClausePaste
{
    /// <summary>
    /// La specifica di questa tabella dentro l'elenco delle tabelle importabili. I titoli sono neutri perché
    /// qui non c'è una lingua: a riconoscere l'intestazione ci pensano i <b>sinonimi</b>, che coprono anche
    /// l'inglese delle LoA.
    /// </summary>
    public static SpecImport Spec { get; } =
        SpecTabelle.ClausoleAccordo("Punti", "Livello", "Ricevente", "Condizione");

    /// <summary>
    /// Legge il testo incollato. Ogni riga è «punti · livello [· ricevente] [· condizione]»; le righe vuote si
    /// saltano, quelle che non si leggono restano nell'esito con il loro errore.
    ///
    /// <para>
    /// ⚠️ <b>Lo spezzamento non è più di questa classe.</b> Lo fa <see cref="Griglia"/>, che è il primo
    /// stadio di ogni altra tabella importabile: così questo incolla capisce anche il Markdown, la tabella
    /// HTML che Excel mette davvero in clipboard e il CSV con le virgolette, senza che qui viva una seconda
    /// grammatica da tenere d'accordo con la prima. Qui resta ciò che è <b>di dominio</b>: che cos'è una
    /// clausola, e che il ricevente non ci va dentro.
    /// </para>
    /// <para>
    /// ⚠️ <b>La virgola resta esclusa</b>, e ora lo dice chi legge (<c>virgola: false</c>): separa già i punti
    /// dentro una cella («EKMUR, PISIP»), e usarla anche fra le colonne renderebbe le due cose
    /// indistinguibili.
    /// </para>
    /// <para>
    /// ⚠️ E un'<b>intestazione</b> incollata insieme alle righe adesso si riconosce e si salta, invece di
    /// diventare una clausola con i punti «POINTS».
    /// </para>
    /// </summary>
    public static IReadOnlyList<PastedClause> Parse(string? text)
    {
        var result = new List<PastedClause>();
        if (string.IsNullOrWhiteSpace(text)) return result;

        var griglia = Griglia.Leggi(text, virgola: false);
        if (!griglia.Piena) return result;

        var mappatura = MappaturaColonne.Proponi(Spec, griglia);
        var salta = mappatura.Intestazione ? 1 : 0;

        // I numeri di riga sono quelli del TESTO INCOLLATO, righe vuote comprese: dicono DOVE, e chi rilegge
        // conta le righe sullo schermo da cui ha copiato. Valgono finché la lettura non ha unito righe (una
        // cella fra virgolette può contenere un a-capo): in quel caso si numera in ordine, che è il meglio
        // che si possa promettere.
        var numeri = NumeriDiRiga(text!);
        var allineati = numeri.Count == griglia.Righe.Count;

        for (var i = salta; i < griglia.Righe.Count; i++)
        {
            var celle = griglia.Righe[i];
            var numero = allineati ? numeri[i] : i + 1;
            result.Add(ParseLine(numero, string.Join(" · ", celle), Colonne(celle, mappatura)));
        }
        return result;
    }

    /// <summary>Le posizioni (da 1) delle righe non vuote nel testo incollato.</summary>
    private static IReadOnlyList<int> NumeriDiRiga(string testo)
    {
        var numeri = new List<int>();
        var righe = testo.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (var i = 0; i < righe.Length; i++)
            if (!string.IsNullOrWhiteSpace(righe[i]))
                numeri.Add(i + 1);
        return numeri;
    }

    /// <summary>Le quattro colonne della clausola, prese dove la mappatura dice che stanno.</summary>
    private static string[] Colonne(IReadOnlyList<string> celle, MappaturaColonne mappatura)
    {
        var fuori = new string[Spec.Colonne.Count];
        for (var c = 0; c < fuori.Length; c++)
        {
            var g = c < mappatura.Colonne.Count ? mappatura.Colonne[c] : -1;
            fuori[c] = g >= 0 && g < celle.Count ? (celle[g] ?? "").Trim() : "";
        }
        return fuori;
    }

    private static PastedClause ParseLine(int line, string raw, string[] cols)
    {
        var points = cols.Length > 0 ? cols[0] : "";
        var levelText = cols.Length > 1 ? cols[1] : "";
        var receiver = cols.Length > 2 ? NullIfBlank(cols[2]) : null;
        var condition = cols.Length > 3 ? NullIfBlank(cols[3]) : null;

        if (CopList.Format(CopList.Parse(points)).Length == 0 && levelText.Length == 0)
            return new PastedClause(line, raw, null, null, "Riga senza punti né livello.");

        // ⚠️ Parse non fallisce mai: ciò che non è un livello diventa il livello «speciale» («per aerovia»), che
        // è un valore legittimo. Quindi non c'è un errore da riportare qui — c'è da NON stupirsi se una colonna
        // sbagliata finisce come testo libero, ed è per questo che l'anteprima mostra il livello RESO e non
        // quello scritto: chi rilegge vede cosa il sistema ha capito.
        var level = LevelFormatting.Parse(levelText);

        return new PastedClause(line, raw, new AgreementClauseInput
        {
            Cops = points,
            LevelValue = level.Constraint == LevelConstraint.Special ? null : level.Value,
            LevelUnit = level.Unit,
            LevelConstraint = level.Constraint,
            LevelSpecial = level.Constraint == LevelConstraint.Special ? NullIfBlank(level.Special) : null,
            Parity = level.Parity,
            VerticalState = level.VerticalState,
            // La condizione incollata è testo libero: non si prova a indovinare se sia una pista o un'area.
            // Indovinare vorrebbe dire scrivere «pista 16R» dove il documento diceva «con 16R in uso e R403B
            // attiva», cioè metà del significato.
            ConditionCustomLabel = condition,
        }, receiver, null);
    }

    /// <summary>
    /// I riceventi distinti citati dalle righe lette. Righe con riceventi diversi sono <b>accordi diversi</b> —
    /// il modello lo dice, e l'incolla deve dirlo prima di scrivere invece di mettere tutto sotto lo stesso.
    /// </summary>
    public static IReadOnlyList<string> DistinctReceivers(IEnumerable<PastedClause> parsed) =>
        parsed.Where(p => p.Ok && !string.IsNullOrWhiteSpace(p.Receiver))
            .Select(p => p.Receiver!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static string? NullIfBlank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
