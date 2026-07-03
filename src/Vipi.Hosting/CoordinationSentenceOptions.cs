using Microsoft.Extensions.Options;
using Vipi.Application.Content;

namespace Vipi.Hosting;

/// <summary>Binding del file «content/coordination-sentence.json» (sezione «CoordinationSentence»).
/// Campi nullable: un file parziale lascia i default. Vedi <see cref="CoordinationSentenceTemplate"/>.</summary>
public sealed class CoordinationSentenceOptions
{
    public const string SectionName = "CoordinationSentence";

    public string? Template { get; set; }
    public string? TargetWithCode { get; set; }
    public string? TargetNoCode { get; set; }
    public string? Airport { get; set; }
    public StateWords? Stato { get; set; }
    public string? FallbackMissingPoint { get; set; }

    public sealed class StateWords
    {
        public string? AtOrBelow { get; set; }
        public string? AtOrAbove { get; set; }
        public string? Exact { get; set; }
        public string? Special { get; set; }
    }
}

/// <summary>Espone il template corrente della frase di coordinamento, con hot-reload dal file (IOptionsMonitor).
/// I campi mancanti nel file ricadono sul default hardcoded di <see cref="CoordinationSentenceTemplate.Default"/>.</summary>
public sealed class CoordinationSentenceTemplateProvider : ICoordinationSentenceTemplate
{
    private readonly IOptionsMonitor<CoordinationSentenceOptions> _mon;

    public CoordinationSentenceTemplateProvider(IOptionsMonitor<CoordinationSentenceOptions> mon) => _mon = mon;

    public CoordinationSentenceTemplate Current
    {
        get
        {
            var o = _mon.CurrentValue;
            var d = CoordinationSentenceTemplate.Default;
            return new CoordinationSentenceTemplate
            {
                Template = Fallback(o.Template, d.Template),
                TargetWithCode = Fallback(o.TargetWithCode, d.TargetWithCode),
                TargetNoCode = Fallback(o.TargetNoCode, d.TargetNoCode),
                Airport = Fallback(o.Airport, d.Airport),
                FallbackMissingPoint = Fallback(o.FallbackMissingPoint, d.FallbackMissingPoint),
                Stato = new CoordinationSentenceState
                {
                    // Special ha default "" (vuoto): usa il valore del file se presente (anche stringa vuota).
                    AtOrBelow = Fallback(o.Stato?.AtOrBelow, d.Stato.AtOrBelow),
                    AtOrAbove = Fallback(o.Stato?.AtOrAbove, d.Stato.AtOrAbove),
                    Exact = Fallback(o.Stato?.Exact, d.Stato.Exact),
                    Special = o.Stato?.Special ?? d.Stato.Special,
                },
            };
        }
    }

    private static string Fallback(string? value, string dflt) => string.IsNullOrWhiteSpace(value) ? dflt : value;
}
