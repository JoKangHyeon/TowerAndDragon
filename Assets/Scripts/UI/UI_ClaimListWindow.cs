using System.Collections.Generic;
using UnityEngine;

// 점령(원정) 진행 중인 청크들을 리스트로 표시한다. ConquestManager.OnExpeditionsChanged가 발생할 때마다
// 활성 원정 전체를 다시 그린다 - ExpeditionMarkerRenderer와 동일한 패턴(ComponentPool + 전체 재계산).
// 원정이 완료되면 ActiveExpeditions에서 빠지므로, 재빌드 시 해당 슬롯이 자동으로 사라진다.
public class UI_ClaimListWindow : MonoBehaviour
{
    [SerializeField]
    private ConquestManager _conquestManager;

    [Tooltip("점령 리스트 슬롯 프리팹(Slot_ClaimList).")]
    [SerializeField]
    private UI_ClaimListSlot _slotPrefab;

    [Tooltip("생성된 슬롯이 들어갈 부모(ClaimList_window 루트).")]
    [SerializeField]
    private Transform _slotContainer;

    [Tooltip("지형 스프라이트. 인덱스는 TerrainType 선언 순서(Grass/Rock/Volcano/Desert/Snow/Default)와 일치해야 한다.")]
    [SerializeField]
    private Sprite[] _terrainSprites;

    private ComponentPool<UI_ClaimListSlot> _slotPool;

    private void Awake()
    {
        _slotPool = new ComponentPool<UI_ClaimListSlot>(_slotPrefab, _slotContainer);
    }

    private void OnEnable()
    {
        if (_conquestManager != null)
        {
            _conquestManager.OnExpeditionsChanged.AddListener(Refresh);
        }

        Refresh();
    }

    private void OnDisable()
    {
        if (_conquestManager != null)
        {
            _conquestManager.OnExpeditionsChanged.RemoveListener(Refresh);
        }
    }

    private void Refresh()
    {
        if (_conquestManager == null)
        {
            return;
        }

        IReadOnlyList<ConquestExpedition> expeditions = _conquestManager.ActiveExpeditions;

        for (int i = 0; i < expeditions.Count; i++)
        {
            ConquestExpedition expedition = expeditions[i];

            UI_ClaimListSlot slot = _slotPool.Get(i);
            Sprite terrainSprite = ResolveTerrainSprite(_conquestManager.GetDominantTerrain(expedition.TargetChunkCoord));
            slot.Setup(terrainSprite, expedition.DaysRequired - expedition.DaysProgressed);
        }

        _slotPool.DeactivateFrom(expeditions.Count);
    }

    private Sprite ResolveTerrainSprite(TerrainType terrain)
    {
        int index = (int)terrain;
        return _terrainSprites != null && index >= 0 && index < _terrainSprites.Length ? _terrainSprites[index] : null;
    }
}
