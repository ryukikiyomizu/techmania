using System.Runtime.InteropServices;

public static class NativeNfcConstants
{
    public const uint AbiVersion = 0x00010000u;
    public const int Ok = 0;

    public const int ReaderStopped = 0;
    public const int ReaderSearching = 1;
    public const int ReaderConnected = 2;
    public const int ReaderError = 3;

    public const int EventNone = 0;
    public const int EventReaderConnected = 1;
    public const int EventReaderDisconnected = 2;
    public const int EventCardPresent = 3;
    public const int EventCardRemoved = 4;
    public const int EventError = 5;

    public const int CardNone = 0;
    public const int CardMifare = 1;
    public const int CardFelica = 2;
    public const int CardOther = 3;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct NativeNfcEvent
{
    public uint structSize;
    public uint abiVersion;
    public uint sequence;
    public int eventType;
    public int cardKind;
    public byte cardId0;
    public byte cardId1;
    public byte cardId2;
    public byte cardId3;
    public byte cardId4;
    public byte cardId5;
    public byte cardId6;
    public byte cardId7;
    public int errorCode;
    public byte reserved0;
    public byte reserved1;
    public byte reserved2;
    public byte reserved3;
    public byte reserved4;
    public byte reserved5;
    public byte reserved6;
    public byte reserved7;
    public byte reserved8;
    public byte reserved9;
    public byte reserved10;
    public byte reserved11;
    public byte reserved12;
    public byte reserved13;
    public byte reserved14;
    public byte reserved15;
    public byte reserved16;
    public byte reserved17;
    public byte reserved18;
    public byte reserved19;
    public byte reserved20;
    public byte reserved21;
    public byte reserved22;
    public byte reserved23;
    public byte reserved24;
    public byte reserved25;
    public byte reserved26;
    public byte reserved27;

    public static NativeNfcEvent CreateForPoll()
    {
        return new NativeNfcEvent
        {
            structSize = (uint)Marshal.SizeOf<NativeNfcEvent>(),
            abiVersion = NativeNfcConstants.AbiVersion
        };
    }

    public byte[] CardIdBytes()
    {
        return new[]
        {
            cardId0, cardId1, cardId2, cardId3,
            cardId4, cardId5, cardId6, cardId7
        };
    }
}

public interface INfcNativeApi
{
    uint GetAbiVersion();
    int Start();
    void Stop();
    int Poll(ref NativeNfcEvent eventData);
    int GetReaderState();
}

internal sealed class TechmaniaNfcNativeApi : INfcNativeApi
{
    private const string DllName = "TechmaniaNfcHook";

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern uint tm_nfc_get_abi_version();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int tm_nfc_start();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern void tm_nfc_stop();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int tm_nfc_poll(ref NativeNfcEvent eventData);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int tm_nfc_get_reader_state();

    public uint GetAbiVersion() => tm_nfc_get_abi_version();
    public int Start() => tm_nfc_start();
    public void Stop() => tm_nfc_stop();
    public int Poll(ref NativeNfcEvent eventData) => tm_nfc_poll(ref eventData);
    public int GetReaderState() => tm_nfc_get_reader_state();
}

