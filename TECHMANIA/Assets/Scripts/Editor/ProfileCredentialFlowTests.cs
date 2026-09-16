using System.Reflection;
using NUnit.Framework;

public class ProfileCredentialFlowTests
{
    private const BindingFlags PrivateStatic =
        BindingFlags.NonPublic | BindingFlags.Static;

    [SetUp]
    public void BlockPhysicalUsbDiscovery()
    {
        FieldInfo usbTask = typeof(ProfileManager).GetField(
            "usbScanTask", PrivateStatic);
        var pendingScan = new System.Threading.Tasks.TaskCompletionSource<string>();
        usbTask?.SetValue(null, pendingScan.Task);
    }

    [TearDown]
    public void ReleasePhysicalUsbDiscoveryBlock()
    {
        typeof(ProfileManager).GetField("usbScanTask", PrivateStatic)
            ?.SetValue(null, null);
        typeof(ProfileManager).GetField("usbScanExpectedToken", PrivateStatic)
            ?.SetValue(null, null);
    }

    [Test]
    public void NfcPresentationFeedsTheSamePendingLoginContractAsUsb()
    {
        var native = new FlowNativeApi();
        var service = new NfcReaderService(native);
        Assert.That(service.Start(), Is.True);

        FieldInfo readerField = typeof(ProfileManager).GetField(
            "nfcReaderService", PrivateStatic);
        FieldInfo pendingField = typeof(ProfileManager).GetField(
            "pendingCardId", PrivateStatic);
        FieldInfo presentedField = typeof(ProfileManager).GetField(
            "presentedCardId", PrivateStatic);
        FieldInfo sessionField = typeof(ProfileManager).GetField(
            "sessionCardId", PrivateStatic);
        FieldInfo presenceField = typeof(ProfileManager).GetField(
            "sessionCardPresent", PrivateStatic);

        Assert.That(readerField, Is.Not.Null);
        try
        {
            readerField.SetValue(null, service);
            pendingField.SetValue(null, null);
            presentedField.SetValue(null, null);
            sessionField.SetValue(null, null);
            presenceField.SetValue(null, false);

            native.Enqueue(CardEvent(NativeNfcConstants.EventCardPresent));

            Assert.That(ProfileManager.pollForCredential(), Is.True);
            Assert.That(ProfileManager.hasPendingToken(), Is.True);
            Assert.That(ProfileManager.pendingCredentialKind(), Is.EqualTo("nfc"));

            native.Enqueue(CardEvent(NativeNfcConstants.EventCardRemoved));
            Assert.That(ProfileManager.pollPendingTokenPresence(), Is.False);
        }
        finally
        {
            service.Dispose();
            readerField?.SetValue(null, null);
            pendingField?.SetValue(null, null);
            presentedField?.SetValue(null, null);
            sessionField?.SetValue(null, null);
            presenceField?.SetValue(null, false);
        }
    }

    [Test]
    public void DismissingWithNothingPresentedKeepsTheDiscardedCardSessionOut()
    {
        var native = new FlowNativeApi();
        var service = new NfcReaderService(native);
        Assert.That(service.Start(), Is.True);

        FieldInfo readerField = typeof(ProfileManager).GetField(
            "nfcReaderService", PrivateStatic);
        FieldInfo pendingField = typeof(ProfileManager).GetField(
            "pendingCardId", PrivateStatic);
        FieldInfo presentedField = typeof(ProfileManager).GetField(
            "presentedCardId", PrivateStatic);
        FieldInfo dismissedField = typeof(ProfileManager).GetField(
            "dismissedCredentialId", PrivateStatic);

        Assert.That(readerField, Is.Not.Null);
        try
        {
            readerField.SetValue(null, service);
            pendingField.SetValue(null, null);
            presentedField.SetValue(null, null);
            dismissedField.SetValue(null, null);

            native.Enqueue(CardEvent(NativeNfcConstants.EventCardPresent));
            Assert.That(ProfileManager.pollForCredential(), Is.True);
            string held = (string)pendingField.GetValue(null);
            Assert.That(held, Is.Not.Null.And.Not.Empty);

            // Returning to the home screen discards the member's session and
            // records its still-held credential as dismissed, exactly as
            // beginLoginCredentialLoop does without running a real logout.
            dismissedField.SetValue(null, held);
            pendingField.SetValue(null, null);
            presentedField.SetValue(null, null);
            Assert.That(ProfileManager.pollForCredential(), Is.False,
                "A card discarded on the way home must not re-arm the login.");

            // Backing out of the card page: nothing is presented, so there is
            // no credential to ignore and the standing dismissal must survive.
            ProfileManager.dismissPendingCredential();
            Assert.That(dismissedField.GetValue(null), Is.EqualTo(held),
                "Back with an empty reader must not release the dismissal.");
            Assert.That(ProfileManager.pollForCredential(), Is.False,
                "Back from the card page must not log the previous member in again.");

            native.Enqueue(CardEvent(NativeNfcConstants.EventCardRemoved));
            Assert.That(ProfileManager.pollForCredential(), Is.False);
            native.Enqueue(CardEvent(NativeNfcConstants.EventCardPresent));
            Assert.That(ProfileManager.pollForCredential(), Is.True,
                "A genuine lift and re-present must still log that member in.");
        }
        finally
        {
            service.Dispose();
            readerField?.SetValue(null, null);
            pendingField?.SetValue(null, null);
            presentedField?.SetValue(null, null);
            dismissedField?.SetValue(null, null);
        }
    }

    [Test]
    public void ThemeApiExposesCredentialOperationsWithoutExposingNativeDll()
    {
        TypeInfo type = typeof(ThemeApi.ThemeProfileApi).GetTypeInfo();
        Assert.That(type.GetMethod("beginLoginCredentialLoop"), Is.Not.Null);
        Assert.That(type.GetMethod("dismissPendingCredential"), Is.Not.Null);
        Assert.That(type.GetMethod("pollForCredential"), Is.Not.Null);
        Assert.That(type.GetMethod("requestPresentedCredential"), Is.Not.Null);
        Assert.That(type.GetMethod("pendingCredentialKind"), Is.Not.Null);
        Assert.That(type.GetMethod("loginWithPendingCredential"), Is.Not.Null);
        Assert.That(type.GetMethod("createProfileWithPendingCredential"), Is.Not.Null);
        Assert.That(type.GetMethod("beginCredentialLink"), Is.Not.Null);
        Assert.That(type.GetMethod("linkPendingCredential"), Is.Not.Null);
        Assert.That(type.GetMethod("cancelCredentialLink"), Is.Not.Null);
    }

    [Test]
    public void DismissingUnknownCardAllowsDifferentCardWithoutARemovalEdge()
    {
        var native = new FlowNativeApi();
        var service = new NfcReaderService(native);
        Assert.That(service.Start(), Is.True);

        FieldInfo readerField = typeof(ProfileManager).GetField(
            "nfcReaderService", PrivateStatic);
        FieldInfo pendingField = typeof(ProfileManager).GetField(
            "pendingCardId", PrivateStatic);
        FieldInfo presentedField = typeof(ProfileManager).GetField(
            "presentedCardId", PrivateStatic);
        MethodInfo dismiss = typeof(ProfileManager).GetMethod(
            "dismissPendingCredential",
            BindingFlags.Public | BindingFlags.Static);

        Assert.That(readerField, Is.Not.Null);
        try
        {
            readerField.SetValue(null, service);
            pendingField.SetValue(null, null);
            presentedField.SetValue(null, null);

            native.Enqueue(CardEvent(NativeNfcConstants.EventCardPresent, 0x01));
            Assert.That(ProfileManager.pollForCredential(), Is.True);

            Assert.That(dismiss, Is.Not.Null,
                "Back needs a credential-dismiss operation, not a disabled scanner.");
            dismiss.Invoke(null, null);
            Assert.That(ProfileManager.hasPendingToken(), Is.False,
                "The same held card must stay dismissed after Back.");

            native.Enqueue(CardEvent(NativeNfcConstants.EventCardPresent, 0x02));
            Assert.That(ProfileManager.pollForCredential(), Is.True,
                "A different card must authenticate without requiring an empty reader frame.");
            Assert.That(pendingField.GetValue(null),
                Is.EqualTo("nfc:cardio:022E5CE0E9C67778"));
        }
        finally
        {
            service.Dispose();
            readerField?.SetValue(null, null);
            pendingField?.SetValue(null, null);
            presentedField?.SetValue(null, null);
        }
    }

    [Test]
    public void HeldCardDismissedByBackIsReadAgainByAnExplicitRequest()
    {
        var native = new FlowNativeApi();
        var service = new NfcReaderService(native);
        Assert.That(service.Start(), Is.True);

        FieldInfo readerField = typeof(ProfileManager).GetField(
            "nfcReaderService", PrivateStatic);
        FieldInfo pendingField = typeof(ProfileManager).GetField(
            "pendingCardId", PrivateStatic);
        FieldInfo presentedField = typeof(ProfileManager).GetField(
            "presentedCardId", PrivateStatic);
        FieldInfo dismissedField = typeof(ProfileManager).GetField(
            "dismissedCredentialId", PrivateStatic);

        Assert.That(readerField, Is.Not.Null);
        try
        {
            readerField.SetValue(null, service);
            pendingField.SetValue(null, null);
            presentedField.SetValue(null, null);
            dismissedField.SetValue(null, null);

            native.Enqueue(CardEvent(NativeNfcConstants.EventCardPresent));
            Assert.That(ProfileManager.pollForCredential(), Is.True);
            string held = (string)pendingField.GetValue(null);
            Assert.That(held, Is.Not.Null.And.Not.Empty);

            // An unknown card reaches the sign-up page; Back dismisses it while
            // it is still resting on the reader, so no removal edge will ever
            // be emitted for it and automatic scanning has to keep ignoring it.
            ProfileManager.dismissPendingCredential();
            Assert.That(ProfileManager.pollForCredential(), Is.False,
                "Automatic scanning must keep ignoring the card Back discarded.");
            Assert.That(ProfileManager.pollForCredential(), Is.False,
                "Repeated scans must not quietly re-admit it either.");

            // MEMBER LOGIN is the deliberate read request, so it must be able
            // to use that same card without asking the player to lift it.
            Assert.That(ProfileManager.requestPresentedCredential(), Is.True,
                "An explicit request must read the card that is on the reader now.");
            Assert.That(pendingField.GetValue(null), Is.EqualTo(held));
            Assert.That(ProfileManager.hasPendingToken(), Is.True,
                "The re-admitted card must stay pending for the login attempt.");
        }
        finally
        {
            service.Dispose();
            readerField?.SetValue(null, null);
            pendingField?.SetValue(null, null);
            presentedField?.SetValue(null, null);
            dismissedField?.SetValue(null, null);
        }
    }

    [Test]
    public void DismissalIsReleasedWhenTheReaderStopsReportingThatCard()
    {
        var native = new FlowNativeApi();
        var service = new NfcReaderService(native);
        Assert.That(service.Start(), Is.True);

        FieldInfo readerField = typeof(ProfileManager).GetField(
            "nfcReaderService", PrivateStatic);
        FieldInfo pendingField = typeof(ProfileManager).GetField(
            "pendingCardId", PrivateStatic);
        FieldInfo presentedField = typeof(ProfileManager).GetField(
            "presentedCardId", PrivateStatic);
        FieldInfo dismissedField = typeof(ProfileManager).GetField(
            "dismissedCredentialId", PrivateStatic);
        MethodInfo setCardPresent = typeof(NfcReaderService)
            .GetProperty("CardPresent",
                BindingFlags.Public | BindingFlags.Instance)
            ?.GetSetMethod(true);

        Assert.That(readerField, Is.Not.Null);
        Assert.That(setCardPresent, Is.Not.Null);
        try
        {
            readerField.SetValue(null, service);
            pendingField.SetValue(null, null);
            presentedField.SetValue(null, null);
            dismissedField.SetValue(null, null);

            native.Enqueue(CardEvent(NativeNfcConstants.EventCardPresent));
            Assert.That(ProfileManager.pollForCredential(), Is.True);
            ProfileManager.dismissPendingCredential();
            Assert.That(ProfileManager.pollForCredential(), Is.False);

            // The state left behind when the observation queue is cleared after
            // the card is already gone: the removal never reaches ProfileManager,
            // yet the reader's level state proves the dismissed card is absent.
            setCardPresent.Invoke(service, new object[] { false });

            Assert.That(ProfileManager.pollForCredential(), Is.False,
                "An empty reader has no credential to offer.");
            Assert.That(dismissedField.GetValue(null), Is.Null,
                "A dismissal the reader no longer backs must be released.");

            native.Enqueue(CardEvent(NativeNfcConstants.EventCardPresent));
            Assert.That(ProfileManager.pollForCredential(), Is.True,
                "That card must work again once the reader stops reporting it.");
        }
        finally
        {
            service.Dispose();
            readerField?.SetValue(null, null);
            pendingField?.SetValue(null, null);
            presentedField?.SetValue(null, null);
            dismissedField?.SetValue(null, null);
        }
    }

    [Test]
    public void BeginningLoginLoopRearmsAnNfcCardAlreadyOnTheReader()
    {
        var native = new FlowNativeApi();
        var service = new NfcReaderService(native);
        Assert.That(service.Start(), Is.True);

        FieldInfo readerField = typeof(ProfileManager).GetField(
            "nfcReaderService", PrivateStatic);
        FieldInfo pendingField = typeof(ProfileManager).GetField(
            "pendingCardId", PrivateStatic);
        FieldInfo presentedField = typeof(ProfileManager).GetField(
            "presentedCardId", PrivateStatic);
        MethodInfo beginLoop = typeof(ProfileManager).GetMethod(
            "beginLoginCredentialLoop",
            BindingFlags.Public | BindingFlags.Static);

        Assert.That(readerField, Is.Not.Null);
        try
        {
            readerField.SetValue(null, service);
            pendingField.SetValue(null, null);
            presentedField.SetValue(null, null);

            native.Enqueue(CardEvent(NativeNfcConstants.EventCardPresent));
            Assert.That(ProfileManager.pollForCredential(), Is.True,
                "The first NFC presentation should arm the login.");

            Assert.That(beginLoop, Is.Not.Null,
                "Login needs one explicit session-reset and re-arm entry point.");
            beginLoop.Invoke(null, null);

            Assert.That(ProfileManager.pollForCredential(), Is.True,
                "Entering Login must immediately re-arm a card that is still presented.");
        }
        finally
        {
            service.Dispose();
            readerField?.SetValue(null, null);
            pendingField?.SetValue(null, null);
            presentedField?.SetValue(null, null);
        }
    }

    [Test]
    public void BeginningLoginLoopDoesNotRearmDismissedCardUntilRemoval()
    {
        var native = new FlowNativeApi();
        var service = new NfcReaderService(native);
        Assert.That(service.Start(), Is.True);

        FieldInfo readerField = typeof(ProfileManager).GetField(
            "nfcReaderService", PrivateStatic);
        FieldInfo pendingField = typeof(ProfileManager).GetField(
            "pendingCardId", PrivateStatic);
        FieldInfo presentedField = typeof(ProfileManager).GetField(
            "presentedCardId", PrivateStatic);
        FieldInfo dismissedField = typeof(ProfileManager).GetField(
            "dismissedCredentialId", PrivateStatic);
        MethodInfo beginLoop = typeof(ProfileManager).GetMethod(
            "beginLoginCredentialLoop",
            BindingFlags.Public | BindingFlags.Static);

        Assert.That(readerField, Is.Not.Null);
        try
        {
            readerField.SetValue(null, service);
            pendingField.SetValue(null, null);
            presentedField.SetValue(null, null);
            dismissedField.SetValue(null, null);

            native.Enqueue(CardEvent(NativeNfcConstants.EventCardPresent));
            Assert.That(ProfileManager.pollForCredential(), Is.True);
            ProfileManager.dismissPendingCredential();
            Assert.That(ProfileManager.hasPendingToken(), Is.False);

            beginLoop.Invoke(null, null);
            Assert.That(ProfileManager.hasPendingToken(), Is.False,
                "A held card dismissed before returning to Login must remain ignored.");

            native.Enqueue(CardEvent(NativeNfcConstants.EventCardRemoved));
            Assert.That(ProfileManager.hasPendingToken(), Is.False);
            native.Enqueue(CardEvent(NativeNfcConstants.EventCardPresent));
            Assert.That(ProfileManager.pollForCredential(), Is.True,
                "The same card must be accepted again only after removal and re-present.");
        }
        finally
        {
            service.Dispose();
            readerField?.SetValue(null, null);
            pendingField?.SetValue(null, null);
            presentedField?.SetValue(null, null);
            dismissedField?.SetValue(null, null);
        }
    }

    [Test]
    public void ReaderDisconnectClearsDismissedCardForNextLoginSession()
    {
        var native = new FlowNativeApi();
        var service = new NfcReaderService(native);
        Assert.That(service.Start(), Is.True);

        FieldInfo readerField = typeof(ProfileManager).GetField(
            "nfcReaderService", PrivateStatic);
        FieldInfo pendingField = typeof(ProfileManager).GetField(
            "pendingCardId", PrivateStatic);
        FieldInfo presentedField = typeof(ProfileManager).GetField(
            "presentedCardId", PrivateStatic);
        FieldInfo dismissedField = typeof(ProfileManager).GetField(
            "dismissedCredentialId", PrivateStatic);

        Assert.That(readerField, Is.Not.Null);
        try
        {
            readerField.SetValue(null, service);
            pendingField.SetValue(null, null);
            presentedField.SetValue(null, null);
            dismissedField.SetValue(null, null);

            native.Enqueue(CardEvent(NativeNfcConstants.EventCardPresent));
            Assert.That(ProfileManager.pollForCredential(), Is.True);
            ProfileManager.dismissPendingCredential();
            Assert.That(ProfileManager.hasPendingToken(), Is.False);

            // Reader reconnects do not necessarily include a separate card-
            // removed event. Its level state still proves the dismissed card
            // is no longer presented and must make it eligible next session.
            native.Enqueue(ReaderEvent(NativeNfcConstants.EventReaderDisconnected));
            native.Enqueue(ReaderEvent(NativeNfcConstants.EventReaderConnected));
            native.Enqueue(CardEvent(NativeNfcConstants.EventCardPresent));

            Assert.That(ProfileManager.pollForCredential(), Is.True,
                "The same card must work in the next session after the reader reports it absent.");
        }
        finally
        {
            service.Dispose();
            readerField?.SetValue(null, null);
            pendingField?.SetValue(null, null);
            presentedField?.SetValue(null, null);
            dismissedField?.SetValue(null, null);
        }
    }

    [Test]
    public void LateUsbResultCannotReplaceTheNfcCredentialAlreadyBeingPresented()
    {
        MethodInfo accept = typeof(ProfileManager).GetMethod(
            "TryAcceptPresentedCredential", PrivateStatic);
        FieldInfo pending = typeof(ProfileManager).GetField(
            "pendingCardId", PrivateStatic);
        FieldInfo presented = typeof(ProfileManager).GetField(
            "presentedCardId", PrivateStatic);

        Assert.That(accept, Is.Not.Null);
        try
        {
            pending.SetValue(null, "nfc:cardio:012E5CE0E9C67778");
            presented.SetValue(null, "nfc:cardio:012E5CE0E9C67778");

            bool accepted = (bool)accept.Invoke(null, new object[]
            {
                "usb:token:abcdef0123456789abcdef0123456789"
            });

            Assert.That(accepted, Is.False);
            Assert.That(pending.GetValue(null),
                Is.EqualTo("nfc:cardio:012E5CE0E9C67778"));
            Assert.That(presented.GetValue(null),
                Is.EqualTo("nfc:cardio:012E5CE0E9C67778"));
        }
        finally
        {
            pending?.SetValue(null, null);
            presented?.SetValue(null, null);
        }
    }

    private static NativeNfcEvent CardEvent(int eventType, byte firstByte = 0x01)
    {
        NativeNfcEvent value = NativeNfcEvent.CreateForPoll();
        value.eventType = eventType;
        value.cardKind = NativeNfcConstants.CardFelica;
        value.cardId0 = firstByte;
        value.cardId1 = 0x2E;
        value.cardId2 = 0x5C;
        value.cardId3 = 0xE0;
        value.cardId4 = 0xE9;
        value.cardId5 = 0xC6;
        value.cardId6 = 0x77;
        value.cardId7 = 0x78;
        return value;
    }

    private static NativeNfcEvent ReaderEvent(int eventType)
    {
        NativeNfcEvent value = NativeNfcEvent.CreateForPoll();
        value.eventType = eventType;
        return value;
    }

    private sealed class FlowNativeApi : INfcNativeApi
    {
        private readonly System.Collections.Generic.Queue<NativeNfcEvent> events =
            new System.Collections.Generic.Queue<NativeNfcEvent>();

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

        public void Enqueue(NativeNfcEvent eventData)
        {
            events.Enqueue(eventData);
        }
    }
}
