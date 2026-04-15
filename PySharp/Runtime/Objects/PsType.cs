namespace PySharp.Runtime.Objects;

public class PsType : PsObject
{
    public string DunderName { get; }
    public PsType[] DunderBases { get; }

    internal PsType(string name, PsType[] bases)
    {
        DunderName = name;
        DunderBases = bases;
    }

    public PsType(string name, PsType[] bases, PsType type)
        : base(type)
    {
        DunderName = name;
        DunderBases = bases;
    }
}
