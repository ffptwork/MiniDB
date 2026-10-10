using System.Globalization;
using MiniDB.Core.DataTypes;
using MiniDB.Core.Models;

namespace MiniDB.Server;

internal static class ValueTextFormatter
{
    public static string Format(IDataType type, object value)
    {
        if (!type.ValidateValue(value))
        {
            throw new ArgumentException($"Значення не відповідає типу '{type.Name}'.", nameof(value));
        }

        return value switch
        {
            int integer => integer.ToString(CultureInfo.InvariantCulture),
            double real => real.ToString("R", CultureInfo.InvariantCulture),
            char character => character.ToString(),
            string text => text,
            Color color => color.ToString(),
            ColorInterval interval => interval.ToString(),
            _ => throw new ArgumentException($"Непідтримуваний тип значення '{value.GetType().Name}'.", nameof(value))
        };
    }
}
