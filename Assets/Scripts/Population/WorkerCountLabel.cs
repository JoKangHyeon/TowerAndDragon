using TMPro;
using UnityEngine;

// 인구 배치 모드에서 건물 위에 띄우는 "배치/정원" 표식. 배치 수만 갱신한다.
// 숫자와 슬래시만 쓰므로 언어와 무관하다(스트링테이블 대상 아님).
// ChunkClaimTimer와 같은 구조 - 월드 스페이스 프리팹 루트에 붙여 텍스트만 채운다.
public class WorkerCountLabel : MonoBehaviour
{
    private const string COUNT_FORMAT = "{0}/{1}";

    [Tooltip("배치/정원을 표시할 텍스트.")]
    [SerializeField] private TMP_Text _countText;

    public void SetCount(int assignedPopulation, int capacity)
    {
        if (_countText != null)
        {
            _countText.text = string.Format(COUNT_FORMAT, assignedPopulation, capacity);
        }
    }

    public void SetColor(Color color)
    {
        if (_countText != null)
        {
            _countText.color = color;
        }
    }
}
