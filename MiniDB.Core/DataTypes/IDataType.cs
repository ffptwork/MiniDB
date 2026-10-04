namespace MiniDB.Core.DataTypes;

public interface IDataType
{
    string Name { get; }

    bool Validate(string? value);

    bool ValidateValue(object? value);

    object Parse(string value);
}
