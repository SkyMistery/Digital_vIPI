using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Vipi.SectorLab.Ui.Servizi;

public sealed partial class SessioneDelLab
{
    // --- il kit di prova per gli AOD (carta «Il kit di prova per gli AOD», 3 novembre 2026) -----------------------------

    private const string FileDelGiro = "giro-di-prova.json";

    /// <summary>
    /// K2: la cartella di lavoro che il primo avvio propone (<c>--lavoro-proposto</c>, il <c>.cmd</c> del kit passa la
    /// cartella <c>lavoro</c> accanto allo zip). Null: il campo nasce vuoto, com'era.
    /// </summary>
    public string? LavoroProposto { get; set; }

    private bool _giroLetto;
    private bool _giroAcceso;
    private readonly Dictionary<string, EsitoDelGiro> _esitiDelGiro = new(StringComparer.Ordinal);

    /// <summary>K3: il Giro di prova c'è (acceso dal kit con <c>--giro-di-prova</c>, poi ricordato nei dati).</summary>
    public bool GiroAcceso
    {
        get
        {
            LeggiIlGiro();
            return _giroAcceso;
        }
    }

    /// <summary>K5: il pannello del giro è aperto sopra la pagina.</summary>
    public bool GiroAperto { get; private set; }

    /// <summary>L'indice del compito che il pannello mostra.</summary>
    public int CompitoAperto { get; private set; }

    /// <summary>Gli esiti dati, per compito.</summary>
    public IReadOnlyDictionary<string, EsitoDelGiro> EsitiDelGiro
    {
        get
        {
            LeggiIlGiro();
            return _esitiDelGiro;
        }
    }

    /// <summary>Quanti compiti hanno la loro risposta.</summary>
    public int CompitiFatti => GiroDiProva.Compiti.Count(c => GiroDiProva.Fatto(c, EsitiDelGiro.GetValueOrDefault(c.Id)));

    /// <summary>Perché «Manda gli esiti» non è partito, o null.</summary>
    public string? PercheNonMandaGliEsiti { get; private set; }

    /// <summary>L'ultima pagina della issue aperta, per riaprirla; e se il testo era tagliato (K8).</summary>
    public Uri? PaginaDegliEsiti { get; private set; }

    /// <summary>K8: il testo intero non stava nel link: va trascinato nella issue il file degli esiti.</summary>
    public bool EsitiTagliati { get; private set; }

    /// <summary>Il file con gli esiti interi, nei dati del Lab.</summary>
    public string FileDegliEsitiDelGiro => Path.Combine(_cartellaDeiDati, GiroDiProva.FileDegliEsiti);

    /// <summary>K3: accende il giro (all'avvio, dal kit). Già acceso, non cambia niente.</summary>
    public void AccendiIlGiro()
    {
        LeggiIlGiro();
        if (_giroAcceso)
            return;
        _giroAcceso = true;
        GiroAperto = true;
        CompitoAperto = PrimoDaFare();
        ScriviIlGiro();
        Registro.Scrivi("giro", "acceso");
        Avvisa();
    }

    /// <summary>Il tasto della barra: apre o chiude il pannello.</summary>
    public void ApriOChiudiIlGiro()
    {
        GiroAperto = !GiroAperto;
        Avvisa();
    }

    /// <summary>Il pannello mostra il compito <paramref name="indice"/> (i pallini, ‹ ›).</summary>
    public void VaiAlCompito(int indice)
    {
        CompitoAperto = Math.Clamp(indice, 0, GiroDiProva.Compiti.Count - 1);
        Avvisa();
    }

    /// <summary>«Vai a …»: la pagina del compito.</summary>
    public void VaiAllaPaginaDelCompito(CompitoDelGiro compito)
    {
        ArgumentNullException.ThrowIfNull(compito);
        if (compito.Pagina is { } pagina)
            ScegliLaPagina(pagina);
    }

    /// <summary>
    /// K5, K7: l'esito di un compito, con la nota; si scrive subito nei dati, e il pannello passa al primo compito dopo
    /// che non ha ancora il suo (se sono fatti tutti, resta lì).
    /// </summary>
    public void DaiLEsito(string compito, EsitoDelCompito esito, string? nota)
    {
        ScriviLEsito(compito, esito, nota);
        int qui = IndiceDel(compito);
        int quanti = GiroDiProva.Compiti.Count;
        CompitoAperto = qui;
        for (int passo = 1; passo < quanti; passo++)
        {
            var dopo = GiroDiProva.Compiti[(qui + passo) % quanti];
            if (!GiroDiProva.Fatto(dopo, _esitiDelGiro.GetValueOrDefault(dopo.Id)))
            {
                CompitoAperto = (qui + passo) % quanti;
                break;
            }
        }

        Avvisa();
    }

    /// <summary>La nota di un compito, senza cambiarne l'esito (o la risposta di un compito che è solo una domanda).</summary>
    public void ScriviLaNota(string compito, string? nota)
    {
        ScriviLEsito(compito, EsitiDelGiro.GetValueOrDefault(compito)?.Esito, nota);
        Avvisa();
    }

    /// <summary>
    /// K8: «Manda gli esiti». Scrive il testo intero in <see cref="FileDegliEsitiDelGiro"/> e apre nel browser la pagina
    /// di GitHub «nuova issue» sul repo del sector, già compilata. Falso, col perché in
    /// <see cref="PercheNonMandaGliEsiti"/>: senza VID, o se il repo del sector non è GitHub.
    /// </summary>
    public bool MandaGliEsiti(DateTime? adesso = null)
    {
        PercheNonMandaGliEsiti = null;
        if (Vid is not { } vid)
            return NonMandaGliEsiti("Prima scrivi il tuo VID, nelle Impostazioni (compito G1): la issue dice chi l'ha mandata.");
        if (Core.Git.Invio.RepoGitHub(IndirizzoDelSector) is not { } repo)
            return NonMandaGliEsiti($"Il repo del sector ({IndirizzoDelSector}) non è su GitHub: gli esiti stanno in {FileDegliEsitiDelGiro}.");

        var (titolo, testo) = GiroDiProva.Issue(vid, VersioneDelLab, Environment.OSVersion.VersionString, adesso ?? DateTime.Now,
            EsitiDelGiro, GiroDiProva.ErroriDelRegistro(Registro.Cartella));
        try
        {
            Directory.CreateDirectory(_cartellaDeiDati);
            File.WriteAllText(FileDegliEsitiDelGiro, $"# {titolo}\n\n{testo}");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Registro.Errore("giro", e);
        }

        var (pagina, tagliato) = GiroDiProva.PaginaDellaIssue(repo, titolo, testo, FileDegliEsitiDelGiro);
        PaginaDegliEsiti = pagina;
        EsitiTagliati = tagliato;
        Registro.Scrivi("giro", $"manda gli esiti a {repo}: {CompitiFatti} compiti su {GiroDiProva.Compiti.Count}{(tagliato ? ", testo tagliato" : "")}");
        ApriNelBrowser(pagina);
        Avvisa();
        return true;
    }

    /// <summary>Apre la cartella dei dati del Lab, dove sta il file degli esiti (per trascinarlo nella issue).</summary>
    public void ApriLaCartellaDegliEsiti()
    {
        if (!OperatingSystem.IsWindows())
            return;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"/select,\"{FileDegliEsitiDelGiro}\"")
                { UseShellExecute = true })?.Dispose();
        }
        catch (System.ComponentModel.Win32Exception e)
        {
            Registro.Errore("giro", e);
        }
    }

    /// <summary>La versione del Lab con l'impronta corta del commit («0.9.0+42153ee8»): dice quale eseguibile ha provato.</summary>
    public static string VersioneDelLab { get; } = Accorcia(Assembly.GetEntryAssembly()?
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "?");

    private static string Accorcia(string versione) =>
        versione.IndexOf('+', StringComparison.Ordinal) is var piu and >= 0 && versione.Length > piu + 9
            ? versione[..(piu + 9)]
            : versione;

    private bool NonMandaGliEsiti(string perche)
    {
        PercheNonMandaGliEsiti = perche;
        Registro.Scrivi("giro", "esiti non mandati: " + perche);
        Avvisa();
        return false;
    }

    private void ScriviLEsito(string compito, EsitoDelCompito? esito, string? nota)
    {
        if (IndiceDel(compito) < 0)
            throw new ArgumentException($"Il compito «{compito}» non c'è.", nameof(compito));
        LeggiIlGiro();
        _esitiDelGiro[compito] = new EsitoDelGiro(compito, esito, string.IsNullOrWhiteSpace(nota) ? null : nota.Trim(), DateTime.Now);
        ScriviIlGiro();
        Registro.Scrivi("giro", $"{compito}: {esito?.ToString() ?? "nota"}{(string.IsNullOrWhiteSpace(nota) ? "" : " · " + nota.Trim())}");
    }

    private static int IndiceDel(string compito)
    {
        for (int i = 0; i < GiroDiProva.Compiti.Count; i++)
        {
            if (string.Equals(GiroDiProva.Compiti[i].Id, compito, StringComparison.Ordinal))
                return i;
        }

        return -1;
    }

    private int PrimoDaFare()
    {
        for (int i = 0; i < GiroDiProva.Compiti.Count; i++)
        {
            if (!GiroDiProva.Fatto(GiroDiProva.Compiti[i], _esitiDelGiro.GetValueOrDefault(GiroDiProva.Compiti[i].Id)))
                return i;
        }

        return 0;
    }

    /// <summary>Il file del giro nei dati, letto una volta (K7). Acceso, il pannello nasce aperto sul primo compito da fare.</summary>
    private void LeggiIlGiro()
    {
        if (_giroLetto)
            return;
        _giroLetto = true;
        if (Ricordata(FileDelGiro) is not { } scritto)
            return;
        try
        {
            if (JsonSerializer.Deserialize<FileDelGiroScritto>(scritto, OpzioniDelGiro) is { } letto)
            {
                _giroAcceso = letto.Acceso;
                foreach (var esito in letto.Esiti ?? [])
                {
                    if (IndiceDel(esito.Compito) >= 0)
                        _esitiDelGiro[esito.Compito] = esito;
                }
            }
        }
        catch (JsonException e)
        {
            // Un file rovinato: il giro riparte, e il registro lo dice.
            Registro.Errore("giro", e);
        }

        GiroAperto = _giroAcceso;
        CompitoAperto = PrimoDaFare();
    }

    // Non con Ricorda: scriverebbe nel registro il file intero a ogni esito (lo dice già ScriviLEsito, in una riga).
    private void ScriviIlGiro()
    {
        try
        {
            Directory.CreateDirectory(_cartellaDeiDati);
            File.WriteAllText(Path.Combine(_cartellaDeiDati, FileDelGiro),
                JsonSerializer.Serialize(new FileDelGiroScritto(_giroAcceso, [.. _esitiDelGiro.Values]), OpzioniDelGiro));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Vale per questa sessione anche se il disco non lo ricorda; il registro lo dice.
            Registro.Errore("giro", e);
        }
    }

    // Gli esiti col nome («NonHoCapito»), non col numero: il file si legge a occhio, e regge un esito aggiunto in mezzo.
    private static readonly JsonSerializerOptions OpzioniDelGiro = new() { Converters = { new JsonStringEnumConverter() } };

    private sealed record FileDelGiroScritto(bool Acceso, List<EsitoDelGiro>? Esiti);
}
