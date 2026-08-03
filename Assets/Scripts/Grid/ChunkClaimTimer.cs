using TMPro;
using UnityEngine;

// 점령 원정이 진행 중인 청크 위에 띄우는 타이머 표식. 아이콘은 프리팹에 고정돼 있고, 남은 일수만 갱신한다.
public class ChunkClaimTimer : MonoBehaviour
{
    // 남은 일수 표기 형식. 예: 3 → "3 DAY".
    private const string DAYS_LOC_KEY = "claim_chunk_timer";
    private static string DaysFormat => StringTable.GetString(DAYS_LOC_KEY);

    [Tooltip("남은 일수를 표시할 텍스트.")]
    [SerializeField] private TMP_Text _daysText;

    public void SetRemainingDays(int days)
    {
        if (_daysText != null)
        {
            _daysText.text = string.Format(DaysFormat, days);
        }
    }
}
