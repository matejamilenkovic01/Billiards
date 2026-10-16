using UnityEngine;

// Turns pocket events into screen positions, so the UI tray can fly balls in from the right pocket.
public class ShotFxView : MonoBehaviour
{
    [SerializeField] private TableView _table;
    [SerializeField] private Camera _camera;

    private void OnEnable() => GameEvents.OnBallPocketed += HandleBallPocketed;

    private void OnDisable() => GameEvents.OnBallPocketed -= HandleBallPocketed;

    private void HandleBallPocketed(int number, int pocketIndex)
    {
        if (number == BallGroups.CueBall)
            return;
        Vector3 screen = _camera.WorldToScreenPoint(_table.PocketWorld(pocketIndex));
        GameEvents.RaiseBallPocketedOnScreen(number, screen);
    }
}
