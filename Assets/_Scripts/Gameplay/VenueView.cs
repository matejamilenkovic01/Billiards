using UnityEngine;

// Applies the venue theme picked for this match (MatchSetup.VenueId, else the first) to the table, its light,
// the room (one of the rooms in the scene is on), the reflections and the ambient, without new materials: tints go
// through property blocks. The menu applies the player's current division's venue.
public class VenueView : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

    [SerializeField] private VenueTheme[] _themes;
    [Tooltip("One room per theme, same order; only the current one is active.")]
    [SerializeField] private GameObject[] _rooms;
    [SerializeField] private ReflectionProbe _probe;
    [SerializeField] private Renderer[] _feltRenderers;
    [SerializeField] private Renderer[] _cushionRenderers;
    [SerializeField] private Renderer[] _capRenderers;
    [SerializeField] private Renderer[] _woodRenderers;
    [SerializeField] private Light _tableLight;
    [SerializeField] private Renderer _lampPanel;

    public VenueTheme Current { get; private set; }

    private void Awake() => Apply(Find(MatchSetup.VenueId));

    public void Apply(string venueId) => Apply(Find(venueId));

    private VenueTheme Find(string id)
    {
        if (_themes == null || _themes.Length == 0)
            return null;
        foreach (VenueTheme theme in _themes)
        {
            if (theme != null && theme.Id == id)
                return theme;
        }
        return _themes[0];
    }

    public void Apply(VenueTheme theme)
    {
        if (theme == null)
            return;
        Current = theme;
        var block = new MaterialPropertyBlock();
        block.SetColor(BaseColorId, theme.FeltColor);
        foreach (Renderer felt in _feltRenderers)
            felt.SetPropertyBlock(block);
        block.SetColor(BaseColorId, theme.FeltColor * theme.CushionShade);
        foreach (Renderer cushion in _cushionRenderers)
            cushion.SetPropertyBlock(block);
        if (theme.CapMaterial != null)
        {
            foreach (Renderer cap in _capRenderers)
                cap.sharedMaterial = theme.CapMaterial;
        }
        if (theme.WoodMaterial != null)
        {
            foreach (Renderer wood in _woodRenderers)
                wood.sharedMaterial = theme.WoodMaterial;
        }
        if (_tableLight != null)
        {
            _tableLight.color = theme.TableLightColor;
            _tableLight.intensity = theme.TableLightIntensity;
        }
        if (_lampPanel != null)
        {
            var panel = new MaterialPropertyBlock();
            panel.SetColor(EmissionId, theme.LampPanelEmission);
            _lampPanel.SetPropertyBlock(panel);
        }
        RenderSettings.ambientLight = theme.AmbientColor;

        int index = System.Array.IndexOf(_themes, theme);
        if (_rooms != null)
        {
            for (int i = 0; i < _rooms.Length; i++)
            {
                if (_rooms[i] != null)
                    _rooms[i].SetActive(i == index);
            }
        }
        if (_probe != null && theme.Reflection != null)
            _probe.customBakedTexture = theme.Reflection;
    }
}
