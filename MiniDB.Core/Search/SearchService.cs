using MiniDB.Core.DataTypes;
using MiniDB.Core.Models;

namespace MiniDB.Core.Search;


public sealed class SearchService
{
    public IReadOnlyList<Row> Search(Table table, string columnName, string pattern)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(pattern);
        var column = table.GetColumn(columnName);

        if (column.DataType is StringType or CharType)
        {
            var parts = pattern.Split('*');

            return table.Rows
                .Where(row => MatchesPattern(row.GetValue(column.Name), parts))
                .ToList()
                .AsReadOnly();
        }

        // Parse once, even when the table has no rows, so invalid patterns always fail.
        var expected = column.DataType.Parse(pattern);

        return table.Rows
            .Where(row => Equals(row.GetValue(column.Name), expected))
            .ToList()
            .AsReadOnly();
    }

    private static bool MatchesPattern(object value, string[] parts)
    {
        var text = value is string stringValue ? stringValue : ((char)value).ToString();

        if (parts.Length == 1)
        {
            return string.Equals(text, parts[0], StringComparison.Ordinal);
        }

        if (!text.StartsWith(parts[0], StringComparison.Ordinal)
            || !text.EndsWith(parts[^1], StringComparison.Ordinal))
        {
            return false;
        }

        var position = parts[0].Length;
        var end = text.Length - parts[^1].Length;

        if (position > end)
        {
            return false;
        }

        // Reserve the suffix so literal fragments cannot overlap each other.
        for (var index = 1; index < parts.Length - 1; index++)
        {
            var part = parts[index];

            if (part.Length == 0)
            {
                continue;
            }

            var match = text.IndexOf(part, position, end - position, StringComparison.Ordinal);

            if (match < 0)
            {
                return false;
            }

            position = match + part.Length;
        }

        return true;
    }
}
