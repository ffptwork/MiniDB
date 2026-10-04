using System.Diagnostics.CodeAnalysis;
using MiniDB.Core.Models;

namespace MiniDB.Core.DataTypes;

public sealed class ColorIntervalType : DataType<ColorInterval>
{
    public override string Name => "colorInvl";

    protected override bool TryParse(string? value, [MaybeNullWhen(false)] out ColorInterval parsed)
    {
        parsed = null;

        if (value is null)
        {
            return false;
        }

        var text = value.Trim();

        if (text.Length < 2 || text[0] != '[' || text[^1] != ']')
        {
            return false;
        }

        var bounds = text[1..^1].Split(';');

        if (bounds.Length != 2
            || !ColorType.TryParseColor(bounds[0], out var minColor)
            || !ColorType.TryParseColor(bounds[1], out var maxColor)
            || !ColorInterval.IsOrdered(minColor, maxColor))
        {
            return false;
        }

        parsed = new ColorInterval(minColor, maxColor);
        return true;
    }
}
