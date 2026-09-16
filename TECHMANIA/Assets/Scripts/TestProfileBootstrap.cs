using UnityEngine;
public class TestProfileBootstrap : MonoBehaviour
{
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1))
            ProfileManager.createProfile("TestPlayer");
        if (Input.GetKeyDown(KeyCode.F2))
            ProfileManager.logout();
        if (Input.GetKeyDown(KeyCode.F3))
            Debug.Log(ProfileManager.currentProfile());
    }
}