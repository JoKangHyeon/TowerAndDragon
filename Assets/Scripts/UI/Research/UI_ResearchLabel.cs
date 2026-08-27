using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 연구 트리의 글자표 하나. 갈래 이름표(타워/생산/편의)와 티어 라벨이 함께 쓴다.
//
// 창은 프리팹 속을 모르고 이 컴포넌트만 다룬다. 예전에는 TextMeshProUGUI를 그대로 프리팹으로
// 물었는데, 프리팹에 배경 이미지가 붙자 참조가 끊겼다. 자식이 된 글자를 다시 물려도 소용없다 -
// Instantiate는 그 컴포넌트가 붙은 오브젝트와 자식만 복제하므로 부모인 배경이 떨어져 나간다.
// 그래서 루트에 이 컴포넌트를 두고 창은 루트를 복제한다. 프리팹에 장식을 더 붙여도 창은 그대로다.
[RequireComponent(typeof(RectTransform))]
public sealed class UI_ResearchLabel : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _text;

    [Tooltip("글자 뒤 판. 갈래 이름표에서만 켠다 - 티어 라벨은 배경 없이 옅게 깔리는 글자다.")]
    [WiringOptional]
    [SerializeField] private Image _background;

    /// <summary>자리를 잡을 때 쓰는 루트. 글자가 아니라 이쪽을 옮겨야 배경도 함께 따라온다.</summary>
    public RectTransform Rect => (RectTransform)transform;

    public TextMeshProUGUI Text => _text;

    /// <summary>
    /// 배경판을 켜고 끈다. 오브젝트가 아니라 Image만 끄는 이유는 배경이 루트에 붙어 있어도
    /// 안전하게 하기 위함이다 - 오브젝트를 끄면 글자까지 사라진다.
    /// </summary>
    public void SetBackgroundVisible(bool visible)
    {
        if (_background != null)
        {
            _background.enabled = visible;
        }
    }
}
