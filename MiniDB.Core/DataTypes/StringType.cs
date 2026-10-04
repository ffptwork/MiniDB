using System.Diagnostics.CodeAnalysis;

namespace MiniDB.Core.DataTypes;

public sealed class StringType : DataType<string>
{
    public override string Name => "string";

    protected override bool TryParse(string? value, [MaybeNullWhen(false)] out string parsed)
    {
        parsed = value;
        return value is not null;
    }
}
