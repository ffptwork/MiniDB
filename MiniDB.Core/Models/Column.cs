using MiniDB.Core.DataTypes;

namespace MiniDB.Core.Models;

public sealed class Column
{
    public string Name { get; }
    public IDataType DataType { get; }

    public Column(string name, IDataType dataType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(dataType);

        Name = name;
        DataType = dataType;
    }
}
