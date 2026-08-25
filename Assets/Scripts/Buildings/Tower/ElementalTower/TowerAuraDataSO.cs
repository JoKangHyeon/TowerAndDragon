using UnityEngine;

[CreateAssetMenu(
    menuName = "TowerAndDragon/Tower Aura Data",
    fileName = "TowerAuraData"
)]
public sealed class TowerAuraDataSO : ScriptableObject
{
    [Header("Range")]
    [Min(0f)]
    [SerializeField] private float _radius;

    [SerializeField] private bool _scaleRadiusWithStaffing;

    [Header("Offense")]
    [Min(1f)]
    [SerializeField] private float _damageMultiplier = 1f;

    [Min(1f)]
    [SerializeField] private float _attackSpeedMultiplier = 1f;

    [Header("Recovery")]
    [Min(1f)]
    [SerializeField] private float _reviveSpeedMultiplier = 1f;

    [Header("Defense")]
    [Min(1f)]
    [SerializeField] private float _maxHealthMultiplier = 1f;

    [Min(0f)]
    [SerializeField] private float _shieldAmount;

    [Header("Utility")]
    [SerializeField] private bool _isStealth;
    [SerializeField] private int _soulPopulation;

    // 오라 데이터가 자기 연출을 들고 있으므로, 새 오라를 추가할 때 어딘가의 배선표를 늘리지
    // 않아도 된다(StatusEffectSO와 같은 방식).
    //
    // 둘이 짝을 이룬다 - 클릭한 타워는 발밑 마커로, 그 오라를 받는 타워들은 머리 위 구체로 알린다.
    // 비워 두는 것이 기본이다 - 안 꽂으면 아무것도 뜨지 않는다.
    [Header("VFX")]
    [WiringOptional]
    [Tooltip("이 오라를 받는 타워 머리 위에 띄울 구체 프리팹. 비우면 표시하지 않는다.")]
    [SerializeField] private GameObject _recipientOrbPrefab;

    [WiringOptional]
    [Tooltip("이 오라를 뿌리는 타워를 클릭했을 때 발밑에 깔 범위 마커 프리팹. 비우면 표시하지 않는다.")]
    [SerializeField] private GameObject _rangeMarkerPrefab;

    public GameObject RecipientOrbPrefab => _recipientOrbPrefab;
    public bool HasRecipientOrb => _recipientOrbPrefab != null;
    public GameObject RangeMarkerPrefab => _rangeMarkerPrefab;
    public bool HasRangeMarker => _rangeMarkerPrefab != null;

    public float Radius => _radius;
    public bool ScaleRadiusWithStaffing => _scaleRadiusWithStaffing;
    public float DamageMultiplier => _damageMultiplier;
    public float AttackSpeedMultiplier => _attackSpeedMultiplier;
    public float ReviveSpeedMultiplier => _reviveSpeedMultiplier;
    public float MaxHealthMultiplier => _maxHealthMultiplier;
    public float ShieldAmount => _shieldAmount;
    public bool IsStealth => _isStealth;
    public int SoulPopulation => _soulPopulation;

    public bool HasArea => _radius > 0f;

#if UNITY_EDITOR
    // 파티클이 없는 프리팹을 꽂으면 화면에 아무것도 안 뜨는데 에러도 안 난다.
    // "배선은 했는데 왜인지 모르겠다"가 되기 전에 인스펙터에서 잡는다.
    private void OnValidate()
    {
        WarnIfNotParticlePrefab(_recipientOrbPrefab, nameof(_recipientOrbPrefab));
        WarnIfNotParticlePrefab(_rangeMarkerPrefab, nameof(_rangeMarkerPrefab));
    }

    private void WarnIfNotParticlePrefab(GameObject prefab, string memberName)
    {
        if (prefab == null ||
            prefab.GetComponentInChildren<ParticleSystem>(true) != null)
        {
            return;
        }

        Debug.LogWarning(
            $"[{name}] {memberName} '{prefab.name}'에 ParticleSystem이 없습니다. " +
            "이 연출은 파티클 프리팹을 전제로 합니다.",
            this);
    }
#endif
}
