using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 몬스터/타워 머리 위에 표시하는 월드 스페이스 체력바. 값 주입만 받는 순수 View.
/// 만피 상태에서는 표시하지 않는다. 파괴(체력 0)되어 부활 대기 중인 타워는
/// IReviveProgress를 통해 부활 게이지로 전환해 보여준다.
/// </summary>
public class UI_WorldHealthBar : MonoBehaviour
{
    [Tooltip("체력/부활 진행도를 채우는 Fill 이미지 (Image Type = Filled).")]
    [SerializeField] private Image _fillImage;

    [Tooltip("피해를 입었을 때 Fill 색상.")]
    [SerializeField] private Color _healthColor = new Color(0.85f, 0.2f, 0.2f);

    [Tooltip("파괴 후 부활 대기 중일 때 Fill 색상.")]
    [SerializeField] private Color _reviveColor = new Color(0.3f, 0.6f, 1f);

    [Tooltip("SpriteRenderer를 찾지 못했을 때 쓰는 기본 Y 오프셋(월드 유닛).")]
    [SerializeField] private float _fallbackYOffset = 0.6f;

    [Tooltip("스프라이트 위쪽 끝에서 얼마나 더 띄울지(월드 유닛).")]
    [SerializeField] private float _yPadding = 0.1f;

    private Health _health;
    private IReviveProgress _reviveSource;

    /// <summary>주어진 Health/부활 진행도 소스에 바를 연결한다. 이미 다른 대상에 연결돼 있었다면 먼저 해제한다.</summary>
    public void Bind(Health health, IReviveProgress reviveSource, SpriteRenderer ownerRenderer)
    {
        Unbind();

        _health = health;
        _reviveSource = reviveSource;

        ApplyScaleCompensation();
        ApplyYOffset(ownerRenderer);

        if (_health != null)
        {
            _health.HealthChanged.AddListener(HandleHealthChanged);
            _health.Died.AddListener(HandleDied);
            // 구독 직후 현재 값을 한 번 수동으로 반영한다 - 초기 발화를 놓쳐도 안전하도록.
            Render(_health.CurrentHealth, _health.MaxHealth);
        }

        // 부활 대기 중일 때만 매 프레임 진행도를 폴링한다 - 몬스터(부활 없음)는 Update를 돌리지 않는다.
        enabled = _reviveSource != null;
    }

    private void Unbind()
    {
        if (_health != null)
        {
            _health.HealthChanged.RemoveListener(HandleHealthChanged);
            _health.Died.RemoveListener(HandleDied);
        }

        _health = null;
        _reviveSource = null;
        enabled = false;
    }

    private void OnDestroy() => Unbind();

    private void Update()
    {
        if (_reviveSource != null && _reviveSource.IsReviving)
        {
            RenderRevive();
        }
    }

    private void HandleHealthChanged(float current, float max) => Render(current, max);

    private void HandleDied()
    {
        // 타워는 곧이어 부활 대기(IsReviving)로 전환되므로 이후 Update가 이어받는다.
        // 몬스터는 Died 직후 파괴되므로(BaseMonster.HandleDeath) 이 갱신은 잠깐 빈 바를 보였다 함께 사라진다.
        if (_reviveSource != null && _reviveSource.IsReviving)
        {
            RenderRevive();
        }
    }

    private void Render(float current, float max)
    {
        if (_reviveSource != null && _reviveSource.IsReviving)
        {
            RenderRevive();
            return;
        }

        bool isFullHealth = max <= 0f || current >= max;
        gameObject.SetActive(!isFullHealth);

        if (isFullHealth || _fillImage == null)
        {
            return;
        }

        _fillImage.color = _healthColor;
        _fillImage.fillAmount = Mathf.Clamp01(current / max);
    }

    private void RenderRevive()
    {
        gameObject.SetActive(true);

        if (_fillImage == null)
        {
            return;
        }

        _fillImage.color = _reviveColor;
        _fillImage.fillAmount = Mathf.Clamp01(_reviveSource.ReviveProgress);
    }

    // 프리팹마다 루트 스케일이 제각각이라(0.12배~3배), 부모 스케일의 역수를 걸어
    // 모든 개체 위에서 바가 화면상 같은 크기로 보이게 한다. 회전으로 인한 미러(음수 스케일)도 함께 상쇄된다.
    private void ApplyScaleCompensation()
    {
        Vector3 parentLossyScale = transform.parent != null
            ? transform.parent.lossyScale
            : Vector3.one;

        transform.localScale = new Vector3(
            SafeReciprocal(parentLossyScale.x),
            SafeReciprocal(parentLossyScale.y),
            SafeReciprocal(parentLossyScale.z));
    }

    private static float SafeReciprocal(float value) =>
        Mathf.Approximately(value, 0f) ? 1f : 1f / value;

    // 소유 스프라이트 위쪽 끝 + 패딩 위치에 바를 놓는다. 렌더러가 없으면 고정 오프셋으로 대체한다.
    private void ApplyYOffset(SpriteRenderer ownerRenderer)
    {
        Transform parent = transform.parent;
        float parentScaleY = parent != null ? parent.lossyScale.y : 1f;

        float localOffsetY;
        if (ownerRenderer != null && parent != null && !Mathf.Approximately(parentScaleY, 0f))
        {
            float worldTopY = ownerRenderer.bounds.max.y + _yPadding;
            localOffsetY = (worldTopY - parent.position.y) / parentScaleY;
        }
        else
        {
            localOffsetY = Mathf.Approximately(parentScaleY, 0f)
                ? _fallbackYOffset
                : _fallbackYOffset / parentScaleY;
        }

        transform.localPosition = new Vector3(0f, localOffsetY, 0f);
    }
}
