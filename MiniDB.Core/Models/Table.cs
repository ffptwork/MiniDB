namespace MiniDB.Core.Models;

public sealed class Table
{
    private readonly List<Column> _columns = [];
    private readonly List<Row> _rows = [];

    public string Name { get; }
    public IReadOnlyList<Column> Columns { get; }
    public IReadOnlyList<Row> Rows { get; }

    public Table(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name;
        Columns = _columns.AsReadOnly();
        Rows = _rows.AsReadOnly();
    }

    public void AddColumn(Column column)
    {
        ArgumentNullException.ThrowIfNull(column);

        if (_rows.Count != 0)
        {
            throw new InvalidOperationException("Columns must be added before rows.");
        }

        if (_columns.Any(existing => existing.Name == column.Name))
        {
            throw new ArgumentException($"Column '{column.Name}' already exists.", nameof(column));
        }

        _columns.Add(column);
    }

    /// <summary>Values follow the order of Columns and are parsed into their column types.</summary>
    public Row AddRow(IEnumerable<string> values)
    {
        if (_columns.Count == 0)
        {
            throw new InvalidOperationException("Add columns before adding rows.");
        }

        var row = new Row(_columns, values);
        _rows.Add(row);
        return row;
    }

    public void EditRow(int rowIndex, IEnumerable<string> values)
    {
        if (rowIndex < 0 || rowIndex >= _rows.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(rowIndex));
        }

        _rows[rowIndex].ReplaceValues(values);
    }

    public Column GetColumn(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return _columns.FirstOrDefault(column => column.Name == name)
            ?? throw new KeyNotFoundException($"Column '{name}' does not exist in table '{Name}'.");
    }

    public IReadOnlyList<Row> GetRows() => Rows;
}
