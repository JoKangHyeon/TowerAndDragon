using UnityEngine;

/// <summary>
/// MonsterData에는 아이콘 필드가 없어, 인게임 프리팹의 SpriteRenderer(스프라이트 + 색)를 그대로
/// UI 아이콘으로 쓴다. 여러 몬스터 변형(용사냥꾼·공허껍질·저격수 등)이 같은 스프라이트를 색만 바꿔
/// 구분하므로, 스프라이트만 옮기면 UI에서 서로 구분되지 않는다
/// (MonsterCodexCatalogSO·PortalWavePreviewRenderer가 공용으로 쓴다).
/// </summary>
public readonly struct MonsterIcon
{
    public readonly Sprite Sprite;
    public readonly Color Tint;

    public MonsterIcon(Sprite sprite, Color tint)
    {
        Sprite = sprite;
        Tint = tint;
    }

    public static MonsterIcon Resolve(BaseMonster prefab)
    {
        if (prefab == null)
        {
            return new MonsterIcon(null, Color.white);
        }

        SpriteRenderer spriteRenderer = prefab.GetComponentInChildren<SpriteRenderer>(true);

        return spriteRenderer != null
            ? new MonsterIcon(spriteRenderer.sprite, spriteRenderer.color)
            : new MonsterIcon(null, Color.white);
    }
}
