namespace PySharp.Runtime.Objects;

public class PsObject
{
    public PsType DunderClass { get; internal set; }

    public PsObject(PsType type)
    {
        DunderClass = type;
    }

    internal PsObject()
    {
        DunderClass = null!;
    }
}
