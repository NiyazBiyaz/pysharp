namespace PySharp.Runtime.Objects;

public static class PsConstants
{
    public static PsType Object { get; }
    public static PsType Type { get; }
    public static PsType Str { get; }
    public static PsType Int { get; }
    public static PsType Float { get; }
    public static PsType Bool { get; }
    public static PsType NoneType { get; }

    public static PsBool True { get; }
    public static PsBool False { get; }
    public static PsNone None { get; }

    static PsConstants()
    {
        Object = new("object", []);
        Type = new("type", [Object]);

        Object.DunderClass = Type;
        Type.DunderClass = Type;

        Str = new("str", [Object], Type);
        Float = new("float", [Object], Type);
        Int = new("int", [Object], Type);
        Bool = new("bool", [Int], Type);

        NoneType = new("NoneType", [Object]);

        True = (PsBool)true;
        False = (PsBool)false;
        None = new PsNone();
    }
}
