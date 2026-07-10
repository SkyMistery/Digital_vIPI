using Vipi.Application.Abstractions;
using Vipi.Domain;

namespace Vipi.Application.Content;

/// <inheritdoc cref="IReleaseTargetRegistry"/>
public sealed class ReleaseTargetRegistry : IReleaseTargetRegistry
{
    private readonly Dictionary<ReleaseTargetType, IReleaseTarget> _byType;
    private readonly Dictionary<ManagedDocKind, IReleaseTarget> _byKind;

    public ReleaseTargetRegistry(IEnumerable<IReleaseTarget> targets)
    {
        var list = targets.ToList();
        _byType = list.ToDictionary(t => t.Type);
        _byKind = list.ToDictionary(t => t.ManagedKind);
        ByDescribeOrder = list.OrderBy(t => t.DescribeOrder).ToList();
    }

    public IReadOnlyList<IReleaseTarget> ByDescribeOrder { get; }

    public IReleaseTarget For(ReleaseTargetType type) => _byType[type];

    public IReleaseTarget For(ManagedDocKind kind) => _byKind[kind];
}
