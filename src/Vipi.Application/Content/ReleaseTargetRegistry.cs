using Vipi.Application.Abstractions;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <inheritdoc cref="IReleaseTargetRegistry"/>
public sealed class ReleaseTargetRegistry : IReleaseTargetRegistry
{
    private readonly Dictionary<ReleaseTargetType, IReleaseTarget> _byType;

    public ReleaseTargetRegistry(IEnumerable<IReleaseTarget> targets)
    {
        var list = targets.ToList();
        _byType = list.ToDictionary(t => t.Type);
        ByDescribeOrder = list.OrderBy(t => t.DescribeOrder).ToList();
    }

    public IReadOnlyList<IReleaseTarget> ByDescribeOrder { get; }

    public IReleaseTarget For(ReleaseTargetType type) => _byType[type];
}
