using System.Text;

namespace PySharp.SyntaxAnalysis.Common.Ast;

public abstract record GreenNode : IGreenNode
{
    public virtual int FullWidth
    {
        get
        {
            if (Children is not null && Children.Count > 0 && field == default)
            {
                foreach (var child in Children)
                {
                    field += child.FullWidth;
                }
            }

            return field;
        }
    }

    public virtual int? TriviaWidth
    {
        get
        {
            if (field == null && Children != null && Children.Count != 0)
            {
                for (int i = 0; i < Children.Count; i++)
                {
                    if (Children[i].TriviaWidth is not null)
                    {
                        field = Children[i].TriviaWidth;
                        break;
                    }
                }
            }

            field ??= 0;

            return field;
        }
    }

    public abstract IRedView GetView(int position, IRedView? parent);

    public virtual INodeArray<IGreenNode>? Children { get; init; }

    public bool IsArray => false;

    public virtual string RecoverText()
    {
        if (Children is null)
            return string.Empty;

        var builder = new StringBuilder();
        foreach (var child in Children)
            child.AcceptRecoverText(builder);

        return builder.ToString();
    }

    public virtual void AcceptRecoverText(StringBuilder builder)
    {
        if (Children is null)
            return;

        foreach (var child in Children)
            child.AcceptRecoverText(builder);
    }

    public string PrettyPrint()
    {
        var builder = new StringBuilder();

        AcceptPrettyPrint(builder, 0);

        return builder.ToString();
    }

    private const string indentation_unit = "  ";

    public virtual void AcceptPrettyPrint(StringBuilder builder, int indentation)
    {
        builder.Append(GetType().Name);

        if (Children != null)
        {
            builder.AppendLine(" {");
            foreach (var (index, child) in Children.Index())
            {
                AddIndentation(builder, indentation + 1);
                builder.AppendFormat("Slot[{0}]: ", index);
                child.AcceptPrettyPrint(builder, indentation + 1);
                builder.AppendLine(",");
            }
            AddIndentation(builder, indentation);
            builder.Append('}');
        }
        else
        {
            builder.Append("()");
        }
    }

    internal static void AddIndentation(StringBuilder builder, int indentUnitsCount)
    {
        for (int i = 0; i < indentUnitsCount; i++)
        {
            builder.Append(indentation_unit);
        }
    }
}
