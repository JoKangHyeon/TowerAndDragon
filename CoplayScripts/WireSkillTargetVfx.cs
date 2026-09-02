using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// M4~M7 - 어미용 액티브 스킬 에셋에 대상별 시전 연출(B계층) 프리팹을 채운다.
///
/// <b>왜 DragonSkillTreeAssetGenerator를 돌리지 않는가</b>: 그 생성기는 스킬·노드·게이트 에셋을
/// 통째로 다시 만들고 스트링테이블 행까지 건드린다. 지금 필요한 것은 기존 에셋의 새 필드 하나를
/// 채우는 것뿐이라, 다른 작업자의 변경을 덮어쓸 위험을 감수할 이유가 없다.
/// (생성기 쪽에도 같은 배선을 넣어 뒀으므로, 나중에 생성기를 다시 돌려도 값이 유지된다.)
///
/// 비어 있는 스킬은 연출만 생략된다 - Skill.PlayTargetVfx가 널을 그냥 넘긴다.
///
/// 여러 번 돌려도 안전하다. 진행되지 않은 마일스톤의 프리팹은 아직 없으므로 "경로 없음"으로 보고만 한다.
/// </summary>
public static class WireSkillTargetVfx
{
    private const string SKILL_FOLDER = "Assets/Data/Dragon/Skills";

    private sealed class Entry
    {
        public string SkillAsset;
        public string VfxPath;
        public float LifetimeSeconds;
        public string Note;
    }

    private const string BD_FOLDER = "Assets/Imported/Prefabs/BabyDragonEffect/Combat";

    // 성벽 재생 연출의 재생 길이. Impact_Tower_Life는 자식 lengthInSec가 0.25~1초에 흩어져
    // 있어 가장 긴 것에 맞춘다 - 짧게 끊으면 화살표가 다 올라오기 전에 잘린다.
    private const float CASTLE_HEAL_LIFETIME = 1.2f;

    // 프리팹의 실제 lengthInSec에 맞춘다 - 짧게 잡으면 연출이 중간에 잘리고, 길게 잡으면
    // 다 끝난 인스턴스가 풀로 안 돌아와 동시 대여 수가 늘어난다.
    // Impact_BD_Fire 1초 / Impact_BD_Time 1초 (측정: DumpOverlayEmitters와 같은 방식)
    private const float IMPACT_LIFETIME = 1f;

    // Impact_BD_Stone은 PS가 20개고 최장 5초짜리가 섞여 있다(잔해가 천천히 가라앉는다).
    // 1초로 끊으면 착지 폭발만 보이고 마무리가 잘린다.
    private const float STONE_IMPACT_LIFETIME = 2.5f;

    // ⚠️ 얼음(SK_Dragon_FreezeAll)은 <b>일부러 비워 둔다.</b> 빙결 B는 이미 DS_IceFreeze의
    //   지속 연출(FX_Status_IceFreeze - 몬스터를 감싸는 얼음 큐브)로 구현돼 있다. 여기에 임팩트를
    //   더하면 큐브 위에 또 하나가 겹친다. M1 실측에서 큐브만으로도 Tank급 한 마리가 화면 폭의
    //   17%를 덮는 것이 확인됐으므로, 20~30마리 동시 발동에서 시각 잡음이 된다.
    private static readonly Entry[] ENTRIES =
    {
        new Entry
        {
            SkillAsset = "SK_Dragon_CastleHeal",
            VfxPath = "Assets/Imported/Prefabs/SkillCastOverlay/FX_CastleHealImpact.prefab",
            LifetimeSeconds = CASTLE_HEAL_LIFETIME,
            Note = "M4 성벽 재생 - Impact_Tower_Life(화살표)를 성 크기로 키운 파생본. " +
                "새끼용 FX_BD_LifeHealWave에서 교체했다"
        },
        new Entry
        {
            SkillAsset = "SK_Dragon_GlobalDamage",
            VfxPath = $"{BD_FOLDER}/Impact_BD_Fire.prefab",
            LifetimeSeconds = IMPACT_LIFETIME,
            Note = "M5 전역 화염 - 몬스터마다 발밑에 타격 임팩트 (설계 §7-2 1순위)"
        },
        new Entry
        {
            SkillAsset = "SK_Dragon_RepairTowers",
            VfxPath = "Assets/Imported/Prefabs/SkillCastOverlay/FX_TowerRepairImpact.prefab",
            LifetimeSeconds = CASTLE_HEAL_LIFETIME,
            Note = "M6 즉시 재활성화 - 성벽 재생과 같은 화살표 연출, 색만 노랑(시간 42도). " +
                "타워 크기 기준이라 성벽 재생본보다 작다"
        },
        new Entry
        {
            SkillAsset = "SK_Dragon_Meteor",
            VfxPath = $"{BD_FOLDER}/Impact_BD_Stone.prefab",
            LifetimeSeconds = STONE_IMPACT_LIFETIME,
            Note = "M7 메테오 - 피격 몬스터마다 임팩트 (반경 1.5라 대상이 한 자리 수)"
        }
    };

    public static string Execute()
    {
        var report = new StringBuilder();

        foreach (Entry entry in ENTRIES)
        {
            string path = $"{SKILL_FOLDER}/{entry.SkillAsset}.asset";
            var skill = AssetDatabase.LoadAssetAtPath<SkillSO>(path);

            if (skill == null)
            {
                report.Append(entry.SkillAsset).AppendLine("  스킬 에셋을 찾지 못했습니다: " + path);
                continue;
            }

            var vfx = AssetDatabase.LoadAssetAtPath<GameObject>(entry.VfxPath);

            if (vfx == null)
            {
                report.Append(entry.SkillAsset).AppendLine("  연출 프리팹을 찾지 못했습니다: " + entry.VfxPath);
                continue;
            }

            var serialized = new SerializedObject(skill);
            SerializedProperty prefabProperty = serialized.FindProperty("TargetVfxPrefab");
            SerializedProperty lifetimeProperty = serialized.FindProperty("TargetVfxLifetimeSeconds");

            if (prefabProperty == null || lifetimeProperty == null)
            {
                report.Append(entry.SkillAsset)
                    .AppendLine("  SkillSO에 TargetVfx 필드가 없습니다 - 스크립트가 최신인지 확인하세요.");
                continue;
            }

            prefabProperty.objectReferenceValue = vfx;
            lifetimeProperty.floatValue = entry.LifetimeSeconds;
            serialized.ApplyModifiedProperties();

            EditorUtility.SetDirty(skill);

            report.Append(entry.SkillAsset).Append("  ← ").Append(vfx.name)
                .Append("  ").Append(entry.LifetimeSeconds.ToString("0.##")).AppendLine("초")
                .Append("    ").AppendLine(entry.Note);
        }

        AssetDatabase.SaveAssets();

        report.AppendLine("저장 완료.");

        return report.ToString();
    }
}
