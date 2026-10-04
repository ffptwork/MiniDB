namespace MiniDB.Core.DataTypes;

public sealed class CharType : DataType<char>
{
    public override string Name => "char";

    protected override bool TryParse(string? value, out char parsed)
    {
        parsed = default;

        if (value is not { Length: 1 })
        {
            return false;
        }

        parsed = value[0];
        return true;
    }
}
