using Vipi.Sectorfile.Shared;

namespace Vipi.SectorLab.Core.Mappa;

/// <summary>Che cosa si disegna: un punto, una linea (uno o più tratti) o un'area chiusa.</summary>
public enum TipoDiForma
{
    Punto,
    Linea,
    Area,
}

/// <summary>
/// Un record del sector come lo vede la mappa (carta F3 §2.2 passo 4, slice 3): i suoi tratti, già in coordinate —
/// i punti scritti per nome sono stati risolti nel catalogo del master scelto.
/// </summary>
/// <param name="File">Il file, relativo alla radice del clone.</param>
/// <param name="Record">L'indice del record nel file: è l'aggancio fra mappa, elenco e ispettore.</param>
/// <param name="Tipo">Punto, linea o area.</param>
/// <param name="Etichetta">Come si chiama a schermo (il nome del settore, dell'area, della procedura…).</param>
/// <param name="Tratti">Uno o più tratti di punti; un punto solo per <see cref="TipoDiForma.Punto"/>.</param>
/// <param name="NomiNonRisolti">I nomi citati che il catalogo non conosce: il tratto lì si interrompe.</param>
public sealed record FormaDellaMappa(
    string File,
    int Record,
    TipoDiForma Tipo,
    string Etichetta,
    IReadOnlyList<IReadOnlyList<Coordinate>> Tratti,
    IReadOnlyList<string> NomiNonRisolti)
{
    public int Punti => Tratti.Sum(t => t.Count);
}
