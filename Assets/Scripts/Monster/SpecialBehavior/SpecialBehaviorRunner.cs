using System.Collections.Generic;

/// <summary>
/// 한 몬스터가 가진 특수 행동의 생성, 갱신, 정리를 담당한다.
/// </summary>
public sealed class SpecialBehaviorRunner
{
    private readonly List<ISpecialBehavior> _behaviors = new();

    public void Initialize(
        BaseMonster owner,
        IReadOnlyList<SpecialBehaviorSO> behaviorData)
    {
        Dispose();

        if (owner == null || behaviorData == null)
        {
            return;
        }

        foreach (SpecialBehaviorSO data in behaviorData)
        {
            if (data == null)
            {
                continue;
            }

            ISpecialBehavior behavior = data.CreateInstance();

            if (behavior == null)
            {
                continue;
            }

            behavior.Initialize(owner);
            _behaviors.Add(behavior);
        }
    }

    public void Tick(float deltaTime)
    {
        foreach (ISpecialBehavior behavior in _behaviors)
        {
            behavior.Tick(deltaTime);
        }
    }

    public void Dispose()
    {
        foreach (ISpecialBehavior behavior in _behaviors)
        {
            behavior.Dispose();
        }

        _behaviors.Clear();
    }
}