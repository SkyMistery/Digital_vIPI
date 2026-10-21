namespace Aip;

/// <summary>
/// Un testo dell'AIP che non è una tabella (carta «L'AIP intero», passo 4): le regole e le definizioni scritte in prosa —
/// l'introduzione di ENR 5.1, le generalità dei poligoni militari, le operazioni con droni in BVLOS (ENR 5.3.4), le regole
/// dei lanci paracadutisti (ENR 5.5.1). L'AIP italiano è in due colonne, l'italiano a sinistra e l'inglese a destra: qui
/// un paragrafo per riga, com'è stampato.
/// </summary>
public sealed record AipText
{
    /// <summary>La sezione dell'AIP: <c>ENR 5.3.4</c>.</summary>
    public required string Section { get; init; }

    /// <summary>Il testo italiano, un paragrafo per riga.</summary>
    public required string Text { get; init; }

    /// <summary>Il testo inglese, un paragrafo per riga.</summary>
    public string? English { get; init; }

    /// <summary>La prima pagina del testo, con la sua data.</summary>
    public AipPage? Page { get; init; }
}
