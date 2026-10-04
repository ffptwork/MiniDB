using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;
using MiniDB.Core.Models;

namespace MiniDB.Core.DataTypes;

public sealed class ColorType : DataType<Color>
{
    private static readonly Regex RgbPattern = new(
        @"\A\s*RGB\(\s*([0-9]+)\s*,\s*([0-9]+)\s*,\s*([0-9]+)\s*\)\s*\z",
        RegexOptions.CultureInvariant);

    public override string Name => "color";

    protected override bool TryParse(string? value, [MaybeNullWhen(false)] out Color parsed) =>
        TryParseColor(value, out parsed);

    internal static bool TryParseColor(string? value, [MaybeNullWhen(false)] out Color parsed)
    {
        parsed = null;

        if (value is null)
        {
            return false;
        }

        var match = RgbPattern.Match(value);

        if (!match.Success
            || !TryParseComponent(match.Groups[1].Value, out var red)
            || !TryParseComponent(match.Groups[2].Value, out var green)
            || !TryParseComponent(match.Groups[3].Value, out var blue))
        {
            return false;
        }

        parsed = new Color(red, green, blue);
        return true;
    }

    private static bool TryParseComponent(string value, out int component) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out component)
        && Color.IsValidComponent(component);
}
