using System.Collections.Generic;
using MoonSharp.Interpreter;
using UnityEngine;

namespace ThemeApi
{
    // Exposed to Lua as tm.profile
    // All methods delegate to ProfileManager (static C# class).
    // 
    // Typical Lua usage:
    //
    //   -- At SPLASH scene, first frame:
    //   if tm.profile.hasPendingToken() then
    //       transitionTo("WARNING")  -- auto-proceed
    //   end
    //
    //   -- At LOGIN scene, after USB/NFC detected:
    //   local ok = tm.profile.loginByToken(cardId)
    //   if ok then
    //       transitionTo("LOADING")
    //   else
    //       -- first time: show name-entry UI, then:
    //       tm.profile.createProfile(name)
    //   end
    //
    //   -- To show profile name in UI:
    //   local name = tm.profile.currentProfile()  -- "Guest" or player name
    //
    //   -- To list profiles for a picker:
    //   local profiles = tm.profile.listProfiles()  -- array table
    //   for i, name in ipairs(profiles) do ... end
    //
    //   -- Register for profile-change notifications (optional):
    //   tm.profile.setOnProfileChanged(function() refreshProfileUI() end)

    [MoonSharpUserData]
    public class ThemeProfileApi
    {
        // ── Session queries ───────────────────────────────────────────────────

        public string currentProfile()
        {
            return ProfileManager.currentProfile();
        }

        public bool isGuest()
        {
            return ProfileManager.state == ProfileManager.SessionState.Guest;
        }

        // Returns true if a USB token was found at boot and loginByToken()
        // has not yet been called to consume it.
        public bool hasPendingToken()
        {
            return ProfileManager.hasPendingToken();
        }

        // Preferred reader-neutral contract. Themes never call the NFC DLL or
        // enumerate USB volumes directly.
        public bool pollForCredential()
        {
            return ProfileManager.pollForCredential();
        }

        // Starts a new arcade Login session and arms both credential readers.
        public void beginLoginCredentialLoop()
        {
            ProfileManager.beginLoginCredentialLoop();
        }

        // Dismisses only the card/USB that opened the unknown-card screen.
        // A different credential can then auto-authenticate immediately.
        public void dismissPendingCredential()
        {
            ProfileManager.dismissPendingCredential();
        }

        // An explicit "read the card that is on the reader now" press. Unlike
        // pollForCredential it re-admits a credential dismissed by Back or by
        // the walk home, which automatic scanning must keep ignoring.
        public bool requestPresentedCredential()
        {
            return ProfileManager.requestPresentedCredential();
        }

        public string pendingCredentialKind()
        {
            return ProfileManager.pendingCredentialKind();
        }

        // Login using the boot-time USB token (no need to pass cardId).
        // Returns true if a local profile matched, false if new/unknown card.
        public bool loginWithPendingToken()
        {
            return ProfileManager.loginWithPendingToken();
        }

        public bool loginWithPendingCredential()
        {
            return ProfileManager.loginWithPendingCredential();
        }

        // Rescan USB drives now. Returns cardId string if found, null otherwise.
        // Call periodically from the LOGIN scene coroutine.
        public string rescanForToken()
        {
            return ProfileManager.rescanForToken();
        }

        // Returns the last known presence of the USB/card which opened the
        // current login flow while the next check runs off the Unity thread.
        public bool pollPendingTokenPresence()
        {
            return ProfileManager.pollPendingTokenPresence();
        }

        public bool hasSessionCard()
        {
            return ProfileManager.hasSessionCard();
        }

        public string sessionCredentialKind()
        {
            return ProfileManager.sessionCredentialKind();
        }

        public bool pollSessionCardPresence()
        {
            return ProfileManager.pollSessionCardPresence();
        }

        // Returns a Lua array table of profile names (excluding Guest).
        public DynValue listProfiles()
        {
            List<string> names = ProfileManager.listProfiles();
            Table t = new Table(ScriptSession.session);
            for (int i = 0; i < names.Count; i++)
                t.Set(i + 1, DynValue.NewString(names[i]));
            return DynValue.NewTable(t);
        }

        public bool profileNameExists(string name)
        {
            return ProfileManager.profileNameExists(name);
        }

        public long djExp()
        {
            return ProfileManager.currentDjExp();
        }

        public int djLevel()
        {
            return ProfileManager.currentDjLevel();
        }

        public long nextDjLevelExp()
        {
            return ProfileManager.nextDjLevelExp();
        }

        // ── Login / logout ────────────────────────────────────────────────────

        // Returns true if a matching local profile was found and logged in.
        // Returns false if no match (caller should call createProfile).
        public bool loginByToken(string cardId)
        {
            return ProfileManager.loginByToken(cardId);
        }

        // Logs into an existing profile by name directly.
        public void login(string name)
        {
            ProfileManager.login(name);
        }

        // Creates a new profile (generates cardId, writes files) then logs in.
        public void createProfile(string name)
        {
            ProfileManager.createProfile(name);
        }

        // Creates a first-time profile using the USB token retained by the
        // Login flow. Returns false without consuming it when validation or
        // persistence fails, allowing the player to correct and retry.
        public bool createProfileWithPendingToken(string name)
        {
            return ProfileManager.createProfileWithPendingToken(name);
        }

        public bool createProfileWithPendingCredential(string name)
        {
            return ProfileManager.createProfileWithPendingCredential(name);
        }

        public bool beginCredentialLink()
        {
            return ProfileManager.beginCredentialLink();
        }

        public bool linkPendingCredential()
        {
            return ProfileManager.linkPendingCredential();
        }

        public void cancelCredentialLink()
        {
            ProfileManager.cancelCredentialLink();
        }

        public string credentialLinkStatus()
        {
            return ProfileManager.currentCredentialLinkStatus();
        }

        // Logs out, restores Guest session.
        public void logout()
        {
            ProfileManager.logout();
        }

        // ── profileChanged event ──────────────────────────────────────────────

        // Optional. Theme registers a no-arg Lua function to be called
        // whenever login/logout fires. Correctness does NOT depend on
        // this handler being registered — scores/options work either way.
        // Cleared on theme session end by ClearOnProfileChanged().
        private DynValue onProfileChanged;

        public void setOnProfileChanged(DynValue callback)
        {
            // Remove previous handler first (safe to call multiple times).
            ProfileManager.profileChanged -= InvokeProfileChanged;

            onProfileChanged = callback;

            if (callback != null && callback.Type == DataType.Function)
                ProfileManager.profileChanged += InvokeProfileChanged;
        }

        private void InvokeProfileChanged()
        {
            if (onProfileChanged == null) return;
            if (onProfileChanged.Type != DataType.Function) return;
            try { onProfileChanged.Function.Call(); }
            catch (System.Exception ex)
            {
                Debug.LogWarning(
                    $"[ThemeProfileApi] profileChanged callback threw: {ex.Message}");
            }
        }

        // Called by ScriptSession when a theme session ends, so stale Lua
        // closures don't hold onto ProfileManager.profileChanged.
        [MoonSharpHidden]
        public void ClearOnProfileChanged()
        {
            ProfileManager.profileChanged -= InvokeProfileChanged;
            onProfileChanged = null;
        }
    }
}
