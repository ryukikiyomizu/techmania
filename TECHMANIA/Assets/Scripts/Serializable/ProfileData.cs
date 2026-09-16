using System;
using System.Collections.Generic;

// profile.json - per-profile identity and credential aliases.
// Play records, statistics, and player options remain in their own files.

[Serializable]
[FormatVersion(ProfileDataV1.kVersion,
    typeof(ProfileDataV1), isLatest: false)]
[FormatVersion(ProfileData.kVersion,
    typeof(ProfileData), isLatest: true)]
public class ProfileDataBase : SerializableClass<ProfileDataBase>
{
    // ProfileManager owns the profile-specific save path.
}

[Serializable]
public class ProfileCredential
{
    public string key;
    public string linkedAt;
}

[Serializable]
public class ProfileDataV1 : ProfileDataBase
{
    public const string kVersion = "1";

    public string name;
    public string cardId;
    public string createdAt;
    public long userExp;

    public ProfileDataV1()
    {
        version = kVersion;
    }

    protected override ProfileDataBase Upgrade()
    {
        var upgraded = new ProfileData
        {
            profileId = Guid.NewGuid().ToString("N"),
            name = name,
            cardId = cardId,
            createdAt = createdAt,
            userExp = userExp,
            credentials = new List<ProfileCredential>()
        };

        ProfileCredentials.TryLink(upgraded,
            ProfileCredentials.NormalizeLegacy(cardId), createdAt);
        return upgraded;
    }
}

[Serializable]
public class ProfileData : ProfileDataBase
{
    public const string kVersion = "2";

    public string profileId;
    public string name;
    // Kept during the migration window so older builds can still identify
    // the profile. New authentication uses credentials instead.
    public string cardId;
    public string createdAt;
    public long userExp;
    public List<ProfileCredential> credentials;

    public ProfileData()
    {
        version = kVersion;
        profileId = Guid.NewGuid().ToString("N");
        credentials = new List<ProfileCredential>();
    }

    protected override void InitAfterDeserialize()
    {
        if (string.IsNullOrEmpty(profileId))
            profileId = Guid.NewGuid().ToString("N");
        ProfileCredentials.NormalizeInPlace(this);
    }

    protected override void PrepareToSerialize()
    {
        ProfileCredentials.NormalizeInPlace(this);
    }
}
