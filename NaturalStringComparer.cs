using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace FlipRAR
{
  // Nullable-aware comparer so LINQ OrderBy(..., comparer) is happy
  public sealed class NaturalStringComparer : IComparer<string?>
  {
    public static readonly NaturalStringComparer Instance = new();
    private static readonly Regex Chunk = new(@"(\d+)|(\D+)", RegexOptions.Compiled);

    public int Compare(string? x, string? y)
    {
      if (ReferenceEquals(x, y)) return 0;
      if (x is null) return -1;
      if (y is null) return 1;

      var mx = Chunk.Matches(x);
      var my = Chunk.Matches(y);

      for (int i = 0; i < mx.Count && i < my.Count; i++)
      {
        var a = mx[i].Value;
        var b = my[i].Value;

        var aIsNum = int.TryParse(a, out var ai);
        var bIsNum = int.TryParse(b, out var bi);

        int cmp = (aIsNum && bIsNum)
            ? ai.CompareTo(bi)
            : string.Compare(a, b, System.StringComparison.OrdinalIgnoreCase);

        if (cmp != 0) return cmp;
      }
      return mx.Count.CompareTo(my.Count);
    }
  }
}
