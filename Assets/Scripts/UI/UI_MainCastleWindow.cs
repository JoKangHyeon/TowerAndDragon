using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_MainCastleWindow : MonoBehaviour
{
    [SerializeField]
    private GameManager _gameManager;

    [SerializeField]
    private DragonTreeManager _dragonTreeManager;

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
        bool changed = _gameManager.CurrentRun.CurrentDragon.TryChangeType(_currentSelectedType);

        // DragonTreeManager는 이 알림 없이는 속성 변경을 감지할 수 없다(Dragon.OnDragonTypeChanged가
        // 어디서도 invoke되지 않음 - DragonTreeManager.cs 주석 참고). 변경이 실제로 적용됐을 때만
        // 알려야 낮 1회 제한(IsChangedThisDay)에 막힌 시도까지 HUD를 불필요하게 재바인딩하지 않는다.
        if (changed && _dragonTreeManager != null)
        {
            _dragonTreeManager.NotifyActiveAttributeChanged();
        }
    }

    public void Render()
    {

    }
}
