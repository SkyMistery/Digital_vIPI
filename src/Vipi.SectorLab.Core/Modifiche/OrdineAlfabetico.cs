using Vipi.SectorLab.Core.Sessione;
using Vipi.Sectorfile.Models;

namespace Vipi.SectorLab.Core.Modifiche;

/// <summary>
/// Dove va un record nuovo che ha un nome: in ordine alfabetico, dentro la sua SEZIONE (committente, prova 6 del 23
/// settembre: «i fix devono essere inseriti in ordine alfabetico, non sotto quello»). La sezione è il gruppo di record
/// fra due commenti: in <c>APT.fix</c> i fix stanno sotto l'intestazione del loro scalo (<c>//LIBC</c>, <c>//LIBD</c>),
/// e un fix di LIBD non va fra quelli di LIBC solo perché l'alfabeto lo metterebbe lì.
/// <para>Per ora solo i punti col nome (fix, VOR, NDB, punti VFR): l'ordine degli altri file si discute file per file
/// col committente.</para>
/// </summary>
public static class OrdineAlfabetico
{
    /// <summary>Il campo che fa da nome e da ordine, o null se quel tipo di record non si mette in ordine.</summary>
    public static string? CampoDelNome(object record) => record switch
    {
        Fix => nameof(Fix.Name),
        Vor => nameof(Vor.Ident),
        Ndb => nameof(Ndb.Ident),
        VfrPoint => nameof(VfrPoint.Name),
        _ => null,
    };

    public static string Nome(object record)
        => CampoDelNome(record) is { } campo
            ? record.GetType().GetProperty(campo)?.GetValue(record) as string ?? ""
            : "";

    /// <summary>Un nome che si può scrivere in un campo del sector: non vuoto, senza <c>;</c> né spazi ai bordi.</summary>
    public static string? PercheNonVa(string? nome)
        => string.IsNullOrWhiteSpace(nome) ? "Il nome non può essere vuoto."
            : nome.Contains(';', StringComparison.Ordinal) ? "Il nome non può avere il «;»: nel sector separa i campi."
            : nome.Trim() != nome ? "Il nome non può cominciare o finire con uno spazio."
            : nome.StartsWith("//", StringComparison.Ordinal) ? "Un nome che comincia con «//» sarebbe un commento."
            : null;

    /// <summary>
    /// Il record, fra tutti quelli del file, che per nome viene subito prima di <paramref name="nome"/> (o il primo, se
    /// nessuno viene prima): è il modello del nuovo, e la sua sezione è quella dove il nuovo andrà.
    /// </summary>
    public static int IlVicino(IFileConRecord file, string nome)
    {
        ArgumentNullException.ThrowIfNull(file);
        int vicino = -1;
        string? suoNome = null;
        for (int i = 0; i < file.RecordDelModello.Count; i++)
        {
            string suo = Nome(file.RecordDelModello[i]);
            if (Confronta(suo, nome) <= 0 && (suoNome is null || Confronta(suo, suoNome) >= 0))
            {
                vicino = i;
                suoNome = suo;
            }
        }

        return Math.Max(vicino, 0);
    }

    /// <summary>
    /// Il posto per un record chiamato <paramref name="nome"/> nella sezione di <paramref name="vicino"/>: dopo un
    /// record (<c>Dopo</c>) o, se va primo della sezione, prima del suo primo (<c>PrimaDi</c>: così resta sotto
    /// l'intestazione della sezione).
    /// </summary>
    public static (int? Dopo, int? PrimaDi) Posto(IFileConRecord file, int vicino, string nome)
    {
        ArgumentNullException.ThrowIfNull(file);
        var sezioni = file.Sezioni();
        int sezione = sezioni[vicino];
        int primo = vicino, ultimo = vicino;
        while (primo > 0 && sezioni[primo - 1] == sezione)
            primo--;
        while (ultimo < sezioni.Count - 1 && sezioni[ultimo + 1] == sezione)
            ultimo++;

        for (int i = primo; i <= ultimo; i++)
        {
            if (Confronta(Nome(file.RecordDelModello[i]), nome) > 0)
                return i == primo ? (null, i) : (i - 1, null);
        }

        return (ultimo, null);
    }

    private static int Confronta(string a, string b) => string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
}
