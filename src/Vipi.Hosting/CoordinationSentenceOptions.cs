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
    public string? AirportArrival { get; set; }
    public string? AirportDeparture { get; set; }
    public StateWords? Stato { get; set; }
    public string? FallbackMissingPoint { get; set; }
    public string? FallbackAllPoints { get; set; }
    public string? FallbackAllToward { get; set; }

    /// <summary>Forma della frase quando autorizzazione e trasferimento sono due eventi (ACC→APP).</summary>
    public string? TemplateCleared { get; set; }
    public HandoffWords? Handoff { get; set; }
    public SpeedWords? Speed { get; set; }
    /// <summary>Testo della riga «negli altri casi» di un gruppo di varianti.</summary>
    public string? Otherwise { get; set; }

    public sealed class StateWords
    {
        public string? Descending { get; set; }
        public string? Climbing { get; set; }
        public string? Level { get; set; }
    }

    public sealed class HandoffWords
    {
        public string? Point { get; set; }
        public string? AorBoundary { get; set; }
        public string? Custom { get; set; }
        public string? LevelPassing { get; set; }
        public string? LevelAtOrBelow { get; set; }
        public string? LevelAtOrAbove { get; set; }
        public string? Comms { get; set; }
    }

    public sealed class SpeedWords
    {
        public string? AtOrBelow { get; set; }
        public string? AtOrAbove { get; set; }
        public string? Exact { get; set; }
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
                AirportArrival = Fallback(o.AirportArrival, d.AirportArrival),
                AirportDeparture = Fallback(o.AirportDeparture, d.AirportDeparture),
                FallbackMissingPoint = Fallback(o.FallbackMissingPoint, d.FallbackMissingPoint),
                FallbackAllPoints = Fallback(o.FallbackAllPoints, d.FallbackAllPoints),
                FallbackAllToward = Fallback(o.FallbackAllToward, d.FallbackAllToward),
                Stato = new CoordinationSentenceState
                {
                    Descending = Fallback(o.Stato?.Descending, d.Stato.Descending),
                    Climbing = Fallback(o.Stato?.Climbing, d.Stato.Climbing),
                    Level = Fallback(o.Stato?.Level, d.Stato.Level),
                },
                TemplateCleared = Fallback(o.TemplateCleared, d.TemplateCleared),
                Otherwise = Fallback(o.Otherwise, d.Otherwise),
                Handoff = new CoordinationSentenceHandoff
                {
                    Point = Fallback(o.Handoff?.Point, d.Handoff.Point),
                    AorBoundary = Fallback(o.Handoff?.AorBoundary, d.Handoff.AorBoundary),
                    Custom = Fallback(o.Handoff?.Custom, d.Handoff.Custom),
                    LevelPassing = Fallback(o.Handoff?.LevelPassing, d.Handoff.LevelPassing),
                    LevelAtOrBelow = Fallback(o.Handoff?.LevelAtOrBelow, d.Handoff.LevelAtOrBelow),
                    LevelAtOrAbove = Fallback(o.Handoff?.LevelAtOrAbove, d.Handoff.LevelAtOrAbove),
                    Comms = Fallback(o.Handoff?.Comms, d.Handoff.Comms),
                },
                Speed = new CoordinationSentenceSpeed
                {
                    AtOrBelow = Fallback(o.Speed?.AtOrBelow, d.Speed.AtOrBelow),
                    AtOrAbove = Fallback(o.Speed?.AtOrAbove, d.Speed.AtOrAbove),
                    Exact = Fallback(o.Speed?.Exact, d.Speed.Exact),
                },
            };
        }
    }

    private static string Fallback(string? value, string dflt) => string.IsNullOrWhiteSpace(value) ? dflt : value;
}
