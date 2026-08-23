using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 상태 지속 연출 프리팹의 시뮬레이션 공간과 루트 회전을 찍어 본다.
///
/// 시뮬레이션 공간이 World면 이미 방출된 입자가 태어난 자리에 남아, 몬스터가 걸어갈 때 불이
/// 뒤로 늘어진다 - 부모로 붙이든 위치를 따라가게 하든 결과가 같으므로 미리 확인해야 한다.
/// (YAML 필드 이름은 simulationSpace가 아니라 moveWithTransform이라 grep으로는 잘 안 잡힌다.)
/// </summary>
public static class InspectStatusVfxPrefab
{
    private const string VFX_PATH =
        "Assets/Imported/Prefabs/Effects/Status/FX_Status_FireBurn.prefab";

    public static void Execute()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VFX_PATH);

        if (prefab == null)
        {
            Debug.LogError($"[InspectStatusVfxPrefab] 프리팹을 찾지 못했습니다: {VFX_PATH}");
            return;
        }

        var report = new StringBuilder();

        report.AppendLine($"[InspectStatusVfxPrefab] {prefab.name}");
        report.AppendLine($"  루트 회전: {prefab.transform.localRotation.eulerAngles}");
        report.AppendLine($"  루트 스케일: {prefab.transform.localScale}");
        report.AppendLine();

        foreach (ParticleSystem particles in prefab.GetComponentsInChildren<ParticleSystem>(true))
        {
            ParticleSystem.MainModule main = particles.main;

            report.AppendLine(
                $"  {particles.name}: space={main.simulationSpace} " +
                $"scaling={main.scalingMode} loop={main.loop} " +
                $"startLifetime={main.startLifetime.constant} " +
                $"rot={particles.transform.localRotation.eulerAngles}");
        }

        Debug.Log(report.ToString());
    }
}
