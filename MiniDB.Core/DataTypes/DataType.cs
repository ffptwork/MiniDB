using System.Diagnostics.CodeAnalysis;

namespace MiniDB.Core.DataTypes;

/// <summary>Uses one parsing rule for both validation and conversion.</summary>
public abstract class DataType<T> : IDataType where T : notnull
{
    public abstract string Name { get; }

    public bool Validate(string? value) => TryParse(value, out _);

    public object Parse(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (!TryParse(value, out var parsed))
        {
            throw new FormatException($"Value is not a valid {Name}.");
        }

        return parsed;
    }

    protected abstract bool TryParse(string? value, [MaybeNullWhen(false)] out T parsed);
}
