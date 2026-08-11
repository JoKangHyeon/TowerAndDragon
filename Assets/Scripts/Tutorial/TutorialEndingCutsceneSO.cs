using System.Collections.Generic;
using UnityEngine;

/// <summary>엔딩 컷 목록. 순서가 곧 재생 순서다.</summary>
[CreateAssetMenu(menuName = "TowerAndDragon/Tutorial/Ending Cutscene", fileName = "TE_EndingCutscene")]
public sealed class TutorialEndingCutsceneSO : ScriptableObject
{
    [SerializeField] private List<TutorialEndingBeatSO> _beats = new();

    public IReadOnlyList<TutorialEndingBeatSO> Beats => _beats;

    private void OnValidate()
    {
        for (int i = 0; i < _beats.Count; i++)
        {
            if (_beats[i] == null)
            {
                Debug.LogWarning($"[TutorialEndingCutsceneSO] {name}: {i}번째 컷이 비어 있습니다.", this);
            }
        }
    }
}
