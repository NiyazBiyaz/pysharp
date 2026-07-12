using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace PySharp.SyntaxAnalysis.Generator;

internal record GroupIdentifier(string? DecoratorValue, HashableArray<AlternativeIdentifier> Alternatives);

internal record AlternativeIdentifier(
    HashableArray<string> Names,
    HashableArray<QuantifierKind> Quantifiers,
    string? ActionName,
    HashableArray<string> FieldNames,
    HashableArray<string> VariableNames);

internal readonly struct HashableArray<T>(IEnumerable<T> items) : IEquatable<HashableArray<T>>
    where T : notnull
{
    private readonly ImmutableArray<T> items = [.. items];

    public override bool Equals([NotNullWhen(true)] object? obj) => obj is HashableArray<T> other && Equals(other);

    public bool Equals(HashableArray<T> other)
    {
        if (items.Length != other.items.Length)
            return false;

        for (int i = 0; i < items.Length; i++)
        {
            if (!items[i].Equals(other.items[i]))
                return false;
        }

        return true;
    }

    public override int GetHashCode()
    {
        HashCode hash = new();

        for (int i = 0; i < items.Length; i++)
        {
            hash.Add(items[i]);
        }

        return hash.ToHashCode();
    }
}
