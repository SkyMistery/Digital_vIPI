namespace Vipi.SectorLab.Core.Aurora;

/// <summary>
/// La prova della seconda metà di T1 («file per file» §20, aperta dal committente il 17 ottobre): un
/// <c>SYMBOLS_&lt;RUOLO&gt;=</c> scritto in un <c>.cpr</c> del sector vale? I profili di Aurora salvano il simbolo di un ruolo
/// come disegno (<c>SYMBOLS_AIRPORT=</c> con i 13 gruppi, misurato nei profili installati), nella sezione <c>[PREFS]</c>
/// come i <c>.cpr</c> del sector. La prova mette in <c>TWR.cpr</c> (il profilo delle torri) il simbolo degli aeroporti
/// col disegno del simbolo «VOR» di <c>symbols.sym</c>: se vale, con una posizione di torre gli aeroporti diventano VOR.
/// 🔴 Un <c>.cpr</c> del sector sovrascrive l'impostazione dell'utente a ogni connessione (§14): è anche quello che si guarda.
/// </summary>
public sealed class SimboliNelProfilo : ProvaInAurora
{
    /// <summary>Il profilo della prova, dalla radice del repo.</summary>
    public const string File = "SectorFiles/Include/IT/PREFS/TWR.cpr";

    /// <summary>Il simbolo di <c>symbols.sym</c> il cui disegno va sugli aeroporti.</summary>
    public const string Simbolo = "VOR";

    public override string Argomento => "simboli-nel-profilo";

    public override string Titolo => "un simbolo scritto in un profilo del sector (T1, SYMBOLS_)";

    public override string Perche
        => "«File per file» T1: SYMBOLS_AIRPORT= in TWR.cpr col disegno del VOR di symbols.sym. Aurora lo applica? Sovrascrive la scelta dell'utente?";

    public override ProvaPreparata? Prepara(string radice, out string? perche)
    {
        string cpr = Path.Combine(radice, File.Replace('/', Path.DirectorySeparatorChar));
        string sym = Path.Combine(radice, "SectorFiles", "Include", "IT", "symbols.sym");
        if (!System.IO.File.Exists(cpr) || !System.IO.File.Exists(sym))
        {
            perche = $"Mancano {File} o symbols.sym.";
            return null;
        }

        // Il disegno: la riga di pixel subito sotto il commento //VOR (la forma è quella dei profili).
        var simboli = RigheDelFile.Leggi(sym).Righe;
        int nome = simboli.FindIndex(r => r.Trim().Equals("//" + Simbolo, StringComparison.Ordinal));
        string? disegno = nome >= 0 ? simboli.Skip(nome + 1).Select(r => r.Trim()).FirstOrDefault(r => r.Length > 0) : null;
        if (disegno is null || Vipi.Sectorfile.Models.Simbolo.Leggi(disegno) is null)
        {
            perche = $"In symbols.sym non trovo il disegno di «{Simbolo}» (13 gruppi di 13 cifre sotto //{Simbolo}).";
            return null;
        }

        var righe = RigheDelFile.Leggi(cpr);
        if (righe.Righe.Any(r => r.TrimStart().StartsWith("SYMBOLS_AIRPORT=", StringComparison.OrdinalIgnoreCase)))
        {
            perche = $"{File} ha già un SYMBOLS_AIRPORT: la prova vuole vedere l'effetto di uno nuovo.";
            return null;
        }

        int prefs = righe.Righe.FindIndex(r => r.Trim().Equals("[PREFS]", StringComparison.OrdinalIgnoreCase));
        if (prefs < 0)
        {
            righe.Righe.Insert(0, "[PREFS]");
            prefs = 0;
        }

        int fine = righe.Righe.FindIndex(prefs + 1, r => r.TrimStart().StartsWith('['));
        righe.Righe.Insert(fine < 0 ? righe.Righe.Count : fine, "SYMBOLS_AIRPORT=" + disegno);
        perche = null;
        var domande = new List<DomandaDellaProva>
        {
            new($"Con una posizione di torre, gli aeroporti sulla mappa hanno il simbolo «{Simbolo}» invece di quello di prima?",
                "scegliendo o connettendo una posizione _TWR (il profilo TWR.cpr), per esempio LIRF_TWR; gli aeroporti sulla mappa"),
            new($"Nelle impostazioni, linguetta SYMBOLS, il ruolo AIRPORTS mostra il simbolo «{Simbolo}»?", "SETTINGS → SYMBOLS → AIRPORTS, con la posizione di torre"),
            new("Passando poi a una posizione che non è di torre, gli aeroporti tornano al simbolo di prima, o resta quello del profilo?", "una posizione _CTR o _APP dopo la torre"),
            new("Il sector si carica senza errori nuovi?", "i messaggi di Aurora al caricamento del sector (SectorError.log)"),
        };
        var fatto = new List<string> { $"{File}: SYMBOLS_AIRPORT= col disegno di «{Simbolo}» di symbols.sym, in [PREFS]." };
        return new ProvaPreparata([new FileDellaProva(File, righe.Testo())], domande, fatto);
    }
}
