using System.Globalization;
using System.Text.RegularExpressions;
using Vipi.Sectorfile.IO;
using Vipi.Sectorfile.Shared;

namespace Vipi.Sectorfile.Validazione;

/// <summary>
/// Gli avvisi dei punti e delle rotte VFR (lotto «Subito» slice 16b; «file per file» F4, S4): i punti dei <c>.vfi</c>
/// contro i loro fix nascosti, il codice ripetuto o scritto nel campo sbagliato, le rotte militari a metà.
/// </summary>
/// <remarks>
/// <para><b>La convenzione italiana.</b> Per il manuale IVAO il 2° campo di <c>[VFRFIX]</c> è la quota; nei file
/// italiani è il <b>codice</b> del punto (<c>COLOMBO;RFS3;…</c>), e ogni codice ha un gemello in
/// <c>NAVAIDS/VFR_NASCOSTI.fix</c> (<c>RFS3;…;3;</c>): un piano di volo che cita il punto lo riconosce solo se sta in un
/// <c>.fix</c>. Senza quel file il sector non segue la convenzione, e dei codici non si dice niente.</para>
/// <para>Misure sul fork del 6 ottobre 2026 (74 <c>.vfi</c> di scalo, 3 di <c>ENRVFI</c>, 586 punti): 510 col codice,
/// 72 con altro nel 2° campo (<c>ED</c>, <c>PA</c>, <c>2500</c>: non chiedono un gemello), 2 vuoti; 4 righe di
/// <c>lipa.vfi</c> con nome e codice scambiati (<c>PASW1;CONEGLIANO;</c>), <c>lict.vfi:8</c> e <c>liph.vfi:2</c> col
/// codice in coda al nome; 2 codici su due punti (<c>MJNW1</c>, <c>PKS1</c>). Nei <c>.vrt</c>: 52 rotte, 8 militari
/// (tutte le righe a 1), nessuna a metà.</para>
/// <para>Si legge dalle righe: gli avvisi vanno sulla riga, e la riga corretta è quella di prima con un campo cambiato.</para>
/// </remarks>
public static partial class ControlloDeiVfr
{
    // Lo stesso punto scritto in due forme (compatta, coi punti) si legge uguale al millesimo di secondo: un metro basta.
    private const double StessoPuntoMetri = 1;

    private sealed record PuntoVfr(string Relativo, int Riga, string Testo, string Nome, string Codice, Coordinate? Dove, string[] Campi);

    private sealed record FixNascosto(int Riga, string Testo, string Nome, Coordinate? Dove);

    /// <summary>Vero se il testo è un codice VFR italiano: due-cinque lettere e una o due cifre (<c>RFS3</c>, <c>BNNW1</c>).</summary>
    public static bool EUnCodice(string? testo) => testo is not null && Codice().IsMatch(testo.Trim());

    /// <summary>Vero se il file è quello dei fix nascosti dei punti VFR.</summary>
    public static bool ENascosti(string percorso)
        => string.Equals(Path.GetFileName(percorso.Replace('\\', '/')), "VFR_NASCOSTI.fix", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// I problemi dei punti e delle rotte VFR. <paramref name="vfi"/> e <paramref name="vrt"/>: il percorso da mostrare
    /// e le righe di ogni file; <paramref name="nascosti"/>: il file dei fix nascosti, o null se il sector non l'ha.
    /// </summary>
    public static IEnumerable<ProblemaDelSector> Di(IReadOnlyList<(string Relativo, IReadOnlyList<string> Righe)> vfi,
                                                    (string Relativo, IReadOnlyList<string> Righe)? nascosti,
                                                    IReadOnlyList<(string Relativo, IReadOnlyList<string> Righe)> vrt)
    {
        ArgumentNullException.ThrowIfNull(vfi);
        ArgumentNullException.ThrowIfNull(vrt);

        var problemi = new List<ProblemaDelSector>();
        if (nascosti is { } file)
            problemi.AddRange(DeiCodici(vfi, file));
        foreach (var (relativo, righe) in vrt)
            problemi.AddRange(DelleRotte(relativo, righe));
        return problemi.OrderBy(p => p.File, StringComparer.Ordinal).ThenBy(p => p.Riga);
    }

    private static IEnumerable<ProblemaDelSector> DeiCodici(IReadOnlyList<(string Relativo, IReadOnlyList<string> Righe)> vfi,
                                                           (string Relativo, IReadOnlyList<string> Righe) nascosti)
    {
        var fix = new Dictionary<string, List<FixNascosto>>(StringComparer.Ordinal);
        for (int i = 0; i < nascosti.Righe.Count; i++)
        {
            if (Campi(nascosti.Righe[i]) is not { Length: >= 3 } campi)
                continue;
            string nome = campi[0].Trim();
            if (!fix.TryGetValue(nome, out var suoi))
                fix[nome] = suoi = [];
            suoi.Add(new FixNascosto(i + 1, nascosti.Righe[i], nome, Leggi(campi[1], campi[2])));
        }

        var punti = new List<PuntoVfr>();
        foreach (var (relativo, righe) in vfi)
        {
            string scalo = Path.GetFileNameWithoutExtension(relativo.Replace('\\', '/')).ToUpperInvariant();
            string coppia = scalo.Length == 4 ? scalo[2..] : string.Empty;
            for (int i = 0; i < righe.Count; i++)
            {
                if (Campi(righe[i]) is not { Length: >= 4 } campi)
                    continue;
                string nome = campi[0].Trim(), codice = campi[1].Trim();
                if (FuoriPosto(nome, codice, coppia, fix) is (var vero, var veroNome))
                {
                    string[] corretti = [.. campi];
                    (corretti[0], corretti[1]) = (veroNome, vero);
                    yield return new(Regola.CodiceVfrFuoriPosto, relativo, i + 1, righe[i],
                        EUnCodice(nome)
                            ? $"nome e codice sono scambiati: «{nome}» è il codice, «{codice}» il nome del punto"
                            : $"il codice «{vero}» è finito in coda al nome, e il 2° campo è vuoto: manca un «;»",
                        string.Join(';', corretti) + ";");
                    (nome, codice) = (veroNome, vero);
                }

                if (EUnCodice(codice))
                    punti.Add(new PuntoVfr(relativo, i + 1, righe[i], nome, codice, Leggi(campi[2], campi[3]), campi));
            }
        }

        var perCodice = punti.GroupBy(p => p.Codice, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
        foreach (var (codice, suoi) in perCodice.OrderBy(c => c.Key, StringComparer.Ordinal))
        {
            if (suoi.Count > 1)
            {
                // Il punto «di casa» è quello dello scalo che ha le sue due lettere nel codice (RFS3 in lirf.vfi).
                var primo = suoi.FirstOrDefault(p => DelloScalo(p.Relativo, codice)) ?? suoi[0];
                foreach (var altro in suoi.Where(p => !ReferenceEquals(p, primo)))
                {
                    yield return new(Regola.CodiceVfrRipetuto, altro.Relativo, altro.Riga, altro.Testo,
                        $"il codice {codice} è già di «{primo.Nome}» in {Path.GetFileName(primo.Relativo)}:{primo.Riga}: due punti con lo stesso codice hanno un fix nascosto solo");
                }

                continue;
            }

            var punto = suoi[0];
            if (!fix.TryGetValue(codice, out var gemelli))
            {
                yield return new(Regola.GemelloVfrMancante, punto.Relativo, punto.Riga, punto.Testo,
                    $"il punto «{punto.Nome}» ({codice}) non ha il suo fix nascosto in {Path.GetFileName(nascosti.Relativo)}: un piano di volo che lo cita non lo riconosce");
            }
            else if (gemelli is [{ Dove: { } delFix } gemello] && punto.Dove is { } delPunto
                     && Validatore.Metri(delPunto, delFix) is var metri && metri > StessoPuntoMetri)
            {
                // Committente, 6 ottobre (sui cinque del fork): è giusto il fix. La riga corretta porta il punto dov'è
                // il fix, nella forma in cui la riga scrive le sue coordinate; chi sa che è giusto il punto sposta il fix.
                string[] corretti = [.. punto.Campi];
                corretti[2] = CoordinateConverter.LatitudeToDottedDms(delFix.LatitudeDeg);
                corretti[3] = CoordinateConverter.LongitudeToDottedDms(delFix.LongitudeDeg);
                string proposta = FormaDelPunto.In([string.Join(';', corretti) + ";"], FormaDelPunto.Di([punto.Testo]) ?? FormaDelPunto.Forma.Puntata).First();
                yield return new(Regola.GemelloVfrDiverso, punto.Relativo, punto.Riga, punto.Testo,
                    $"il fix nascosto {codice} ({Path.GetFileName(nascosti.Relativo)}:{gemello.Riga}) è a {Distanza(metri)} dal punto «{punto.Nome}»: la riga corretta porta il punto dov'è il fix",
                    proposta);
            }
        }

        foreach (var (nome, suoi) in fix.OrderBy(f => f.Value[0].Riga))
        {
            if (perCodice.ContainsKey(nome) || NelSecondoCampo(vfi, nome))
                continue;
            foreach (var orfano in suoi)
            {
                yield return new(Regola.FixNascostoSenzaPunto, nascosti.Relativo, orfano.Riga, orfano.Testo,
                    $"il fix nascosto {nome} non ha un punto VFR con quel codice in nessun .vfi");
            }
        }
    }

    /// <summary>
    /// Il codice e il nome veri di una riga che li ha fuori posto, o null: scambiati (<c>PASW1;CONEGLIANO;</c>), o col
    /// codice in coda al nome e il 2° campo vuoto (<c>MAZARA DEL VALLOCTSE3;;</c>, <c>CAORLE - PHE2;;</c>). Il codice
    /// si riconosce, in tutti e due i casi, se è un fix nascosto o se comincia con le due lettere dello scalo.
    /// </summary>
    private static (string Codice, string Nome)? FuoriPosto(string nome, string codice, string coppia, Dictionary<string, List<FixNascosto>> fix)
    {
        // Scambiati solo se il 1° campo è davvero un codice di qui: un fix nascosto, o le due lettere dello scalo.
        // `IP31;PL;` in lipl.vfi è un punto che si chiama IP31 (initial point), non un codice fuori posto.
        if (EUnCodice(nome) && codice.Length > 0 && !EUnCodice(codice)
            && (fix.ContainsKey(nome) || (coppia.Length > 0 && nome.StartsWith(coppia, StringComparison.Ordinal))))
        {
            return (nome, codice);
        }

        if (codice.Length > 0)
            return null;

        string? trovato = null;
        for (int lungo = Math.Min(7, nome.Length - 1); lungo >= 3; lungo--)
        {
            string coda = nome[^lungo..];
            if (!EUnCodice(coda))
                continue;
            if (fix.ContainsKey(coda))
            {
                trovato = coda;
                break;
            }

            if (coppia.Length > 0 && coda.StartsWith(coppia, StringComparison.Ordinal))
                trovato ??= coda;
        }

        return trovato is null ? null : (trovato, nome[..^trovato.Length].TrimEnd(' ', '-'));
    }

    private static IEnumerable<ProblemaDelSector> DelleRotte(string relativo, IReadOnlyList<string> righe)
    {
        // Una rotta: le righe di seguito con lo stesso numero (come VrtParser). I tag dei punti non la chiudono.
        var rotta = new List<(int Indice, string[] Campi)>();
        string? numero = null;
        var problemi = new List<ProblemaDelSector>();

        void Chiudi()
        {
            int militari = rotta.Count(r => Militare(r.Campi));
            if (militari > 0 && militari < rotta.Count)
            {
                // Vince la maggioranza delle righe; alla pari, militare.
                bool versoIlMilitare = militari * 2 >= rotta.Count;
                foreach (var (indice, campi) in rotta.Where(r => Militare(r.Campi) != versoIlMilitare))
                {
                    string proposta = string.Join(';', campi.Take(3).Select(c => c.Trim())) + (versoIlMilitare ? ";;1;" : ";");
                    problemi.Add(new(Regola.RottaMilitareAMeta, relativo, indice + 1, righe[indice],
                        versoIlMilitare
                            ? $"la rotta {numero} è militare su {militari} righe su {rotta.Count}, e su questa no: il 5° campo va uguale su tutte"
                            : $"la rotta {numero} è militare solo su {militari} righe su {rotta.Count}, e questa è una: il 5° campo va uguale su tutte",
                        proposta));
                }
            }

            rotta.Clear();
            numero = null;
        }

        for (int i = 0; i < righe.Count; i++)
        {
            string riga = righe[i].Trim();
            if (riga.StartsWith("//@@", StringComparison.Ordinal))
                continue;
            if (Campi(righe[i]) is not { Length: >= 3 } campi || !campi[0].Trim().All(char.IsAsciiDigit) || campi[0].Trim().Length == 0)
            {
                Chiudi();
                continue;
            }

            if (numero is not null && numero != campi[0].Trim())
                Chiudi();
            numero = campi[0].Trim();
            rotta.Add((i, campi));
        }

        Chiudi();
        return problemi;
    }

    private static bool Militare(string[] campi) => campi.Length >= 5 && campi[4].Trim() == "1";

    // Il codice sta com'è nel 2° campo di un .vfi anche se non ha la forma di un codice (RZNE in lirz.vfi, senza numero).
    private static bool NelSecondoCampo(IReadOnlyList<(string Relativo, IReadOnlyList<string> Righe)> vfi, string nome)
        => vfi.Any(f => f.Righe.Any(r => Campi(r) is { Length: >= 4 } campi && string.Equals(campi[1].Trim(), nome, StringComparison.Ordinal)));

    private static bool DelloScalo(string relativo, string codice)
        => Path.GetFileNameWithoutExtension(relativo.Replace('\\', '/')).ToUpperInvariant() is { Length: 4 } scalo
           && codice.StartsWith(scalo[2..], StringComparison.Ordinal);

    /// <summary>I campi di una riga di dati (senza l'ultimo vuoto dopo il «;» finale), o null per vuote e commenti.</summary>
    private static string[]? Campi(string riga)
    {
        string t = riga.Trim();
        if (t.Length == 0 || t.StartsWith("//", StringComparison.Ordinal))
            return null;
        string[] campi = t.Split(';');
        return campi[^1].Trim().Length == 0 ? campi[..^1] : campi;
    }

    private static Coordinate? Leggi(string lat, string lon)
    {
        try
        {
            return CoordinateConverter.ParsePair(lat.Trim(), lon.Trim());
        }
        catch (CoordinateParseException)
        {
            return null;
        }
    }

    private static string Distanza(double metri)
        => metri < 185.2
            ? metri.ToString("0", CultureInfo.InvariantCulture) + " m"
            : (metri / 1852).ToString(metri < 1852 ? "0.##" : "0.#", CultureInfo.InvariantCulture) + " NM";

    [GeneratedRegex(@"^[A-Z]{2,5}\d{1,2}$")]
    private static partial Regex Codice();
}
