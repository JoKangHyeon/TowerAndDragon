using System;

/// <summary>인스펙터에 비워둬도 정상 동작하는 직렬화 필드임을 표시한다.
/// WiringChecker(에디터 도구)가 이 필드의 미연결을 에러가 아닌 경고로 분류한다.
/// 런타임 가드인 <see cref="WiringGuard.Optional"/>와 짝을 이루는 정적 표기다.
/// 배열·리스트 필드에 붙이면 그 안의 원소 미연결까지 함께 선택 항목으로 취급한다.</summary>
[AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
public sealed class WiringOptionalAttribute : Attribute
{
}
