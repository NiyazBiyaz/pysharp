namespace PySharp.Runtime.Objects;

public class PsFloat : PsObject
{
    private readonly double value;

    public PsFloat(double value)
        : base(PsConstants.Float)
    {
        this.value = value;
    }

    public static explicit operator double(PsFloat psFloat) => psFloat.value;
    public static explicit operator PsFloat(double clrFloat) => new(clrFloat);
}
