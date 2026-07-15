public enum ChunkState
{
        Conquered, // 점령 = 활성화
        Visible,   // 미점령 & 가시 범위 안에 존재
        Hidden     // 미점령 & 가시 범위 밖에 존재
}