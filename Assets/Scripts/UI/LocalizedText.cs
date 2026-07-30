using TMPro;
using UnityEngine;

/// <summary>
/// 스트링테이블 key 하나를 TMP_Text에 자동 반영하는 컴포넌트.
/// TMP_Text와 같은 오브젝트에 붙이고, 인스펙터에서 key를 지정한다.
/// - 활성화될 때 현재 언어로 글자를 채우고, 언어가 바뀌면(OnLanguageChanged) 스스로 다시 그린다.
/// - "디자인 시점에 고정된 라벨"용. {0} 등 런타임 값이 들어가는 텍스트나
///   코드가 매번 갱신하는 텍스트(예: 데이터 바인딩)에는 붙이지 않는다.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class LocalizedText : MonoBehaviour
{
    [Tooltip("스트링테이블 key. 예: research_window_header")]
    [SerializeField] private string _locKey;

    private TMP_Text _text;

    private TMP_Text Text
    {
        get
        {
            if (_text == null)
            {
                _text = GetComponent<TMP_Text>();
            }
            return _text;
        }
    }

    private void OnEnable()
    {
        // 구독은 OnEnable에서(활성화 동안만 유효). 구독 직후 현재 값을 한 번 반영해 초기 발화를 놓쳐도 안전하게 한다.
        StringTable.OnLanguageChanged += Apply;
        Apply();
    }

    private void OnDisable()
    {
        StringTable.OnLanguageChanged -= Apply;
    }

    // 코드에서 key를 바꿔야 할 때 사용(즉시 반영). 인스펙터로 고정한 라벨은 호출할 필요 없다.
    public void SetKey(string locKey)
    {
        _locKey = locKey;
        Apply();
    }

    private void Apply()
    {
        if (Text != null)
        {
            Text.text = StringTable.GetString(_locKey);
        }
    }
}
