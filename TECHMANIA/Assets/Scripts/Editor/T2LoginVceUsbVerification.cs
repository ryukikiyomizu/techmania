using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Xml.Linq;
using MoonSharp.Interpreter;
using UnityEditor;
using UnityEngine;

public static class T2LoginVceUsbVerification
{
    private const string EvidenceRoot =
        @"C:\Users\Jen\Downloads\Technika Projects\t2\resource\LoginState";

    private static readonly string[] SourceVces =
    {
        "inputcard_head", "inputcard_panel", "banner_image", "inputcard_main",
        "inputcard_login", "inputcard_loading", "inputcard_error",
        "inputcard_identify1", "inputcard_identify2", "inputcard_join",
        "inputcard_joinok", "inputcard_transfer", "inputcard_transfering",
        "button_back", "button_guest", "button_idlogin", "button_join",
        "button_joinnow", "button_memberlogin", "button_startgame",
        "button_transfer", "keypad", "key", "cursor", "keypad_join",
        "keypad_next"
    };

    private static readonly string[] ActorRoots =
    {
        "login-vce-head", "login-vce-panel", "login-vce-banner",
        "login-vce-state", "login-vce-button-1", "login-vce-button-2",
        "login-vce-button-3", "login-vce-keypad",
        "login-vce-key-feedback", "login-vce-cursor"
    };

    private static readonly string[] ExactTextures =
    {
        "inputcard2.png", "inputcard3.png", "glow.jpg",
        "nocard_char.png", "nocard_embl.png"
    };

    private static readonly string[] StateKeys =
    {
        "main", "login", "loading", "join", "nameKeypad", "identify2", "error",
        "transfer"
    };

    public static void Run()
    {
        List<string> failures = Evaluate();
        if (failures.Count > 0)
            throw new InvalidOperationException(string.Join(
                Environment.NewLine, failures));
        Debug.Log("[T2 Login Test] Authentic LoginState VCE/USB contracts passed.");
    }

    public static void RunWithUsb()
    {
        Run();

        const System.Reflection.BindingFlags privateStatic =
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Static;
        Type manager = typeof(ProfileManager);
        System.Reflection.FieldInfo pendingField = manager.GetField(
            "pendingCardId", privateStatic);
        System.Reflection.FieldInfo taskField = manager.GetField(
            "usbScanTask", privateStatic);
        pendingField?.SetValue(null, null);
        taskField?.SetValue(null, null);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        string token = ProfileManager.rescanForToken();
        stopwatch.Stop();
        if (stopwatch.ElapsedMilliseconds > 100)
            throw new InvalidOperationException(
                $"Starting a USB scan blocked the Unity thread for {stopwatch.ElapsedMilliseconds} ms.");
        if (!string.IsNullOrEmpty(token))
            throw new InvalidOperationException(
                "USB rescan completed synchronously instead of yielding to the Unity thread.");

        long worstPollMs = stopwatch.ElapsedMilliseconds;
        var timeout = System.Diagnostics.Stopwatch.StartNew();
        while (timeout.ElapsedMilliseconds < 10000)
        {
            System.Threading.Thread.Sleep(25);
            stopwatch.Restart();
            token = ProfileManager.rescanForToken();
            stopwatch.Stop();
            worstPollMs = Math.Max(worstPollMs, stopwatch.ElapsedMilliseconds);
            if (stopwatch.ElapsedMilliseconds > 100)
                throw new InvalidOperationException(
                    $"Polling an active USB scan blocked the Unity thread for {stopwatch.ElapsedMilliseconds} ms.");
            if (!string.IsNullOrEmpty(token))
            {
                Debug.Log($"[T2 Login Test] Attached USB detected asynchronously; " +
                    $"worst main-thread poll={worstPollMs} ms.");
                return;
            }
        }

        throw new InvalidOperationException(
            "The currently attached USB could not be detected or provisioned within 10 seconds.");
    }

    public static void RunRemovalWithoutUsb()
    {
        Run();
        const System.Reflection.BindingFlags privateStatic =
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Static;
        Type manager = typeof(ProfileManager);
        manager.GetField("pendingCardId", privateStatic)?.SetValue(
            null, "verification-token-not-on-disk");
        manager.GetField("presentedCardId", privateStatic)?.SetValue(
            null, "verification-token-not-on-disk");
        manager.GetField("usbScanTask", privateStatic)?.SetValue(null, null);
        manager.GetField("usbScanExpectedToken", privateStatic)?.SetValue(null, null);

        var pollTimer = System.Diagnostics.Stopwatch.StartNew();
        if (!ProfileManager.pollPendingTokenPresence())
            throw new InvalidOperationException(
                "Presence polling discarded the last-known card before its worker completed.");
        pollTimer.Stop();
        long worstPollMs = pollTimer.ElapsedMilliseconds;

        var timeout = System.Diagnostics.Stopwatch.StartNew();
        while (timeout.ElapsedMilliseconds < 10000)
        {
            System.Threading.Thread.Sleep(25);
            pollTimer.Restart();
            bool present = ProfileManager.pollPendingTokenPresence();
            pollTimer.Stop();
            worstPollMs = Math.Max(worstPollMs, pollTimer.ElapsedMilliseconds);
            if (pollTimer.ElapsedMilliseconds > 100)
                throw new InvalidOperationException(
                    $"Removal polling blocked Unity for {pollTimer.ElapsedMilliseconds} ms.");
            if (!present)
            {
                if (ProfileManager.hasPendingToken())
                    throw new InvalidOperationException(
                        "Removed card remained available as a pending login token.");
                Debug.Log($"[T2 Login Test] Asynchronous card removal detected; " +
                    $"worst main-thread poll={worstPollMs} ms.");
                return;
            }
        }

        throw new InvalidOperationException(
            "Background card-removal detection did not finish within 10 seconds.");
    }

    public static List<string> Evaluate()
    {
        var failures = new List<string>();
        string uiRoot = Path.Combine(Application.dataPath, "UI");
        string vceRoot = Path.Combine(uiRoot, "Sprites", "Login", "T2", "VCE");
        string mainTree = Read(Path.Combine(uiRoot, "MainTree.uxml"));
        string loginTree = Slice(mainTree, "name=\"login-screen\"",
            "name=\"profile-editor-screen\"");
        string mainScript = Read(Path.Combine(uiRoot, "MainScript.txt"));
        string manifest = Read(Path.Combine(uiRoot, "Scripts", "Login VCE Manifest.txt"));
        string loginScreenLua = Read(Path.Combine(uiRoot, "Scripts", "Login Screen.txt"));
        string loginLua = Read(Path.Combine(uiRoot, "Scripts", "Login VCE Flow.txt"));
        string titleLua = Read(Path.Combine(uiRoot, "Scripts", "Title Screen.txt"));
        string selectModeLua = Read(Path.Combine(uiRoot, "Scripts", "Select Mode Screen.txt"));
        string profileLua = Read(Path.Combine(uiRoot, "Scripts", "Profile Screen.txt"));
        string themeOptionsLua = Read(Path.Combine(uiRoot, "Scripts", "Theme Options.txt"));
        string gameLua = Read(Path.Combine(uiRoot, "Scripts", "Game Screen.txt"));
        string allClearLua = Read(Path.Combine(uiRoot, "Scripts", "Arcade All Clear V2 Screen.txt"));
        string profileManager = Read(Path.Combine(Application.dataPath,
            "Scripts", "ProfileManager.cs"));
        string themeApi = Read(Path.Combine(Application.dataPath, "Scripts",
            "Theme API", "ThemeProfileApi.cs"));
        string externalWatcher = Read(Path.Combine(Application.dataPath, "Scripts",
            "Components", "Main Scene", "ExternalRecordsWatcher.cs"));
        string generator = Read(Path.Combine(Directory.GetParent(Application.dataPath).FullName,
            "tools", "generate_login_vce_assets.py"));

        Check(manifest.Contains("from t2/resource/LoginState"),
            "Generated Login manifest does not name LoginState as its authority.", failures);
        foreach (string vce in SourceVces)
        {
            Check(Directory.GetFiles(EvidenceRoot, vce + ".vce",
                    SearchOption.AllDirectories).Length == 1,
                "Missing authoritative LoginState VCE: " + vce, failures);
            Check(manifest.Contains(vce + " = { fps = 60"),
                "Generated manifest is missing the 60 FPS " + vce + " timeline.",
                failures);
        }

        foreach (string texture in ExactTextures)
        {
            string original = Path.Combine(EvidenceRoot, texture);
            string packaged = Path.Combine(vceRoot, texture.ToLowerInvariant());
            Check(File.Exists(packaged), "Missing packaged LoginState texture: " + texture,
                failures);
            if (File.Exists(original) && File.Exists(packaged))
                Check(Sha256(original) == Sha256(packaged),
                    texture + " is not byte-identical to LoginState.", failures);
        }

        foreach (string stateKey in StateKeys)
            Check(loginLua.Contains("\"" + stateKey + "\"") ||
                  loginLua.Contains(stateKey + " ="),
                "Login flow is missing arcade state " + stateKey + ".", failures);

        foreach (string elementName in ActorRoots)
            Check(mainTree.Contains("name=\"" + elementName + "\""),
                "Login hierarchy is missing " + elementName + ".", failures);

        Check(mainScript.Contains("Login VCE Manifest.txt") &&
              mainScript.Contains("Login VCE Flow.txt"),
            "MainScript does not load the generated manifest and runtime VCE flow.",
            failures);
        Check(loginLua.Contains("CreateActor = function(rootName, layerCount)") &&
              loginLua.Contains("loginActors = {") &&
              loginLua.Contains("loginFlow = {") &&
              loginLua.Contains("SetState = function(name)") &&
              loginLua.Contains("StopStateCoroutine = function()") &&
              loginLua.Contains("BeginUsbScan = function()") &&
              loginLua.Contains("ProceedOnce = function()"),
            "Login does not use the explicit LoginState VCE/USB controller.", failures);
        string loginShow = Slice(loginLua, "loginScreen.Show = function()",
            "local legacyHide = loginScreen.Hide");
        int beginLoopIndex = loginShow.IndexOf(
            "tm.profile.beginLoginCredentialLoop()", StringComparison.Ordinal);
        int mainStateIndex = loginShow.IndexOf(
            "loginFlow.SetState(\"main\")", StringComparison.Ordinal);
        Check(beginLoopIndex >= 0 && mainStateIndex > beginLoopIndex,
            "Login does not end the previous session and re-arm credential detection before entering idle.",
            failures);
        Check(!loginTree.Contains("Credit(s)"),
            "Login still renders the legacy Credit(s) status tag.", failures);
        Check(loginLua.Contains("if (name == \"main\") then") &&
              loginLua.Contains("loginCountdown.Start()") &&
              loginLua.Contains("loginCountdown.Stop()"),
            "Login countdown is not gated to the idle main state.", failures);
        Check(loginLua.Contains("tm.profile.loginWithPendingCredential()") &&
              loginLua.Contains("tm.profile.createProfileWithPendingCredential(") &&
              loginLua.Contains("tm.profile.pollForCredential()"),
            "Login Lua does not execute reader-neutral card and USB flows.", failures);
        Check(titleLua.Contains("tm.profile.beginLoginCredentialLoop()") &&
              !titleLua.Contains("ArmCredentialWatch = function()") &&
              !titleLua.Contains("tm.profile.pollForCredential()") &&
              CountOccurrences(titleLua, "titleScreen.ProceedFromStart()") == 1,
            "Logo must wait for an explicit Start press instead of auto-starting from a credential present at boot.",
            failures);
        string transferState = Slice(loginLua,
            "elseif (name == \"transfer\") then", "end\n    end,");
        Check(!transferState.Contains("loginFlow.BeginUsbScan()") &&
              !transferState.Contains("tm.profile.pollForCredential()") &&
              transferState.Contains("local transfer guidance"),
            "Data Transfer must remain an informational local-guidance panel and must not authenticate a presented credential.",
            failures);
        string returnToMain = Slice(loginLua, "ReturnToMain = function()",
            "HideTransientActors = function()");
        Check(returnToMain.Contains("tm.profile.dismissPendingCredential()") &&
              !returnToMain.Contains("usbScanSuppressed = true") &&
              returnToMain.Contains("if (tm.profile.hasPendingToken() or") &&
              returnToMain.Contains("tm.profile.pollPendingTokenPresence()) then"),
            "Login Back suppresses all credentials instead of dismissing only the rejected card/USB, or dismisses an empty reader and so re-admits the discarded card session.",
            failures);
        string dismissBody = Slice(profileManager,
            "public static void dismissPendingCredential()",
            "public static bool requestPresentedCredential()");
        Check(dismissBody.Contains(
                  "if (!string.IsNullOrEmpty(target)) dismissedCredentialId = target;"),
            "Dismissing with an empty reader still overwrites the standing dismissal, so Back re-admits the card session that going home discarded.",
            failures);
        string usbScanCompletion = Slice(profileManager,
            "private static void CompleteUsbScanIfReady()",
            "public static void Initialize()");
        Check(usbScanCompletion.Contains("bool scanFailed = false") &&
              usbScanCompletion.Contains("if (!scanFailed &&") &&
              usbScanCompletion.Contains(
                  "ProfileCredentials.Kind(dismissedCredentialId) == \"usb\"") &&
              usbScanCompletion.Contains("dismissedCredentialId = null;"),
            "A dismissed USB profile has no removal edge, so an unplug never makes that drive eligible again.",
            failures);
        string timerBounce = Slice(loginScreenLua, "Bounce = function()",
            "Start = function()");
        Check(timerBounce.Contains("{ time = 0, values = {1, 1} }") &&
              timerBounce.Contains("{ time = 3 / 60, values = {84 / 68, 104 / 87} }") &&
              timerBounce.Contains("{ time = 15 / 60, values = {1, 1} }") &&
              timerBounce.Contains("translateTrack(loginCountdown.tensElement, -5)") &&
              timerBounce.Contains("translateTrack(loginCountdown.onesElement, 5)"),
            "Login timer tick does not match Count_Star2.vce's 60 fps overshoot and spread.",
            failures);
        Check(loginScreenLua.Contains("unity.mathf.Clamp(") &&
              loginScreenLua.Contains("1,") &&
              loginScreenLua.Contains("99))") &&
              loginScreenLua.Contains("local tens = unity.mathf.FloorToInt(value / 10)") &&
              loginScreenLua.Contains("local ones = value % 10") &&
              loginScreenLua.Contains("tensElement.display = true") &&
              loginScreenLua.Contains("onesElement.display = true"),
            "Login timer does not preserve a fixed two-digit display across configured values 1-99.",
            failures);
        Check(loginTree.Contains("name=\"countdown\"") &&
              loginTree.Contains("right: 33px; top: 33.5px; width: 131px; height: 87px") &&
              loginTree.Contains("name=\"ones\"") &&
              loginTree.Contains("left: 63px; top: 0; width: 68px; height: 87px"),
            "Login timer geometry does not match Count_Star2.vce's authored digit positions.",
            failures);
        Check(!loginTree.Contains("profile-name-input") &&
              !loginTree.Contains("<ui:TextField"),
            "Login must use the arcade touch keypad, not a generic TextField.", failures);
        Check(manifest.Contains("blendClass") &&
              loginLua.Contains("sample[12] == 1") &&
              mainTree.Contains("Additive Shader Material In Theme"),
            "Login VCE keyframe blend classes are not routed through the additive material.", failures);
        string keypadManifest = Slice(manifest, "loginKeypad = {", null);
        Check(manifest.Contains("characters = \"1234567890QWERTYUIOPASDFGHJKL  ZXCVBNM\"") &&
              manifest.Contains("hitTargets = {") &&
              CountOccurrences(keypadManifest, "109,91,") == 38 &&
              CountOccurrences(keypadManifest, "key_text_") == 38 &&
              mainTree.Contains("name=\"login-key-glyph-38\""),
            "Login keypad does not expose the 38 authoritative VCE touch targets.", failures);
        Check(loginLua.Contains("loginKeypadController = {") &&
              loginLua.Contains("Append = function(index)") &&
              loginLua.Contains("Backspace = function()") &&
              loginLua.Contains("Confirm = function()") &&
              loginLua.Contains("maxLength = 12"),
            "Login does not implement the approved single-name arcade keypad.", failures);
        Check(loginLua.Contains("ApplyGuestProfile = function()") &&
              loginLua.Contains("loginFlow.ApplyGuestProfile()") &&
              loginLua.Contains("djInfo.noCard = true"),
            "Guest start does not explicitly restore the no-card identity.", failures);
        Check(loginLua.Contains("loginFlow.SetState(\"identify2\")") &&
              !loginLua.Contains("loginFlow.SetState(\"joinok\")") &&
              !loginLua.Contains("loginFlow.SetState(\"identify1\")") &&
              !loginLua.Contains("PlayAt(\"button_startgame\""),
            "New and returning cards must share the single rainbow identification reveal.",
            failures);
        // The client's card page is real: inputcard_login owns three copy rows
        // plus the ID LOGIN and DATA TRANSFER buttons, so suppressing the page
        // also suppressed two of the arcade's own buttons. What must never come
        // back is the page as a *redundant* step -- BeginCardAuthentication
        // still jumps straight to loading whenever a credential is already
        // held, and MEMBER LOGIN only opens the page with nothing presented.
        string beginCardAuth = Slice(loginLua,
            "BeginCardAuthentication = function()", "BeginUsbScan = function()");
        string memberTouch = Slice(loginLua,
            "loginFlow.screen.Q(\"login-touch-member\").RegisterCallback",
            "loginFlow.screen.Q(\"login-touch-guest\").RegisterCallback");
        Check(beginCardAuth.Contains(
                  "if (not tm.profile.pollForCredential()) then return end") &&
              beginCardAuth.Contains("loginFlow.SetState(\"loading\")") &&
              !beginCardAuth.Contains("loginFlow.SetState(\"login\")") &&
              memberTouch.Contains(
                  "if (tm.profile.requestPresentedCredential()) then") &&
              memberTouch.Contains("loginFlow.BeginCardAuthentication()") &&
              memberTouch.Contains("loginFlow.SetState(\"login\")"),
            "A held credential must authenticate immediately; the card page is only for when nothing is presented.",
            failures);
        // MEMBER LOGIN is the deliberate "read what is on the reader now" press,
        // so it is the one caller allowed to re-admit a dismissed credential.
        // Backing out of the sign-up page leaves the new card dismissed, and a
        // card that never physically leaves the reader emits no removal edge --
        // without an explicit request that card could never be used again.
        // Automatic scanning must keep going through pollForCredential, which
        // still refuses it; that is what stops Back and the walk home from
        // logging a still-held card straight back in.
        string requestPresented = Slice(profileManager,
            "public static bool requestPresentedCredential()",
            "public static string pendingCredentialKind()");
        Check(requestPresented.Contains("dismissedCredentialId = null;") &&
              CountOccurrences(requestPresented, "PumpNfcEvents();") == 2 &&
              !Slice(profileManager, "public static bool pollForCredential()",
                     "public static void beginLoginCredentialLoop()")
                  .Contains("dismissedCredentialId = null") &&
              themeApi.Contains(
                  "public bool requestPresentedCredential()") &&
              themeApi.Contains(
                  "return ProfileManager.requestPresentedCredential();"),
            "An explicitly requested card read must clear the dismissal and re-latch the reader's level state, while automatic polling keeps ignoring it.",
            failures);
        string pumpNfc = Slice(profileManager,
            "private static void PumpNfcEvents()",
            "private static bool TryAcceptPresentedCredential(string key)");
        Check(pumpNfc.Contains(
                  "ProfileCredentials.Kind(dismissedCredentialId) == \"nfc\" &&") &&
              pumpNfc.Contains("!reader.CardPresent || !string.Equals(") &&
              pumpNfc.Contains("reader.CurrentCredentialKey, dismissedCredentialId,"),
            "A dismissal must not outlive the card it describes; the reader's level state has to release it like an empty USB rescan does.",
            failures);
        // inputcard_login's own dummy.png anchors: L008 (948,377) carries the
        // 270x120 ID LOGIN button and L009 (627,592) the 280x80 DATA TRANSFER
        // button -- 280x80 is button_transfer's exact size and appears nowhere
        // else in the set, which is what proves DATA TRANSFER belongs to this
        // page and not to the init screen.
        Check(loginLua.Contains("elseif (name == \"login\") then") &&
              loginLua.Contains(
                  "loginActors.state.Play(\"inputcard_login\", true, nil)") &&
              loginLua.Contains(
                  "loginActors.button1.PlayAt(\"button_idlogin\", false, 948, 377, nil)") &&
              loginLua.Contains(
                  "loginActors.button2.PlayAt(\"button_transfer\", false, 627, 592, nil)") &&
              loginLua.Contains("loginFlow.ShowTouch(\"login-touch-idlogin\")") &&
              loginLua.Contains("loginFlow.ShowTouch(\"login-touch-transfer\")") &&
              mainTree.Contains(
                  "name=\"login-touch-idlogin\" style=\"position: absolute; left: 948px; top: 377px; width: 270px; height: 120px") &&
              mainTree.Contains(
                  "name=\"login-touch-transfer\" style=\"position: absolute; left: 627px; top: 592px; width: 280px; height: 80px"),
            "The card page must place ID LOGIN and DATA TRANSFER on inputcard_login's own dummy anchors.",
            failures);
        // inputcard_main's L012 is 210x80, which is button_joinnow's size and
        // no other button's, so the init screen is MEMBER / GUEST / JOIN NOW.
        // A cardless JOIN NOW is reachable because tm.profile.createProfile
        // generates its own cardId, so the keypad never dead-ends; the
        // credentialled path still binds the held card instead.
        Check(loginLua.Contains(
                  "loginActors.button3.PlayAt(\"button_joinnow\", false, 634, 623, nil)") &&
              loginLua.Contains("loginFlow.ShowTouch(\"login-touch-joinnow\")") &&
              mainTree.Contains(
                  "name=\"login-touch-joinnow\" style=\"position: absolute; left: 634px; top: 623px; width: 210px; height: 80px") &&
              loginLua.Contains("tm.profile.createProfile(name)") &&
              themeApi.Contains("void createProfile(string name)") &&
              loginLua.Contains("loginKeypadController.mode = \"create\"") &&
              loginLua.Contains("loginKeypadController.mode = \"login\"") &&
              loginLua.Contains("tm.profile.login(name)") &&
              themeApi.Contains("void login(string name)"),
            "The init screen's third button must be JOIN NOW on L012, with a cardless fallback and an ID-login keypad mode.",
            failures);
        // nameKeypad and identify2 are now reachable with nothing presented, so
        // an unconditional presence watch would read false on its first tick
        // and bounce a valid session to the error screen.
        Check(loginLua.Contains("BeginPresenceWatchIfCredentialled = function()") &&
              loginLua.Contains("tm.profile.hasSessionCard()") &&
              CountOccurrences(loginLua,
                  "loginFlow.BeginPresenceWatchIfCredentialled()") == 2 &&
              themeApi.Contains("hasSessionCard()"),
            "Cardless registration and ID login must not be bounced to error by a credential presence watch.",
            failures);
        // inputcard_head layer 1 is the identical pl_0000..pl_0020 set that
        // StartPlatinumLogoAnimation already loads, so playing the actor would
        // draw the logo twice. The document stays the timing authority instead:
        // texture 1 -> 21 over frames 0-60 and 21 -> 1 over frames 60-300 at
        // 60 fps. The old 0.045s ramp peaked in 0.945s and then held.
        Check(loginScreenLua.Contains("PlatinumLogoTextureIndex = function(timer)") &&
              loginScreenLua.Contains(
                  "if (timer >= 1) then progress = 1 - (timer - 1) / 4 end") &&
              loginScreenLua.Contains(
                  "unity.mathf.FloorToInt(1 + (count - 1) * progress)") &&
              loginScreenLua.Contains("StartPlatinumLogoAnimation = function()") &&
              loginLua.Contains("loginScreen.StartPlatinumLogoAnimation()") &&
              !loginLua.Contains("loginActors.head.Play(") &&
              profileLua.Contains("loginScreen.PlatinumLogoTextureIndex(timer)") &&
              !loginScreenLua.Contains("local lengthPerTexture = 0.045") &&
              !profileLua.Contains("local lengthPerTexture = 0.045"),
            "The Platinum Crew logo must share one ramp driven by inputcard_head's 1s-up / 4s-down keys.",
            failures);
        Check(loginLua.Contains("SetTextureOverride = function(index, texture)") &&
              loginLua.Contains("SetTextureOverride(3, djInfo.djIcon)") &&
              loginLua.Contains("SetTextureOverride(4, djInfo.djPlate)") &&
              loginLua.Contains("SetTextureOverride(5, djInfo.djPattern") &&
              mainTree.Contains("name=\"login-profile-name\"") &&
              mainTree.Contains("class=\"login-profile-name\""),
            "Rainbow identification does not apply the active profile icon, plate, pattern, and name.",
            failures);
        Check(loginScreenLua.Contains("leftPosterAssetPath =") &&
              loginScreenLua.Contains("SetLeftPosterAssetPath = function(path)") &&
              loginLua.Contains("loginScreen.SetLeftPosterAssetPath("),
            "Login poster remains hard-wired inside the generated VCE manifest.", failures);
        Check(profileLua.Contains("SyncSessionIdentity = function()") &&
              profileLua.Contains("if (tm.profile.isGuest()) then return end") &&
              profileLua.Contains("themeOptions.SetBool(\"NoCard\", false)") &&
              profileLua.Contains(
                  "themeOptions.SetString(\"DjName\", tm.profile.currentProfile())"),
            "Logged-in USB identity is not synchronized into the profile editor form.",
            failures);
        Check(!loginLua.Contains("PlayAt(\"button_memberlogin\", true") &&
              !loginLua.Contains("PlayAt(\"button_guest\", true") &&
              !loginLua.Contains("PlayAt(\"button_transfer\", true") &&
              !loginLua.Contains("PlayAt(\"button_idlogin\", true") &&
              !loginLua.Contains("PlayAt(\"button_joinnow\", true") &&
              !loginLua.Contains("PlayAt(\"button_join\", true") &&
              !loginLua.Contains("PlayAt(\"button_back\", true") &&
              !loginLua.Contains("PlayAt(\"button_startgame\", true") &&
              !loginLua.Contains("PlayAt(\"keypad_next\", true"),
            "Short VCE button feedback timelines must not loop continuously.",
            failures);
        Check(loginLua.Contains("Logging in. Please wait.") &&
              loginLua.Contains("Please enter your name."),
            "Login does not use the approved single-loader arcade copy.", failures);
        try
        {
            XDocument tree = XDocument.Parse(mainTree);
            XElement copyLayer = tree.Descendants().FirstOrDefault(element =>
                (string)element.Attribute("name") == "login-main-copy-layer");
            List<XElement> visibleCopy = copyLayer == null
                ? new List<XElement>()
                : copyLayer.Descendants().Where(element =>
                    element.Name.LocalName == "Label" &&
                    !((string)element.Attribute("style") ?? "").Contains(
                        "display: none") &&
                    !string.IsNullOrWhiteSpace((string)element.Attribute("text")))
                    .ToList();
            XElement prompt = visibleCopy.Count == 1 ? visibleCopy[0] : null;
            string promptStyle = (string)prompt?.Attribute("style") ?? "";
            Check(prompt != null &&
                  (string)prompt.Attribute("name") == "login-main-card-prompt" &&
                  (string)prompt.Attribute("text") == "Please present a card or USB profile." &&
                  promptStyle.Contains(
                      "left: 742px; top: 337px; width: 356px; height: 26px"),
                "Main login must contain only the single arcade card prompt at its authored slot.",
                failures);
        }
        catch (Exception ex)
        {
            failures.Add("Main login UXML could not be inspected: " + ex.Message);
        }
        Check(mainTree.Contains("name=\"login-keypad-next-stable\"") &&
              mainTree.Contains("left: 735px; top: 173px; width: 186px; height: 116px") &&
              loginLua.Contains("unity.enum.keyCode.Return") &&
              loginLua.Contains("unity.enum.keyCode.KeypadEnter") &&
              loginLua.Contains("loginKeypadController.Confirm()"),
            "The arcade keypad has no stable NEXT control or physical Enter fallback.",
            failures);
        Check(loginLua.Contains(
                  "keyFeedback = CreateActor(\"login-vce-key-feedback-front\", 4)") &&
              !loginLua.Contains(
                  "keyFeedback = CreateActor(\"login-vce-key-feedback\", 5)"),
            "Pressed-key feedback still renders key_form.png's first glyph over the selected key.",
            failures);
        Check(CountOccurrences(mainTree, "name=\"login-key-base-") == 38 &&
              loginLua.Contains("\"login-key-base-\" .. index") &&
              loginLua.Contains("keyIdleWhite") &&
              loginLua.Contains("keyIdleBlue"),
            "The arcade keypad is missing its 38 persistent authored key faces.",
            failures);
        Check(loginLua.Contains("target[1] + 16") &&
              loginLua.Contains("target[2] + 12") &&
              loginLua.Contains("glyph.style.width = StyleLength(78)") &&
              loginLua.Contains("glyph.style.height = StyleLength(64)"),
            "Keypad glyphs are not aligned to key.vce's authored 78x64 slot.",
            failures);
        Check(loginLua.Contains("loginActors.keyFeedback.Stop()") &&
              loginLua.Contains("PlayAt(\n            \"key\", false"),
            "Pressed-key feedback remains latched and jumps to the next key.",
            failures);
        Check(generator.Contains("(column * image.width) // 13") &&
              generator.Contains("((column + 1) * image.width) // 13"),
            "The LoginState glyph atlas is not sliced by its authentic 13-column grid.",
            failures);
        string symbolGlyphManifest = Slice(manifest, "symbolGlyphs = {", "symbols = {");
        Check(CountOccurrences(symbolGlyphManifest, "key_form_") == 38 &&
              manifest.Contains("symbolGlyphs = {") &&
              manifest.Contains("symbols = {") &&
              loginLua.Contains("symbolMode = false") &&
              loginLua.Contains("ToggleSymbols = function()") &&
              loginLua.Contains("loginKeypadController.ToggleSymbols()"),
            "The arcade ?! key does not switch to the authentic symbol/ABC page.",
            failures);
        for (int index = 1; index <= 38; index++)
        {
            string glyphPath = Path.Combine(vceRoot, $"key_text_{index:00}.png");
            Check(File.Exists(glyphPath),
                $"Missing generated keypad glyph {index:00}.", failures);
            if (!File.Exists(glyphPath)) continue;

            var glyph = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Check(glyph.LoadImage(File.ReadAllBytes(glyphPath), false) &&
                      glyph.width == 78 && glyph.height == 64,
                    $"Keypad glyph {index:00} is not the authored 78x64 VCE slot.", failures);
                bool touchesCellEdge = false;
                for (int x = 0; x < glyph.width && !touchesCellEdge; x++)
                    touchesCellEdge = glyph.GetPixel(x, 0).a > 0f ||
                                      glyph.GetPixel(x, glyph.height - 1).a > 0f;
                for (int y = 0; y < glyph.height && !touchesCellEdge; y++)
                    touchesCellEdge = glyph.GetPixel(0, y).a > 0f ||
                                      glyph.GetPixel(glyph.width - 1, y).a > 0f;
                Check(!touchesCellEdge,
                    $"Keypad glyph {index:00} touches its atlas cell edge and may be clipped.",
                    failures);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(glyph);
            }

            string symbolPath = Path.Combine(vceRoot, $"key_form_{index:00}.png");
            Check(File.Exists(symbolPath),
                $"Missing generated symbol keypad glyph {index:00}.", failures);
        }
        Check(mainTree.Contains("name=\"login-touch-keypad-close\"") &&
              mainTree.Contains("left: 1151.5px; top: 59.5px; width: 75px; height: 75px") &&
              loginLua.Contains("login-touch-keypad-close") &&
              loginLua.Contains("loginFlow.SetState(\"main\")"),
            "The visible arcade keypad close button is not wired to return to Login.",
            failures);
        Check(profileManager.Contains("profileNameExists(string name)") &&
              profileManager.Contains("StringComparison.OrdinalIgnoreCase") &&
              themeApi.Contains("profileNameExists(string name)") &&
              loginLua.Contains("tm.profile.profileNameExists(name)") &&
              loginLua.Contains("That DJ name already exists."),
            "First-time USB creation cannot distinguish an existing DJ name from card failure.",
            failures);
        // button_back's art stays at the arcade's own (1114,672); only the
        // invisible hit rect is grown to 200x90 so a finger can reach it on a
        // touchscreen. inputcard_login authors no back dummy at all, but the
        // corner slot is fixed across every other state and without it that
        // page would trap a touch-only player until the 30s timeout.
        Check(mainTree.Contains(
                  "name=\"login-touch-back\" style=\"position: absolute; left: 1040px; top: 630px; width: 200px; height: 90px") &&
              CountOccurrences(loginLua,
                  "PlayAt(\"button_back\", false, 1114, 672, nil)") == 4 &&
              loginLua.Contains("loginFlow.currentState == \"join\"") &&
              loginLua.Contains("loginFlow.currentState == \"login\"") &&
              loginLua.Contains("event.keyCode == unity.enum.keyCode.Escape") &&
              loginLua.Contains("ReturnToMain = function()") &&
              loginLua.Contains("tm.profile.dismissPendingCredential()") &&
              profileManager.Contains("dismissedCredentialId") &&
              themeApi.Contains("dismissPendingCredential()"),
            "Join/Welcome back navigation does not dismiss only the rejected credential.",
            failures);

        Check(loginLua.Contains("BeginCardAuthentication = function()") &&
              loginLua.Contains("elseif (name == \"loading\") then") &&
              loginLua.Contains("elseif (name == \"identify2\") then") &&
              !loginLua.Contains("elseif (name == \"identify1\") then") &&
              loginLua.Contains("loginFlow.SetState(\"loading\")") &&
              !loginLua.Contains("loginFlow.SetState(\"identify1\")") &&
              loginLua.Contains("loginFlow.SetState(\"identify2\")"),
            "Login must use one TECHNIKA loader followed by one rainbow profile reveal.",
            failures);
        Check(loginLua.Contains("ClipSeconds = function(name)") &&
              loginLua.Contains(
                  "loginVceManifest[name].maxFrame / loginVceManifest[name].fps") &&
              loginLua.Contains("ScheduleClip = function(name, action)") &&
              !loginLua.Contains("loginFlow.Wait(0.45)") &&
              !loginLua.Contains("loginFlow.Schedule(1.15") &&
              !loginLua.Contains("loginFlow.Schedule(1.35"),
            "Automatic login transitions do not follow authoritative VCE durations.",
            failures);
        Check(loginLua.Contains("PlayLoginSfx = function(name)") &&
              loginLua.Contains("loginVceAudio[name]") &&
              loginLua.Contains("element.time = 0"),
            "Login does not use the authentic sound map or restart its background video.",
            failures);
        Check(loginLua.Contains("AppendCharacter = function(character)") &&
              loginLua.Contains("unity.enum.keyCode.Backspace") &&
              loginLua.Contains("event.character"),
            "The service keyboard does not mirror arcade character and delete input.",
            failures);
        Check(profileManager.Contains("pollPendingTokenPresence()") &&
              profileManager.Contains("FindPresentedToken(string expectedToken)") &&
              profileManager.Contains("usbScanExpectedToken") &&
              themeApi.Contains("pollPendingTokenPresence()") &&
              loginLua.Contains("tm.profile.pollPendingTokenPresence()"),
            "Presented USB removal is not detected through the asynchronous profile API.",
            failures);
        Check(profileManager.Contains("private static string sessionCardId") &&
              profileManager.Contains("public static bool hasSessionCard()") &&
              profileManager.Contains("public static bool pollSessionCardPresence()") &&
              themeApi.Contains("public bool hasSessionCard()") &&
              themeApi.Contains("public bool pollSessionCardPresence()") &&
              allClearLua.Contains("StartCardRemovalGate = function") &&
              allClearLua.Contains("tm.profile.pollSessionCardPresence()") &&
              mainTree.Contains("allclear-remove-card-indicator"),
            "Authenticated USB sessions are not detached from media or gated at All Clear exit.",
            failures);
        Check(themeOptionsLua.Contains("CurrentDict = function()") &&
              !themeOptionsLua.Contains("dict = tm.options.GetThemeOptions") &&
              gameLua.Contains("themeOptions.SetBool(\"StarGuideEnabled\"") &&
              gameLua.Contains("tm.options.SaveToFile()"),
            "Profile theme settings or the Star Guide preference can still leak to Guest.",
            failures);
        Check(profileManager.Contains("public static void RestoreActiveStorageRoutes()") &&
              externalWatcher.Contains("ProfileManager.RestoreActiveStorageRoutes()") &&
              externalWatcher.Contains("ProfileManager.SessionState.LoggedIn"),
            "External score fallback can still replace the active profile route.", failures);
        Check(selectModeLua.Contains("modeOrder = {\"star\", \"pop\", \"club\"}") &&
              selectModeLua.Contains("SelectOrConfirmMode(\"club\")") &&
              selectModeLua.Contains("ShowClubUnavailable") &&
              selectModeLua.Contains("if (selectModeScreen.currentMode == \"club\") then") &&
              mainTree.Contains("club-unavailable-indicator") &&
              mainTree.Contains("MODE NOT AVAILABLE") &&
              !mainTree.Contains("club-unavailable-popup"),
            "Club Mixing must remain browsable in the carousel and reject confirmation with a non-blocking status.",
            failures);

        string loginSfxRoot = Path.Combine(uiRoot, "SFX", "Login");
        foreach (var sound in new[]
        {
            new { Name = "group_click.ogg", Source = Path.Combine(EvidenceRoot, "group_click.ogg") },
            new { Name = "keypad.ogg", Source = Path.Combine(EvidenceRoot, "keyboard", "keypad.ogg") },
            new { Name = "ok.ogg", Source = Path.Combine(EvidenceRoot, "keyboard", "ok.ogg") },
            new { Name = "back.ogg", Source = Path.Combine(EvidenceRoot, "keyboard", "back.ogg") },
            new { Name = "quit.ogg", Source = Path.Combine(EvidenceRoot, "keyboard", "quit.ogg") },
            new { Name = "warning.ogg", Source = Path.Combine(EvidenceRoot, "warning.ogg") }
        })
        {
            string packaged = Path.Combine(loginSfxRoot, sound.Name);
            Check(File.Exists(packaged),
                "Missing authentic LoginState sound: " + sound.Name, failures);
            if (File.Exists(sound.Source) && File.Exists(packaged))
                Check(Sha256(sound.Source) == Sha256(packaged),
                    sound.Name + " is not byte-identical to LoginState.", failures);
        }
        Check(manifest.Contains("loginVceAudio = {") &&
              manifest.Contains(
                  "confirm = \"Assets/UI/SFX/Login/ok.ogg\"") &&
              manifest.Contains(
                  "warning = \"Assets/UI/SFX/Login/warning.ogg\""),
            "Generated Login manifest does not expose the authentic sound map.",
            failures);

        try
        {
            var lua = new Script(CoreModules.Preset_SoftSandbox);
            lua.LoadString(manifest, null, "Login VCE Manifest.txt");
            lua.LoadString(loginLua, null, "Login VCE Flow.txt");
        }
        catch (SyntaxErrorException ex)
        {
            failures.Add("Generated Login Lua does not parse: " + ex.DecoratedMessage);
        }

        Check(profileManager.Contains(
                "public static bool createProfileWithPendingCredential(string name)") &&
              profileManager.Contains(
                "pendingCardId = null; // clear only after persistence succeeds"),
            "ProfileManager cannot safely bind first-time profiles to pending credentials.",
            failures);
        Check(themeApi.Contains(
                "public bool createProfileWithPendingCredential(string name)"),
            "ThemeProfileApi does not expose credential-bound profile creation.", failures);
        Check(profileManager.Contains("private static bool IsUsbDrive(string drive)") &&
              profileManager.Contains("kBusTypeUsb") &&
              profileManager.Contains("Provisioned first-time USB token"),
            "ProfileManager does not provision plain USB drives, including fixed-type USB enclosures.",
            failures);
        string rescanMethod = Slice(profileManager,
            "public static string rescanForToken()", "// Called from Startup.cs");
        Check(profileManager.Contains("using System.Threading.Tasks;") &&
              profileManager.Contains("Task<string> usbScanTask") &&
              profileManager.Contains("Task.Run(FindUsbToken)") &&
              profileManager.Contains("CompleteUsbScanIfReady()") &&
              !rescanMethod.Contains("Environment.GetLogicalDrives()") &&
              !rescanMethod.Contains("File.ReadAllText("),
            "USB scanning still performs drive/device I/O on Unity's main thread.",
            failures);

        return failures;
    }

    private static string Read(string path)
    {
        return File.Exists(path) ? File.ReadAllText(path).Replace("\r\n", "\n") : "";
    }

    private static string Sha256(string path)
    {
        using (SHA256 sha = SHA256.Create())
        using (FileStream stream = File.OpenRead(path))
            return BitConverter.ToString(sha.ComputeHash(stream));
    }

    private static void Check(bool condition, string failure,
        ICollection<string> failures)
    {
        if (!condition) failures.Add(failure);
    }

    private static int CountOccurrences(string text, string value)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }
        return count;
    }

    private static string Slice(string text, string start, string end)
    {
        int startIndex = text.IndexOf(start, StringComparison.Ordinal);
        if (startIndex < 0) return "";
        if (string.IsNullOrEmpty(end)) return text.Substring(startIndex);
        int endIndex = text.IndexOf(end, startIndex, StringComparison.Ordinal);
        return endIndex < 0
            ? text.Substring(startIndex)
            : text.Substring(startIndex, endIndex - startIndex);
    }
}
