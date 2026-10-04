namespace MiniDB.Core.Models;

public sealed record ColorInterval
{
    public Color MinColor { get; }
    public Color MaxColor { get; }

    public ColorInterval(Color minColor, Color maxColor)
    {
        ArgumentNullException.ThrowIfNull(minColor);
        ArgumentNullException.ThrowIfNull(maxColor);

        if (!IsOrdered(minColor, maxColor))
        {
            throw new ArgumentException("Each minimum RGB component must be less than or equal to its maximum.", nameof(maxColor));
        }

        MinColor = minColor;
        MaxColor = maxColor;
    }

    internal static bool IsOrdered(Color minColor, Color maxColor) =>
        minColor.Red <= maxColor.Red
        && minColor.Green <= maxColor.Green
        && minColor.Blue <= maxColor.Blue;

    public override string ToString() => $"[{MinColor}; {MaxColor}]";
}
