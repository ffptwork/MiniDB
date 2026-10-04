using System.Globalization;

namespace MiniDB.Core.DataTypes;

public sealed class IntegerType : DataType<int>
{
    public override string Name => "integer";

    protected override bool TryParse(string? value, out int parsed) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed);
}
