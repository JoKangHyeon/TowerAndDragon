using UnityEditor;
using UnityEngine;

/// <summary>
/// 상태 연출의 실제 그림을 보기 위해 씬 뷰 카메라를 대상 앞에 세운다.
///
/// 밤 조명 아래라 캡처가 실루엣으로만 나오는 것을 피하려고, 확인하는 동안만 전역 광원을 밝힌다.
/// 플레이 모드에서만 만지므로 씬 애셋은 바뀌지 않는다.
/// </summary>
public static class CaptureStatusVfxLook
{
    private const float VIEW_SIZE = 3.5f;
    private const float PROBE_LIGHT_INTENSITY = 1f;

    public static void Execute()
    {
        if (!Application.isPlaying)
        {
            Debug.LogError("[CaptureStatusVfxLook] 플레이 모드에서 실행해야 합니다.");
            return;
        }

        int brightened = 0;

        foreach (UnityEngine.Rendering.Universal.Light2D light in
            Object.FindObjectsByType<UnityEngine.Rendering.Universal.Light2D>(
                FindObjectsSortMode.None))
        {
            if (light.lightType != UnityEngine.Rendering.Universal.Light2D.LightType.Global)
            {
                continue;
            }

            light.intensity = PROBE_LIGHT_INTENSITY;
            light.color = Color.white;
            brightened++;
        }

        SceneView view = SceneView.lastActiveSceneView;

        if (view != null)
        {
            // 세 대상이 x = 0 / 2.2 / 4.4에 서 있으니 가운데를 본다.
            view.in2DMode = true;
            view.LookAt(new Vector3(2.2f, 0.4f, 0f), Quaternion.identity, VIEW_SIZE);
            view.Repaint();
        }

        Debug.Log($"[CaptureStatusVfxLook] 전역 광원 {brightened}개를 밝혔습니다.");
    }
}
