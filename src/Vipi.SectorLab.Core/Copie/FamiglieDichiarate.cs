using Vipi.SectorLab.Core.Sessione;
using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Validazione;

namespace Vipi.SectorLab.Core.Copie;

/// <summary>Un record di una famiglia dichiarata, con la sua parte che ha la forma della famiglia (null: non ce l'ha).</summary>
public sealed record MembroDellaFamiglia(string File, int Record, string Etichetta, ParteDiForma? Parte)
{
    public bool Uguale => Parte is not null;
}

/// <summary>Una famiglia di forme dichiarata col tag <c>form=NOME</c>: i suoi record, nell'ordine dei file.</summary>
public sealed record FamigliaDichiarata(string Nome, IReadOnlyList<MembroDellaFamiglia> Membri)
{
    /// <summary>Vero se qualche membro non ha la forma della famiglia (e la famiglia ne ha almeno due).</summary>
    public bool ConCopieDiverse => Membri.Count > 1 && Membri.Any(m => !m.Uguale);
}

/// <summary>
/// Le famiglie di forme dichiarate (lotto «Subito» slice 8b, «file per file» D5, §M): i record che portano lo stesso
/// <c>form=NOME</c> — nei settori, nei confini, nelle mappe degli <c>.str</c>, nei <c>.geo</c> e nei <c>.pol</c> —
/// sono la stessa forma. La forma della famiglia è quella che hanno più membri (<see cref="FormeUguali.Confronta"/>);
/// chi non ce l'ha è una «copia di forma diversa» (<see cref="Regola.FormeDiverse"/>, avviso).
/// </summary>
/// <remarks>
/// La famiglia la trova il Lab dalla geometria (slice 8a) e la DICHIARA il tag: senza tag nessun avviso, perché due
/// settori che si somigliano (<c>LIMM_ES2_CTR</c> e <c>LIMM_ES5_CTR</c>, 12 vertici su 130 diversi) non sono per forza
/// la stessa cosa. Il nome della famiglia si confronta com'è scritto (maiuscole comprese), come ogni valore di §M.
/// </remarks>
public static class FamiglieDichiarate
{
    public const string Chiave = "form";

    /// <summary>Le famiglie della sessione com'è adesso, per nome.</summary>
    public static IReadOnlyList<FamigliaDichiarata> Di(SessioneAperta sessione, FormeUguali forme)
    {
        ArgumentNullException.ThrowIfNull(sessione);
        ArgumentNullException.ThrowIfNull(forme);

        var perNome = new SortedDictionary<string, List<(string File, int Record)>>(StringComparer.Ordinal);
        foreach (var file in sessione.File.Values.OrderBy(f => f.Relativo, StringComparer.Ordinal))
        {
            if (file is not IFileConRecord conRecord || conRecord.CatalogoDeiTag?.AmmetteDelRecord(Chiave) != true)
                continue;
            var chiavi = conRecord.ChiaviDeiRecord();
            for (int i = 0; i < chiavi.Count; i++)
            {
                if (chiavi[i]?.GetValueOrDefault(Chiave) is not { } scritto || Metadati.Testo(scritto).Trim() is not { Length: > 0 } nome)
                    continue;
                if (!perNome.TryGetValue(nome, out var membri))
                    perNome[nome] = membri = [];
                membri.Add((file.Relativo, i));
            }
        }

        return [.. perNome.Select(f => new FamigliaDichiarata(f.Key,
            [.. forme.Confronta(f.Value).Select(m => new MembroDellaFamiglia(m.File, m.Record, Etichetta(sessione, m.File, m.Record), m.Parte))]))];
    }

    /// <summary>La famiglia di un record, o null se il record non ne dichiara una.</summary>
    public static FamigliaDichiarata? Del(IReadOnlyList<FamigliaDichiarata> famiglie, SessioneAperta sessione, string file, int record)
    {
        ArgumentNullException.ThrowIfNull(famiglie);
        ArgumentNullException.ThrowIfNull(sessione);
        return sessione.File.GetValueOrDefault(file) is IFileConRecord conRecord
               && conRecord.ChiaviDi(record)?.GetValueOrDefault(Chiave) is { } scritto
               && Metadati.Testo(scritto).Trim() is { Length: > 0 } nome
            ? famiglie.FirstOrDefault(f => f.Nome == nome)
            : null;
    }

    /// <summary>
    /// L'avviso «copie di forma diverse» per ogni membro che non ha la forma della sua famiglia, sulla prima riga del
    /// record, coi nomi dei file giusti della sessione (si agganciano con <c>ProblemiDelLab.Aggancia</c>).
    /// </summary>
    public static IEnumerable<ProblemaDelSector> Problemi(IReadOnlyList<FamigliaDichiarata> famiglie, SessioneAperta sessione)
    {
        ArgumentNullException.ThrowIfNull(famiglie);
        ArgumentNullException.ThrowIfNull(sessione);
        foreach (var famiglia in famiglie.Where(f => f.ConCopieDiverse))
        {
            var uguali = famiglia.Membri.Where(m => m.Uguale).ToList();
            foreach (var membro in famiglia.Membri.Where(m => !m.Uguale))
            {
                var riga = sessione.File.GetValueOrDefault(membro.File) is IFileConRecord conRecord
                    ? conRecord.RigheDelRecord(membro.Record, 0).FirstOrDefault(r => r.DelRecord)
                    : null;
                string elenco = string.Join(", ", uguali.Take(3).Select(m => NomeDelFile(m.File) + " " + m.Etichetta))
                                + (uguali.Count > 3 ? $" e altre {uguali.Count - 3}" : "");
                string dettaglio = uguali.Count == 0
                    ? $"Famiglia «{famiglia.Nome}»: nessun membro ha la forma di un altro."
                    : $"Famiglia «{famiglia.Nome}»: {membro.Etichetta} ha una forma diversa da {elenco}.";
                yield return new ProblemaDelSector(Regola.FormeDiverse, membro.File, riga?.Numero ?? 0, riga?.Testo ?? "", dettaglio);
            }
        }
    }

    private static string Etichetta(SessioneAperta sessione, string file, int record)
        => sessione.File.GetValueOrDefault(file) is IFileConRecord conRecord && record < conRecord.RecordDelModello.Count
           && Metadati.NomeDelRecord(conRecord.RecordDelModello[record]) is { Length: > 0 } nome
            ? nome.Trim()
            : $"record {record}";

    private static string NomeDelFile(string relativo) => relativo[(relativo.LastIndexOf('/') + 1)..];
}
