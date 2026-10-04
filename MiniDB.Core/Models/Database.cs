namespace MiniDB.Core.Models;

public sealed class Database
{
    private readonly List<Table> _tables = [];

    public string Name { get; }
    public IReadOnlyList<Table> Tables { get; }

    public Database(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name;
        Tables = _tables.AsReadOnly();
    }

    public void AddTable(Table table)
    {
        ArgumentNullException.ThrowIfNull(table);

        if (_tables.Any(existing => existing.Name == table.Name))
        {
            throw new ArgumentException($"Table '{table.Name}' already exists.", nameof(table));
        }

        _tables.Add(table);
    }

    public bool RemoveTable(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var table = _tables.FirstOrDefault(table => table.Name == name);

        return table is not null && _tables.Remove(table);
    }

    public Table GetTable(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return _tables.FirstOrDefault(table => table.Name == name)
            ?? throw new KeyNotFoundException($"Table '{name}' does not exist in database '{Name}'.");
    }
}
