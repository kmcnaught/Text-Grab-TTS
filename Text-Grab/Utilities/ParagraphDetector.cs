using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Text_Grab.Models;
using Windows.Foundation;

namespace Text_Grab.Utilities;

public class ParagraphGroup
{
    public List<IOcrLine> Lines { get; } = new();
    public Rect BoundingBox { get; private set; }

    public void AddLine(IOcrLine line)
    {
        Lines.Add(line);
        double left = Lines.Min(l => l.BoundingBox.Left);
        double top = Lines.Min(l => l.BoundingBox.Top);
        double right = Lines.Max(l => l.BoundingBox.Right);
        double bottom = Lines.Max(l => l.BoundingBox.Bottom);
        BoundingBox = new Rect(left, top, right - left, bottom - top);
    }

    public string GetText(bool isSpaceJoining)
    {
        StringBuilder sb = new();
        foreach (IOcrLine line in Lines)
            line.GetTextFromOcrLine(isSpaceJoining, sb);
        return sb.ToString().TrimEnd();
    }
}

public static class ParagraphDetector
{
    public static List<ParagraphGroup> GroupLinesIntoParagraphs(IOcrLine[] lines)
    {
        List<ParagraphGroup> result = new();

        if (lines.Length == 0)
            return result;

        IOcrLine[] sorted = lines.OrderBy(l => l.BoundingBox.Top).ToArray();

        double[] lineHeights = sorted
            .Where(l => l.BoundingBox.Height > 0)
            .Select(l => l.BoundingBox.Height)
            .OrderBy(h => h)
            .ToArray();

        double medianLineHeight = lineHeights.Length > 0
            ? lineHeights[lineHeights.Length / 2]
            : 20.0;

        double gapThreshold = 1.5 * medianLineHeight;

        ParagraphGroup current = new();
        current.AddLine(sorted[0]);

        for (int i = 1; i < sorted.Length; i++)
        {
            double gap = sorted[i].BoundingBox.Top - sorted[i - 1].BoundingBox.Bottom;

            if (gap > gapThreshold)
            {
                result.Add(current);
                current = new ParagraphGroup();
            }

            current.AddLine(sorted[i]);
        }

        result.Add(current);
        return result;
    }
}
