using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 건물 효과 표식 한 칸. 값 주입만 받는 순수 View다(UI_ConquestRewardSlot과 같은 형태).
///
/// 이 프리팹의 Image·Text는 반드시 Raycast Target을 꺼야 한다 - 건물 위에 겹쳐 뜨는 월드
/// 오브젝트라, 켜두면 EventSystem.IsPointerOverGameObject가 참이 되어 건물 배치·인구 배치·
/// 카메라 드래그가 전부 막힌다(BuildingPlacementController·WorkerModeController·CameraController가
/// 그 검사로 월드 입력을 차단한다).
/// </summary>
public class UI_BuildingEffectIconSlot : MonoBehaviour
{
    [Tooltip("효과 아이콘. 스프라이트가 배정되지 않은 종류는 그리지 않는다.")]
    [SerializeField] private Image _iconImage;

    [Tooltip("아이콘 옆 수치 라벨(-30%, 2 등). 수치가 없는 종류는 그리지 않는다.")]
    [SerializeField] private TMP_Text _amountText;

    /// <summary>
    /// 슬롯은 ComponentPool로 재사용되므로 값이 없을 때도 반드시 대입한다 - 대입을 건너뛰면
    /// 직전 건물의 아이콘·숫자가 그대로 남는다(UI_ChunkInfoCard.Setup과 같은 이유).
    /// </summary>
    public void Setup(Sprite icon, Color iconColor, string amountLabel)
    {
        if (_iconImage != null)
        {
            _iconImage.sprite = icon;
            _iconImage.color = iconColor;
            _iconImage.enabled = icon != null;
        }

        if (_amountText != null)
        {
            _amountText.text = amountLabel;
            _amountText.enabled = !string.IsNullOrEmpty(amountLabel);
        }
    }
}
