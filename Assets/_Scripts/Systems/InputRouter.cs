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
        if (SceneLoader.Instance != null && SceneLoader.Instance.IsLoading)
            return;

        // The top popup decides what Back means (close, resume, or ignore).
        if (TooltipManager.Instance != null && TooltipManager.Instance.IsShown)
            TooltipManager.Instance.Hide();
        if (PopupManager.Instance != null && PopupManager.Instance.HideTop(fromBack: true))
            return;

        if (SceneManager.GetActiveScene().name == MatchSceneName)
            GameEvents.RaisePauseRequested();
    }
}
