using MiniDB.Core.Models;

namespace MiniDB.Core.Validation;

/// <summary>Checks stored typed values against the target table schema.</summary>
public sealed class RowValidator
{
    private readonly TableValidator _tableValidator = new();

    public void Validate(Table table, Row row)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(row);
        _tableValidator.Validate(table);

        var columns = table.Columns.ToDictionary(column => column.Name, StringComparer.Ordinal);

        foreach (var name in row.Values.Keys)
        {
            if (!columns.ContainsKey(name))
            {
                throw new ArgumentException($"Row contains unknown column '{name}'.", nameof(row));
            }
        }

        foreach (var column in table.Columns)
        {
            if (!row.Values.TryGetValue(column.Name, out var value))
            {
                throw new ArgumentException($"Row is missing a value for column '{column.Name}'.", nameof(row));
            }

            if (!column.DataType.ValidateValue(value))
            {
                throw new ArgumentException($"Value for column '{column.Name}' does not match type '{column.DataType.Name}'.", nameof(row));
            }
        }

        if (row.Values.Count != table.Columns.Count)
        {
            throw new ArgumentException("The number of values must match the number of columns.", nameof(row));
        }
    }
}
