using NUnit.Framework;

public class DjProgressionTests
{
    [TestCase(0, 1)]
    [TestCase(219, 1)]
    [TestCase(220, 2)]
    [TestCase(5999, 19)]
    [TestCase(6000, 20)]
    [TestCase(149998, 48)]
    [TestCase(149999, 49)]
    [TestCase(150000, 50)]
    [TestCase(999999, 50)]
    public void LevelForExp_UsesDmt2CumulativeThresholds(long exp, int expected)
    {
        Assert.That(DjProgression.LevelForExp(exp), Is.EqualTo(expected));
    }

    [TestCase(1, "S++", 7)]
    [TestCase(1, "A++", 6)]
    [TestCase(1, "B", 5)]
    [TestCase(1, "F", 1)]
    [TestCase(5, "S+", 9)]
    [TestCase(10, "S++", 12)]
    [TestCase(10, "C", 8)]
    [TestCase(0, "S++", 0)]
    [TestCase(99, "S++", 12)]
    public void ExpForResult_UsesDmt2ChartLevelAndRankTable(
        int chartLevel, string rank, int expected)
    {
        Assert.That(DjProgression.ExpForResult(chartLevel, rank),
            Is.EqualTo(expected));
    }

    [Test]
    public void ApplyAward_NeverProducesNegativeExperience()
    {
        Assert.That(DjProgression.ApplyAward(-100, 1, "F"), Is.EqualTo(1));
    }

    [Test]
    public void ProfileData_NewProfileStartsAtZeroExperience()
    {
        Assert.That(new ProfileData().userExp, Is.EqualTo(0));
    }
}
