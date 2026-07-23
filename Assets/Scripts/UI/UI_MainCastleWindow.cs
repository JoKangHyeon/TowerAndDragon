using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_MainCastleWindow : MonoBehaviour
{
    [SerializeField]
    private GameManager _gameManager;

    [Header("UiElements")]
    [Header("Dragon")]
    [SerializeField] private Image _dragonImage;
    [SerializeField] private Button _dragonTypeChangeButton;
    [SerializeField] private Button _dragonSkillTreeButton;

    [Header("Reseach")]
    [SerializeField] private Image _currentResearch;
    [SerializeField] private Slider _reserachStatusSlider;
    [SerializeField] private Button _researchSelectButton;
    [SerializeField] private Button _reserachAddPopButton;
    [SerializeField] private Button _reserachRemovePopButton;
    [SerializeField] private TMP_Text _researchPopText;
    [SerializeField] private TMP_Text _researchTimeLeftText;

    [Header("Pop")]
    [SerializeField] private TMP_Text _currentPopText;
    [SerializeField] private TMP_Text _currentPopFoodConsumeText;



    private DragonType _currentSelectedType = (DragonType)(-1);
    

    public void SelectDragonType(DragonType dragonType)
    {
        _currentSelectedType = dragonType;
    }

    public void ApplyDragonType()
    {
        _gameManager.CurrentRun.CurrentDragon.TryChangeType(_currentSelectedType);
    }

    public void Render()
    {

    }
}
