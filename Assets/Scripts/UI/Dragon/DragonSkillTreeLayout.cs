using UnityEngine;

// 순수 계산 전용 - 속성 인덱스를 방사형 각도로, 각도+반지름을 anchoredPosition으로 변환한다.
// 프로토타입(Docs/용_스킬트리_프로토타입.html)의 각도 컨벤션과 동일: 0도 = 위쪽, 시계방향.
public static class DragonSkillTreeLayout
{
    private const float FULL_CIRCLE_DEGREES = 360f;
    private const int ATTRIBUTE_COUNT = 5;
    public const float ATTRIBUTE_ANGLE_STEP = FULL_CIRCLE_DEGREES / ATTRIBUTE_COUNT;

    public static float AttributeBaseAngle(int attributeIndex) => attributeIndex * ATTRIBUTE_ANGLE_STEP;

    public static Vector2 PositionAt(float angleDegrees, float radius)
    {
        float radians = angleDegrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Sin(radians), Mathf.Cos(radians)) * radius;
    }
}
