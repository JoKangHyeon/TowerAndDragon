using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(menuName = "TowerAndDragon/EnemyEnhancement",fileName = "Enhancement")]
public class EnemyEnhancementProfileSO : ScriptableObject
{
    [SerializeField]
    private List<EnemyEnhancementRule> _rules = new();

    public IReadOnlyList<EnemyEnhancementRule> Rules
    {
        get
        {
            if (_rules == null)
            {
                return System.Array.Empty<EnemyEnhancementRule>();
            }

            return _rules;
        }
    }
}
