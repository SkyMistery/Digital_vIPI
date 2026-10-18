using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Vipi.SectorLab.Core.Aurora;

/// <summary>
/// Una voce del lotto: un gesto salvato (la stessa voce della storia che fa il messaggio di «Registra»), i file che ha
/// toccato, quando è stato salvato e quando è andato in vetrina. <see cref="Portata"/> null = salvato, non ancora in
/// Aurora. Senza file = tutto il sector (cambiato sector o ramo in vetrina).
/// </summary>
public sealed record VoceDelLotto(string Cosa, IReadOnlyList<string> File, DateTimeOffset Salvata, DateTimeOffset? Portata = null)
{
    /// <summary>Dove guardarla in Aurora (proposta da confermare, vedi <see cref="DoveGuardare"/>).</summary>
    public string Dove => File.Count == 0 ? "tutto il sector" : DoveGuardare.Per(File);
}

/// <summary>
/// Il lotto (carta F5, slice 2): da quando l'AOD ha detto l'ultima volta «visto in Aurora», i gesti salvati del sector,
/// ognuno con dove guardarlo. Sta nei dati del Lab, un file per cartella del sector: sopravvive a un riavvio. Le voci
/// salvate aspettano «Porta in Aurora»; portate, sono «da vedere» finché l'AOD non dice «visto».
/// </summary>
public sealed class Lotto
{
    private readonly List<VoceDelLotto> _voci;

    private Lotto(string sector, List<VoceDelLotto> voci, DateTimeOffset? visto)
    {
        Sector = sector;
        _voci = voci;
        Visto = visto;
    }

    /// <summary>La radice del sector a cui il lotto appartiene.</summary>
    public string Sector { get; }

    /// <summary>Le voci, nell'ordine in cui sono state salvate.</summary>
    public IReadOnlyList<VoceDelLotto> Voci => _voci;

    /// <summary>L'ultimo «visto in Aurora», o null.</summary>
    public DateTimeOffset? Visto { get; private set; }

    /// <summary>Le voci in vetrina, da guardare in Aurora dopo una ricarica.</summary>
    public int DaVedere => _voci.Count(v => v.Portata is not null);

    /// <summary>Le voci salvate che in vetrina non ci sono ancora.</summary>
    public int DaPortare => _voci.Count(v => v.Portata is null);

    /// <summary>Il file del lotto di <paramref name="sector"/> nei dati del Lab: il nome viene dal percorso, che può essere lungo.</summary>
    public static string FileDi(string cartellaDeiDati, string sector)
    {
        string chiave = Path.GetFullPath(sector).TrimEnd('\\', '/').ToUpperInvariant();
        string impronta = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(chiave)))[..16].ToLowerInvariant();
        return Path.Combine(cartellaDeiDati, "lotti", impronta + ".json");
    }

    /// <summary>Il lotto di <paramref name="sector"/>; vuoto se non c'è ancora, o se il file non si legge.</summary>
    public static Lotto Leggi(string cartellaDeiDati, string sector)
    {
        string radice = Path.GetFullPath(sector);
        try
        {
            string file = FileDi(cartellaDeiDati, radice);
            if (File.Exists(file) && JsonSerializer.Deserialize<SulDisco>(File.ReadAllText(file)) is { } letto)
                return new Lotto(radice, [.. letto.Voci ?? []], letto.Visto);
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            // Un lotto illeggibile si ricomincia: è un appunto di lavoro, non un dato del sector.
        }

        return new Lotto(radice, [], null);
    }

    /// <summary>Scrive il lotto nei dati del Lab, tutto o niente (un file a parte, poi lo scambio).</summary>
    public void Scrivi(string cartellaDeiDati)
    {
        string file = FileDi(cartellaDeiDati, Sector);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        string temporaneo = file + ".nuovo";
        File.WriteAllText(temporaneo, JsonSerializer.Serialize(new SulDisco(Sector, [.. _voci], Visto)));
        File.Move(temporaneo, file, overwrite: true);
    }

    /// <summary>I gesti di un salvataggio: entrano nel lotto, da portare in Aurora.</summary>
    public void Salvati(IEnumerable<(string Cosa, IReadOnlyList<string> File)> gesti, DateTimeOffset quando)
    {
        ArgumentNullException.ThrowIfNull(gesti);
        foreach (var (cosa, file) in gesti)
            _voci.Add(new VoceDelLotto(cosa, file, quando));
    }

    /// <summary>
    /// «Porta in Aurora»: le voci salvate sono in vetrina. <paramref name="cambiato"/>, se c'è, dice che in vetrina prima
    /// c'era un altro sector o un altro ramo: allora va rivisto tutto, e lo dice una voce sola invece di fingere che sia
    /// una modifica piccola (carta F5, §4).
    /// </summary>
    public void Portato(DateTimeOffset quando, string? cambiato = null)
    {
        for (int i = 0; i < _voci.Count; i++)
        {
            if (_voci[i].Portata is null)
                _voci[i] = _voci[i] with { Portata = quando };
        }

        if (cambiato is not null)
            _voci.Add(new VoceDelLotto(cambiato, [], quando, quando));
    }

    /// <summary>«Visto in Aurora»: le voci in vetrina se ne vanno; quelle non ancora portate restano.</summary>
    public void VistoInAurora(DateTimeOffset quando)
    {
        _voci.RemoveAll(v => v.Portata is not null);
        Visto = quando;
    }

    private sealed record SulDisco(string Sector, List<VoceDelLotto>? Voci, DateTimeOffset? Visto);
}

/// <summary>
/// Dove si guarda in Aurora una modifica, dal tipo di file (carta F5, D3). 🟡 Proposta dell'agente dalle prove fatte
/// finora e dalla carta «file per file»: <b>da confermare</b> dal committente alla prima prova, e da correggere qui.
/// </summary>
public static class DoveGuardare
{
    /// <summary>Dove guardare le modifiche di quei file (percorsi dalla radice del repo): un posto per tipo, senza doppioni.</summary>
    public static string Per(IReadOnlyList<string> file)
    {
        ArgumentNullException.ThrowIfNull(file);
        return string.Join(" · ", file.Select(Di).Distinct(StringComparer.Ordinal));
    }

    /// <summary>Dove guardare le modifiche di un file.</summary>
    public static string Di(string file)
    {
        ArgumentNullException.ThrowIfNull(file);
        string nome = Path.GetFileName(file);
        string tipo = Path.GetExtension(nome).TrimStart('.').ToLowerInvariant();
        string radice = Path.GetFileNameWithoutExtension(nome).ToUpperInvariant();
        string scalo = radice.Length == 4 && radice.All(char.IsLetter) ? " di " + radice : "";
        bool diRotta = file.Replace('\\', '/').Contains("/ENRMVA/", StringComparison.OrdinalIgnoreCase)
                       || file.Replace('\\', '/').Contains("/ENRVFI/", StringComparison.OrdinalIgnoreCase);
        return tipo switch
        {
            "sid" => $"le SID{scalo}: finestra delle procedure, per pista",
            "str" => $"STAR e mappe{scalo}: finestra delle procedure (e il MAPS)",
            "mva" => diRotta ? $"la MVA Selection, voce dell'ACC ({radice})" : $"la MVA Selection, le zone{scalo}",
            "vfi" => diRotta ? $"i punti VFR della FIR ({radice})" : $"i punti VFR{scalo}",
            "vrt" => $"le rotte VFR{scalo}",
            "gts" => $"i parcheggi{scalo}, sulla mappa di terra",
            "txi" => $"le etichette delle taxiway{scalo}",
            "geo" => scalo.Length > 0 ? $"il layout di terra{scalo}" : $"le linee geografiche ({nome})",
            "pol" => $"i riempimenti del layout di terra{scalo}",
            "fix" or "vor" or "ndb" => $"i punti sulla mappa (NAVAIDS: {tipo.ToUpperInvariant()})",
            "lairway" => "le aerovie basse",
            "hairway" => "le aerovie alte",
            "artcc" => "i confini degli ACC (tasto ACC)",
            "lartcc" => "gli spazi aerei bassi",
            "hartcc" => "gli spazi aerei alti",
            "tfl" => $"i settori dinamici ({radice})",
            "atis" or "datis" or "fds" => "la finestra ATIS",
            "sym" => "i simboli (Impostazioni → SYMBOLS)",
            "ap" => "gli aeroporti",
            "rw" => "le piste (e la scelta delle piste in uso)",
            "frq" => "le frequenze",
            "cpr" => $"il profilo {radice}, aprendolo",
            "danger" or "prohibit" or "restrict" => "le aree regolamentate",
            "cpdlc" or "cpdlcnames" => "il CPDLC",
            "hold" => "le attese",
            "isc" => "l'elenco dei file del sector (ricarica completa)",
            "upd" => "niente in Aurora (gli aggiornamenti dei client)",
            _ => $"il file {nome}",
        };
    }
}
