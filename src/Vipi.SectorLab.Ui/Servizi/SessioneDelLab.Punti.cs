using Vipi.SectorLab.Core.Modifiche;
using Vipi.SectorLab.Core.Sessione;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.SectorLab.Ui.Servizi;

/// <summary>Un record di un punto solo che si sposta sulla mappa (carta «L'editor sulla mappa», §11, D4): il suo nome e dov'è.</summary>
public sealed record PuntoDaMuovere(string Nome, Coordinate Dove);

public sealed partial class SessioneDelLab
{
    /// <summary>Il campo della posizione dei record di un punto (D4): lo stesso che la Scheda cambia.</summary>
    public const string CampoDellaPosizione = "Position";

    /// <summary>
    /// Il punto del record, se è un fix, un VOR, un NDB o un punto VFR (D9: questi quattro), o null. Il nome è quello con cui
    /// gli altri record lo citano.
    /// </summary>
    public PuntoDaMuovere? PuntoDelRecord(string fileRelativo, int record)
        => ModelloDelRecord(fileRelativo, record) switch
        {
            Fix f => new PuntoDaMuovere(f.Name, f.Position),
            Vor v => new PuntoDaMuovere(v.Ident, v.Position),
            Ndb n => new PuntoDaMuovere(n.Ident, n.Position),
            VfrPoint p => new PuntoDaMuovere(p.Name, p.Position),
            _ => null,
        };

    private object? ModelloDelRecord(string fileRelativo, int record)
        => Sessione?.File.GetValueOrDefault(fileRelativo) is IFileConRecord conRecord && record >= 0 && record < conRecord.RecordDelModello.Count
            ? conRecord.RecordDelModello[record]
            : null;

    /// <summary>Quanti record citano il punto per nome (R16): lo dice la maniglia.</summary>
    public int CitatoDa(string fileRelativo, int record)
        => PuntoDelRecord(fileRelativo, record) is { } punto && _chiLoUsa is { } chi
            ? chi.Citano(punto.Nome).Count(c => c != (fileRelativo, record))
            : 0;

    /// <summary>
    /// R15: i file che citano per nome uno dei punti dati — dopo che si sono spostati vanno ridisegnati, coi cataloghi
    /// rifatti prima (è lì che il nome trova la sua posizione).
    /// </summary>
    private IReadOnlyList<string> FileCheCitano(IEnumerable<(string File, int Record)> punti)
    {
        if (_chiLoUsa is not { } chi)
            return [];
        return [.. punti
            .Select(p => PuntoDelRecord(p.File, p.Record)?.Nome)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .SelectMany(nome => chi.Citano(nome).Select(c => c.File))
            .Distinct(StringComparer.Ordinal)];
    }

    /// <summary>I record di un punto la cui posizione è fra le modifiche date (le copie gemelle comprese).</summary>
    private static IEnumerable<(string File, int Record)> PuntiSpostati(IEnumerable<Modifica> modifiche)
        => modifiche.OfType<ModificaDiCampo>().Where(m => m.Campo == CampoDellaPosizione).Select(m => (m.File, m.Record));

    /// <summary>
    /// R15: un punto si è spostato (dalla mappa, dal campo della Scheda, da un annulla): i cataloghi si rifanno e chi lo cita
    /// si ridisegna, così sulla mappa settori e procedure lo seguono subito invece che dopo il salvataggio.
    /// </summary>
    private void ChiLoCitaLoSegua(IEnumerable<(string File, int Record)> punti)
    {
        var lista = punti.ToList();
        if (lista.Count == 0 || !lista.Any(p => PuntoDelRecord(p.File, p.Record) is not null))
            return;
        // Nel rigioco i cataloghi si rifanno una volta alla fine (Rigioca), e la geometria aspetta anche lei.
        if (!_rigioco)
            RifaiICataloghi();
        foreach (string file in FileCheCitano(lista))
            RifaiLaGeometria(file);
    }
}
