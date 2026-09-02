using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Vipi.Application.Import;
using Xunit;

namespace Vipi.Application.Tests;

/// <summary>
/// Il secondo e il terzo stadio dell'import: quale colonna va dove, e che cosa il sistema ha capito di ogni
/// cella <b>prima</b> che qualcuno prema «importa».
/// </summary>
public class ImportPropostaTests
{
    private static readonly SpecImport Alternati = new(
        "mildiversion",
        new[]
        {
            new ColonnaSpec("icao", "Aeroporto", TipoCella.Aeroporto, Obbligatoria: true,
                Sinonimi: new[] { "AIRPORT", "ICAO" }),
            new ColonnaSpec("navaids", "Radioassistenze", TipoCella.Radioassistenza,
                Sinonimi: new[] { "NAVAIDS" }),
            new ColonnaSpec("bearing", "Rilevamento", TipoCella.Intero, Sinonimi: new[] { "BEARING" }),
            new ColonnaSpec("distance", "Distanza", TipoCella.Decimale, Sinonimi: new[] { "DISTANCE" }),
        });

    // ---- mappatura -------------------------------------------------------------------------------------

    [Fact]
    public void L_intestazione_si_riconosce_dai_nomi_anche_in_inglese()
    {
        var g = Griglia.Leggi("AIRPORT\tNAVAIDS\tBEARING\tDISTANCE\nLIBA\tMNL\t308\t72.2");

        var m = MappaturaColonne.Proponi(Alternati, g);

        Assert.True(m.Intestazione);
        Assert.Equal(new[] { 0, 1, 2, 3 }, m.Colonne);
    }

    /// <summary>⚠️ Le colonne fuori ordine si seguono per NOME: e' il caso di chi esporta da Excel con le
    /// colonne come gli servivano a lui.</summary>
    [Fact]
    public void Le_colonne_fuori_ordine_si_seguono_per_nome()
    {
        var g = Griglia.Leggi("DISTANCE\tAIRPORT\tBEARING\tNAVAIDS\n72.2\tLIBA\t308\tMNL");

        var m = MappaturaColonne.Proponi(Alternati, g);

        Assert.Equal(new[] { 1, 3, 2, 0 }, m.Colonne);
    }

    /// <summary>
    /// ⚠️ Chi incolla mezza tabella parte dalla prima riga di DATI: togliergliela perche' «la prima riga e'
    /// sempre l'intestazione» vuol dire perdere una riga in silenzio.
    /// </summary>
    [Fact]
    public void Senza_intestazione_le_colonne_si_prendono_in_ordine_e_nessuna_riga_si_perde()
    {
        var g = Griglia.Leggi("LIBA\tMNL\t308\t72.2\nLIBR\tBRD\t95\t46.2");

        var m = MappaturaColonne.Proponi(Alternati, g);

        Assert.False(m.Intestazione);
        Assert.Equal(new[] { 0, 1, 2, 3 }, m.Colonne);
    }

    [Fact]
    public void Una_colonna_che_non_c_e_resta_senza_posto()
    {
        var g = Griglia.Leggi("AIRPORT\tBEARING\nLIBA\t308");

        var m = MappaturaColonne.Proponi(Alternati, g);

        Assert.True(m.Intestazione);
        Assert.Equal(new[] { 0, -1, 1, -1 }, m.Colonne);
        Assert.Equal(2, m.Trovate);
    }

    // ---- proposta --------------------------------------------------------------------------------------

    [Fact]
    public async Task Il_valore_di_una_cella_risolta_viene_dall_archivio_non_dal_testo()
    {
        var g = Griglia.Leggi("AIRPORT\tNAVAIDS\tBEARING\tDISTANCE\nLIBA Amendola\tMNL\t308\t72.2");

        var p = await CostruttoreProposta.CostruisciAsync(g, Alternati, new RisolutoreFinto());

        var riga = Assert.Single(p.Righe);
        Assert.Equal(EsitoCella.Risolta, riga.Celle[0].Esito);
        Assert.Equal("LIBA Amendola (archivio)", riga.Celle[0].Valore);
        Assert.Equal("308", riga.Celle[2].Valore);
        Assert.Equal("72.2", riga.Celle[3].Valore);
        Assert.True(riga.Ok);
        Assert.Single(p.Buone);
    }

    /// <summary>⚠️ Il numero di riga e' quello del TESTO INCOLLATO, intestazione compresa: serve a dire
    /// dove, non solo cosa.</summary>
    [Fact]
    public async Task Il_numero_di_riga_conta_anche_l_intestazione()
    {
        var g = Griglia.Leggi("AIRPORT\tNAVAIDS\tBEARING\tDISTANCE\nLIBA\tMNL\t308\t72.2\nLIBR\tBRD\t95\t46.2");

        var p = await CostruttoreProposta.CostruisciAsync(g, Alternati, new RisolutoreFinto());

        Assert.Equal(new[] { 2, 3 }, p.Righe.Select(r => r.Numero));
    }

    /// <summary>⚠️ Un codice sconosciuto non si crea e non si tiene com'e': la riga resta fuori. Una cella
    /// che sembra a posto e cita un impianto inesistente e' peggio di una cella rossa.</summary>
    [Fact]
    public async Task Un_codice_sconosciuto_tiene_la_riga_fuori()
    {
        var g = Griglia.Leggi("LIBA\tMNL\t308\t72.2\nXXXX\tMNL\t95\t46.2");

        var p = await CostruttoreProposta.CostruisciAsync(g, Alternati, new RisolutoreFinto());

        Assert.Single(p.Buone);
        var scartata = Assert.Single(p.Scartate);
        Assert.Equal(EsitoCella.NonLetta, scartata.Celle[0].Esito);
    }

    /// <summary>⚠️ Un codice che e' di piu' impianti non si rifiuta: si chiede quale, e finche' non si
    /// sceglie la riga non entra.</summary>
    [Fact]
    public async Task Un_codice_ambiguo_aspetta_una_scelta()
    {
        var g = Griglia.Leggi("LIBG\tGRO\t120\t30");

        var p = await CostruttoreProposta.CostruisciAsync(g, Alternati, new RisolutoreFinto());

        var riga = Assert.Single(p.Righe);
        Assert.Equal(EsitoCella.DaScegliere, riga.Celle[1].Esito);
        Assert.Equal(new[] { "GRO VOR", "GRO TACAN 35Y" }, riga.Celle[1].Candidati);
        Assert.False(riga.Ok);
        Assert.Empty(p.Buone);
    }

    [Fact]
    public async Task Un_numero_che_non_e_un_numero_si_dice()
    {
        var g = Griglia.Leggi("LIBA\tMNL\tcirca nord\t72.2");

        var p = await CostruttoreProposta.CostruisciAsync(g, Alternati, new RisolutoreFinto());

        var cella = Assert.Single(p.Righe).Celle[2];
        Assert.Equal(EsitoCella.NonLetta, cella.Esito);
        Assert.Equal("circa nord", cella.Grezzo);
    }

    /// <summary>⚠️ Si chiede al catalogo UNA volta per tipo, non una per cella: quaranta righe con due
    /// colonne di catalogo farebbero ottanta interrogazioni, e le tabelle grosse sono esattamente quelle per
    /// cui l'import esiste.</summary>
    [Fact]
    public async Task Il_catalogo_si_interroga_a_lotti()
    {
        var g = Griglia.Leggi("LIBA\tMNL\t308\t72.2\nLIBR\tBRD\t95\t46.2\nLIBA\tMNL\t100\t10");
        var risolutore = new RisolutoreFinto();

        await CostruttoreProposta.CostruisciAsync(g, Alternati, risolutore);

        Assert.Equal(2, risolutore.Chiamate);                       // aeroporti + radioassistenze
        Assert.Equal(new[] { "LIBA", "LIBR" }, risolutore.Chiesti[TipoCella.Aeroporto]);
    }

    // ---- tabella generica ------------------------------------------------------------------------------

    /// <summary>La tabella generica non ha colonne dichiarate: quelle che ci sono le porta chi incolla.</summary>
    [Fact]
    public async Task La_tabella_generica_prende_le_colonne_che_arrivano()
    {
        var g = Griglia.Leggi("a\tb\tc\nd\te\tf");

        var p = await CostruttoreProposta.CostruisciAsync(g, SpecImport.Generica());

        Assert.Equal(new[] { "Colonna 1", "Colonna 2", "Colonna 3" }, p.Colonne);
        Assert.Equal(2, p.Righe.Count);
        Assert.All(p.Righe, r => Assert.True(r.Ok));
    }

    [Fact]
    public async Task La_tabella_generica_puo_prendere_l_intestazione_dalla_prima_riga()
    {
        var g = Griglia.Leggi("Nome\tNumeri\nAlfa\t1-4");
        var spec = SpecImport.Generica();

        var p = await CostruttoreProposta.CostruisciAsync(
            g, spec, MappaturaColonne.Proponi(spec, g) with { Intestazione = true });

        Assert.Equal(new[] { "Nome", "Numeri" }, p.Colonne);
        Assert.Equal(new[] { "Alfa", "1-4" }, Assert.Single(p.Righe).Celle.Select(c => c.Valore));
    }

    [Fact]
    public async Task Una_griglia_vuota_da_una_proposta_vuota()
    {
        var p = await CostruttoreProposta.CostruisciAsync(Griglia.Vuota, Alternati, new RisolutoreFinto());

        Assert.Empty(p.Righe);
        Assert.Empty(p.Buone);
    }

    // ---- il catalogo finto -----------------------------------------------------------------------------

    /// <summary>Un catalogo che conosce due scali e due radioassistenze, e che tiene il conto di quante
    /// volte gli si e' chiesto qualcosa.</summary>
    private sealed class RisolutoreFinto : IRisolutoreCelle
    {
        public int Chiamate { get; private set; }

        public Dictionary<TipoCella, IReadOnlyList<string>> Chiesti { get; } = new();

        public Task<IReadOnlyDictionary<string, EsitoRisoluzione>> RisolviAsync(
            TipoCella tipo, IReadOnlyCollection<string> valori, CancellationToken ct = default)
        {
            Chiamate++;
            Chiesti[tipo] = valori.Select(Codice).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList();

            var esiti = new Dictionary<string, EsitoRisoluzione>(StringComparer.OrdinalIgnoreCase);
            foreach (var v in valori)
            {
                var codice = Codice(v);
                EsitoRisoluzione? e = (tipo, codice) switch
                {
                    (TipoCella.Aeroporto, "LIBA") => new EsitoRisoluzione("LIBA Amendola (archivio)", EsitoCella.Risolta, "LIBA"),
                    (TipoCella.Aeroporto, "LIBR") => new EsitoRisoluzione("LIBR Brindisi (archivio)", EsitoCella.Risolta, "LIBR"),
                    (TipoCella.Aeroporto, "LIBG") => new EsitoRisoluzione("LIBG Grottaglie (archivio)", EsitoCella.Risolta, "LIBG"),
                    (TipoCella.Radioassistenza, "MNL") => new EsitoRisoluzione("MNL TACAN 99Y", EsitoCella.Risolta, "MNL|TCN|99Y"),
                    (TipoCella.Radioassistenza, "BRD") => new EsitoRisoluzione("BRD TACAN 79X", EsitoCella.Risolta, "BRD|TCN|79X"),
                    (TipoCella.Radioassistenza, "GRO") => new EsitoRisoluzione("", EsitoCella.DaScegliere, null,
                        "due impianti con questo codice", new[] { "GRO VOR", "GRO TACAN 35Y" }),
                    _ => null,
                };
                if (e is not null) esiti[v] = e;
            }
            return Task.FromResult<IReadOnlyDictionary<string, EsitoRisoluzione>>(esiti);
        }

        /// <summary>Il codice dentro la cella: «LIBA Amendola» e' comunque LIBA.</summary>
        private static string Codice(string cella) =>
            cella.Split(' ')[0].ToUpperInvariant();
    }
}
