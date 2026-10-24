using TMPro;
using UnityEngine;
using UnityEngine.UI;

// A CPU character's portrait; a generated avatar carries the character's initials (v3 V13).
public class PortraitView : MonoBehaviour
{
    [SerializeField] private Image _image;
    [SerializeField] private TMP_Text _initials;

    public void Bind(OpponentViewData opponent)
    {
        gameObject.SetActive(opponent != null);
        if (opponent == null)
            return;
        if (opponent.Portrait != null)
            _image.sprite = opponent.Portrait;
        _initials.gameObject.SetActive(opponent.GeneratedPortrait);
        _initials.text = opponent.Initials;
    }
}
