using UnityEngine;

// Menu backdrop: a racked table with the camera slowly circling it.
public class MenuTableOrbit : MonoBehaviour
{
    [SerializeField] private TableView _table;
    [SerializeField] private MatchConfig _config;
    [SerializeField] private float _degreesPerSecond = 6f;
    [SerializeField] private float _radius = 2.3f;
    [SerializeField] private float _height = 1.25f;
    [SerializeField] private float _startYaw = 200f;

    private float _yaw;

    private void Start()
    {
        _yaw = _startYaw;
        BallState[] balls = RackBuilder.Build(new System.Random(7), _table.Geometry, _config.RackGap);
        _table.ShowState(balls);
    }

    private void LateUpdate()
    {
        _yaw += _degreesPerSecond * Time.unscaledDeltaTime;
        Vector3 center = _table.TableToWorld(new Vector2(0f, 0.25f), 0f);
        Quaternion around = Quaternion.Euler(0f, _yaw, 0f);
        Vector3 position = center + around * (Vector3.back * _radius) + Vector3.up * _height;
        transform.SetPositionAndRotation(position, Quaternion.LookRotation(center - position, Vector3.up));
    }
}
