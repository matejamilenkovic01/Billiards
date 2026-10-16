using UnityEditor;
using UnityEngine;

// Editor-only test helpers for a running Match scene (the temporary modes of commits 2-3 live on here).
public static class DebugMenu
{
    private const string Root = "Tools/Billiards/Debug/";

    [MenuItem(Root + "Bot vs Bot")]
    public static void BotVsBot() => Debug.Log(MatchController.DebugBotVsBot(true, System.Environment.TickCount));

    [MenuItem(Root + "Restart (Player Breaks)")]
    public static void PlayerBreaks() => Debug.Log(MatchController.DebugStart(true, null, System.Environment.TickCount));

    [MenuItem(Root + "Set Up Winning Shot")]
    public static void WinningShot() => Debug.Log(MatchController.DebugSetupWin());

    [MenuItem(Root + "Bot vs Bot", true)]
    [MenuItem(Root + "Restart (Player Breaks)", true)]
    [MenuItem(Root + "Set Up Winning Shot", true)]
    private static bool InMatch() => Application.isPlaying && MatchController.Instance != null;
}
