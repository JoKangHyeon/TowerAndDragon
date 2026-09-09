using UnityEngine;

#if UNITY_EDITOR
using TMPro;
#endif

// [에디터 테스트 전용] 인구/자원 보유량 실시간 디버그 표시. 점령(인구 배치/반환/보상)과 생산시설 배치로 인한
// 수량 변화를 텍스트로 바로 확인하기 위한 용도(PopulationDebugPanel 전용).
// 본문 전체가 #if UNITY_EDITOR 안에 있어 빌드에서는 컴파일되지 않는다
// (클래스 껍데기만 남아 씬/프리팹의 컴포넌트 참조가 Missing Script가 되지 않는다).
public class PopulationDebugDisplay : MonoBehaviour
{
#if UNITY_EDITOR
    [SerializeField] private PopulationManager _populationManager;
    [SerializeField] private ResourceManager _resourceManager;

    [SerializeField] private TMP_Text _populationText;
    [SerializeField] private TMP_Text _foodText;
    [SerializeField] private TMP_Text _woodText;
    [SerializeField] private TMP_Text _stoneText;

    private void OnEnable()
    {
        if (_populationManager != null)
            _populationManager.PopulationChanged.AddListener(HandlePopulationChanged);

        if (_resourceManager != null)
            _resourceManager.ResourceChanged.AddListener(HandleResourceChanged);

        RefreshAll();
    }

    private void OnDisable()
    {
        if (_populationManager != null)
            _populationManager.PopulationChanged.RemoveListener(HandlePopulationChanged);

        if (_resourceManager != null)
            _resourceManager.ResourceChanged.RemoveListener(HandleResourceChanged);
    }

    // 이벤트는 변경 시에만 발생하므로, 패널이 켜진 시점의 현재값을 한 번 직접 채워준다.
    private void RefreshAll()
    {
        if (_populationManager != null)
            HandlePopulationChanged(_populationManager.CurrentState);

        if (_resourceManager == null)
            return;

        RefreshResourceText(_foodText, ResourceType.Food, _resourceManager.GetAmount(ResourceType.Food));
        RefreshResourceText(_woodText, ResourceType.Wood, _resourceManager.GetAmount(ResourceType.Wood));
        RefreshResourceText(_stoneText, ResourceType.Stone, _resourceManager.GetAmount(ResourceType.Stone));
    }

    private void HandlePopulationChanged(PopulationState state)
    {
        if (_populationText != null)
            _populationText.text = $"Population {state.AssignedPopulation}/{state.MaxPopulation} (Available {state.AvailablePopulation})";
    }

    // Food/Wood/Stone 외의 자원 변경은 이 패널이 다루지 않으므로 조용히 무시한다.
    private void HandleResourceChanged(ResourceType type, int amount)
    {
        switch (type)
        {
            case ResourceType.Food:
                RefreshResourceText(_foodText, type, amount);
                break;
            case ResourceType.Wood:
                RefreshResourceText(_woodText, type, amount);
                break;
            case ResourceType.Stone:
                RefreshResourceText(_stoneText, type, amount);
                break;
        }
    }

    private static void RefreshResourceText(TMP_Text text, ResourceType type, int amount)
    {
        if (text != null)
            text.text = $"{type}: {amount}";
    }
#endif
}
