using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>추천 조합(프리셋) 버튼 하나.
///
/// 카탈로그의 <see cref="RunMutatorCatalogSO.Presets"/> 길이만큼 복제된다 - 프리셋 개수를
/// 코드나 프리팹에 박아 두지 않는다. 지금 카탈로그에는 프리셋이 0개라 줄이 비어 있고,
/// T6이 저작하면 그대로 늘어난다.
/// </summary>
public sealed class UI_MutatorPresetButton : MonoBehaviour
{
    [Tooltip("버튼 - 보통 이 오브젝트 자신의 Button.")]
    [SerializeField] private Button _button;

    [Tooltip("프리셋 이름. key는 데이터가 갖고 있어 코드가 SetKey로 넣는다.")]
    [SerializeField] private LocalizedText _nameLabel;

    [Tooltip("지금 이 프리셋과 같은 조합임을 보여주는 표식. 비워 두면 표시가 없다.")]
    [WiringOptional]
    [SerializeField] private GameObject _activeMark;

    private int _presetIndex;

    private Action<int> _onClicked;

    private void Awake()
    {
        if (WiringGuard.Require(_button, nameof(_button), this))
        {
            _button.onClick.AddListener(HandleClicked);
        }
    }

    private void OnDestroy()
    {
        if (_button != null)
        {
            _button.onClick.RemoveListener(HandleClicked);
        }
    }

    public void Setup(int presetIndex, string nameLocKey, bool isActive, Action<int> onClicked)
    {
        _presetIndex = presetIndex;
        _onClicked = onClicked;

        if (_nameLabel != null)
        {
            _nameLabel.SetKey(nameLocKey);
        }

        if (_activeMark != null)
        {
            _activeMark.SetActive(isActive);
        }
    }

    private void HandleClicked()
    {
        _onClicked?.Invoke(_presetIndex);
    }
}
