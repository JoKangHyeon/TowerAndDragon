using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 생명 어미용 「성벽 재생」의 대상별 연출(B계층)을 <c>Impact_Tower_Life</c> 기반 파생본으로 만든다.
///
/// 왜 파생본인가: 원본은 <b>스케일 0.38</b>로 일반 타워 명중 기준이다. 성은 스프라이트 경계가
/// 가로·세로 <b>3.2 월드 단위</b>(측정: CoplayScripts/MeasureCastleSize)라 원본을 그대로 띄우면
/// 성 발밑에 작은 점처럼 뜬다. 성 크기에 맞춰 키운 파생본이 필요하다.
///
/// <b>왜 코드에서 스케일을 주지 않고 프리팹으로 만드나</b>: 스폰 경로가
/// <see cref="ProjectilePool.PlayForSeconds"/>인데 이 함수는 위치·회전만 받고 <b>스케일 인자가 없다</b>.
/// 풀이 프리팹의 트랜스폼을 그대로 복제하므로, 크기는 프리팹에 박아야 한다.
/// (풀에 스케일 인자를 추가하는 것은 설계 「뒤집힌 결정」 4번이 금지한다 - 오버로드를 늘리지 않는다.)
///
/// 원본 <c>Assets/Imported/Prefabs/Projectile/Tower/Impact_Tower_Life.prefab</c>은 건드리지 않는다.
/// 그쪽은 생명 타워의 명중 이펙트로 <c>Projectile_Tower_Life</c>가 참조하고 있어, 고치면
/// 타워 공격 연출이 함께 커진다.
///
/// 되돌리려면: git -C Assets/Imported checkout Prefabs/SkillCastOverlay
/// </summary>
public static class BuildCastleHealImpact
{
    private const string SOURCE_PATH =
        "Assets/Imported/Prefabs/Projectile/Tower/Impact_Tower_Life.prefab";

    private const string TARGET_PATH =
        "Assets/Imported/Prefabs/SkillCastOverlay/FX_CastleHealImpact.prefab";

    // 성 스프라이트 경계 가로(3.2 월드 단위, 측정값). 원본은 타워 명중 기준(스케일 0.38)이라
    // 성을 감싸려면 그만큼 키워야 한다.
    private const float CASTLE_WIDTH = 3.2f;

    // 원본 프리팹의 루트 스케일. 이 값에서 몇 배로 갈지 계산하는 기준점이다.
    private const float SOURCE_SCALE = 0.38f;

    // 성 경계를 꽉 채우지 않고 살짝 넘기게 둔다 - 회복이 성 "전체"에 걸린 느낌을 주되
    // 화면을 덮지는 않는 정도다. 1.0이면 딱 성 폭.
    private const float COVERAGE_RATIO = 1.15f;

    public static string Execute()
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(SOURCE_PATH);

        if (source == null)
        {
            return "원본을 찾지 못했습니다: " + SOURCE_PATH;
        }

        var report = new StringBuilder();

        GameObject instance = Object.Instantiate(source);
        instance.name = System.IO.Path.GetFileNameWithoutExtension(TARGET_PATH);

        try
        {
            // 원본 0.38이 타워 하나를 덮는다고 보고, 성 폭에 비례해 키운다.
            float scale = SOURCE_SCALE * CASTLE_WIDTH * COVERAGE_RATIO;

            instance.transform.localScale = new Vector3(scale, scale, scale);

            report.Append("루트 스케일: ").Append(SOURCE_SCALE.ToString("0.##"))
                .Append(" → ").AppendLine(scale.ToString("0.##"));

            int count = 0;

            foreach (ParticleSystem particles in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = particles.main;

                // ⚠️ Hierarchy로 둬야 루트 스케일이 파티클에 곱해진다. Local이면 루트를 키워도
                //   입자 크기가 그대로여서 배치만 벌어지고 그림이 커지지 않는다.
                //   (오버레이는 카메라 자식이라 Local이 맞지만, 이쪽은 반대다.)
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;

                count++;
            }

            report.Append("ParticleSystem ").Append(count).AppendLine("개 scalingMode=Hierarchy");

            PrefabUtility.SaveAsPrefabAsset(instance, TARGET_PATH);
            AssetDatabase.SaveAssets();

            report.AppendLine();
            report.Append("저장: ").AppendLine(TARGET_PATH);
            report.Append("원본(수정 안 함): ").AppendLine(SOURCE_PATH);
            report.Append("성 폭 ").Append(CASTLE_WIDTH.ToString("0.##"))
                .Append(" × 여유 ").Append(COVERAGE_RATIO.ToString("0.##")).AppendLine("배 기준");

            return report.ToString();
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }
}
