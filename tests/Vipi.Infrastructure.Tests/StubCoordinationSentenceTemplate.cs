using Vipi.Application.Content;

namespace Vipi.Infrastructure.Tests;

/// <summary>Template della frase di coordinamento fisso (default hardcoded) per i test.</summary>
internal sealed class StubCoordinationSentenceTemplate : ICoordinationSentenceTemplate
{
    public CoordinationSentenceTemplate Current => CoordinationSentenceTemplate.Default;
}
