using System.Collections.ObjectModel;

namespace MiniDB.Core.Models;

public sealed class Row
{
    private readonly Column[] _columns;
    private readonly Dictionary<string, Column> _columnsByName;
    private readonly Dictionary<string, object> _values;

    public IReadOnlyDictionary<string, object> Values { get; }

    public Row(IEnumerable<Column> columns, IEnumerable<string> values)
    {
        ArgumentNullException.ThrowIfNull(columns);

        _columns = columns.ToArray();

        if (_columns.Length == 0)
        {
            throw new ArgumentException("A row must have at least one column.", nameof(columns));
        }

        _columnsByName = new Dictionary<string, Column>(StringComparer.Ordinal);

        foreach (var column in _columns)
        {
            ArgumentNullException.ThrowIfNull(column);

            if (!_columnsByName.TryAdd(column.Name, column))
            {
                throw new ArgumentException($"Duplicate column name '{column.Name}'.", nameof(columns));
            }
        }

        _values = ParseValues(values);
        Values = new ReadOnlyDictionary<string, object>(_values);
    }

    public object GetValue(string columnName)
    {
        var column = GetColumn(columnName);
        return _values[column.Name];
    }

    public void SetValue(string columnName, string value)
    {
        var column = GetColumn(columnName);
        _values[column.Name] = column.DataType.Parse(value);
    }

    internal void ReplaceValues(IEnumerable<string> values)
    {
        // Parse every value before changing the existing row.
        var parsedValues = ParseValues(values);

        foreach (var (name, value) in parsedValues)
        {
            _values[name] = value;
        }
    }

    private Dictionary<string, object> ParseValues(IEnumerable<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var inputs = values.ToArray();

        if (inputs.Length != _columns.Length)
        {
            throw new ArgumentException("The number of values must match the number of columns.", nameof(values));
        }

        var parsedValues = new Dictionary<string, object>(StringComparer.Ordinal);

        for (var index = 0; index < _columns.Length; index++)
        {
            var column = _columns[index];
            parsedValues.Add(column.Name, column.DataType.Parse(inputs[index]));
        }

        return parsedValues;
    }

    private Column GetColumn(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (!_columnsByName.TryGetValue(name, out var column))
        {
            throw new KeyNotFoundException($"Column '{name}' does not exist in the row.");
        }

        return column;
    }
}
