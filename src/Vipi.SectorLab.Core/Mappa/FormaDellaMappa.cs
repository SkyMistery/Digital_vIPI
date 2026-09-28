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
/// <param name="Tratto">Il colore della linea come è scritto nel record (5° campo dei <c>.geo</c>, bordo di
/// <c>.tfl</c> e <c>.pol</c>): un nome o un valore. Null per i record che il colore lo prendono dallo schema.</param>
/// <param name="Riempimento">Il riempimento come è scritto (<c>.tfl</c>, <c>.pol</c>).</param>
/// <param name="SoloBordo">Il riempimento non si disegna: settore dinamico, o opacità a 1 (lotto «Subito» slice 4, D3).</param>
/// <param name="Chiave">La chiave dello schema di Aurora quando la decide il record e non il file (le voci degli
/// <c>.str</c>: STAR, IAP, GOAROUND…).</param>
public sealed record FormaDellaMappa(
    string File,
    int Record,
    TipoDiForma Tipo,
    string Etichetta,
    IReadOnlyList<IReadOnlyList<Coordinate>> Tratti,
    IReadOnlyList<string> NomiNonRisolti,
    string? Tratto = null,
    string? Riempimento = null,
    bool SoloBordo = false,
    string? Chiave = null)
{
    public int Punti => Tratti.Sum(t => t.Count);
}
