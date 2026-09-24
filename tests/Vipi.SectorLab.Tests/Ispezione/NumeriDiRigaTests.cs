using Vipi.SectorLab.Core.Sessione;

namespace Vipi.SectorLab.Tests.Ispezione;

/// <summary>
/// I numeri di riga della scheda sono quelli del file (committente, 24 settembre: clic sulla riga 30 apriva la 29).
/// Su ogni file dei campioni: la riga numero N della scheda ha lo stesso testo della riga N del file com'è.
/// </summary>
public sealed class NumeriDiRigaTests : IDisposable
{
    private readonly AlberoDiProva _albero = new();

    public void Dispose() => _albero.Dispose();

    [Fact]
    public void LaRigaNumeroNDellaSchedaELaRigaNDelFile()
    {
        // SECTORLAB_ALBERO_VERO = la radice di un clone vero, per rifare la misura sull'albero intero (in locale).
        string radice = Environment.GetEnvironmentVariable("SECTORLAB_ALBERO_VERO") is { Length: > 0 } vero ? vero : _albero.Radice;
        var sessione = SessioneAperta.Apri(CartellaDelSector.Riconosci(radice, out _)!);
        var sbagliate = new List<string>();

        foreach (var file in sessione.File.Values.OfType<IFileConRecord>())
        {
            var righe = file.RigheDelFile([]);
            for (int i = 0; i < file.RecordDelModello.Count; i++)
            {
                foreach (var riga in file.RigheDelRecord(i, contesto: 2))
                {
                    if (riga.Numero < 1 || riga.Numero > righe.Count || righe[riga.Numero - 1] != riga.Testo)
                        sbagliate.Add($"{((FileAperto)file).Relativo}:{riga.Numero} «{riga.Testo}» ≠ «{(riga.Numero >= 1 && riga.Numero <= righe.Count ? righe[riga.Numero - 1] : "—")}»");
                }
            }
        }

        Assert.True(sbagliate.Count == 0, string.Join("\n", sbagliate.Take(10)) + $"\n({sbagliate.Count} in tutto)");
    }
}
