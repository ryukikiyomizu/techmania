using System;
using System.Collections.Generic;

// DJ level/EXP rules copied from the DMT2 arcade data tables:
// Script/Uidata/expTitle.csv and Script/Item/ResultExpPoint.csv.
public static class DjProgression
{
    public const int MaxLevel = 50;

    // Index is DJ level. Level 1 is the new-profile floor; subsequent values
    // are cumulative USEREXP thresholds from expTitle.csv.
    private static readonly long[] minimumExpForLevel =
    {
        0,
        0, 220, 420, 620, 820, 1100, 1300, 1500, 1750, 2000,
        2300, 2600, 3000, 3400, 3800, 4300, 4600, 5100, 5500, 6000,
        7000, 9000, 11000, 13000, 15000, 18000, 21000, 24000, 27000, 30000,
        33000, 36000, 39000, 42000, 46000, 50000, 55000, 60000, 65000, 70000,
        76000, 82000, 88000, 104000, 110000, 118000, 126000, 135000, 149999,
        150000
    };

    private static readonly Dictionary<string, int> rankColumns =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "S++", 0 }, { "S+", 1 }, { "S", 2 },
            { "A++", 3 }, { "A+", 4 }, { "A", 5 },
            { "B", 6 }, { "C", 7 }, { "F", 8 }
        };

    // Rows are chart levels 0..10; columns follow rankColumns above.
    private static readonly int[,] resultExp =
    {
        { 0, 0, 0, 0, 0, 0, 0, 0, 0 },
        { 7, 7, 7, 6, 6, 6, 5, 5, 1 },
        { 8, 7, 7, 7, 6, 6, 6, 5, 1 },
        { 8, 8, 7, 7, 7, 6, 6, 6, 1 },
        { 9, 8, 8, 7, 7, 7, 6, 6, 1 },
        { 9, 9, 8, 8, 7, 7, 7, 6, 1 },
        { 10, 9, 9, 8, 8, 7, 7, 7, 1 },
        { 10, 10, 9, 9, 8, 8, 7, 7, 1 },
        { 11, 10, 10, 9, 9, 8, 8, 7, 1 },
        { 11, 11, 10, 10, 9, 9, 8, 8, 1 },
        { 12, 11, 11, 10, 10, 9, 9, 8, 1 }
    };

    public static int LevelForExp(long exp)
    {
        exp = Math.Max(0, exp);
        for (int level = MaxLevel; level >= 2; level--)
        {
            if (exp >= minimumExpForLevel[level]) return level;
        }
        return 1;
    }

    public static long MinimumExpForLevel(int level)
    {
        level = Math.Max(1, Math.Min(MaxLevel, level));
        return minimumExpForLevel[level];
    }

    public static long MinimumExpForNextLevel(long exp)
    {
        int level = LevelForExp(exp);
        return level >= MaxLevel
            ? minimumExpForLevel[MaxLevel]
            : minimumExpForLevel[level + 1];
    }

    public static int ExpForResult(int chartLevel, string rank)
    {
        if (string.IsNullOrEmpty(rank) || !rankColumns.TryGetValue(rank, out int column))
            return 0;
        chartLevel = Math.Max(0, Math.Min(10, chartLevel));
        return resultExp[chartLevel, column];
    }

    public static long ApplyAward(long currentExp, int chartLevel, string rank)
    {
        long normalized = Math.Max(0, currentExp);
        int award = ExpForResult(chartLevel, rank);
        return normalized > long.MaxValue - award
            ? long.MaxValue
            : normalized + award;
    }
}
