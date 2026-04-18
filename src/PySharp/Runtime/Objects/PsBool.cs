namespace PySharp.Runtime.Objects;

public class PsBool : PsObject
{
    private readonly bool value;

    public PsBool(bool value)
        : base(PsConstants.Bool)
    {
        this.value = value;
    }

    public static explicit operator bool(PsBool psBool) => psBool.value;
    public static explicit operator PsBool(bool clrBool) => new(clrBool);
}
