using MiniDB.Core.Models;

namespace MiniDB.Core.Validation;


public sealed class TableValidator
{
    public void Validate(Table table)
    {
        ArgumentNullException.ThrowIfNull(table);
        ValidateTableName(table.Name);
        ValidateColumns(table.Columns);
    }

    public void ValidateTableName(string name) => ArgumentException.ThrowIfNullOrWhiteSpace(name);

    public void ValidateColumns(IEnumerable<Column> columns)
    {
        ArgumentNullException.ThrowIfNull(columns);
        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (var column in columns)
        {
            if (column is null)
            {
                throw new ArgumentException("Columns must not contain null entries.", nameof(columns));
            }

            ArgumentException.ThrowIfNullOrWhiteSpace(column.Name);

            if (!names.Add(column.Name))
            {
                throw new ArgumentException($"Duplicate column name '{column.Name}'.", nameof(columns));
            }

            if (column.DataType is null || string.IsNullOrWhiteSpace(column.DataType.Name))
            {
                throw new ArgumentException($"Column '{column.Name}' must have a data type with a nonempty name.", nameof(columns));
            }
        }

        if (names.Count == 0)
        {
            throw new ArgumentException("A table must have at least one column.", nameof(columns));
        }
    }
}
