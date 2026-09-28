using System.Text.RegularExpressions;
using Vipi.SectorLab.Core.Ispezione;
using Vipi.SectorLab.Core.Sessione;
using Vipi.Sectorfile.Models;

namespace Vipi.SectorLab.Core.Modifiche;

/// <summary>Che voce è, per dove sta il suo nome nelle righe.</summary>
public enum TipoDellaVoce
{
    /// <summary>
    /// Un confine (<c>.artcc</c>, <c>.hartcc</c>, <c>.lartcc</c>): il 2° campo delle righe <c>T;</c>. Le etichette
    /// <c>L;</c> sono un'altra voce («Etichette (L)»), che non si rinomina: una col testo uguale al gruppo resta.
    /// </summary>
    Confine,

    /// <summary>Una MVA: il 2° campo, e il 5° quando è il gruppo (<c>T;LIMM;…;LIMM;</c>).</summary>
    Mva,

    /// <summary>Un'aerovia: il 2° campo dei tratti, e una parola fra i «-» delle etichette (<c>L;M984-Y740;…</c>).</summary>
    Aerovia,

    /// <summary>Un'area P/R/D: il 6° campo delle linee (<c>…;RESTRICT;R107B;</c>).</summary>
    Area,
}

/// <summary>
/// La rinomina di una voce della finestra di selezione (lotto «Subito» slice 7f, rimandata dalla 6b): il nome che sta
/// nelle righe di dati — il 2° campo dei confini, delle MVA e delle aerovie, il 6° delle aree P/R/D. Vale nel file della
/// voce: Aurora raccoglie per nome, e sul fork nessun nome di voce sta in due file (le MVA di scalo «2000» in
/// <c>licc.mva</c> e <c>lipe.mva</c> sono zone di scali diversi). Si cambiano anche le righe nascoste della voce (5c: se
/// si mostrano, tornano con lei) e i tag <c>//@"NOME"</c> dei suoi blocchi.
/// </summary>
public static class RinominaDellaVoce
{
    /// <summary>Il tipo della voce dal primo record del file, o null se il nome non sta nelle righe di dati.</summary>
    public static TipoDellaVoce? Tipo(IFileConRecord file, VoceDellaSelezione voce)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(voce);
        if (voce.NomeDalCommento || voce.Record.Count == 0)
            return null;
        return file.RecordDelModello[voce.Record[0]] switch
        {
            StaticBoundaryGroup => TipoDellaVoce.Confine,
            // Le zone senza gruppo: il nome non è scritto, e non c'è niente da rinominare.
            MvaSector => voce.Nome == VociDellaSelezione.SenzaGruppo ? null : TipoDellaVoce.Mva,
            // L'etichetta condivisa «L613-L615» segue le sue aerovie: si rinominano loro, una per una.
            Airway => voce.Nome.Contains('-', StringComparison.Ordinal) ? null : TipoDellaVoce.Aerovia,
            Line { Nome: not null } => TipoDellaVoce.Area,
            _ => null,
        };
    }

    /// <summary>Perché il nome nuovo non va, o null.</summary>
    public static string? PercheNonVa(string? nuovo, TipoDellaVoce tipo, IEnumerable<VoceDellaSelezione> altre)
    {
        ArgumentNullException.ThrowIfNull(altre);
        string testo = (nuovo ?? "").Trim();
        if (OrdineAlfabetico.PercheNonVa(testo) is { } perche)
            return perche;
        if (testo.StartsWith('@'))
            return "Un nome non comincia con «@»: sarebbe un tag //@ del Lab.";
        if (testo.Contains('"', StringComparison.Ordinal))
            return "Un nome non ha le virgolette: nei tag //@ le chiudono.";
        if (testo.Equals("DUMMY", StringComparison.OrdinalIgnoreCase) || testo.Equals("BREAK", StringComparison.OrdinalIgnoreCase))
            return $"«{testo}» per Aurora non è un nome: separa i pezzi.";
        if (tipo == TipoDellaVoce.Aerovia && (testo.Contains('-', StringComparison.Ordinal) || testo.Contains(' ', StringComparison.Ordinal)))
            return "Il nome di un'aerovia non ha spazi né «-»: nelle etichette il «-» separa le aerovie che condividono un tratto.";
        if (altre.FirstOrDefault(v => string.Equals(v.Nome, testo, StringComparison.OrdinalIgnoreCase)) is { } gia)
            return $"C'è già una voce «{gia.Nome}» in questo file: le due diventerebbero una.";
        return null;
    }

    /// <summary>
    /// Le righe da cambiare (numero da 1 → la riga nuova), nel file com'è adesso. Le righe commentate che, senza il
    /// «//», sarebbero della voce, si cambiano col «//» tenuto.
    /// </summary>
    public static IReadOnlyDictionary<int, IReadOnlyList<string>> Sostituzioni(IReadOnlyList<string> righe, TipoDellaVoce tipo, string vecchio, string nuovo)
    {
        ArgumentNullException.ThrowIfNull(righe);
        var sostituzioni = new Dictionary<int, IReadOnlyList<string>>();
        for (int i = 0; i < righe.Count; i++)
        {
            string riga = righe[i];
            string testa = riga.TrimStart();
            string? nuova;
            if (testa.StartsWith("//@", StringComparison.Ordinal))
            {
                // Il blocco della voce (§M, E1/B1): //@"M984" …
                nuova = Regex.Replace(riga, $@"^(\s*//@""){Regex.Escape(vecchio)}("")", m => m.Groups[1].Value + nuovo + m.Groups[2].Value, RegexOptions.IgnoreCase);
                nuova = nuova == riga ? null : nuova;
            }
            else if (testa.StartsWith("//", StringComparison.Ordinal))
            {
                // Una riga nascosta: il «//» (e gli spazi) davanti, poi una riga di dati che forse è della voce.
                var m = Regex.Match(riga, @"^(\s*//+\s*)(.*)$");
                nuova = NellaRiga(m.Groups[2].Value, tipo, vecchio, nuovo) is { } dentro ? m.Groups[1].Value + dentro : null;
            }
            else
            {
                nuova = NellaRiga(riga, tipo, vecchio, nuovo);
            }

            if (nuova is not null)
                sostituzioni[i + 1] = [nuova];
        }

        return sostituzioni;
    }

    // Una riga di dati col nome della voce cambiato, o null se non è della voce.
    private static string? NellaRiga(string riga, TipoDellaVoce tipo, string vecchio, string nuovo)
    {
        string[] campi = riga.Split(';');
        bool Uguale(int i) => i < campi.Length && string.Equals(campi[i].Trim(), vecchio, StringComparison.OrdinalIgnoreCase);
        void Metti(int i) => campi[i] = campi[i].Replace(campi[i].Trim(), nuovo, StringComparison.Ordinal);
        string tipoDellaRiga = campi[0].Trim().ToUpperInvariant();
        bool cambiata = false;

        switch (tipo)
        {
            case TipoDellaVoce.Confine when tipoDellaRiga == "T" && Uguale(1):
                Metti(1);
                cambiata = true;
                break;

            case TipoDellaVoce.Mva when tipoDellaRiga is "T" or "L":
                if (Uguale(1))
                {
                    Metti(1);
                    cambiata = true;
                    // Il 5° campo è il gruppo: segue il nome solo dove era uguale.
                    if (Uguale(4))
                        Metti(4);
                }

                break;

            case TipoDellaVoce.Aerovia when tipoDellaRiga == "T" && Uguale(1):
                Metti(1);
                cambiata = true;
                break;

            case TipoDellaVoce.Aerovia when tipoDellaRiga == "L" && campi.Length > 1:
                string[] parole = campi[1].Split('-');
                for (int p = 0; p < parole.Length; p++)
                {
                    if (string.Equals(parole[p].Trim(), vecchio, StringComparison.OrdinalIgnoreCase))
                    {
                        parole[p] = parole[p].Replace(parole[p].Trim(), nuovo, StringComparison.Ordinal);
                        cambiata = true;
                    }
                }

                campi[1] = string.Join('-', parole);
                break;

            case TipoDellaVoce.Area when Uguale(5):
                Metti(5);
                cambiata = true;
                break;
        }

        return cambiata ? string.Join(';', campi) : null;
    }
}
