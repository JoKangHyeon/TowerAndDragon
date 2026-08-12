using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

/// <summary>
/// 포탈들의 지상 경로(SplineContainer)를 격자 셀 집합으로 굽는다.
/// 방벽처럼 "몬스터가 지나오는 길 위에만" 놓을 수 있어야 하는 것들이 이 조회원을 통해 판정한다.
///
/// 경로는 씬에 미리 그려 둔 정적 데이터라 매번 다시 샘플링할 이유가 없다 - 첫 조회 때 한 번 굽고
/// 그대로 재사용한다. 포탈이 봉인돼도 경로 자체는 남기므로, 화면에 그려진 선(PortalPathVisibility)과
/// 설치 가능 범위가 어긋나지 않는다.
///
/// 샘플 좌표를 GroundSplineMovement와 똑같이 ConvertWorldToGrid로 되짚는 이유:
/// 스플라인 위의 점은 고저차가 반영되기 전의 평면 좌표라, 화면 기준으로 타일을 고르는
/// PickCellAtWorldPoint를 쓰면 언덕에서 실제 몬스터가 지나는 셀과 어긋난다.
/// </summary>
public sealed class MonsterPathMap : MonoBehaviour, IMonsterPathQuery
{
    // 셀 한 칸을 통째로 건너뛰지 않도록 셀 크기의 절반 간격으로 샘플링한다.
    private const float SAMPLE_STEP_RATIO = 0.5f;

    // 셀 크기를 구하지 못한 경우(그리드 미초기화)의 최소 간격 - 0으로 두면 샘플 수가 무한이 된다.
    private const float MIN_SAMPLE_STEP = 0.05f;

    // 씬 뷰 기즈모로 그리는 경로 셀 표식의 크기(월드 단위) - 판정에는 영향이 없다.
    private const float GIZMO_CELL_SIZE = 0.3f;

    [SerializeField] private GridMap _gridMap;

    [Tooltip("비우면 씬의 모든 Portal에서 지상 경로를 모은다. 특정 포탈만 쓰고 싶을 때 채운다.")]
    [SerializeField] private List<Portal> _portals = new();

    private readonly HashSet<Vector3Int> _pathCells = new();
    private bool _isBuilt;

    private void OnEnable()
    {
        if (_gridMap != null)
        {
            _gridMap.MonsterPathQuery = this;
        }
    }

    private void OnDisable()
    {
        if (_gridMap != null && ReferenceEquals(_gridMap.MonsterPathQuery, this))
        {
            _gridMap.MonsterPathQuery = null;
        }
    }

    public bool IsOnMonsterPath(Vector3Int coord)
    {
        EnsureBuilt();
        return _pathCells.Contains(coord);
    }

    // 굽는 시점을 Awake가 아니라 첫 조회로 미루는 이유: GridMap의 셀 생성이 끝난 뒤여야
    // ConvertWorldToGrid가 올바른 좌표를 돌려준다(오브젝트 간 Awake 순서는 보장되지 않는다).
    private void EnsureBuilt()
    {
        if (_isBuilt)
        {
            return;
        }

        _isBuilt = true;

        if (!WiringGuard.Require(_gridMap, nameof(_gridMap), this))
        {
            return;
        }

        float step = ResolveSampleStep();

        foreach (Portal portal in ResolvePortals())
        {
            // GroundPaths는 RouteCount와 달리 null 가드가 없어 직접 확인한다(Portal.cs 실측).
            if (portal == null || portal.GroundPaths == null)
            {
                continue;
            }

            foreach (SplineContainer path in portal.GroundPaths)
            {
                BakePath(path, step);
            }
        }

        LogBakeSummary();
    }

    // 구운 결과를 한 번 요약해 남긴다 - "방벽이 안 세워진다"의 원인이 (a) 경로를 못 구웠다인지
    // (b) 클릭 좌표가 경로와 다른 좌표계로 계산된다인지, 셀 범위를 눈으로 비교하면 바로 갈린다.
    private void LogBakeSummary()
    {
        if (_pathCells.Count == 0)
        {
            Debug.LogWarning(
                "[MonsterPathMap] 경로 셀을 하나도 굽지 못했습니다. 포탈의 지상 경로(GroundPaths) 배선을 확인하세요.",
                this);
            return;
        }

        var min = new Vector3Int(int.MaxValue, int.MaxValue, 0);
        var max = new Vector3Int(int.MinValue, int.MinValue, 0);

        foreach (Vector3Int coord in _pathCells)
        {
            min.x = Mathf.Min(min.x, coord.x);
            min.y = Mathf.Min(min.y, coord.y);
            max.x = Mathf.Max(max.x, coord.x);
            max.y = Mathf.Max(max.y, coord.y);
        }

        Debug.Log(
            $"[MonsterPathMap] 경로 셀 {_pathCells.Count}칸을 구웠습니다. " +
            $"x {min.x}~{max.x}, y {min.y}~{max.y} 범위입니다.",
            this);
    }

    private IEnumerable<Portal> ResolvePortals()
    {
        if (_portals != null && _portals.Count > 0)
        {
            return _portals;
        }

        return FindObjectsByType<Portal>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    }

    private void BakePath(SplineContainer path, float step)
    {
        if (path == null)
        {
            return;
        }

        float length = path.CalculateLength();

        if (length <= 0f)
        {
            return;
        }

        // 균등 t 샘플링은 호 길이 기준으로는 약간 불균등하지만, 간격이 셀의 절반이라
        // 그 오차로 셀이 통째로 빠지지는 않는다.
        int sampleCount = Mathf.CeilToInt(length / step);

        for (int i = 0; i <= sampleCount; i++)
        {
            float progress = (float)i / sampleCount;
            Vector3 point = path.EvaluatePosition(progress);
            _pathCells.Add(_gridMap.ConvertWorldToGrid(point));
        }
    }

    // 구워진 경로 셀을 씬 뷰에 그린다 - "경로가 제대로 구워졌는가"와 "내가 찍은 칸이 경로인가"를
    // 눈으로 바로 확인할 수 있어야 방벽이 안 세워질 때 원인을 좁힐 수 있다.
    private void OnDrawGizmosSelected()
    {
        if (_gridMap == null || _pathCells.Count == 0)
        {
            return;
        }

        Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.6f);

        foreach (Vector3Int coord in _pathCells)
        {
            Gizmos.DrawWireCube(_gridMap.ConvertGridToWorld(coord), Vector3.one * GIZMO_CELL_SIZE);
        }
    }

    private float ResolveSampleStep()
    {
        Vector3 origin = _gridMap.ConvertGridToWorld(Vector3Int.zero);
        float cellWidth = (_gridMap.ConvertGridToWorld(Vector3Int.right) - origin).magnitude;
        float cellHeight = (_gridMap.ConvertGridToWorld(Vector3Int.up) - origin).magnitude;

        float smallerSide = Mathf.Min(cellWidth, cellHeight);
        return Mathf.Max(smallerSide * SAMPLE_STEP_RATIO, MIN_SAMPLE_STEP);
    }
}
