using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Esc in the Editor and the Android Back button both arrive as Keyboard.escapeKey.
public class InputRouter : SingletonPersistent<InputRouter>
{
    public const string MenuSceneName = "Menu";
    public const string MatchSceneName = "Match";

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame)
            return;

        HandleBack();
    }

    private void HandleBack()
    {
        if (SceneManager.GetActiveScene().name == MatchSceneName)
            GameEvents.RaisePauseRequested();
    }
}
