using System.Reflection;
using Vipi.SectorLab.Core.Ispezione;
using Vipi.SectorLab.Core.Sessione;
using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Models;

namespace Vipi.SectorLab.Tests.Ispezione;

/// <summary>
/// La scheda tipizzata (lotto «Subito», slice 3a): ogni tipo di record ha la sua descrizione dei campi — nome
/// italiano, significato, editor — e nessun record si apre con un «campo sconosciuto».
/// </summary>
public sealed class DescrizioniDeiCampiTests : IDisposable
{
    private readonly AlberoDiProva _albero = new();

    public void Dispose() => _albero.Dispose();

    /// <summary>
    /// I tipi di record che il motore produce, cercati nel motore stesso: i T dei suoi lettori e i loro figli concreti
    /// (<c>StrRecord</c> → tre, <c>ElementoArtcc</c> → due). Un lettore nuovo, o un tipo nuovo, entra da solo nel test.
    /// </summary>
    private static IReadOnlyList<Type> TipiDiRecordDelMotore()
    {
        var motore = typeof(Fix).Assembly;
        var radici = motore.GetTypes()
            .Where(t => t is { IsAbstract: false, IsGenericTypeDefinition: false })
            .SelectMany(t => t.GetInterfaces())
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IFileParser<>))
            .Select(i => i.GetGenericArguments()[0])
            .Where(t => !t.IsGenericParameter)
            .Distinct()
            .ToList();
        return [.. motore.GetTypes()
            .Where(t => t is { IsAbstract: false, IsClass: true } && radici.Any(r => r.IsAssignableFrom(t)))
            .OrderBy(t => t.Name, StringComparer.Ordinal)];
    }

    private static IEnumerable<string> ProprietaDaMostrare(Type tipo)
        => tipo.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetIndexParameters().Length == 0 && !DescrizioniDeiCampi.Nascoste.Contains(p.Name))
            .Select(p => p.Name);

    [Fact]
    public void IlMotoreHaIVentiseiTipiDiRecordDiOggi()
    {
        // Se il numero cambia, un tipo è nato o sparito: il test qui sotto dice quale descrizione manca. Lotto «Subito»
        // slice 11c: 24 → 26, i messaggi CPDLC e i nomi dei loro gruppi.
        Assert.Equal(26, TipiDiRecordDelMotore().Count);
    }

    [Fact]
    public void OgniTipoDiRecordDelMotoreEDescrittoCampoPerCampo()
    {
        var descritti = DescrizioniDeiCampi.Tutte().ToList();
        var mancanti = new List<string>();
        foreach (var tipo in TipiDiRecordDelMotore())
        {
            var sue = descritti.Where(d => d.Tipo == tipo).ToList();
            if (sue.Count == 0)
            {
                mancanti.Add($"{tipo.Name}: nessuna descrizione");
                continue;
            }

            foreach (var (_, descrizione) in sue)
            {
                var conosciute = descrizione.Campi.Select(c => c.Proprieta).ToHashSet(StringComparer.Ordinal);
                mancanti.AddRange(ProprietaDaMostrare(tipo).Where(p => !conosciute.Contains(p))
                    .Select(p => $"{tipo.Name} ({descrizione.Nome}): {p}"));
            }
        }

        Assert.True(mancanti.Count == 0, "Campi senza descrizione:\n  " + string.Join("\n  ", mancanti));
    }

    [Fact]
    public void OgniDescrizioneNominaUnaProprietaCheCEUnaVoltaSola()
    {
        foreach (var (tipo, descrizione) in DescrizioniDeiCampi.Tutte())
        {
            var vere = ProprietaDaMostrare(tipo).ToHashSet(StringComparer.Ordinal);
            Assert.All(descrizione.Campi, c => Assert.True(vere.Contains(c.Proprieta), $"{tipo.Name}.{c.Proprieta} non c'è nel modello"));
            Assert.Equal(descrizione.Campi.Count, descrizione.Campi.Select(c => c.Proprieta).Distinct(StringComparer.Ordinal).Count());
            Assert.All(descrizione.Campi, c => Assert.False(string.IsNullOrWhiteSpace(c.Nome) || string.IsNullOrWhiteSpace(c.Significato)));
        }
    }

    [Fact]
    public void UnCampoATipoFissoHaISuoiValoriEUnElencoHaLaSuaFonte()
    {
        foreach (var (tipo, descrizione) in DescrizioniDeiCampi.Tutte())
        {
            foreach (var campo in descrizione.Campi)
            {
                // Un tipo fisso ha sempre i suoi valori; un colore può averli (slice 4: i nomi dei riempimenti dei .pol,
                // col significato, sono anche il tipo del nuovo record), gli altri editor no.
                if (campo.Editor == Editor.TipoFisso)
                    Assert.NotEmpty(campo.Valori);
                else if (campo.Editor != Editor.Colore)
                    Assert.Empty(campo.Valori);
                Assert.Equal(campo.Editor == Editor.Elenco, campo.Fonte is not null);
                Assert.Equal(campo.Valori.Count, campo.Valori.Select(v => v.Valore).Distinct(StringComparer.Ordinal).Count());
            }
        }
    }

    [Fact]
    public void UnTipoFissoDiUnEnumHaTuttiISuoiValoriEPerNome()
    {
        // La scheda mostra un enum col suo nome (Airport), e la modifica lo rilegge per nome: i valori devono essere quelli.
        foreach (var (tipo, descrizione) in DescrizioniDeiCampi.Tutte())
        {
            foreach (var campo in descrizione.Campi.Where(c => c.Editor == Editor.TipoFisso))
            {
                var suo = tipo.GetProperty(campo.Proprieta)!.PropertyType;
                var vero = Nullable.GetUnderlyingType(suo) ?? suo;
                if (!vero.IsEnum)
                    continue;
                Assert.Equal(
                    Enum.GetNames(vero).Order(StringComparer.Ordinal),
                    campo.Valori.Select(v => v.Valore).Where(v => v.Length > 0).Order(StringComparer.Ordinal));
            }
        }
    }

    [Fact]
    public void LaSchedaDiUnFixHaICampiNellOrdineDellaRigaColNomeItaliano()
    {
        var sessione = SessioneAperta.Apri(CartellaDelSector.Riconosci(_albero.Radice, out _)!);
        var file = sessione.File["SectorFiles/Include/IT/NAVAIDS/APT.fix"];
        int indice = Ispettore.Etichette(file, null).ToList().IndexOf("BC404");

        var scheda = Ispettore.Scheda(file, indice, null)!;

        Assert.Equal("Fix", scheda.NomeDelTipo);
        Assert.Equal(["Name", "Position", "DisplayType", "ExtraField", "NomeDellAttesa"], scheda.Campi.Select(c => c.Nome));
        Assert.Equal(["Nome", "Posizione", "Tipo", "Confine", "Attesa"], scheda.Campi.Select(c => c.NomeDaMostrare));
        // Da dove viene il record lo dicono la testa e le righe: non è un campo.
        Assert.DoesNotContain(scheda.Campi, c => c.Nome == "Source");
    }

    [Fact]
    public void UnaProprietaSenzaDescrizioneSiVedeComeCampoSconosciuto()
    {
        var soloIlNome = new DescrizioneDelTipo("Fix", [new DescrizioneDelCampo("Name", "Nome", "Il nome.", Editor.Testo)]);

        var campi = Ispettore.Campi(new Fix { Name = "ABBOZ" }, soloIlNome);

        Assert.Equal("Name", campi[0].Nome);
        Assert.False(campi[0].Sconosciuto);
        Assert.Equal(["Position", "DisplayType", "ExtraField", "NomeDellAttesa"], campi.Skip(1).Select(c => c.Nome));
        Assert.All(campi.Skip(1), c => Assert.True(c.Sconosciuto));
        Assert.Equal("Position", campi[1].NomeDaMostrare);
    }

    [Fact]
    public void LeMvaDiAccEDiScaloHannoDueDescrizioni()
    {
        var zona = new MvaSector();

        var diAcc = DescrizioniDeiCampi.Di(zona, "SectorFiles/Include/IT/ENRMVA/lirr.mva")!;
        var diScalo = DescrizioniDeiCampi.Di(zona, "SectorFiles/Include/IT/liba.mva")!;

        var quotaDiAcc = diAcc.Campi.Single(c => c.Proprieta == "AltLabel");
        Assert.Equal((Editor.Quota, true), (quotaDiAcc.Editor, quotaDiAcc.InCentinaia));
        // Negli scali il motore legge la quota dal 2° campo e la riscrive nel 5°, dove sul fork c'è quasi sempre la quota
        // vera: finché la slice 15 non le legge come le ACC, quota e carattere non si scrivono dalla scheda.
        Assert.Equal(Editor.SolaLettura, diScalo.Campi.Single(c => c.Proprieta == "AltLabel").Editor);
        Assert.Equal(Editor.SolaLettura, diScalo.Campi.Single(c => c.Proprieta == "LabelSize").Editor);
    }

    [Fact]
    public void OgniRecordDiOgniFileDelCampioneSiApreSenzaCampiSconosciuti()
    {
        // L'uscita della slice 3 in piccolo: i campioni sono file veri del master, uno per formato. La stessa prova
        // sull'albero intero del fork la fa lo strumento della traccia (§6).
        var sessione = SessioneAperta.Apri(CartellaDelSector.Riconosci(_albero.Radice, out _)!);
        var tipiVisti = new HashSet<string>(StringComparer.Ordinal);
        var sconosciuti = new List<string>();
        foreach (var (relativo, file) in sessione.File)
        {
            if (file is not IFileConRecord conRecord)
                continue;
            for (int i = 0; i < conRecord.RecordDelModello.Count; i++)
            {
                var scheda = Ispettore.Scheda(file, i, null)!;
                tipiVisti.Add(scheda.Tipo);
                sconosciuti.AddRange(scheda.Campi.Where(c => c.Sconosciuto).Select(c => $"{relativo}#{i} {scheda.Tipo}.{c.Nome}"));
            }
        }

        Assert.True(sconosciuti.Count == 0, string.Join("\n", sconosciuti.Distinct().Take(20)));
        Assert.True(tipiVisti.Count >= 20, $"i campioni coprono solo {tipiVisti.Count} tipi: {string.Join(", ", tipiVisti)}");
    }
}
