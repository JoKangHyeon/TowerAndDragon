using System;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

// 메인 성 위에 띄우는 어미용 속성 표시(Overlay_MotherDragon_Type 프리팹 루트에 붙인다).
// 자식으로 달린 Type_* 아이콘 5개 중 현재 속성 하나만 켠다 - 아이콘 스프라이트를 코드에서
// 갈아 끼우지 않고 오브젝트 활성만 토글하므로, 아이콘 교체는 프리팹 쪽에서 끝난다.
//
// 속성이 바뀌는 경로가 둘이라 구독도 둘이다.
//   - DragonTreeManager.ActiveAttributeChanged : 속성 변경 UI(용 창 팝업·메인 성 창)와 낮 시작
//   - RunData.OnInventoryChanged               : 세이브 복원(RestoreInventory - 위 이벤트를 거치지 않는다)
public class MotherDragonTypeOverlay : MonoBehaviour
{
    [Tooltip("현재 어미용의 출처. 프리팹 모드가 아니라 씬의 Grid 인스턴스에서 오버라이드로 연결한다.")]
    [SerializeField]
    private GameManager _gameManager;

    [Tooltip("속성 하나당 아이콘 루트 하나(Type_*). 항목마다 속성을 함께 지정하므로 배열 순서는 상관없다 - " +
        "다섯 속성이 모두 한 번씩 들어가기만 하면 된다.")]
    [SerializeField]
    private TypeIcon[] _typeIcons;

    /// <summary>속성과 그 속성을 나타내는 아이콘 루트의 짝.
    /// 배열 순서만으로 속성을 알아내면(인덱스 = enum 값) 인스펙터에서 잘못 꽂았을 때
    /// 조용히 다른 아이콘이 켜진다 - 짝을 명시해 순서 의존을 없앤다.</summary>
    [Serializable]
    private struct TypeIcon
    {
        public DragonType Type;
        public GameObject Root;
    }

    private DragonTreeManager _dragonTreeManager;
    private RunData _run;

    private Dragon CurrentDragon =>
        _gameManager != null ? _gameManager.CurrentRun?.CurrentDragon : null;

    private void Awake()
    {
        if (WiringGuard.RequireNotEmpty(_typeIcons, nameof(_typeIcons), this))
        {
            WarnMissingTypes();
        }

        if (!WiringGuard.Require(_gameManager, nameof(_gameManager), this))
        {
            return;
        }

        _dragonTreeManager = _gameManager.DragonTreeManager;
        _run = _gameManager.CurrentRun;

        if (WiringGuard.Require(_dragonTreeManager, nameof(_dragonTreeManager), this))
        {
            _dragonTreeManager.ActiveAttributeChanged.AddListener(ApplyType);
        }

        if (WiringGuard.RequireRef(_run, nameof(_run), this))
        {
            _run.OnInventoryChanged.AddListener(Refresh);
        }
    }

    // 구독보다 먼저 정해져 있던 속성을 놓치지 않도록 현재 값을 한 번 직접 반영한다.
    // Awake가 아니라 Start인 이유: 어미용을 채우는 매니저들의 초기화가 Awake 단계에 걸쳐 있다.
    private void Start()
    {
        Refresh();
    }

    private void OnDestroy()
    {
        if (_dragonTreeManager != null)
        {
            _dragonTreeManager.ActiveAttributeChanged.RemoveListener(ApplyType);
        }

        if (_run != null)
        {
            _run.OnInventoryChanged.RemoveListener(Refresh);
        }
    }

    /// <summary>현재 어미용 속성을 다시 읽어 아이콘을 맞춘다. 어미용이 아직 없으면 전부 끈다.</summary>
    public void Refresh()
    {
        Dragon dragon = CurrentDragon;

        if (dragon == null)
        {
            HideAll();
            return;
        }

        ApplyType(dragon.CurrentType);
    }

    private void ApplyType(DragonType type)
    {
        if (!WiringGuard.RequireNotEmpty(_typeIcons, nameof(_typeIcons), this))
        {
            return;
        }

        foreach (TypeIcon icon in _typeIcons)
        {
            icon.Root?.SetActive(icon.Type == type);
        }
    }

    private void HideAll()
    {
        if (_typeIcons == null)
        {
            return;
        }

        foreach (TypeIcon icon in _typeIcons)
        {
            icon.Root?.SetActive(false);
        }
    }

    // 짝을 명시하는 방식이라 순서는 틀릴 수 없지만, 항목을 빠뜨리면 그 속성일 때 아무 아이콘도 안 뜬다.
    // 컴파일도 런타임 에러도 나지 않으므로 시작할 때 한 번 훑어 에디터 콘솔에 남긴다.
    [Conditional("UNITY_EDITOR")]
    private void WarnMissingTypes()
    {
        foreach (DragonType type in Enum.GetValues(typeof(DragonType)))
        {
            bool hasIcon = false;

            foreach (TypeIcon icon in _typeIcons)
            {
                if (icon.Type == type && icon.Root != null)
                {
                    hasIcon = true;
                    break;
                }
            }

            if (!hasIcon)
            {
                Debug.LogError($"[{nameof(MotherDragonTypeOverlay)}] {type} 속성의 아이콘이 " +
                    $"{nameof(_typeIcons)}에 없습니다 - 그 속성일 때 아무것도 표시되지 않습니다.", this);
            }
        }
    }
}
