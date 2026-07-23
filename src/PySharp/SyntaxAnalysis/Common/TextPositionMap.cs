namespace PySharp.SyntaxAnalysis.Common;

public class TextPositionMap
{
    // Maybe faster way exists, but not now.
    private readonly List<int> linefeedPositions = [];

    /// <summary>
    /// Append given <paramref name="linefeed"/> position to the collection of positions.
    /// <paramref name="linefeed"/> should have position that greater than last appended element.
    /// </summary>
    public void Append(int linefeed)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(linefeed, nameof(linefeed));

        if (linefeedPositions.Count > 0 && linefeed <= linefeedPositions[^1])
        {
            throw new ArgumentOutOfRangeException(nameof(linefeed), $"Append linefeed in ascending order ({linefeed}).");
        }

        linefeedPositions.Add(linefeed);
    }

    public void Clear() => linefeedPositions.Clear();

    /// <summary>
    /// Get line of the given position based on the previously added linefeed positions.
    /// </summary>
    public int GetLineForPosition(int position)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(position, nameof(position));

        int index = linefeedPositions.BinarySearch(position);

        return index < 0 ? ~index : index + 1;
    }

    public int GetColumnForPosition(int position)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(position, nameof(position));

        int index = linefeedPositions.BinarySearch(position);

        return index switch
        {
            -1 => position,
            < 0 => position - linefeedPositions[~index - 1],
            _ => 0,
        };
    }

    public Position2D GetPosition2D(int position) => new()
    {
        Line = GetLineForPosition(position),
        Column = GetColumnForPosition(position),
    };
}
