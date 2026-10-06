using System.Globalization;
using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Shared;
using Vipi.Sectorfile.Validazione;

namespace Vipi.SectorLab.Core.Modifiche;

/// <summary>Un'etichetta da scrivere: la riga nuova e dopo quale riga del file (0: in testa).</summary>
public sealed record EtichettaDaAggiungere(string Nome, string Riga, int DopoLaRiga, string Tratto);

/// <summary>Un'etichetta col nome da cambiare: la riga (da 1), com'è e come diventa.</summary>
public sealed record EtichettaDaRinominare(int Riga, string Prima, string Dopo);

/// <summary>Un'etichetta da togliere: è lontana dalla sua aerovia, e il tratto giusto ne riceve una nuova.</summary>
public sealed record EtichettaDaTogliere(int Riga, string Testo, double LontanaNm);

/// <summary>Che cosa cambia nelle etichette di un file di aerovie per metterle a posto.</summary>
public sealed record PianoDelleEtichette(
    IReadOnlyList<EtichettaDaAggiungere> DaAggiungere,
    IReadOnlyList<EtichettaDaRinominare> DaRinominare,
    IReadOnlyList<EtichettaDaTogliere> DaTogliere,
    double SogliaNm)
{
    public bool Vuoto => DaAggiungere.Count == 0 && DaRinominare.Count == 0 && DaTogliere.Count == 0;

    /// <summary>Il piano come sostituzioni di righe (da 1) del file com'è: una riga può sparire, cambiare, o averne altre dopo.</summary>
    public IReadOnlyDictionary<int, IReadOnlyList<string>> Sostituzioni(IReadOnlyList<string> righe)
    {
        ArgumentNullException.ThrowIfNull(righe);
        var nuove = new Dictionary<int, List<string>>();
        List<string> Di(int riga)
        {
            if (!nuove.TryGetValue(riga, out var sue))
                nuove[riga] = sue = [righe[riga - 1]];
            return sue;
        }

        foreach (var rinominata in DaRinominare)
            Di(rinominata.Riga)[0] = rinominata.Dopo;
        foreach (var tolta in DaTogliere)
            Di(tolta.Riga).RemoveAt(0);
        // In testa al file (nessuna etichetta più piccola): prima della riga 1.
        foreach (var gruppo in DaAggiungere.GroupBy(a => a.DopoLaRiga).OrderBy(g => g.Key))
        {
            var ordinate = gruppo.OrderBy(a => a.Nome, StringComparer.Ordinal).ThenBy(a => a.Riga, StringComparer.Ordinal).Select(a => a.Riga);
            if (gruppo.Key == 0)
                Di(1).InsertRange(0, ordinate);
            else
                Di(gruppo.Key).AddRange(ordinate);
        }

        return nuove.ToDictionary(n => n.Key, n => (IReadOnlyList<string>)n.Value);
    }
}

/// <summary>
/// Le etichette calcolate dai tracciati (lotto «Subito» slice 14d, «file per file» B4): una a metà di ogni tratto, coi
/// nomi delle aerovie che lo condividono uniti dal trattino in ordine alfabetico.
/// </summary>
/// <remarks>
/// <para><b>Che cosa tocca</b> (decisioni del committente del 6 ottobre 2026). Un tratto lungo almeno la soglia e senza
/// un'etichetta ne riceve una a metà; la <b>soglia</b> è quella che il file segue già — nessun tratto sotto i 10 NM ha
/// un'etichetta — ed è un'impostazione dell'app. Un'etichetta a più di mezzo miglio dalla sua aerovia si toglie, e il
/// tratto giusto ne riceve una nuova («rifalle a metà»). Un'etichetta sul suo tratto col nome sbagliato (le vecchie «U»,
/// un'aerovia in più o in meno su un tratto condiviso) prende il nome giusto e <b>resta dov'è</b>: 98 etichette del
/// fork stanno sul tratto ma non a metà, e chi le ha spostate aveva una ragione.</para>
/// <para><b>Dove scrive</b>. Il file è in due parti, i tracciati sopra e le etichette sotto; l'ordine a blocchi (B1)
/// aspetta la prova in Aurora (B9). Un'etichetta nuova va nella parte delle etichette, in ordine di nome: dopo l'ultima
/// etichetta col nome più grande fra quelli che non la superano. Il resto del file non si tocca.</para>
/// <para>Rifare il piano dopo averlo applicato non trova più niente.</para>
/// </remarks>
public static class EtichetteDelleAerovie
{
    /// <summary>La soglia di base: nessun tratto più corto ha un'etichetta sul fork del 6 ottobre 2026.</summary>
    public const double SogliaDiBaseNm = 10;

    private sealed record Tratto(string Da, string A, Coordinate Inizio, Coordinate Fine, SortedSet<string> Aerovie)
    {
        public double LungoNm => Distanze.Nm(Inizio, Fine);

        public Coordinate Meta => new((Inizio.LatitudeDeg + Fine.LatitudeDeg) / 2, (Inizio.LongitudeDeg + Fine.LongitudeDeg) / 2);

        public string Nome => string.Join('-', Aerovie);
    }

    /// <summary>
    /// Il piano per le etichette del file. <paramref name="solo"/>: se dato, solo i tratti e le etichette di
    /// quell'aerovia (chi ne aggiunge una non mette mano alle altre).
    /// </summary>
    public static PianoDelleEtichette Piano(IReadOnlyList<string> righe, Func<string, Coordinate?> punto, double sogliaNm, string? solo = null)
    {
        ArgumentNullException.ThrowIfNull(righe);
        ArgumentNullException.ThrowIfNull(punto);
        var lette = RigheDelleAerovie.Leggi(righe, punto);

        // I tratti distinti: due punti di seguito, in qualunque verso, con le aerovie che ci passano.
        var tratti = new Dictionary<(string, string), Tratto>();
        var delLAerovia = new Dictionary<string, List<Tratto>>(StringComparer.Ordinal);
        foreach (string nome in lette.Nomi)
        {
            delLAerovia[nome] = [];
            foreach (var (da, a) in lette.TrattiDi(nome))
            {
                var chiave = string.CompareOrdinal(da.Nome.ToUpperInvariant(), a.Nome.ToUpperInvariant()) <= 0
                    ? (da.Nome.ToUpperInvariant(), a.Nome.ToUpperInvariant())
                    : (a.Nome.ToUpperInvariant(), da.Nome.ToUpperInvariant());
                if (!tratti.TryGetValue(chiave, out var tratto))
                    tratti[chiave] = tratto = new Tratto(da.Nome, a.Nome, da.Posizione!.Value, a.Posizione!.Value, new SortedSet<string>(StringComparer.Ordinal));
                tratto.Aerovie.Add(nome);
                if (!delLAerovia[nome].Contains(tratto))
                    delLAerovia[nome].Add(tratto);
            }
        }

        var etichettati = new HashSet<Tratto>();
        var daRinominare = new List<EtichettaDaRinominare>();
        var daTogliere = new List<EtichettaDaTogliere>();
        foreach (var etichetta in lette.Etichette)
        {
            var vere = etichetta.Nomi.Where(delLAerovia.ContainsKey).ToList();
            if (etichetta.Posizione is not { } qui || vere.Count == 0)
                continue;
            var suoi = vere.SelectMany(v => delLAerovia[v]).Distinct().ToList();
            if (suoi.Count == 0)
                continue;
            var vicino = suoi.MinBy(t => Distanze.MetriDalSegmento(qui, t.Inizio, t.Fine))!;
            double lontana = Distanze.NmDalSegmento(qui, vicino.Inizio, vicino.Fine);
            bool sua = solo is null || vere.Contains(solo, StringComparer.Ordinal) || vicino.Aerovie.Contains(solo);
            if (lontana > ControlloDelleAerovie.DallAeroviaNm)
            {
                if (sua)
                    daTogliere.Add(new(etichetta.Riga, righe[etichetta.Riga - 1], lontana));
                continue;
            }

            etichettati.Add(vicino);
            if (sua && !vicino.Aerovie.SetEquals(etichetta.Nomi))
            {
                string[] campi = righe[etichetta.Riga - 1].Split(';');
                campi[1] = vicino.Nome;
                daRinominare.Add(new(etichetta.Riga, righe[etichetta.Riga - 1], string.Join(';', campi)));
            }
        }

        // Dove va un'etichetta nuova: dopo l'ultima etichetta col nome più grande fra quelli che non la superano.
        var perNome = lette.Etichette
            .Select(e => (e.Riga, Nome: e.Nomi.FirstOrDefault(delLAerovia.ContainsKey) ?? e.Nome))
            .ToList();
        int Dopo(string nome)
        {
            var nonOltre = perNome.Where(e => string.CompareOrdinal(e.Nome, nome) <= 0).ToList();
            if (nonOltre.Count > 0)
            {
                string massimo = nonOltre.Select(e => e.Nome).Max(StringComparer.Ordinal)!;
                return nonOltre.Last(e => e.Nome == massimo).Riga;
            }

            // Nessuna più piccola: prima della prima etichetta, o in fondo al file se non ce ne sono.
            return perNome.Count > 0 ? perNome.Min(e => e.Riga) - 1 : righe.Count;
        }

        var daAggiungere = tratti.Values
            .Where(t => !etichettati.Contains(t) && t.LungoNm >= sogliaNm && (solo is null || t.Aerovie.Contains(solo)))
            .Select(t => new EtichettaDaAggiungere(t.Nome,
                $"L;{t.Nome};{CoordinateConverter.LatitudeToDottedDms(t.Meta.LatitudeDeg)};{CoordinateConverter.LongitudeToDottedDms(t.Meta.LongitudeDeg)};",
                Dopo(t.Aerovie.Min!), $"{t.Da} → {t.A} ({t.LungoNm.ToString("0.#", CultureInfo.InvariantCulture)} NM)"))
            .OrderBy(a => a.Nome, StringComparer.Ordinal).ThenBy(a => a.Riga, StringComparer.Ordinal)
            .ToList();

        return new(daAggiungere, daRinominare, daTogliere, sogliaNm);
    }
}
