using MiniDB.Core.DataTypes;
using MiniDB.Core.Models;
using MiniDB.Core.Validation;

namespace MiniDB.Server.Persistence;

internal static class DatabaseStorageMapper
{
    private static readonly TableValidator TableValidator = new();
    private static readonly RowValidator RowValidator = new();

    public static DatabaseStorageDto ToDto(Database database)
    {
        ArgumentNullException.ThrowIfNull(database);
        var tables = new List<TableStorageDto>();

        foreach (var table in database.Tables)
        {
            TableValidator.Validate(table);
            var types = table.Columns.ToDictionary(column => column.Name,
                column => DataTypeFactory.Create(column.DataType.Name), StringComparer.Ordinal);
            var rows = new List<RowStorageDto>();

            foreach (var row in table.Rows)
            {
                RowValidator.Validate(table, row);
                var values = new Dictionary<string, string>(StringComparer.Ordinal);

                foreach (var column in table.Columns)
                {
                    values.Add(column.Name, ValueTextFormatter.Format(types[column.Name], row.GetValue(column.Name)));
                }

                rows.Add(new RowStorageDto { Values = values });
            }

            tables.Add(new TableStorageDto
            {
                Name = table.Name,
                Columns = table.Columns.Select(column => new ColumnStorageDto
                {
                    Name = column.Name,
                    DataTypeName = column.DataType.Name
                }).ToList(),
                Rows = rows
            });
        }

        return new DatabaseStorageDto { Name = database.Name, Tables = tables };
    }

    public static Database FromDto(DatabaseStorageDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ArgumentNullException.ThrowIfNull(dto.Tables);
        var database = new Database(dto.Name);

        foreach (var tableDto in dto.Tables)
        {
            ArgumentNullException.ThrowIfNull(tableDto);
            ArgumentNullException.ThrowIfNull(tableDto.Columns);
            ArgumentNullException.ThrowIfNull(tableDto.Rows);
            var table = new Table(tableDto.Name);

            foreach (var columnDto in tableDto.Columns)
            {
                ArgumentNullException.ThrowIfNull(columnDto);
                table.AddColumn(new Column(columnDto.Name, DataTypeFactory.Create(columnDto.DataTypeName)));
            }

            TableValidator.Validate(table);
            var names = table.Columns.Select(column => column.Name).ToHashSet(StringComparer.Ordinal);

            foreach (var rowDto in tableDto.Rows)
            {
                ArgumentNullException.ThrowIfNull(rowDto);
                ArgumentNullException.ThrowIfNull(rowDto.Values);

                if (rowDto.Values.Count != table.Columns.Count || rowDto.Values.Keys.Any(name => !names.Contains(name)))
                {
                    throw new ArgumentException($"Row values do not match the schema of table '{table.Name}'.", nameof(dto));
                }

                // Parse through the domain API, preserving the order of the schema, not the JSON keys.
                var row = table.AddRow(table.Columns.Select(column => rowDto.Values[column.Name]));
                RowValidator.Validate(table, row);
            }

            database.AddTable(table);
        }

        return database;
    }

}
