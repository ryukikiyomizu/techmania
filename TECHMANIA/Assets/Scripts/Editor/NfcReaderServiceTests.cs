using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using NUnit.Framework;

public class NfcReaderServiceTests
{
    private sealed class FakeNativeApi : INfcNativeApi
    {
        private readonly Queue<NativeNfcEvent> events;

        public FakeNativeApi(params NativeNfcEvent[] events)
        {
            this.events = new Queue<NativeNfcEvent>(events);
        }

        public uint GetAbiVersion() => NativeNfcConstants.AbiVersion;
        public int Start() => NativeNfcConstants.Ok;
        public void Stop() { }
        public int GetReaderState() => NativeNfcConstants.ReaderConnected;

        public int Poll(ref NativeNfcEvent eventData)
        {
            if (events.Count == 0) return 0;
            eventData = events.Dequeue();
            return 1;
        }
    }

    private sealed class MissingNativeApi : INfcNativeApi
    {
        public uint GetAbiVersion() => throw new DllNotFoundException();
        public int Start() => throw new DllNotFoundException();
        public void Stop() { }
        public int Poll(ref NativeNfcEvent eventData) => 0;
        public int GetReaderState() => NativeNfcConstants.ReaderStopped;
    }

    [Test]
    public void NativeEvent_MatchesThePackedDllAbi()
    {
        Assert.That(Marshal.SizeOf<NativeNfcEvent>(), Is.EqualTo(60));
        Assert.That(NativeNfcConstants.AbiVersion, Is.EqualTo(0x00010000u));
    }

    [Test]
    public void MissingDll_DisablesNfcWithoutThrowingOrBlockingFallbacks()
    {
        using (var service = new NfcReaderService(new MissingNativeApi()))
        {
            Assert.That(service.Start(), Is.False);
            Assert.That(service.PluginAvailable, Is.False);
            Assert.That(service.ReaderAvailable, Is.False);
            Assert.That(service.TryDequeue(out _), Is.False);
        }
    }

    [Test]
    public void Poll_TranslatesOrderedCardEventsIntoNamespacedCredentials()
    {
        NativeNfcEvent present = Event(
            NativeNfcConstants.EventCardPresent,
            NativeNfcConstants.CardFelica,
            new byte[] { 0x01,0x2E,0x5C,0xE0,0xE9,0xC6,0x77,0x78 });
        NativeNfcEvent removed = Event(
            NativeNfcConstants.EventCardRemoved,
            NativeNfcConstants.CardFelica,
            new byte[] { 0x01,0x2E,0x5C,0xE0,0xE9,0xC6,0x77,0x78 });

        using (var service = new NfcReaderService(
            new FakeNativeApi(present, removed)))
        {
            Assert.That(service.Start(), Is.True);
            service.Poll();

            Assert.That(service.TryDequeue(out CredentialObservation first), Is.True);
            Assert.That(first.EventType, Is.EqualTo(CredentialEventType.Present));
            Assert.That(first.Source, Is.EqualTo("nfc"));
            Assert.That(first.CredentialKey,
                Is.EqualTo("nfc:cardio:012E5CE0E9C67778"));
            Assert.That(service.TryDequeue(out CredentialObservation second), Is.True);
            Assert.That(second.EventType, Is.EqualTo(CredentialEventType.Removed));
            Assert.That(second.CredentialKey, Is.EqualTo(first.CredentialKey));
            Assert.That(service.CardPresent, Is.False);
        }
    }

    private static NativeNfcEvent Event(int type, int kind, byte[] id)
    {
        return new NativeNfcEvent
        {
            structSize = 60,
            abiVersion = NativeNfcConstants.AbiVersion,
            eventType = type,
            cardKind = kind,
            cardId0 = id[0], cardId1 = id[1],
            cardId2 = id[2], cardId3 = id[3],
            cardId4 = id[4], cardId5 = id[5],
            cardId6 = id[6], cardId7 = id[7]
        };
    }
}
