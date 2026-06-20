using PySharp.SyntaxAnalysis.Common.Ast;

namespace PySharp.SyntaxAnalysis.Generator.Ast;

internal abstract record TargetNode : GreenNode;

internal record NameTargetNode(string Name) : TargetNode;

internal record GroupAxisTargetNode(string Name, string AxisName) : TargetNode;

internal record StringTargetNode(TargetNode TokenTarget) : TargetNode;

internal record ParseStringTargetNode(TargetNode TokenTarget) : TargetNode;

internal record ToArrayTargetNode(string Name) : TargetNode;

internal record BoolConstTargetNode(bool Value) : TargetNode;
