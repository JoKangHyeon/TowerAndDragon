// 볼륨 조절 대상 채널. AudioMixer(Assets/AudioMixer.mixer)의 그룹 구성과 1:1로 대응한다.
// Master 아래에 BGM·SE가 자식으로 달려 있으므로, Master를 내리면 나머지도 함께 내려간다.
public enum AudioChannel
{
    Master,
    BGM,
    SE,
}
