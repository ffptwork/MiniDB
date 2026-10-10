using MiniDB.Core.Models;

namespace MiniDB.Server.Contracts;

internal static class ApiMapper
{
    public static DatabaseResponse ToResponse(Database database) =>
        new(database.Name, database.Tables.Select(ToResponse).ToArray());

    public static TableResponse ToResponse(Table table) =>
        new(table.Name,
            table.Columns.Select(column => new ColumnResponse(column.Name, column.DataType.Name)).ToArray(),
            table.Rows.Select((row, index) => ToResponse(table, row, index)).ToArray());

    public static RowResponse ToResponse(Table table, Row row, int index) =>
        new(index, table.Columns.ToDictionary(column => column.Name,
            column => ValueTextFormatter.Format(column.DataType, row.GetValue(column.Name)),
            StringComparer.Ordinal));

    public static string[] GetOrderedValues(Table table, Dictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var names = table.Columns.Select(column => column.Name).ToHashSet(StringComparer.Ordinal);
        if (values.Count != names.Count || values.Keys.Any(name => !names.Contains(name)))
        {
            throw new ArgumentException("Значення рядка мають точно відповідати колонкам таблиці.", nameof(values));
        }

        return table.Columns.Select(column => values[column.Name]
            ?? throw new ArgumentException($"Не задано значення колонки '{column.Name}'.", nameof(values))).ToArray();
    }
}
