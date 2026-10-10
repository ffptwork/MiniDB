namespace MiniDB.Core.DataTypes;


public static class DataTypeFactory
{
    public static IDataType Create(string typeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(typeName);

        return typeName switch
        {
            "integer" => new IntegerType(),
            "real" => new RealType(),
            "char" => new CharType(),
            "string" => new StringType(),
            "color" => new ColorType(),
            "colorInvl" => new ColorIntervalType(),
            _ => throw new ArgumentException($"Unknown data type '{typeName}'.", nameof(typeName))
        };
    }
}
