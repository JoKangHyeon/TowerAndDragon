using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public class CameraController : MonoBehaviour
{
    public static CameraController Instance { get; private set; }

    [Header("Zoom Setting")]
    [SerializeField] private float zoomSpeed     = 30f;
    [SerializeField] private float smoothness    = 10f;
    [SerializeField] private float minZoom       = 8f;
    [SerializeField] private float maxZoom       = 15f;

    [Header("Pan Setting")]
    [SerializeField] private float panSmoothness = 10f;
    [SerializeField] private LayerMask tableMask = ~0; // LayerMask 이용해서 드래그영역 설정

    private Camera cam;
    private float targetZoom;
    private Vector3 targetPosition;

    private bool isPanning;
    private Vector3 panStartWorld;
    private Vector3 panStartCamPos;

    private void Awake()
    {
        Instance = this;
        cam = GetComponent<Camera>();
        targetZoom = cam.orthographicSize;
        targetPosition = transform.position;
    }

    private void Update()
    {
        CameraZoom();
        HandlePan();
        ApplySmoothing();
    }

    private void CameraZoom()
    {
        if (Mouse.current == null) 
            return;
        float scroll = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) < 0.01f) 
            return;

        Vector2 mouseScreen = Mouse.current.position.ReadValue();
        Vector3 before = cam.ScreenToWorldPoint(new Vector3(mouseScreen.x, mouseScreen.y, 0));

        targetZoom = Mathf.Clamp(targetZoom - scroll * zoomSpeed * 0.01f, minZoom, maxZoom);

        // 줌 전후 월드 좌표 차이만큼 targetPosition을 보정해 마우스 포인터 기준으로 확대
        float prevSize = cam.orthographicSize;
        cam.orthographicSize = targetZoom;
        Vector3 after = cam.ScreenToWorldPoint(new Vector3(mouseScreen.x, mouseScreen.y, 0));
        cam.orthographicSize = prevSize;

        Vector3 offset = before - after;
        targetPosition += offset;

        if (isPanning) 
            panStartCamPos += offset;
    }

    public void StartPan(Vector2 screenPosition)
    {
        Ray ray = cam.ScreenPointToRay(screenPosition);

        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, tableMask))
        {
            panStartWorld  = hit.point;
            panStartCamPos = transform.position;
            isPanning      = true;
        }
    }

    private void HandlePan()
    {
        if (!isPanning || Mouse.current == null)
            return;

        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = cam.ScreenPointToRay(mousePos);

        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, tableMask))
        {
            Vector3 delta  = panStartWorld - hit.point;
            targetPosition = panStartCamPos + delta;
        }
    }

    public void EndPan()
    {
        isPanning = false;
    }

    public bool IsPanning => isPanning;

    private void ApplySmoothing()
    {
        cam.orthographicSize = Mathf.Lerp(
            cam.orthographicSize, targetZoom, Time.deltaTime * smoothness);

        transform.position = Vector3.Lerp(
                transform.position, targetPosition, Time.deltaTime * panSmoothness);
    }

   
}