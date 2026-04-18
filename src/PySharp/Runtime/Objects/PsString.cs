namespace PySharp.Runtime.Objects;

public class PsString : PsObject
{
    private readonly string value;

    public PsString(string value)
        : base(PsConstants.Str)
    {
        this.value = value;
    }

    public static explicit operator string(PsString str) => str.value;
    public static explicit operator PsString(string str) => new(str);
}
