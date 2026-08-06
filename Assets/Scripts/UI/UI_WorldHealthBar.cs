using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 몬스터/타워 머리 위에 표시하는 월드 스페이스 체력바. 값 주입만 받는 순수 View.
/// 무손상(체력 만피 + 방어막 무손상) 상태에서는 표시하지 않는다. 방어막은 체력 위에 회색으로
/// 덮어 그리다가 깎이는 만큼 빨간 체력이 드러난다. 파괴(체력 0)되어 부활 대기 중인 타워는
/// IReviveProgress를 통해 부활 게이지로 전환해 보여준다.
/// </summary>
public class UI_WorldHealthBar : MonoBehaviour
{
    [Tooltip("숨김 여부를 토글하는 대상 Canvas. SetActive 대신 이걸 꺼서, Update()가 계속 돌게 한다.")]
    [SerializeField] private Canvas _canvas;

    [Tooltip("체력/부활 진행도를 채우는 Fill 이미지 (Image Type = Filled).")]
    [SerializeField] private Image _fillImage;

    [Tooltip("방어막 양을 체력 Fill 위에 덮어 그리는 회색 Fill 이미지 (Image Type = Filled).")]
    [SerializeField] private Image _shieldFillImage;

    [Tooltip("피해를 입었을 때 Fill 색상.")]
    [SerializeField] private Color _healthColor = new Color(0.85f, 0.2f, 0.2f);

    [Tooltip("방어막이 남아있을 때 Fill 색상. 알파는 반드시 1이어야 한다 - 1 미만이면 아래 체력색과 섞여 회색이 아니라 탁한 색으로 보인다.")]
    [SerializeField] private Color _shieldColor = new Color(0.7f, 0.7f, 0.7f);

    [Tooltip("파괴 후 부활 대기 중일 때 Fill 색상.")]
    [SerializeField] private Color _reviveColor = new Color(0.3f, 0.6f, 1f);

    [Tooltip("SpriteRenderer를 찾지 못했을 때 쓰는 기본 Y 오프셋(월드 유닛).")]
    [SerializeField] private float _fallbackYOffset = 0.6f;

    [Tooltip("스프라이트 위쪽 끝에서 얼마나 더 띄울지(월드 유닛).")]
    [SerializeField] private float _yPadding = 0.1f;

    private Health _health;
    private IReviveProgress _reviveSource;
    private IShieldInfo _shieldSource;

    /// <summary>주어진 Health/부활 진행도/방어막 소스에 바를 연결한다. 이미 다른 대상에 연결돼 있었다면 먼저 해제한다.</summary>
    public void Bind(Health health, IReviveProgress reviveSource, IShieldInfo shieldSource, SpriteRenderer ownerRenderer)
    {
        Unbind();

        _health = health;
        _reviveSource = reviveSource;
        _shieldSource = shieldSource;

        ApplyScaleCompensation();
        ApplyYOffset(ownerRenderer);

        if (_health != null)
        {
            // 대상(몬스터/타워)이 런타임에 동적으로 주입되므로, Awake/OnEnable이 아니라
            // 여기(Bind)에서 구독한다 - 구독 직후 현재 값을 한 번 수동으로 반영해 초기 발화를 놓쳐도 안전하게 한다.
            _health.HealthChanged.AddListener(HandleHealthChanged);
        }

        Render();

        // 부활 대기 중이거나 방어막을 보유한 동안만 매 프레임 폴링한다(IShieldInfo는 이벤트가 없는
        // 폴링 전용 계약이라 여기서 갱신을 받는다) - 아무것도 없는 몬스터는 계속 Update를 돌리지 않는다.
        enabled = _reviveSource != null || _shieldSource != null;
    }

    private void Unbind()
    {
        if (_health != null)
        {
            _health.HealthChanged.RemoveListener(HandleHealthChanged);
        }

        _health = null;
        _reviveSource = null;
        _shieldSource = null;
        enabled = false;
    }

    private void OnDestroy() => Unbind();

    private void Update()
    {
        if (_reviveSource != null && _reviveSource.IsReviving)
        {
            RenderRevive();
            return;
        }

        Render();
    }

    private void HandleHealthChanged(float current, float max) => Render();

    private void Render()
    {
        if (_health == null)
        {
            return;
        }

        if (_reviveSource != null && _reviveSource.IsReviving)
        {
            RenderRevive();
            return;
        }

        float maxHealth = _health.MaxHealth;
        bool hasShield = _shieldSource != null && _shieldSource.HasShield;
        bool isUndamaged =
            maxHealth <= 0f ||
            (_health.CurrentHealth >= maxHealth && (!hasShield || _shieldSource.IsIntact));

        SetHidden(isUndamaged);

        if (isUndamaged)
        {
            return;
        }

        if (_fillImage != null)
        {
            _fillImage.color = _healthColor;
            _fillImage.fillAmount = Mathf.Clamp01(_health.CurrentHealth / maxHealth);
        }

        if (_shieldFillImage != null)
        {
            // 죽는 순간(방어막을 우회하는 즉사 등)에는 방어막이 남아 있어도 회색을 보이지 않는다.
            float shieldRatio = !hasShield || _health.IsDead
                ? 0f
                : Mathf.Clamp01(_shieldSource.CurrentShield / maxHealth);

            _shieldFillImage.color = _shieldColor;
            _shieldFillImage.fillAmount = shieldRatio;
        }
    }

    private void RenderRevive()
    {
        SetHidden(false);

        if (_shieldFillImage != null)
        {
            _shieldFillImage.fillAmount = 0f;
        }

        if (!WiringGuard.Require(_fillImage, nameof(_fillImage), this))
        {
            return;
        }

        _fillImage.color = _reviveColor;
        _fillImage.fillAmount = Mathf.Clamp01(_reviveSource.ReviveProgress);
    }

    private void SetHidden(bool isHidden)
    {
        if (_canvas != null)
        {
            _canvas.enabled = !isHidden;
        }
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
