using Vipi.Application.Content;
using Vipi.Domain;
using Vipi.Ui.Components.Doc;
using Xunit;

namespace Vipi.Ui.Tests;

/// <summary>
/// Il filtro del selettore dell'unione (carta <c>docs/feature/2026-09-03-documenti-uniti.md</c> §5).
///
/// <para>
/// ⚠️ <b>Perché un filtro e non un <c>&lt;input list&gt;</c> con <c>&lt;datalist&gt;</c></b>, che altrove in
/// questo repository è il modo di casa (alternati, radioassistenze, piste): quel modo lega la <b>chiave</b>,
/// e un aeroporto e il suo vSOP militare hanno la <b>stessa</b> chiave — l'ICAO. Scrivere «LIMN» non direbbe
/// quale dei due, ed è il caso <b>misto</b>, cioè quello per cui l'unione esiste. Qui il filtro restringe e
/// basta: l'identità resta l'id del documento.
/// </para>
/// </summary>
public class RicercaDeiCandidatiTests
{
    private static UnionCandidate C(int id, ReleaseTargetType tipo, string chiave, string titolo, bool stesso = false) =>
        new(new ManagedDoc(tipo, titolo, chiave, "LIRR", IsPublished: true, HasDraft: false, IsHidden: false,
                           tipo, chiave, id), stesso);

    private static readonly IReadOnlyList<UnionCandidate> Tutti = new[]
    {
        C(3,  ReleaseTargetType.App,        "LIBA_APP", "Amendola Approach", stesso: true),
        C(26, ReleaseTargetType.Airport,    "LIBA",     "vIPI — LIBA Amendola", stesso: true),
        C(28, ReleaseTargetType.Airport,    "LIMN",     "vIPI — LIMN Cameri"),
        C(29, ReleaseTargetType.AirportMil, "LIMN",     "vSOP MIL — LIMN Cameri"),
        C(6,  ReleaseTargetType.App,        "LIPE_W_APP", "Bologna Radar"),
    };

    [Fact]
    public void Senza_testo_torna_tutti()
    {
        Assert.Equal(5, UnionPanel.Filtra(Tutti, null).Count);
        Assert.Equal(5, UnionPanel.Filtra(Tutti, "   ").Count);
    }

    [Fact]
    public void Cerca_nel_titolo_e_nella_CHIAVE()
    {
        Assert.Equal(new[] { 6 }, UnionPanel.Filtra(Tutti, "bologna").Select(c => c.DocumentId));
        // la chiave, che nel titolo non compare
        Assert.Equal(new[] { 6 }, UnionPanel.Filtra(Tutti, "LIPE_W").Select(c => c.DocumentId));
    }

    /// <summary>⚠️ Le parole si cercano TUTTE e in qualunque ordine: «liba app» trova «Amendola Approach —
    /// LIBA_APP», che scrivendo il solo titolo o la sola chiave non si troverebbe.</summary>
    [Fact]
    public void Piu_parole_in_qualunque_ordine_e_tutte()
    {
        Assert.Equal(new[] { 3 }, UnionPanel.Filtra(Tutti, "liba app").Select(c => c.DocumentId));
        Assert.Equal(new[] { 3 }, UnionPanel.Filtra(Tutti, "app liba").Select(c => c.DocumentId));
        Assert.Empty(UnionPanel.Filtra(Tutti, "liba bologna"));
    }

    [Fact]
    public void Non_guarda_le_maiuscole()
    {
        Assert.Equal(UnionPanel.Filtra(Tutti, "AMENDOLA").Select(c => c.DocumentId),
                     UnionPanel.Filtra(Tutti, "amendola").Select(c => c.DocumentId));
    }

    /// <summary>
    /// 🔴 Il caso che ha deciso la FORMA del comando: su un campo misto la chiave è la stessa per tutti e
    /// due i documenti. Cercare «LIMN» deve lasciarne <b>due</b>, non sceglierne uno — ed è il motivo per
    /// cui l'identità non può essere la chiave.
    /// </summary>
    [Fact]
    public void Su_un_campo_MISTO_la_chiave_non_basta_a_distinguere()
    {
        var trovati = UnionPanel.Filtra(Tutti, "LIMN");

        Assert.Equal(new[] { 28, 29 }, trovati.Select(c => c.DocumentId));
        Assert.Single(trovati.Select(c => c.Doc.ReleaseKey).Distinct());   // una chiave sola, due documenti
        // e a distinguerli è il titolo, che infatti filtra
        Assert.Equal(new[] { 29 }, UnionPanel.Filtra(Tutti, "limn mil").Select(c => c.DocumentId));
    }
}
