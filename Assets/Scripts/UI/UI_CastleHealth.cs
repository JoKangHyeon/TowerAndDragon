using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 메인 성 체력바 View. Castle의 체력 변화를 구독해 Slider를 갱신하기만 한다.
/// 게임 로직/HP 계산은 하지 않으며, 성이 데미지를 어떻게 받는지 알지 못한다.
/// </summary>
public class UI_CastleHealth : MonoBehaviour
{
    [SerializeField] private Castle _castle;
    [SerializeField] private Slider _bar;

    private void OnEnable()
    {
        _castle.HealthChanged += Render;
        // 구독 시점과 무관하게 즉시 현재값 반영 (창을 껐다 다시 켠 경우 포함).
        Render(_castle.CurrentHealth, _castle.MaxHealth);
    }

    private void OnDisable()
    {
        _castle.HealthChanged -= Render;
    }

    private void Render(float current, float max)
    {
        _bar.value = max > 0 ? current / max : 0;
    }
}
