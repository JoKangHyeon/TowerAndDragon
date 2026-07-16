using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 미니맵 UI(RawImage) 위에서 클릭·드래그하면 해당 지점으로 메인 카메라를 이동시킨다.
///   - 클릭: 클릭한 월드 위치로 카메라 이동 (뷰포트 사각형이 그 지점으로 이동)
///   - 드래그: 포인터를 따라 카메라가 연속 이동
/// 실제 이동은 CameraController.MoveTo가 담당하므로 맵 경계 클램프·스무딩이 그대로 적용된다.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class MinimapNavigator : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    [Header("참조")]
    [SerializeField] private Camera _minimapCamera;
    [SerializeField] private CameraController _cameraController;

    private RectTransform _rectTransform;

    private void Awake()
    {
        _rectTransform = (RectTransform)transform;
    }

    public void OnPointerDown(PointerEventData eventData) => MoveCameraToPointer(eventData);

    public void OnDrag(PointerEventData eventData) => MoveCameraToPointer(eventData);

    /// 포인터 스크린 좌표 → 미니맵 로컬 좌표 → 뷰포트(0~1) → 월드 좌표로 변환해 카메라 이동
    private void MoveCameraToPointer(PointerEventData eventData)
    {
        if (_minimapCamera == null || _cameraController == null)
            return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rectTransform, eventData.position, eventData.pressEventCamera, out Vector2 localPoint))
            return;

        Rect rect = _rectTransform.rect;
        // 드래그 중 포인터가 미니맵 밖으로 나가도 미니맵 범위 안으로 한정
        Vector2 viewport = new Vector2(
            Mathf.Clamp01((localPoint.x - rect.xMin) / rect.width),
            Mathf.Clamp01((localPoint.y - rect.yMin) / rect.height));

        Vector3 world = _minimapCamera.ViewportToWorldPoint(new Vector3(viewport.x, viewport.y, 0f));
        _cameraController.MoveTo(world);
    }
}
