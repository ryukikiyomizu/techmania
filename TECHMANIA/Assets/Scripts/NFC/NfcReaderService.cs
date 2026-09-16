using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

public enum CredentialEventType
{
    Present,
    Removed
}

public readonly struct CredentialObservation
{
    public CredentialObservation(
        CredentialEventType eventType,
        string source,
        string cardKind,
        string credentialKey,
        uint sequence)
    {
        EventType = eventType;
        Source = source;
        CardKind = cardKind;
        CredentialKey = credentialKey;
        Sequence = sequence;
    }

    public CredentialEventType EventType { get; }
    public string Source { get; }
    public string CardKind { get; }
    public string CredentialKey { get; }
    public uint Sequence { get; }
}

public sealed class NfcReaderService : IDisposable
{
    private readonly INfcNativeApi native;
    private readonly Queue<CredentialObservation> observations =
        new Queue<CredentialObservation>();
    private bool started;
    private bool disposed;

    public NfcReaderService()
        : this(new TechmaniaNfcNativeApi())
    {
    }

    public NfcReaderService(INfcNativeApi native)
    {
        this.native = native ?? throw new ArgumentNullException(nameof(native));
    }

    public bool PluginAvailable { get; private set; }
    public bool ReaderAvailable { get; private set; }
    public bool CardPresent { get; private set; }
    public string CurrentCredentialKey { get; private set; }
    public string LastError { get; private set; }

    public bool Start()
    {
        if (disposed) return false;
        if (started) return true;

        try
        {
            if (native.GetAbiVersion() != NativeNfcConstants.AbiVersion)
            {
                LastError = "NFC hook ABI mismatch.";
                return false;
            }

            int result = native.Start();
            if (result != NativeNfcConstants.Ok)
            {
                LastError = "NFC hook failed to start.";
                return false;
            }

            PluginAvailable = true;
            started = true;
            ReaderAvailable =
                native.GetReaderState() == NativeNfcConstants.ReaderConnected;
            LastError = null;
            return true;
        }
        catch (DllNotFoundException)
        {
            return Disable("NFC hook DLL is not installed.");
        }
        catch (EntryPointNotFoundException)
        {
            return Disable("NFC hook DLL exports are incompatible.");
        }
        catch (BadImageFormatException)
        {
            return Disable("NFC hook DLL architecture is incompatible.");
        }
        catch (SEHException)
        {
            return Disable("NFC hook failed inside native code.");
        }
    }

    public void Poll(int maximumEvents = 16)
    {
        if (!started || disposed || maximumEvents <= 0) return;

        try
        {
            ReaderAvailable =
                native.GetReaderState() == NativeNfcConstants.ReaderConnected;
            for (int index = 0; index < maximumEvents; ++index)
            {
                NativeNfcEvent eventData = NativeNfcEvent.CreateForPoll();
                int result = native.Poll(ref eventData);
                if (result <= 0)
                {
                    if (result < 0) LastError = "NFC hook poll failed.";
                    break;
                }
                Handle(eventData);
            }
        }
        catch (DllNotFoundException)
        {
            Disable("NFC hook DLL was removed.");
        }
        catch (EntryPointNotFoundException)
        {
            Disable("NFC hook DLL exports changed.");
        }
        catch (BadImageFormatException)
        {
            Disable("NFC hook DLL architecture changed.");
        }
        catch (SEHException)
        {
            Disable("NFC hook failed inside native code.");
        }
    }

    public bool TryDequeue(out CredentialObservation observation)
    {
        if (observations.Count == 0)
        {
            observation = default;
            return false;
        }

        observation = observations.Dequeue();
        return true;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (started)
        {
            try { native.Stop(); }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
            catch (BadImageFormatException) { }
            catch (SEHException) { }
        }
        started = false;
        PluginAvailable = false;
        ReaderAvailable = false;
        CardPresent = false;
        CurrentCredentialKey = null;
        observations.Clear();
    }

    private void Handle(NativeNfcEvent eventData)
    {
        switch (eventData.eventType)
        {
            case NativeNfcConstants.EventReaderConnected:
                ReaderAvailable = true;
                return;
            case NativeNfcConstants.EventReaderDisconnected:
                ReaderAvailable = false;
                if (CardPresent && !string.IsNullOrEmpty(CurrentCredentialKey))
                {
                    observations.Enqueue(new CredentialObservation(
                        CredentialEventType.Removed,
                        "nfc",
                        "unknown",
                        CurrentCredentialKey,
                        eventData.sequence));
                }
                CardPresent = false;
                CurrentCredentialKey = null;
                return;
            case NativeNfcConstants.EventCardPresent:
                EnqueueCard(eventData, CredentialEventType.Present);
                CardPresent = true;
                return;
            case NativeNfcConstants.EventCardRemoved:
                EnqueueCard(eventData, CredentialEventType.Removed);
                CardPresent = false;
                CurrentCredentialKey = null;
                return;
            case NativeNfcConstants.EventError:
                LastError = "NFC reader reported an error.";
                return;
        }
    }

    private void EnqueueCard(
        NativeNfcEvent eventData,
        CredentialEventType eventType)
    {
        string id = ToHex(eventData.CardIdBytes());
        string key = "nfc:cardio:" + id;
        CurrentCredentialKey = key;
        observations.Enqueue(new CredentialObservation(
            eventType,
            "nfc",
            CardKindName(eventData.cardKind),
            key,
            eventData.sequence));
    }

    private bool Disable(string reason)
    {
        LastError = reason;
        started = false;
        PluginAvailable = false;
        ReaderAvailable = false;
        CardPresent = false;
        CurrentCredentialKey = null;
        observations.Clear();
        return false;
    }

    private static string CardKindName(int kind)
    {
        switch (kind)
        {
            case NativeNfcConstants.CardMifare: return "mifare";
            case NativeNfcConstants.CardFelica: return "felica";
            case NativeNfcConstants.CardOther: return "other";
            default: return "unknown";
        }
    }

    private static string ToHex(byte[] bytes)
    {
        var output = new StringBuilder(bytes.Length * 2);
        for (int index = 0; index < bytes.Length; ++index)
            output.Append(bytes[index].ToString("X2"));
        return output.ToString();
    }
}
