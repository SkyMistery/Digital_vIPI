using Vipi.SectorLab.Core.Copie;
using Vipi.SectorLab.Core.Modifiche;
using Vipi.SectorLab.Core.Sessione;
using Vipi.Sectorfile.Models;

namespace Vipi.SectorLab.Tests.Copie;

/// <summary>
/// Carta F3-bis, slice 2: una modifica di campo va anche sulle copie gemelle che avevano lo stesso valore. Il criterio
/// della carta: <b>«cambio LIRF in <c>lirr.ap</c>» = −1 +1 in due file</b>, una voce sola, e si annulla insieme. Sui
/// campioni veri: <c>LIRF</c> è uguale in <c>itap.ap</c> e <c>lirr.ap</c>, <c>LIBA</c> no (182 e 185 ft).
/// </summary>
public sealed class PropagazioneAlleCopieTests : IDisposable
{
    private const string Ap = "SectorFiles/Include/IT/OTHER/itap.ap";
    private const string ApFir = "SectorFiles/Include/IT/OTHER/lirr.ap";

    private readonly AlberoDiProva _albero = new();
    private readonly ModificheInSospeso _modifiche = new();
    private readonly SessioneAperta _sessione;

    public PropagazioneAlleCopieTests()
        => _sessione = SessioneAperta.Apri(CartellaDelSector.Riconosci(_albero.Radice, out _)!);

    public void Dispose() => _albero.Dispose();

    private FileAperto File(string relativo) => _sessione.File[relativo];

    private int Indice(string file, string icao)
        => ((IFileConRecord)File(file)).RecordDelModello.Select((r, i) => (r, i))
            .First(v => v.r is AirportInfo a && a.IcaoCode == icao && !a.IsDisabled).i;

    private AirportInfo Scalo(string file, string icao)
        => (AirportInfo)((IFileConRecord)File(file)).RecordDelModello[Indice(file, icao)];

    private object Cambia(string file, string icao, string valore)
        => _modifiche.CambiaAncheLeCopie(File(file), Indice(file, icao), "ElevationFt", valore,
            GemelliDellaSessione.Di(_sessione), f => _sessione.File.GetValueOrDefault(f), icao);

    [Fact]
    public void CambiareLirfInUnFileLoCambiaAncheNellAltro()
    {
        var principale = Assert.IsType<ModificaDiCampo>(Cambia(ApFir, "LIRF", "15"));

        Assert.Equal(15, Scalo(Ap, "LIRF").ElevationFt);
        Assert.Equal(1, _modifiche.Quante);
        Assert.Equal(2, _modifiche.Tutte.Count);
        Assert.Equal([Ap, ApFir], _modifiche.FileToccati);
        var copia = Assert.Single(_modifiche.CopieDi(principale));
        Assert.Equal((Ap, "14", "15"), (copia.File, copia.Prima, copia.Dopo));
        Assert.Equal("ElevationFt: 14 → 15, come in lirr.ap", copia.Descrizione);
        Assert.Empty(principale.NonToccate);

        // Il criterio della carta: una riga tolta e una aggiunta, in ciascuno dei due file.
        foreach (string file in new[] { Ap, ApFir })
        {
            var diff = _modifiche.DiffDi(File(file));
            Assert.Equal((1, 1), (diff.Tolte, diff.Aggiunte));
        }
    }

    [Fact]
    public void AnnullareLaVoceRimetteTutteLeCopie()
    {
        var principale = Assert.IsType<ModificaDiCampo>(Cambia(ApFir, "LIRF", "15"));

        Assert.True(_modifiche.Annulla(File(ApFir), principale));

        Assert.Equal(14, Scalo(Ap, "LIRF").ElevationFt);
        Assert.Equal(14, Scalo(ApFir, "LIRF").ElevationFt);
        Assert.False(_modifiche.CEQualcosa);
    }

    // Dal pannello si può premere «annulla» anche sulla riga della copia, nel suo file: va via la voce intera.
    [Fact]
    public void AnnullareLaCopiaAnnullaLaSuaVoce()
    {
        var principale = Assert.IsType<ModificaDiCampo>(Cambia(ApFir, "LIRF", "15"));
        var copia = Assert.Single(_modifiche.CopieDi(principale));

        Assert.True(_modifiche.Annulla(File(Ap), copia, f => _sessione.File.GetValueOrDefault(f)));

        Assert.Equal(14, Scalo(ApFir, "LIRF").ElevationFt);
        Assert.False(_modifiche.CEQualcosa);
    }

    // D2: LIBA ha 182 in itap.ap e 185 in lirr.ap. Cambiando lirr.ap, itap.ap non si tocca: lo si dice.
    [Fact]
    public void UnaCopiaGiaDiversaNonSiToccaESiDice()
    {
        var principale = Assert.IsType<ModificaDiCampo>(Cambia(ApFir, "LIBA", "186"));

        Assert.Equal(182, Scalo(Ap, "LIBA").ElevationFt);
        Assert.Empty(_modifiche.CopieDi(principale));
        var lasciata = Assert.Single(principale.NonToccate);
        Assert.Equal((Ap, "182"), (lasciata.File, lasciata.Valore));
        Assert.Equal("in itap.ap ha 182: non cambiato", lasciata.Descrizione);
        Assert.Equal([ApFir], _modifiche.FileToccati);
    }

    [Fact]
    public void AllineareUnaCopiaLaPortaNellaVoce()
    {
        var principale = Assert.IsType<ModificaDiCampo>(Cambia(ApFir, "LIBA", "186"));

        Assert.IsType<ModificaDiCampo>(_modifiche.AllineaLaCopia(principale, principale.NonToccate[0], f => _sessione.File.GetValueOrDefault(f)));

        Assert.Equal(186, Scalo(Ap, "LIBA").ElevationFt);
        Assert.Equal(1, _modifiche.Quante);
        var aggiornata = Assert.IsType<ModificaDiCampo>(Assert.Single(_modifiche.Voci));
        Assert.Empty(aggiornata.NonToccate);
        Assert.Equal(Ap, Assert.Single(_modifiche.CopieDi(aggiornata)).File);

        // E si annulla con lei: itap.ap torna a 182, il suo valore dell'apertura.
        _modifiche.Annulla(File(ApFir), aggiornata);
        Assert.Equal(182, Scalo(Ap, "LIBA").ElevationFt);
        Assert.Equal(185, Scalo(ApFir, "LIBA").ElevationFt);
    }

    // Un secondo cambio sullo stesso campo porta anche le copie, e rimettere il valore dell'apertura le rimette tutte.
    [Fact]
    public void DueCambiDiFilaEIlRitornoValgonoPerLeCopie()
    {
        Cambia(ApFir, "LIRF", "15");
        Cambia(ApFir, "LIRF", "16");
        Assert.Equal(16, Scalo(Ap, "LIRF").ElevationFt);
        Assert.Equal(1, _modifiche.Quante);
        Assert.Equal("14", Assert.Single(_modifiche.CopieDi(_modifiche.Voci[0])).Prima);

        Cambia(ApFir, "LIRF", "14");
        Assert.Equal(14, Scalo(Ap, "LIRF").ElevationFt);
        Assert.False(_modifiche.CEQualcosa);
    }

    // Un record aggiunto in testa a lirr.ap fa scorrere il numero di LIRF: la copia deve seguirlo, o diventerebbe
    // una voce orfana e annullare la principale non la rimetterebbe più.
    [Fact]
    public void LaCopiaSegueLaPrincipaleQuandoIRecordScorrono()
    {
        Cambia(ApFir, "LIRF", "15");

        Assert.IsType<ModificaDiStruttura>(_modifiche.AggiungiRecord(File(ApFir), 0));

        var principale = Assert.Single(_modifiche.Voci.OfType<ModificaDiCampo>());
        Assert.Equal(Indice(ApFir, "LIRF"), principale.Record);
        Assert.Single(_modifiche.CopieDi(principale));
        _modifiche.Annulla(File(ApFir), principale);
        Assert.Equal(14, Scalo(Ap, "LIRF").ElevationFt);
    }

    [Fact]
    public void UnoScaloSenzaCopieSiCambiaDaSolo()
    {
        var principale = Assert.IsType<ModificaDiCampo>(Cambia(Ap, "LIML", "400"));

        Assert.Empty(_modifiche.CopieDi(principale));
        Assert.Empty(principale.NonToccate);
        Assert.Equal([Ap], _modifiche.FileToccati);
    }
}
