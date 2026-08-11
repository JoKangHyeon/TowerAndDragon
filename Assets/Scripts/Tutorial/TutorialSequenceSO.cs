using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 안내 단계의 순서. 리스트 순서가 곧 진행 순서다.
/// 진행도는 단계의 StepId로 남기므로, 중간에 단계를 끼워 넣어도 이미 지난 단계가 다시 뜨지 않는다.
/// </summary>
[CreateAssetMenu(menuName = "TowerAndDragon/Tutorial/Sequence", fileName = "TS_Sequence")]
public sealed class TutorialSequenceSO : ScriptableObject
{
    [Tooltip("위에서 아래로 진행한다.")]
    [SerializeField] private List<TutorialStepSO> _steps = new();

    public IReadOnlyList<TutorialStepSO> Steps => _steps;

    private void OnValidate()
    {
        var seenIds = new HashSet<string>();
        for (int i = 0; i < _steps.Count; i++)
        {
            TutorialStepSO step = _steps[i];
            if (step == null)
            {
                Debug.LogWarning($"[TutorialSequenceSO] {name}: {i}번 항목이 비어 있습니다.", this);
                continue;
            }

            // 같은 StepId가 두 번 나오면 앞의 것을 지난 것만으로 뒤의 것까지 지난 것이 되어 단계가 건너뛰어진다.
            if (!string.IsNullOrWhiteSpace(step.StepId) && !seenIds.Add(step.StepId))
            {
                Debug.LogWarning(
                    $"[TutorialSequenceSO] {name}: StepId '{step.StepId}'가 중복입니다({i}번). 진행도가 어긋납니다.", this);
            }
        }
    }
}
