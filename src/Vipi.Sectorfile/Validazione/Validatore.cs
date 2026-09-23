using System.Collections;
using System.Text.RegularExpressions;
using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Models;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.Validazione;

/// <summary>
/// Il validatore del sector (carta F2 §3, slice 8): dice che cosa non va, con il file e la riga, e non corregge niente.
/// </summary>
/// <remarks>
/// Le regole di un file guardano tre cose: i CAMPI di ogni riga che il motore legge (coordinate e coppie, anche nelle
/// righe che il lettore ha capito: una frazione ambigua si legge lo stesso), le righe che il lettore NON ha capito (e
/// dice perché, se una regola più precisa non l'ha già detto), e i RECORD (punti per nome, poligoni, tag
/// <c>//@</c>). I file che il motore non interpreta (<c>.txt</c>, <c>.cpr</c>…) non si guardano.
/// </remarks>
public static partial class Validatore
{
    // I campi obbligatori in testa alla riga, dove un campo vuoto rende la riga illeggibile (APT.fix, `;;`). Non la
    // frequenza dei .vor: vuota è un TACAN (itvor.vor:81 `GRO;;…;35Y`, slice 9).
    private static readonly Dictionary<string, int> CampiObbligatori = new(StringComparer.OrdinalIgnoreCase)
    {
        ["vor"] = 4, ["ndb"] = 4, ["fix"] = 3, ["vfi"] = 4, ["gts"] = 4, ["hold"] = 4,
    };

    /// <summary>
    /// Le regole di un file. <paramref name="relativo"/> è il nome col quale il file compare nei problemi
    /// (di solito il percorso sotto la cartella del sector).
    /// </summary>
    public static IReadOnlyList<ProblemaDelSector> ValidaIlFile(string percorso, string relativo)
        => LeggiIlFile(percorso, relativo)?.Problemi ?? Array.Empty<ProblemaDelSector>();

    /// <summary>Il file letto per il validatore: i suoi problemi, e i nomi che usa e che dichiara (per le regole dell'albero).</summary>
    internal sealed record EsitoDelFile(
        IReadOnlyList<ProblemaDelSector> Problemi,
        IReadOnlyList<NomeUsato> Usati,
        IReadOnlyList<NomeDichiarato> Dichiarati,
        IReadOnlyList<object> Record);

    /// <summary>Un punto per nome, alla riga dove compare.</summary>
    internal readonly record struct NomeUsato(string Nome, int Riga, string Testo);

    /// <summary>Un nome del catalogo (fix, VOR, NDB, scalo, VRP), col punto e la riga.</summary>
    internal readonly record struct NomeDichiarato(string Nome, string Catalogo, Coordinate Posizione, int Riga, string Testo);

    /// <summary>Null se il motore non interpreta il file.</summary>
    internal static EsitoDelFile? LeggiIlFile(string percorso, string relativo)
    {
        ArgumentException.ThrowIfNullOrEmpty(percorso);
        ArgumentException.ThrowIfNullOrEmpty(relativo);

        var avvisi = new Avvisi();
        if (!Formati.Usa(percorso, avvisi, new Lettura(percorso), out var letto))
        {
            return null;
        }

        var diRecord = letto.Problemi;

        string estensione = Path.GetExtension(percorso).TrimStart('.').ToLowerInvariant();
        var problemi = new List<ProblemaDelSector>();
        var righe = SectorFileReader.Read(percorso).Lines;

        // 1. I campi di ogni riga.
        var righeSpiegate = new HashSet<int>();
        for (int i = 0; i < righe.Count; i++)
        {
            string t = righe[i].TrimStart();
            if (t.Length == 0 || t.StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }

            int prima = problemi.Count;
            foreach (var (regola, dettaglio) in RegoleDeiCampi(righe[i], estensione))
            {
                problemi.Add(new(regola, relativo, i + 1, righe[i], dettaglio));
            }

            if (problemi.Count > prima)
            {
                righeSpiegate.Add(i + 1);
            }
        }

        // 2. Le righe che il lettore non ha capito.
        foreach (var avviso in avvisi.Snapshot())
        {
            int riga = avviso.LineNumber ?? 0;
            string testo = riga >= 1 && riga <= righe.Count ? righe[riga - 1] : avviso.RawSnippet ?? string.Empty;
            if (avviso.Message.StartsWith("Polygon with", StringComparison.Ordinal))
            {
                problemi.Add(new(Regola.PoligonoConPochiVertici, relativo, riga, testo, avviso.Message));
            }
            else if (!righeSpiegate.Contains(riga))
            {
                problemi.Add(CampoVuoto(testo, estensione) is { } campo
                    ? new(Regola.CampoVuoto, relativo, riga, testo, $"campo {campo} vuoto")
                    : new(Regola.RigaIllegibile, relativo, riga, testo, avviso.Message));
            }
        }

        // 3. I record.
        problemi.AddRange(diRecord.Select(p => p with { File = relativo }));

        return letto with { Problemi = problemi.OrderBy(p => p.Riga).ThenBy(p => p.Regola).ToList() };
    }

    /// <summary>Le regole sui campi di una riga di dati: coordinate una per una, e le coppie.</summary>
    internal static IEnumerable<(Regola Regola, string Dettaglio)> RegoleDeiCampi(string riga, string estensione)
    {
        string[] campi = riga.Split(';');
        for (int i = 0; i < campi.Length; i++)
        {
            string c = campi[i].Trim();
            if (PareDms(c))
            {
                if (c.Any(char.IsWhiteSpace))
                {
                    string[] pezzi = c.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                    yield return pezzi.Length == 2 && pezzi.All(PareDms)
                        ? (Regola.SeparatoreSbagliato, $"«{c}»: due coordinate separate da uno spazio invece che da ';'")
                        : (Regola.CoordinataIllegibile, $"«{c}»");
                    continue;
                }

                string? errore = null;
                try
                {
                    CoordinateConverter.Parse(c);
                }
                catch (CoordinateParseException ex)
                {
                    errore = ex.Message;
                }

                if (errore is not null)
                {
                    yield return errore.Contains("out of range", StringComparison.Ordinal)
                        ? (Regola.CoordinataFuoriCampo, $"«{c}»: {Fuori(c)}")
                        : (Regola.CoordinataIllegibile, $"«{c}»");
                    continue;
                }

                if (char.IsLower(c[0]))
                {
                    yield return (Regola.EmisferoMinuscolo, $"«{c}»");
                }

                string[] parti = c[1..].Split('.');
                if (parti.Length == 4 && parti[3].Length != 3)
                {
                    yield return (Regola.FrazioneAmbigua, $"«{c}»: {parti[3].Length} cifre dopo i secondi");
                }
            }

            if (i + 1 < campi.Length)
            {
                string d = campi[i + 1].Trim();
                if ((PareLatitudineDms(c) && Decimale().IsMatch(d)) || (Decimale().IsMatch(c) && PareLongitudineDms(d)))
                {
                    yield return (Regola.DmsEDecimaleMescolati, $"«{c};{d}»");
                }
                else if (estensione != "txi" && Decimale().IsMatch(c) && Decimale().IsMatch(d))
                {
                    yield return (Regola.CoppiaDecimale, $"«{c};{d}»");
                    i++;
                }
            }
        }
    }

    // Quale parte è fuori: si legge dal token, perché il messaggio del convertitore non lo dice.
    private static string Fuori(string c)
    {
        string[] parti = c[1..].Split('.');
        if (parti.Length >= 3)
        {
            if (int.TryParse(parti[1], out int minuti) && minuti > 59)
            {
                return $"minuti {minuti}";
            }

            if (int.TryParse(parti[2], out int secondi) && secondi > 59)
            {
                return $"secondi {secondi}";
            }
        }

        return "gradi oltre il limite";
    }

    private static string? CampoVuoto(string riga, string estensione)
    {
        if (!CampiObbligatori.TryGetValue(estensione, out int quanti))
        {
            return null;
        }

        string[] campi = riga.Split(';');
        for (int i = 0; i < Math.Min(quanti, campi.Length); i++)
        {
            // La frequenza di un .vor vuota è un TACAN (GRO;;…;35Y), non un campo mancante (slice 9).
            if (campi[i].Trim().Length == 0 && !(i == 1 && estensione.Equals("vor", StringComparison.OrdinalIgnoreCase)))
            {
                return (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        return null;
    }

    // Emisfero e cifra, e poi un punto (DMS puntato, anche sbagliato: `E008-11.31.443`) o almeno sei cifre (compatto).
    // Le aerovie si chiamano `N503`, `W36-Z636`, `S1`: nomi, non coordinate (132 falsi errori senza questa regola).
    private static bool PareDms(string c)
        => c.Length > 1 && "NSEWnsew".Contains(c[0]) && char.IsAsciiDigit(c[1])
        && (c.Contains('.', StringComparison.Ordinal) || (c.Length >= 7 && c[1..].All(char.IsAsciiDigit)));

    private static bool PareLatitudineDms(string c) => PareDms(c) && char.ToUpperInvariant(c[0]) is 'N' or 'S';

    private static bool PareLongitudineDms(string c) => PareDms(c) && char.ToUpperInvariant(c[0]) is 'E' or 'W';

    // Un decimale da coordinata: almeno quattro cifre dopo il punto (una frequenza ne ha tre: 118.700).
    [GeneratedRegex(@"^[+-]?[0-9]{1,3}\.[0-9]{4,}$")]
    private static partial Regex Decimale();

    /// <summary>Le regole sui record: vogliono il file letto, col tipo dei suoi record.</summary>
    private sealed class Lettura(string percorso) : IUsoDelFormato<EsitoDelFile>
    {
        public EsitoDelFile Usa<T>(IFileParser<T> lettore, IFileSaver<T> scrittore)
            where T : class
        {
            var letto = lettore.Parse(percorso, new ColorPalette());
            var problemi = new List<ProblemaDelSector>();
            var usati = new List<NomeUsato>();
            var dichiarati = new List<NomeDichiarato>();

            int numero = 0;
            var intestazioni = new Dictionary<object, (int Riga, string Testo)>(ReferenceEqualityComparer.Instance);
            foreach (var pezzo in letto.Chunks)
            {
                if (pezzo is RawChunk<T> grezze)
                {
                    numero += grezze.Lines.Length;
                    continue;
                }

                var rec = (RecordChunk<T>)pezzo;
                numero += rec.LeadingComments.Length + (rec.HasMarkers ? 1 : 0);
                int primaRiga = numero + 1;
                intestazioni[rec.Record] = (primaRiga, rec.RawLines.Length > 0 ? rec.RawLines[0] : string.Empty);

                // Due nomi diversi: la riga esatta si trova fra quelle del record, cercando la coppia di campi.
                var coppie = NomiDiversi(rec.Record).ToHashSet();
                for (int i = 0; coppie.Count > 0 && i < rec.RawLines.Length; i++)
                {
                    string[] campi = rec.RawLines[i].Split(';').Select(c => c.Trim()).ToArray();
                    for (int k = 0; k + 1 < campi.Length; k++)
                    {
                        if (coppie.Contains((campi[k], campi[k + 1])))
                        {
                            problemi.Add(new(Regola.DueNomiDiversi, string.Empty, numero + i + 1, rec.RawLines[i],
                                $"«{campi[k]}» e «{campi[k + 1]}»: Aurora prende la latitudine dal primo e la longitudine dal secondo"));
                            break;
                        }
                    }
                }

                // I nomi usati, alle righe dove compaiono (non nelle righe disattivate: Aurora non le legge).
                var nomi = NomiUsati(rec.Record).ToHashSet(StringComparer.Ordinal);
                for (int i = 0; nomi.Count > 0 && i < rec.RawLines.Length; i++)
                {
                    if (rec.RawLines[i].TrimStart().StartsWith("//", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    foreach (string campo in rec.RawLines[i].Split(';').Select(c => c.Trim()).Distinct(StringComparer.Ordinal))
                    {
                        if (nomi.Contains(campo))
                        {
                            usati.Add(new(campo, numero + i + 1, rec.RawLines[i]));
                        }
                    }
                }

                if (Dichiarato(rec.Record) is { } dichiarato)
                {
                    foreach (string nome in dichiarato.Nomi.Where(n => n.Length > 0).Distinct(StringComparer.Ordinal))
                    {
                        dichiarati.Add(new(nome, dichiarato.Catalogo, dichiarato.Posizione, primaRiga, rec.RawLines[0]));
                    }
                }

                if (rec.Record is TflSector { Vertices.Count: < 3 } settore)
                {
                    problemi.Add(new(Regola.PoligonoConPochiVertici, string.Empty, primaRiga, rec.RawLines[0],
                        $"settore {settore.SectorCode}: {settore.Vertices.Count} vertici"));
                }

                numero += rec.RawLines.Length + (rec.HasMarkers ? 1 : 0);
            }

            // I tag //@ (slice 7), solo dove ci sono: .sid e .str.
            IReadOnlyList<ProblemaDeiMetadati> tag = letto switch
            {
                ParseResult<SidProcedure> sid => Metadati.Leggi(sid, Metadati.NomeSid).Problemi,
                ParseResult<StrRecord> str => Metadati.Leggi(str, Metadati.NomeStr).Problemi,
                _ => Array.Empty<ProblemaDeiMetadati>(),
            };
            problemi.AddRange(tag.Select(p => new ProblemaDelSector(
                p.EUnErrore ? Regola.TagNonValido : Regola.TagFuoriCatalogo, string.Empty, p.Riga, p.Testo, p.Tipo.ToString())));

            // Le mappe composte (F3-bis slice 4): l'elenco deve nominare procedure del file, e la mappa deve essere
            // come la rigenererebbe il Lab. Il problema sta sull'intestazione della mappa: il clic porta al record.
            if (letto is ParseResult<StrRecord> deiStr)
            {
                foreach (var composta in MappeComposte.Di(deiStr))
                {
                    var (riga, testo) = intestazioni.GetValueOrDefault(composta.Mappa, (composta.Riga, string.Empty));
                    string nome = Metadati.NomeStr(composta.Mappa);
                    if (composta.Elenco is null)
                    {
                        problemi.Add(new(Regola.CompostaConProceduraAssente, string.Empty, riga, testo,
                            $"«{nome}»: l'elenco «{composta.Valore}» non si legge"));
                        continue;
                    }

                    var rigenerata = composta.Componi(deiStr.Records);
                    foreach (var mancante in rigenerata.Mancanti)
                    {
                        problemi.Add(new(Regola.CompostaConProceduraAssente, string.Empty, riga, testo,
                            $"«{nome}»: «{(mancante.Pista is null ? "" : mancante.Pista + ":")}{mancante.Nome}» non c'è in questo file"));
                    }

                    if (!MappeComposte.Uguale(composta.Mappa, rigenerata.Punti))
                    {
                        problemi.Add(new(Regola.CompostaNonAllineata, string.Empty, riga, testo,
                            $"«{nome}»: {MappeComposte.PuntiDi(composta.Mappa).Count} punti, rigenerata ne avrebbe {rigenerata.Punti.Count}"));
                    }
                }
            }

            return new EsitoDelFile(problemi, usati, dichiarati, letto.Records.Cast<object>().ToList());
        }
    }

    // Il nome (o i nomi) col quale un record entra in un catalogo: sta in Models/Catalog/Cataloghi.cs, perché lo
    // chiede anche il catalogo dei punti dell'app (carta F3, slice 3).
    private static NomeDiCatalogo? Dichiarato(object record) => Cataloghi.Dichiarato(record);

    /// <summary>
    /// I nomi di punto che un record usa e che vanno risolti nei cataloghi: i <see cref="Punto"/> per nome (tutti e due
    /// i campi), i punti dei <c>.str</c>, i vertici per nome delle righe <c>T;</c>, le etichette delle aerovie.
    /// </summary>
    internal static IEnumerable<string> NomiUsati(object record)
    {
        IEnumerable<string> nomi = record switch
        {
            ProcedureStrRecord procedura => procedura.Waypoints.SelectMany(p => new[] { p.FixName, p.DisplayLabel }),
            HoldingStrRecord attesa => attesa.Points.OfType<HoldingFixPoint>().SelectMany(p => new[] { p.FixName, p.DisplayLabel }),
            StaticBoundaryGroup gruppo => gruppo.Polygons.SelectMany(p => p.Vertices)
                .Where(v => v.Position is null).SelectMany(v => new[] { v.FixA ?? string.Empty, v.FixB ?? string.Empty }),
            Airway aerovia => aerovia.FixLabels.Where(l => Punto.TryLeggi(l, l, out var p) && p.PerNome),
            _ => Punti(record, profondita: 0).Where(p => p.PerNome).SelectMany(p => new[] { p.Nome!, p.NomeLongitudine! }),
        };

        // Una coordinata sbagliata tenuta come «fix» (N047.25.60.000) non è un nome: la dicono le regole dei campi.
        return nomi.Select(n => n.Trim()).Where(n => n.Length > 0 && Punto.TryLeggi(n, n, out var p) && p.PerNome);
    }

    /// <summary>
    /// Le coppie di nomi diversi di un record: i <see cref="Punto"/> per nome, dovunque stiano nel record, e i punti per
    /// nome dei <c>.str</c> (che il modello di A tiene come fix e etichetta: sono i due campi del punto).
    /// </summary>
    internal static IEnumerable<(string Nome, string NomeLongitudine)> NomiDiversi(object record)
    {
        switch (record)
        {
            case ProcedureStrRecord procedura:
                foreach (var p in procedura.Waypoints.Where(p => p.FixName != p.DisplayLabel))
                {
                    yield return (p.FixName, p.DisplayLabel);
                }

                yield break;

            case HoldingStrRecord attesa:
                foreach (var p in attesa.Points.OfType<HoldingFixPoint>().Where(p => p.FixName != p.DisplayLabel))
                {
                    yield return (p.FixName, p.DisplayLabel);
                }

                yield break;
        }

        foreach (var punto in Punti(record, profondita: 0))
        {
            if (punto.PerNome && punto.Nome != punto.NomeLongitudine)
            {
                yield return (punto.Nome!, punto.NomeLongitudine!);
            }
        }
    }

    // Tutti i Punto di un record, anche dentro le sue liste (i vertici di un settore, il tracciato di una SID…).
    private static IEnumerable<Punto> Punti(object oggetto, int profondita)
    {
        if (profondita > 4)
        {
            yield break;
        }

        foreach (var proprieta in oggetto.GetType().GetProperties().Where(p => p.GetIndexParameters().Length == 0))
        {
            object? valore = proprieta.GetValue(oggetto);
            switch (valore)
            {
                case Punto punto:
                    yield return punto;
                    break;

                case IEnumerable elenco and not string:
                    foreach (object? elemento in elenco)
                    {
                        if (elemento is Punto p)
                        {
                            yield return p;
                        }
                        else if (elemento is not null && elemento.GetType().Namespace == typeof(TflSector).Namespace)
                        {
                            foreach (var dentro in Punti(elemento, profondita + 1))
                            {
                                yield return dentro;
                            }
                        }
                    }

                    break;
            }
        }
    }

    /// <summary>Raccoglie gli avvisi del lettore: sono le righe che non ha capito.</summary>
    private sealed class Avvisi : IWarningCollector
    {
        private readonly List<LoadWarning> _avvisi = new();

        public event EventHandler<LoadWarning>? WarningAdded;

        public int Count => _avvisi.Count;

        public void Add(LoadWarning warning)
        {
            _avvisi.Add(warning);
            WarningAdded?.Invoke(this, warning);
        }

        public void Add(WarningSeverity severity, WarningCategory category, string source, string message,
                        int? lineNumber = null, string? rawSnippet = null)
            => Add(new LoadWarning(severity, category, source, message, lineNumber, rawSnippet));

        public IReadOnlyList<LoadWarning> Snapshot() => _avvisi.ToArray();

        public void Clear() => _avvisi.Clear();
    }
}
