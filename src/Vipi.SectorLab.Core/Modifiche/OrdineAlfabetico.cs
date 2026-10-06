using Vipi.SectorLab.Core.Copie;
using Vipi.SectorLab.Core.Sessione;
using Vipi.Sectorfile.Models;

namespace Vipi.SectorLab.Core.Modifiche;

/// <summary>
/// Dove va un record nuovo che ha un nome: in ordine alfabetico, dentro la sua SEZIONE (committente, prova 6 del 23
/// settembre: «i fix devono essere inseriti in ordine alfabetico, non sotto quello»). La sezione è il gruppo di record
/// fra due commenti: in <c>APT.fix</c> i fix stanno sotto l'intestazione del loro scalo (<c>//LIBC</c>, <c>//LIBD</c>),
/// e un fix di LIBD non va fra quelli di LIBC solo perché l'alfabeto lo metterebbe lì. Quale sezione lo dice il
/// prefisso del nome (<see cref="NellaSezioneGiusta"/>), non il record da cui si parte.
/// <para>Per ora solo i punti col nome (fix, VOR, NDB, punti VFR): l'ordine degli altri file si discute file per file
/// col committente.</para>
/// <para>Slice 16c (F3): i punti VFR si chiamano col nome ma stanno in ordine di CODICE (<see cref="Chiave"/>): 67
/// <c>.vfi</c> su 77 del fork sono così, e solo 4 in ordine di nome. Il numero del codice conta come numero
/// (<c>RFS4</c> prima di <c>RFS10</c>).</para>
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

    /// <summary>Quel che mette in ordine il record nel suo file: il nome, o il codice per un punto VFR.</summary>
    public static string Chiave(object record) => record is VfrPoint punto ? punto.Code.Trim() : Nome(record);

    /// <summary>Un nome che si può scrivere in un campo del sector: non vuoto, senza <c>;</c> né spazi ai bordi.</summary>
    public static string? PercheNonVa(string? nome)
        => string.IsNullOrWhiteSpace(nome) ? "Il nome non può essere vuoto."
            : nome.Contains(';', StringComparison.Ordinal) ? "Il nome non può avere il «;»: nel sector separa i campi."
            : nome.Trim() != nome ? "Il nome non può cominciare o finire con uno spazio."
            : nome.StartsWith("//", StringComparison.Ordinal) ? "Un nome che comincia con «//» sarebbe un commento."
            : null;

    /// <summary>
    /// Il record, fra tutti quelli del file, che per chiave (il nome; il codice per un punto VFR) viene subito prima di
    /// <paramref name="nome"/> (o il primo, se nessuno viene prima): è il modello del nuovo, e la sua sezione è quella
    /// dove il nuovo andrà.
    /// </summary>
    public static int IlVicino(IFileConRecord file, string nome)
    {
        ArgumentNullException.ThrowIfNull(file);
        int vicino = -1;
        string? suoNome = null;
        for (int i = 0; i < file.RecordDelModello.Count; i++)
        {
            string suo = Chiave(file.RecordDelModello[i]);
            if (Confronta(suo, nome) <= 0 && (suoNome is null || Confronta(suo, suoNome) >= 0))
            {
                vicino = i;
                suoNome = suo;
            }
        }

        return Math.Max(vicino, 0);
    }

    /// <summary>
    /// Un record della sezione dove va un record chiamato <paramref name="nome"/>: quella i cui nomi hanno in comune
    /// con lui il PREFISSO più lungo (in <c>APT.fix</c> <c>BD100</c> e <c>BD430</c> vanno sotto <c>//LIBD</c>, anche se
    /// l'alfabeto metterebbe <c>BD100</c> dopo l'ultimo BC). A parità vince la sezione di <paramref name="modello"/>.
    /// 🔴 Prove 40-41 del committente: con la sezione del record scelto, BD430 da un fix di LIBC finiva in fondo a LIBC.
    /// </summary>
    public static int NellaSezioneGiusta(IFileConRecord file, int modello, string nome)
    {
        ArgumentNullException.ThrowIfNull(file);
        var sezioni = file.Sezioni();
        int scelto = modello, meglio = -1;
        for (int i = 0; i < file.RecordDelModello.Count; i++)
        {
            int suo = Prefisso(Chiave(file.RecordDelModello[i]), nome);
            bool aParitaNelModello = suo == meglio && sezioni[i] == sezioni[modello] && sezioni[scelto] != sezioni[modello];
            if (suo > meglio || aParitaNelModello)
            {
                scelto = i;
                meglio = suo;
            }
        }

        return scelto;
    }

    private static int Prefisso(string a, string b)
    {
        int n = 0;
        while (n < a.Length && n < b.Length && char.ToUpperInvariant(a[n]) == char.ToUpperInvariant(b[n]))
            n++;
        return n;
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
            if (Confronta(Chiave(file.RecordDelModello[i]), nome) > 0)
                return i == primo ? (null, i) : (i - 1, null);
        }

        return (ultimo, null);
    }

    // Due codici VFR con le stesse lettere si confrontano per numero; tutto il resto per alfabeto.
    private static int Confronta(string a, string b)
        => CodiciVfr.Parti(a) is (var lettereA, var numeroA) && CodiciVfr.Parti(b) is (var lettereB, var numeroB)
           && string.Equals(lettereA, lettereB, StringComparison.OrdinalIgnoreCase)
            ? numeroA.CompareTo(numeroB)
            : string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
}
