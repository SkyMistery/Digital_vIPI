using Vipi.SectorLab.Core.Sessione;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Validazione;

namespace Vipi.SectorLab.Core.Copie;

/// <summary>La domanda dopo aver tolto un punto .vfi col gemello (8e): «togli anche il fix nascosto?».</summary>
public sealed record GemelloDaTogliere(string Punto, string Codice, string File);

/// <summary>
/// Il gemello di un punto VFR o di un fix nascosto (lotto «Subito» slice 8e, «file per file» F2), come lo mostra la scheda.
/// </summary>
/// <param name="Codice">Il codice che li lega (<c>RFS3</c>): il 2° campo del <c>.vfi</c>, il nome del fix.</param>
/// <param name="Gemelli">Le copie nell'altro file: una se il gemello c'è, nessuna se manca, più di una se il codice si
/// ripete (<c>MJNW1</c>, <c>PKS1</c>: allora non si sa quale va con quale, e non si propaga niente).</param>
/// <param name="Uguale">Vero se la posizione del gemello (unico) è la stessa.</param>
public sealed record GemelloVfr(string Codice, IReadOnlyList<CopiaGemella> Gemelli, bool Uguale);

/// <summary>
/// I gemelli fra tipi diversi (F2): il punto di un <c>.vfi</c> e il suo fix nascosto in <c>NAVAIDS/VFR_NASCOSTI.fix</c>,
/// con chiave il CODICE (<c>COLOMBO;RFS3;…</c> ↔ <c>RFS3;…;3;</c>). Una rotta VFR riconosce un punto solo se sta in un
/// <c>.fix</c>: il gemello è quello che la fa funzionare. Estende le copie gemelle di F3-bis
/// (<see cref="GemelliDellaSessione"/>): fra i due passa solo la <b>posizione</b> — il nome del punto non è il nome del fix.
/// </summary>
/// <remarks>
/// Un codice è due o più lettere e una o due cifre (<c>RFS3</c>, <c>RFNE1</c>, <c>BNNW1</c>); il 2° campo di un <c>.vfi</c>
/// che non è un codice (<c>2500</c> in <c>liba.vfi</c>, <c>BV</c> in <c>libv.vfi</c>) non chiede un gemello.
/// </remarks>
public static class GemelliVfr
{
    /// <summary>La famiglia dei gruppi di gemelli VFR in <see cref="GemelliDellaSessione"/>.</summary>
    public const string Famiglia = "vfr";

    /// <summary>L'unico campo che passa fra i due gemelli.</summary>
    public const string Campo = nameof(VfrPoint.Position);

    /// <summary>Vero se il file è quello dei fix nascosti dei VFR (lo dice il motore: <see cref="ControlloDeiVfr"/>).</summary>
    public static bool ENascosti(string relativo) => ControlloDeiVfr.ENascosti(relativo);

    /// <summary>Vero se il 2° campo di un <c>.vfi</c> è un codice (e chiede un gemello).</summary>
    public static bool EUnCodice(string? codice) => ControlloDeiVfr.EUnCodice(codice);

    /// <summary>
    /// I gruppi di gemelli: per ogni codice che sta in un <c>.vfi</c> e nel file dei nascosti, le copie dei due lati.
    /// <see cref="GruppoDiGemelli.PerOrdine"/> è falso quando un codice si ripete da una parte sola.
    /// </summary>
    public static IReadOnlyList<GruppoDiGemelli> Trova(SessioneAperta sessione)
    {
        ArgumentNullException.ThrowIfNull(sessione);
        var (punti, fix) = Indice(sessione);
        var gruppi = new List<GruppoDiGemelli>();
        foreach (var (codice, suoi) in punti.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            if (!fix.TryGetValue(codice, out var nascosti))
                continue;
            gruppi.Add(new GruppoDiGemelli(Famiglia, codice, [.. suoi, .. nascosti], PerOrdine: suoi.Count == 1 && nascosti.Count == 1));
        }

        return gruppi;
    }

    /// <summary>Il gemello del record (un punto di un <c>.vfi</c> con un codice, o un fix dei nascosti), o null se non ne chiede.</summary>
    public static GemelloVfr? Di(SessioneAperta sessione, string relativo, int indice)
    {
        ArgumentNullException.ThrowIfNull(sessione);
        if (sessione.File.GetValueOrDefault(relativo) is not IFileConRecord conRecord || indice < 0 || indice >= conRecord.RecordDelModello.Count)
            return null;

        var (punti, fix) = Indice(sessione);
        switch (conRecord.RecordDelModello[indice])
        {
            case VfrPoint punto when relativo.EndsWith(".vfi", StringComparison.OrdinalIgnoreCase) && EUnCodice(punto.Code):
            {
                var gemelli = fix.GetValueOrDefault(punto.Code.Trim()) ?? [];
                return new GemelloVfr(punto.Code.Trim(), gemelli, gemelli is [{ Record: Fix f }] && Stesso(f.Position, punto.Position));
            }

            case Fix nascosto when ENascosti(relativo):
            {
                var gemelli = punti.GetValueOrDefault(nascosto.Name.Trim()) ?? [];
                return new GemelloVfr(nascosto.Name.Trim(), gemelli, gemelli is [{ Record: VfrPoint p }] && Stesso(p.Position, nascosto.Position));
            }

            default:
                return null;
        }
    }

    /// <summary>Il file dei fix nascosti della sessione, o null.</summary>
    public static string? FileDeiNascosti(SessioneAperta sessione)
    {
        ArgumentNullException.ThrowIfNull(sessione);
        return sessione.File.Keys.Where(ENascosti).Order(StringComparer.Ordinal).FirstOrDefault();
    }

    // Lo stesso punto al decimo di metro: le due scritture (compatta, col punto) si leggono in numeri appena diversi.
    private static bool Stesso(Sectorfile.Shared.Coordinate a, Sectorfile.Shared.Coordinate b)
        => FormeUguali.Chiave(a) == FormeUguali.Chiave(b);

    // I punti dei .vfi per codice, e i fix dei nascosti per nome, nell'ordine dei file.
    private static (Dictionary<string, List<CopiaGemella>> Punti, Dictionary<string, List<CopiaGemella>> Fix) Indice(SessioneAperta sessione)
    {
        var punti = new Dictionary<string, List<CopiaGemella>>(StringComparer.Ordinal);
        var fix = new Dictionary<string, List<CopiaGemella>>(StringComparer.Ordinal);
        foreach (var file in sessione.File.Values.OrderBy(f => f.Relativo, StringComparer.Ordinal))
        {
            bool vfi = file.Relativo.EndsWith(".vfi", StringComparison.OrdinalIgnoreCase);
            bool nascosti = ENascosti(file.Relativo);
            if ((!vfi && !nascosti) || file is not IFileConRecord conRecord)
                continue;
            var record = conRecord.RecordDelModello;
            for (int i = 0; i < record.Count; i++)
            {
                (string? chiave, var dove) = record[i] switch
                {
                    VfrPoint p when vfi && EUnCodice(p.Code) => (p.Code.Trim(), punti),
                    Fix f when nascosti => (f.Name.Trim(), fix),
                    _ => (null, punti),
                };
                if (chiave is null)
                    continue;
                if (!dove.TryGetValue(chiave, out var copie))
                    dove[chiave] = copie = [];
                copie.Add(new CopiaGemella(file.Relativo, i, record[i], 0));
            }
        }

        return (punti, fix);
    }
}
