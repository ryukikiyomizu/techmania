using System;
using System.Collections.Generic;

public static class ProfileCredentials
{
    public const string UsbPrefix = "usb:token:";
    public const string NfcPrefix = "nfc:cardio:";
    public const string LegacyPrefix = "legacy:card:";

    public static string NormalizeUsb(string token)
    {
        string value = RemovePrefix(token, UsbPrefix);
        return string.IsNullOrWhiteSpace(value)
            ? null
            : UsbPrefix + value.Trim().ToLowerInvariant();
    }

    public static string NormalizeNfc(string cardId)
    {
        string value = RemovePrefix(cardId, NfcPrefix);
        if (string.IsNullOrWhiteSpace(value)) return null;
        value = value.Trim();
        if (value.Length != 16 || !IsHex(value)) return null;
        return NfcPrefix + value.ToUpperInvariant();
    }

    public static string NormalizeLegacy(string cardId)
    {
        if (string.IsNullOrWhiteSpace(cardId)) return null;
        string trimmed = cardId.Trim();
        if (StartsWith(trimmed, UsbPrefix)) return NormalizeUsb(trimmed);
        if (StartsWith(trimmed, NfcPrefix)) return NormalizeNfc(trimmed);
        if (StartsWith(trimmed, LegacyPrefix))
            trimmed = RemovePrefix(trimmed, LegacyPrefix);

        if (trimmed.Length == 32 && IsHex(trimmed))
            return NormalizeUsb(trimmed);
        return LegacyPrefix + trimmed.ToLowerInvariant();
    }

    public static string NormalizePresented(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        string trimmed = key.Trim();
        if (StartsWith(trimmed, UsbPrefix)) return NormalizeUsb(trimmed);
        if (StartsWith(trimmed, NfcPrefix)) return NormalizeNfc(trimmed);
        if (StartsWith(trimmed, LegacyPrefix)) return NormalizeLegacy(trimmed);
        return NormalizeLegacy(trimmed);
    }

    public static IEnumerable<string> LookupCandidates(string presented)
    {
        if (string.IsNullOrWhiteSpace(presented)) yield break;
        string trimmed = presented.Trim();

        if (StartsWith(trimmed, UsbPrefix))
        {
            string usb = NormalizeUsb(trimmed);
            if (usb != null) yield return usb;
            yield break;
        }
        if (StartsWith(trimmed, NfcPrefix))
        {
            string nfc = NormalizeNfc(trimmed);
            if (nfc != null) yield return nfc;
            yield break;
        }
        if (StartsWith(trimmed, LegacyPrefix))
        {
            string legacy = NormalizeLegacy(trimmed);
            if (legacy != null) yield return legacy;
            yield break;
        }

        if (trimmed.Length == 32 && IsHex(trimmed))
        {
            yield return NormalizeUsb(trimmed);
            yield return LegacyPrefix + trimmed.ToLowerInvariant();
            yield break;
        }

        if (trimmed.Length == 16 && IsHex(trimmed) &&
            !trimmed.StartsWith("E00401", StringComparison.OrdinalIgnoreCase))
            yield return NormalizeNfc(trimmed);

        yield return LegacyPrefix + trimmed.ToLowerInvariant();
    }

    public static bool Contains(ProfileData profile, string presented)
    {
        if (profile == null) return false;
        NormalizeInPlace(profile);
        foreach (string candidate in LookupCandidates(presented))
        {
            foreach (ProfileCredential credential in profile.credentials)
            {
                if (string.Equals(credential.key, candidate,
                    StringComparison.Ordinal)) return true;
            }
        }
        return !string.IsNullOrEmpty(profile.cardId) &&
            string.Equals(profile.cardId, presented,
                StringComparison.OrdinalIgnoreCase);
    }

    public static bool TryLink(ProfileData profile, string presented,
        string linkedAt = null)
    {
        if (profile == null) return false;
        string normalized = NormalizePresented(presented);
        if (normalized == null) return false;
        NormalizeInPlace(profile);
        foreach (ProfileCredential credential in profile.credentials)
        {
            if (string.Equals(credential.key, normalized,
                StringComparison.Ordinal)) return false;
        }
        profile.credentials.Add(new ProfileCredential
        {
            key = normalized,
            linkedAt = string.IsNullOrEmpty(linkedAt)
                ? DateTime.UtcNow.ToString("o")
                : linkedAt
        });
        return true;
    }

    public static string Kind(string credentialKey)
    {
        if (StartsWith(credentialKey, NfcPrefix)) return "nfc";
        if (StartsWith(credentialKey, UsbPrefix)) return "usb";
        if (StartsWith(credentialKey, LegacyPrefix)) return "legacy";
        return string.Empty;
    }

    public static void NormalizeInPlace(ProfileData profile)
    {
        if (profile == null) return;
        if (profile.credentials == null)
            profile.credentials = new List<ProfileCredential>();

        var normalized = new List<ProfileCredential>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (ProfileCredential credential in profile.credentials)
        {
            if (credential == null) continue;
            string key = NormalizePresented(credential.key);
            if (key == null || !seen.Add(key)) continue;
            normalized.Add(new ProfileCredential
            {
                key = key,
                linkedAt = credential.linkedAt
            });
        }
        profile.credentials = normalized;
    }

    private static string RemovePrefix(string value, string prefix)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return StartsWith(value, prefix) ? value.Substring(prefix.Length) : value;
    }

    private static bool StartsWith(string value, string prefix)
    {
        return value != null && value.StartsWith(prefix,
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsHex(string value)
    {
        foreach (char character in value)
        {
            bool digit = character >= '0' && character <= '9';
            bool lower = character >= 'a' && character <= 'f';
            bool upper = character >= 'A' && character <= 'F';
            if (!digit && !lower && !upper) return false;
        }
        return true;
    }
}
