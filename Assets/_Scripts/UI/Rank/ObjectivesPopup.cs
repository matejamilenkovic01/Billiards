using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ALL OBJECTIVES (v4 V12): a tab per division (the open sets and the next, locked one), the six objectives of the
// set, and at the bottom the set bonus with its progress. Opens on the player's division.
public class ObjectivesPopup : PopupBase
{
    [SerializeField] private UIButton[] _tabs;
    [SerializeField] private TMP_Text[] _tabLabels;
    [SerializeField] private Image[] _tabFrames;
    [SerializeField] private ObjectiveRowView[] _rows;
    [SerializeField] private TMP_Text _lockText;
    [SerializeField] private TMP_Text _bonus;
    [SerializeField] private Image _bonusFill;
    [SerializeField] private GameObject _bonusIcon;
    [SerializeField] private UIButton _closeButton;

    private ObjectivesViewData _view;
    private int _selected;
    private System.Action[] _tabHandlers;

    protected override void Awake()
    {
        base.Awake();
        _tabHandlers = new System.Action[_tabs.Length];
        for (int i = 0; i < _tabs.Length; i++)
        {
            int tab = i;
            _tabHandlers[i] = () => Select(tab);
        }
    }

    private void OnEnable()
    {
        _closeButton.Clicked += Hide;
        for (int i = 0; i < _tabs.Length; i++)
            _tabs[i].Clicked += _tabHandlers[i];
    }

    private void OnDisable()
    {
        _closeButton.Clicked -= Hide;
        for (int i = 0; i < _tabs.Length; i++)
            _tabs[i].Clicked -= _tabHandlers[i];
    }

    protected override void OnShow(object payload)
    {
        if (payload is not ObjectivesPopupData data)
            return;
        _view = data.Objectives;
        Select(_view.CurrentTab);
    }

    // A CLAIM changed the profile: the menu hands over the fresh view and the same tab stays open.
    public void Refresh(ObjectivesViewData view)
    {
        _view = view;
        Select(_selected);
    }

    private void Select(int tab)
    {
        if (_view == null || _view.Sets.Count == 0)
            return;
        _selected = Mathf.Clamp(tab, 0, _view.Sets.Count - 1);
        for (int i = 0; i < _tabs.Length; i++)
        {
            bool has = i < _view.Sets.Count;
            _tabs[i].gameObject.SetActive(has);
            if (!has)
                continue;
            ObjectiveSetData set = _view.Sets[i];
            _tabLabels[i].text = set.Name;
            _tabFrames[i].gameObject.SetActive(i == _selected);
            _tabLabels[i].alpha = set.Locked ? 0.6f : 1f;
        }
        ObjectiveSetData current = _view.Sets[_selected];
        for (int i = 0; i < _rows.Length; i++)
            _rows[i].Bind(i < current.Objectives.Count ? current.Objectives[i] : null, _view.CueGap);
        _lockText.gameObject.SetActive(current.Locked);
        _lockText.text = current.LockText;
        _bonus.text = current.BonusPaid ? $"SET BONUS  {current.Bonus}  ✓" : $"SET BONUS  {current.Bonus}  ·  {current.Claimed}/{current.Total}";
        _bonusFill.fillAmount = current.Total > 0 ? current.Claimed / (float)current.Total : 0f;
    }
}
