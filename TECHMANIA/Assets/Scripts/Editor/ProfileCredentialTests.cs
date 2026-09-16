using System.Linq;
using NUnit.Framework;

public class ProfileCredentialTests
{
    [Test]
    public void LegacyUsbProfileUpgradesToStableMultiCredentialModel()
    {
        const string json = "{\"version\":\"1\",\"name\":\"Shino\",\"cardId\":\"ABCDEF0123456789ABCDEF0123456789\",\"createdAt\":\"2026-08-15T00:00:00Z\",\"userExp\":123}";

        ProfileDataBase loaded = ProfileDataBase.Deserialize(json, out bool upgraded);
        var profile = loaded as ProfileData;

        Assert.That(upgraded, Is.True);
        Assert.That(profile, Is.Not.Null);
        Assert.That(profile.profileId, Is.Not.Null.And.Not.Empty);
        Assert.That(profile.credentials, Has.Count.EqualTo(1));
        Assert.That(profile.credentials[0].key,
            Is.EqualTo("usb:token:abcdef0123456789abcdef0123456789"));
        Assert.That(profile.cardId,
            Is.EqualTo("ABCDEF0123456789ABCDEF0123456789"),
            "The legacy field must remain readable during the migration window.");
    }

    [Test]
    public void NfcAndUsbCredentialsCanBelongToTheSameProfileWithoutDuplicates()
    {
        var profile = new ProfileData { name = "Shino" };

        Assert.That(ProfileCredentials.TryLink(profile,
            "nfc:cardio:012E5CE0E9C67778", "2026-08-15T00:00:00Z"), Is.True);
        Assert.That(ProfileCredentials.TryLink(profile,
            "usb:token:ABCDEF0123456789ABCDEF0123456789", "2026-08-15T00:00:01Z"), Is.True);
        Assert.That(ProfileCredentials.TryLink(profile,
            "NFC:CARDIO:012e5ce0e9c67778", "2026-08-15T00:00:02Z"), Is.False);

        Assert.That(profile.credentials.Select(item => item.key), Is.EquivalentTo(new[]
        {
            "nfc:cardio:012E5CE0E9C67778",
            "usb:token:abcdef0123456789abcdef0123456789"
        }));
    }

    [Test]
    public void LegacyLookupCandidatesPreserveOldProfilesWithoutTreatingThemAsNfc()
    {
        string[] candidates = ProfileCredentials.LookupCandidates(
            "E00401abcdef0123").ToArray();

        Assert.That(candidates, Does.Contain("legacy:card:e00401abcdef0123"));
        Assert.That(candidates, Does.Not.Contain("nfc:cardio:E00401ABCDEF0123"));
    }
}
