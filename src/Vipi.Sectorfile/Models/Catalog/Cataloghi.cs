using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.Models;

/// <summary>Il nome (o i nomi) col quale un record entra in un catalogo, e dov'è.</summary>
/// <param name="Catalogo">«fix», «vor», «ndb», «scalo» o «vrp».</param>
/// <param name="Posizione">Il punto del record.</param>
/// <param name="Nomi">I nomi con cui lo si cita (il VRP ne ha due: il nome e il codice).</param>
public readonly record struct NomeDiCatalogo(string Catalogo, Coordinate Posizione, IReadOnlyList<string> Nomi);

/// <summary>
/// Quali record del sector sono un punto CHIAMABILE PER NOME: i cataloghi in cui Aurora cerca quando una riga scrive
/// <c>AMSOR;AMSOR;</c> invece delle coordinate (carta F2 §1).
/// <para>Sta qui, nel motore, perché lo chiedono in due: il validatore (i nomi non risolti e i doppi, carta F2 §3) e
/// il catalogo dei punti dell'app, che alla mappa serve per disegnare i vertici scritti per nome (carta F3, slice 3).</para>
/// </summary>
public static class Cataloghi
{
    public static NomeDiCatalogo? Dichiarato(object record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return record switch
        {
            Fix f => new NomeDiCatalogo("fix", f.Position, [f.Name.Trim()]),
            Vor v => new NomeDiCatalogo("vor", v.Position, [v.Ident.Trim()]),
            Ndb n => new NomeDiCatalogo("ndb", n.Position, [n.Ident.Trim()]),
            AirportInfo a => new NomeDiCatalogo("scalo", a.Centre, [a.IcaoCode.Trim()]),
            VfrPoint p => new NomeDiCatalogo("vrp", p.Position, [p.Name.Trim(), p.Code.Trim()]),
            _ => null,
        };
    }
}
