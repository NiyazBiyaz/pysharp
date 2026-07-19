using PySharp.SyntaxAnalysis.Tokens;

namespace PySharp.SyntaxAnalysis.Common.Ast;

public class VoidView(IGreenNode green, TokenPosition position, IRedView? parent) : RedView(green, position, parent);
