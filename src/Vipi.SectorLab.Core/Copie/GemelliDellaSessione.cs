using Vipi.SectorLab.Core.Sessione;
using Vipi.Sectorfile.Validazione;

namespace Vipi.SectorLab.Core.Copie;

/// <summary>
/// Le copie gemelle dei file aperti (carta F3-bis §2.1, slice 1): per ogni record di un <c>.ap</c>, <c>.rw</c>,
/// <c>.frq</c>, le sue copie negli altri file della famiglia. Le regole (chiave, famiglia, ordine) sono del motore
/// (<see cref="CopieGemelle"/>), le stesse del validatore: qui c'è solo l'indice sui file della sessione.
/// </summary>
/// <remarks>
/// Gli indici dei record cambiano quando se ne aggiunge o toglie uno (slice 8 di F3): l'indice si rifà dopo ogni cambio
/// di struttura, e costa poco (un migliaio di record sull'albero vero).
/// </remarks>
public sealed class GemelliDellaSessione
{
    private readonly Dictionary<(string File, int Indice), GruppoDiGemelli> _perCopia;

    private GemelliDellaSessione(IReadOnlyList<GruppoDiGemelli> gruppi)
    {
        Gruppi = gruppi;
        _perCopia = new Dictionary<(string, int), GruppoDiGemelli>(new ConfrontoDellaCopia());
        foreach (var gruppo in gruppi)
        {
            foreach (var copia in gruppo.Copie)
                _perCopia[(copia.File, copia.Indice)] = gruppo;
        }
    }

    /// <summary>Tutti i gruppi: le chiavi che stanno in almeno due file della loro famiglia.</summary>
    public IReadOnlyList<GruppoDiGemelli> Gruppi { get; }

    /// <summary>L'indice dei gemelli dei file aperti, con i record come sono adesso.</summary>
    public static GemelliDellaSessione Di(SessioneAperta sessione)
    {
        ArgumentNullException.ThrowIfNull(sessione);
        var file = sessione.File.Values
            .Where(f => f is IFileConRecord && CopieGemelle.Famiglia(f.Relativo) is not null)
            .Select(f => (f.Relativo, ((IFileConRecord)f).RecordDelModello));
        return new GemelliDellaSessione(CopieGemelle.Trova(file));
    }

    /// <summary>Il gruppo di un record, o null se il record non ha copie in altri file.</summary>
    public GruppoDiGemelli? GruppoDi(string relativo, int indice)
        => _perCopia.GetValueOrDefault((relativo, indice));

    /// <summary>
    /// Le altre copie di un record, dove una modifica si può portare. Vuoto se non ne ha, e vuoto anche quando la chiave
    /// si ripete in un file e le copie non si abbinano (D10): lì non si sa quale copia va con quale.
    /// </summary>
    public IReadOnlyList<CopiaGemella> AltreCopie(string relativo, int indice)
        => GruppoDi(relativo, indice) is { PerOrdine: true } gruppo
            ? gruppo.Copie.Where(c => !Stessa(c, relativo, indice)).ToList()
            : [];

    private static bool Stessa(CopiaGemella copia, string relativo, int indice)
        => copia.Indice == indice && string.Equals(copia.File, relativo, StringComparison.OrdinalIgnoreCase);

    // I percorsi della sessione sono indifferenti a maiuscole e minuscole (è Windows): anche l'indice.
    private sealed class ConfrontoDellaCopia : IEqualityComparer<(string File, int Indice)>
    {
        public bool Equals((string File, int Indice) a, (string File, int Indice) b)
            => a.Indice == b.Indice && string.Equals(a.File, b.File, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string File, int Indice) c)
            => HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(c.File), c.Indice);
    }
}
