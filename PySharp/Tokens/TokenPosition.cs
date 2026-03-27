using System.Diagnostics;

namespace PySharp.Tokens;

[DebuggerDisplay("({Line},{Column})")]
public readonly record struct TokenPosition(int Line, int Column);
