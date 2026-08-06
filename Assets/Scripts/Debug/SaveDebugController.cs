using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// [테스트 전용] 세이브/이어하기 UI가 붙기 전까지 저장·로드를 손으로 돌려 보기 위한 컨트롤러.
///   F5 = 지정 슬롯에 저장
///   F9 = 지정 슬롯으로 이어하기(현재 씬 재로드 → 전체 부팅 경로를 그대로 태운다)
///   F8 = 지정 슬롯 삭제
/// 세이브 UI가 생기면 이 컴포넌트를 제거한다.
/// </summary>
public class SaveDebugController : MonoBehaviour
{
    [SerializeField] private SaveService _saveService;

    [Tooltip("F5/F9/F8이 대상으로 삼을 슬롯. 0번은 자동저장 전용이다.")]
    [SerializeField] private int _slotIndex = 1;

    private void Update()
    {
        if (_saveService == null || Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.f5Key.wasPressedThisFrame)
        {
            SaveAsync().Forget();
        }

        if (Keyboard.current.f9Key.wasPressedThisFrame)
        {
            _saveService.RequestLoadAndReloadScene(_slotIndex);
        }

        if (Keyboard.current.f8Key.wasPressedThisFrame)
        {
            Debug.Log($"[SaveDebugController] 슬롯 {_slotIndex} 삭제: {_saveService.TryDelete(_slotIndex)}");
        }
    }

    private async UniTaskVoid SaveAsync()
    {
        SaveResult result = await _saveService.SaveAsync(
            _slotIndex,
            false,
            this.GetCancellationTokenOnDestroy());

        Debug.Log(result.IsSuccess
            ? $"[SaveDebugController] 슬롯 {_slotIndex} 저장 완료 ({result.Slot.SavedAtUtc:O}, {result.Slot.DayNumber}일차)"
            : $"[SaveDebugController] 슬롯 {_slotIndex} 저장 실패: {result.Reason}");
    }
}
