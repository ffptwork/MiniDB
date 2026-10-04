using System.Globalization;

namespace MiniDB.Core.Models;

public sealed record Color
{
    public int Red { get; }
    public int Green { get; }
    public int Blue { get; }

    public Color(int red, int green, int blue)
    {
        ValidateComponent(red, nameof(red));
        ValidateComponent(green, nameof(green));
        ValidateComponent(blue, nameof(blue));

        Red = red;
        Green = green;
        Blue = blue;
    }

    internal static bool IsValidComponent(int component) => component is >= 0 and <= 255;

    private static void ValidateComponent(int component, string parameterName)
    {
        if (!IsValidComponent(component))
        {
            throw new ArgumentOutOfRangeException(parameterName, component, "RGB components must be between 0 and 255.");
        }
    }

    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"RGB({Red}, {Green}, {Blue})");
}
