using System.Globalization;

namespace MiniDB.Core.DataTypes;

public sealed class RealType : DataType<double>
{
    public override string Name => "real";

    protected override bool TryParse(string? value, out double parsed) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed)
        && IsValidValue(parsed);

    protected override bool IsValidValue(double value) => double.IsFinite(value);
}
