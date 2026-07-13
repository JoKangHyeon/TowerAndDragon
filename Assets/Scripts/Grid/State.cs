public enum State
{
        Active, // 점령 = 활성화
        Inactive, // 미점령 & 가시 범위 안에 존재
        Unknown // 미점령 & 가시 범위 밖에 존재
}