using System.Numerics;

namespace PySharp.Runtime.Objects;

public class PsInteger : PsObject
{
    private readonly BigInteger value;

    public PsInteger(BigInteger value)
        : base(PsConstants.Int)
    {
        this.value = value;
    }

    public PsInteger(long value)
        : base(PsConstants.Int)
    {
        this.value = new BigInteger(value);
    }

    public static explicit operator long(PsInteger integer) => checked((long)integer.value);
    public static explicit operator PsInteger(long integer) => new(integer);
}
