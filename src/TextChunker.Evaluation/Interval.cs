namespace TextChunker.Evaluation
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A half open character interval and helpers for computing union and intersection lengths.
    /// </summary>
    internal readonly struct Interval
    {
        internal int Start { get; }

        internal int End { get; }

        internal Interval(int start, int end)
        {
            Start = start;
            End = end;
        }

        internal int Length => Math.Max(0, End - Start);

        internal static int UnionLength(List<Interval> intervals)
        {
            if (intervals.Count == 0) return 0;

            intervals.Sort((a, b) => a.Start.CompareTo(b.Start));
            int total = 0;
            int currentStart = intervals[0].Start;
            int currentEnd = intervals[0].End;

            for (int i = 1; i < intervals.Count; i++)
            {
                Interval next = intervals[i];
                if (next.Start > currentEnd)
                {
                    total += Math.Max(0, currentEnd - currentStart);
                    currentStart = next.Start;
                    currentEnd = next.End;
                }
                else if (next.End > currentEnd)
                {
                    currentEnd = next.End;
                }
            }

            total += Math.Max(0, currentEnd - currentStart);
            return total;
        }

        internal static int IntersectionWithLength(List<Interval> intervals, Interval clip)
        {
            List<Interval> clipped = new List<Interval>();
            foreach (Interval interval in intervals)
            {
                int start = Math.Max(interval.Start, clip.Start);
                int end = Math.Min(interval.End, clip.End);
                if (end > start) clipped.Add(new Interval(start, end));
            }
            return UnionLength(clipped);
        }
    }
}
