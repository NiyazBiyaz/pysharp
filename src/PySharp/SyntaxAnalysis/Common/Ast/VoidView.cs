namespace PySharp.SyntaxAnalysis.Common.Ast;

public class VoidView(IGreenNode green, int position, IRedView? parent) : RedView(green, position, parent);
