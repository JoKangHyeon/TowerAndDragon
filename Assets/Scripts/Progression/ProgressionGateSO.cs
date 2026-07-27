using UnityEngine;

// 선행 노드 충족만으로 표현할 수 없는 추가 해금 조건(예: 새끼용 보유 수).
// 노드마다 0개 이상 붙일 수 있으며, TryUnlock은 전부 만족해야 통과시킨다.
public abstract class ProgressionGateSO : ScriptableObject
{
    [SerializeField] private string _lockedLocKey;

    public string LockedLocKey => _lockedLocKey;

    public abstract bool IsSatisfied(ProgressionContext context);
}
