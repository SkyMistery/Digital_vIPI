namespace Vipi.Application.Content;

/// <summary>
/// Il riassunto dei due confronti di un documento, come lo conserva <c>DocumentComparison</c> (S133, carta
/// 2026-10-28-stato-dei-documenti §2.3): quante sezioni cambiano rispetto alla release in vigore e alla prossima
/// programmata. La pagina Documenti lo legge per tutto l'elenco in una query.
/// </summary>
public sealed record ConfrontoSalvato(int DocumentId, DateTime CalcolatoUtc, string? CicloInVigore, int VsPubblico,
    string? CicloProgrammata, int VsProgrammata, bool DerivateProgrammata)
{
    /// <summary>Il pubblico legge qualcosa di diverso dalla bozza.</summary>
    public bool DaPubblicare => CicloInVigore is not null && VsPubblico > 0;

    /// <summary>La prossima programmata non porta la bozza di oggi.</summary>
    public bool ProgrammataIndietro => CicloProgrammata is not null && (VsProgrammata > 0 || DerivateProgrammata);

    /// <summary>La prossima programmata porta già la bozza di oggi: quel che il pubblico non vede ancora arriva con lei.</summary>
    public bool ProgrammataAllineata => CicloProgrammata is not null && !ProgrammataIndietro;

    /// <summary>Dalla scheda calcolata dal vivo.</summary>
    public static ConfrontoSalvato Da(SchedaDelDocumento s) => new(s.DocumentId, s.CalcolataUtc, s.CicloInVigore,
        s.RispettoAlPubblico.Count, s.Programmata?.Ciclo, s.Programmata?.Righe.Count ?? 0,
        s.Programmata?.DerivateCambiate ?? false);
}

/// <summary>
/// Il lavoro su un documento come lo mostra la pagina Documenti (S133, carta 2026-10-28-stato-dei-documenti §1 D2): le
/// quattro colonne e i chip che le filtrano. Una regola sola per la colonna, il chip e il suo numero: un chip che dice
/// 3 e ne mostra 2 è un numero che mente.
/// <para>Un documento nascosto non lo legge nessuno: niente da pubblicare né da programmare. Le sezioni vuote e le righe
/// da rivedere restano lavoro.</para>
/// </summary>
/// <param name="Rivedere">Le righe aperte di «Da fare» sul documento; null = nessuna.</param>
public sealed record LavoroSulDocumento(int Vuote, int Traduzioni, ConfrontoSalvato? Confronto, ImpactBadge? Rivedere,
    bool Nascosto)
{
    /// <summary>I valori del filtro, come stanno nell'indirizzo della pagina (<c>?lavoro=</c>).</summary>
    public static readonly IReadOnlySet<string> Filtri =
        new HashSet<string>(StringComparer.Ordinal) { "any", "complete", "publish", "scheduled", "review" };

    public bool Completare => Vuote > 0 || Traduzioni > 0;
    /// <summary>Da pubblicare: il pubblico legge altro, e nessuna programmata porta già la bozza. Con una programmata
    /// allineata il lavoro è fatto — arriva al suo ciclo — ed è la stessa regola che fa tacere la riga «da ripubblicare»
    /// (<c>ProgrammataAllineataAsync</c>): trovato a schermo il 28 ottobre, la colonna chiedeva un lavoro già fatto.</summary>
    public bool Pubblicare => !Nascosto && Confronto is { DaPubblicare: true, ProgrammataAllineata: false };
    public bool Programmata => !Nascosto && Confronto is { ProgrammataIndietro: true };
    public bool DaRivedere => Rivedere is { Total: > 0 };
    public bool Qualcosa => Completare || Pubblicare || Programmata || DaRivedere;

    /// <summary>Vero se il documento passa il filtro; un filtro che non si conosce lascia passare tutto.</summary>
    public bool Passa(string filtro) => filtro switch
    {
        "any" => Qualcosa,
        "complete" => Completare,
        "publish" => Pubblicare,
        "scheduled" => Programmata,
        "review" => DaRivedere,
        _ => true,
    };
}

public interface IConfrontiDocumentiRepository
{
    /// <summary>Tutti i riassunti conservati, per documento.</summary>
    Task<IReadOnlyDictionary<int, ConfrontoSalvato>> TuttiAsync(CancellationToken ct = default);

    /// <summary>Scrive (o riscrive) i riassunti di questi documenti. Un documento che non c'è più si salta.</summary>
    Task ScriviAsync(IReadOnlyList<ConfrontoSalvato> righe, CancellationToken ct = default);

    /// <summary>Toglie i riassunti dei documenti fuori da questo elenco: quelli che il giro non guarda più (nascosti,
    /// senza release), perché un riassunto vecchio direbbe ancora quel che non è più vero.</summary>
    Task TogliAltriAsync(IReadOnlyCollection<int> tenuti, CancellationToken ct = default);
}
