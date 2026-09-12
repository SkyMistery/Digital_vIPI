using System.Reflection;
using Vipi.Application.Abstractions;
using Vipi.Infrastructure.Persistence;
using Xunit;

namespace Vipi.Infrastructure.Tests;

/// <summary>
/// Ogni categoria di <see cref="ImportState"/> deve stare nella sua colonna — <b>chiavi composte comprese</b>.
///
/// <para>🔴 <b>Il difetto che questo test esiste per impedire, e che è arrivato fino a un pacchetto
/// costruito.</b> Il 12 settembre 2026 il gate delle riconciliazioni d'avvio scriveva la chiave
/// <c>RiconciliazioniDocumentali:1.25.1+33aa578</c>: <b>41 caratteri</b> in una colonna da 32, che è la
/// <b>chiave primaria</b> della tabella. In locale — SQLite — funzionava benissimo: là la lunghezza di una
/// <c>varchar</c> non esiste. Provata su MariaDB avrebbe dato uno di due esiti, tutti e due brutti:</para>
///
/// <list type="bullet">
///   <item><b>strict mode</b>: la scrittura fallisce, il gate non si timbra mai e muore in silenzio (c'è un
///   <c>try/catch</c>, quindi nemmeno un guasto: una riga di warning che nessuno legge);</item>
///   <item><b>fuori da strict</b>: MariaDB <b>tronca</b> a 32 — <c>RiconciliazioniDocumentali:1.25</c> — e
///   ogni 1.25.x diventa la <b>stessa riga</b>. La versione dopo troverebbe il timbro di quella prima e
///   salterebbe le proprie riconciliazioni: esattamente il difetto che la chiave col timbro esiste per
///   impedire, prodotto dalla chiave stessa.</item>
/// </list>
///
/// <para>⚠️ E quale dei due modi sia attivo in produzione <b>non lo sappiamo</b>: sta scritto in
/// <c>LEGGIMI-DEPLOY.md</c> («ci basta saperlo»). Un difetto che dipende da un'impostazione che non
/// controlliamo non si valuta, si toglie.</para>
///
/// <para>La lunghezza non è scritta qui: si legge da <see cref="MySqlStringLengths.Map"/>, che è la sorgente
/// che genera davvero la colonna. Se domani quella cambia, questo test la segue.</para>
/// </summary>
public class CategorieDiImportTests
{
    private static int LunghezzaDellaColonna =>
        MySqlStringLengths.Map[("ImportState", "Category")];

    /// <summary>
    /// La costante che il codice usa per decidere dice <b>lo stesso numero</b> della colonna vera. Due
    /// numeri che devono coincidere e vivono in due file sono due numeri che prima o poi divergono: qui
    /// almeno divergono davanti a un test.
    /// </summary>
    [Fact]
    public void Il_tetto_dichiarato_e_quello_della_colonna()
        => Assert.Equal(ImportCategories.MaxLunghezza, LunghezzaDellaColonna);

    /// <summary>Le categorie fisse: quelle che una riga di <c>ImportState</c> porta così come sono.</summary>
    [Fact]
    public void Ogni_categoria_dichiarata_sta_nella_colonna()
    {
        var lunghe = Categorie()
            .Where(c => c.Value.Length > LunghezzaDellaColonna)
            .Select(c => $"{c.Key} = «{c.Value}» ({c.Value.Length} caratteri)")
            .ToList();

        Assert.True(lunghe.Count == 0,
            $"Categorie più lunghe dei {LunghezzaDellaColonna} caratteri della colonna (che è la CHIAVE " +
            $"PRIMARIA di ImportState):\n  {string.Join("\n  ", lunghe)}\n" +
            "Su SQLite passerebbero lo stesso: è il motivo per cui questo test esiste.");
    }

    /// <summary>
    /// <b>La chiave COMPOSTA del gate delle riconciliazioni</b>, che è quella che è sfuggita: non compare in
    /// nessuna costante, la costruisce <see cref="ImportCategories.RiconciliazioniPer"/> a partire dal timbro
    /// di build. Il timbro è il commit corto, che <c>Vipi.Host.csproj</c> prende con
    /// <c>git rev-parse --short=7</c>: sette caratteri. Se ne prova anche uno <b>più lungo</b>, perché il
    /// numero di cifre di un hash corto può crescere in un repository che cresce.
    /// </summary>
    [Theory]
    [InlineData("33aa578")]            // quel che git dà oggi: --short=7
    [InlineData("33aa5781234")]        // e se un domani servissero più cifre
    [InlineData("0000000000000000")]   // il caso assurdo, per vedere dov'è il margine
    public void La_chiave_composta_del_gate_sta_nella_colonna(string timbro)
    {
        var chiave = ImportCategories.RiconciliazioniPer(timbro);

        Assert.True(chiave.Length <= LunghezzaDellaColonna,
            $"La chiave del gate — «{chiave}» — è di {chiave.Length} caratteri e la colonna ne tiene " +
            $"{LunghezzaDellaColonna}. Su MariaDB o fallisce o si TRONCA, e troncata due build diverse " +
            "diventano la stessa riga: il gate salterebbe riconciliazioni mai eseguite.");
    }

    private static IReadOnlyDictionary<string, string> Categorie() =>
        typeof(ImportCategories)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .ToDictionary(f => f.Name, f => (string)f.GetRawConstantValue()!);
}
