using UnityEngine;

/// <summary>단일 대상 스킬(Enemy 타겟팅) 시전 중 커서 아래의 유효한 적 위에 아래를 향한 작은
/// 화살표 아이콘을 띄워 "지금 클릭하면 이 적이 대상이 된다"를 알려준다. 컨트롤러가 매 프레임
/// 현재 대상의 위치로 Show()를 다시 호출해 따라다니게 한다.</summary>
[RequireComponent(typeof(SpriteRenderer))]
public class SkillTargetIndicator : MonoBehaviour
{
    [Tooltip("표시할 화살표 아이콘 - 기본 방향은 오른쪽을 향하는 것으로 가정한다.")]
    [SerializeField] private Sprite _sprite;
    [Tooltip("아이콘 원본이 오른쪽을 향하므로, 대상을 향해 아래로 꽂히도록 돌릴 각도(도).")]
    [SerializeField] private float _rotationDegrees = -90f;
    [Tooltip("아이콘 크기 배율 - 원본 스프라이트 대비.")]
    [SerializeField] private float _scale = 0.35f;
    [Tooltip("대상 위로 띄울 Y 오프셋 - 대상 트랜스폼 기준.")]
    [SerializeField] private float _yOffset = 1f;

    private SpriteRenderer _spriteRenderer;

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();

        if (_sprite != null)
        {
            _spriteRenderer.sprite = _sprite;
        }

        transform.rotation = Quaternion.Euler(0f, 0f, _rotationDegrees);
        transform.localScale = Vector3.one * _scale;
    }

    /// <summary>지정한 대상 위치 위에 화살표 표시를 켠다. 타겟팅 중 매 프레임 호출해 위치를 갱신한다.</summary>
    public void Show(Vector3 anchorWorldPosition)
    {
        gameObject.SetActive(true);
        anchorWorldPosition.y += _yOffset;
        transform.position = anchorWorldPosition;
    }

    /// <summary>표시를 끈다.</summary>
    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
