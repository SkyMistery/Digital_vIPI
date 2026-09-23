using Vipi.SectorLab.Core.Sessione;
using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Models;

namespace Vipi.SectorLab.Core.Modifiche;

/// <summary>
/// Una procedura del file come casella della scheda: la voce che la nomina, le sue piste, che tipo di record è (STAR,
/// avvicinamento, attesa…: nei .str ci sono tutti) e se è nell'elenco.
/// </summary>
public sealed record CasellaDellaComposta(ProceduraDellaComposta Voce, string Piste, string Tipo, bool Scelta)
{
    /// <summary>Falso se il nome non può stare nell'elenco (uno spazio, una virgola…): la casella si mostra spenta.</summary>
    public bool Elencabile => Metadati.NomeElencabile(Voce.Nome);

    /// <summary>Come si scrive nell'elenco e nella scheda: <c>ODIN4E</c>, o <c>25:NENI5A</c> se il nome ha più piste.</summary>
    public string Testo => (Voce.Pista is null ? "" : Voce.Pista + ":") + Voce.Nome;
}

/// <summary>
/// «Composta da» nella scheda di una mappa (F3-bis §2.2, slice 5): una casella per ogni procedura dello stesso
/// <c>.str</c> (D5-D6), spuntate quelle dell'elenco, e se le procedure si disegnano intere.
/// </summary>
/// <param name="DaQuelloCheDisegna">
/// Le procedure che la mappa disegna oggi, nell'ordine dei tratti: con queste diventa composta senza cambiare (21
/// aggregati su 56 sul fork). Spuntare le caselle una per una invece rigenera a ogni casella.
/// </param>
public sealed record SchedaDellaComposta(
    IReadOnlyList<CasellaDellaComposta> Caselle,
    IReadOnlyList<ProceduraDellaComposta> Elenco,
    bool Intere,
    IReadOnlyList<ProceduraDellaComposta> DaQuelloCheDisegna)
{
    /// <summary>Null se il record non è una mappa <c>MAPS</c> di un <c>.str</c>.</summary>
    public static SchedaDellaComposta? Di(FileAperto file, int indice)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (file is not FileLetto<StrRecord> str || indice < 0 || indice >= str.Letto.Records.Count
            || str.Letto.Records[indice] is not { RunwaySpec: "MAPS" } mappa)
        {
            return null;
        }

        var composta = MappeComposte.Di(str.Letto).FirstOrDefault(c => ReferenceEquals(c.Mappa, mappa));
        var elenco = composta?.Elenco ?? [];
        var procedure = str.Letto.Records.Where(r => r.RunwaySpec != "MAPS").ToList();

        // Una casella per procedura. Il nome da solo, se nel file è unico; se no con la sua prima pista (`25:NENI5A`),
        // così spuntarne una non prende anche le altre.
        var caselle = procedure.Select(p =>
        {
            string nome = p.ProcedureId.Trim();
            bool unico = procedure.Count(q => q.ProcedureId.Trim() == nome) == 1;
            var voce = new ProceduraDellaComposta(unico ? null : p.RunwaySpec.Split(':')[0].Trim(), nome);
            return (Casella: new CasellaDellaComposta(voce, p.RunwaySpec, Tipo(p.RecordType), elenco.Any(v => MappeComposte.Nomina(v, p))),
                    Ordine: p.RecordType == StrRecordType.Star ? 0 : 1);
        })
        .DistinctBy(c => c.Casella.Testo)
        .OrderBy(c => c.Ordine)   // prima le STAR, che sono quello che una mappa composta raccoglie
        .Select(c => c.Casella)
        .ToList();

        return new SchedaDellaComposta(caselle, elenco, composta?.Intere ?? false,
            MappeComposte.ProcedureCheDisegna(mappa, str.Letto.Records));
    }

    private static string Tipo(StrRecordType tipo) => tipo switch
    {
        StrRecordType.Star => "STAR",
        StrRecordType.Holding => "attesa",
        StrRecordType.Iap => "avvicinamento",
        StrRecordType.Fap => "finale",
        StrRecordType.GoAround => "mancato",
        _ => "altro",
    };

    /// <summary>L'elenco dopo aver spuntato (in coda) o tolto una casella.</summary>
    public IReadOnlyList<ProceduraDellaComposta> Con(CasellaDellaComposta casella, bool scelta)
        => scelta
            ? [.. Elenco, casella.Voce]
            : [.. Elenco.Where(v => v != casella.Voce && !(v.Pista is null && v.Nome == casella.Voce.Nome))];
}
