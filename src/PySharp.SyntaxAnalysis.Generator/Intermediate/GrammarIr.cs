namespace PySharp.SyntaxAnalysis.Generator.Intermediate;

internal record GrammarIr(
    IEnumerable<IRuleIr> Rules,
    string Header,
    string ParseCallReturnType,
    string ClassSignature
);
