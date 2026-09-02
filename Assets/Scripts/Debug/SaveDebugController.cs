using UnityEngine;

#if UNITY_EDITOR
using Cysharp.Threading.Tasks;
using UnityEngine.InputSystem;
#endif

/// <summary>
/// [에디터 테스트 전용] 저장·로드를 손으로 돌려 보기 위한 컨트롤러.
///   F5 = 지정 슬롯에 저장
///   F9 = 지정 슬롯으로 이어하기(현재 씬 재로드 → 전체 부팅 경로를 그대로 태운다)
///   F8 = 지정 슬롯 삭제
/// 세이브/이어하기 UI는 이미 구현돼 있고, 이 컨트롤러는 개발 편의로만 남긴다.
/// 본문 전체가 #if UNITY_EDITOR 안에 있어 빌드에서는 컴파일되지 않는다
/// (클래스 껍데기만 남아 씬의 컴포넌트 참조가 Missing Script가 되지 않는다).
/// </summary>
public class SaveDebugController : MonoBehaviour
{
#if UNITY_EDITOR
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
#endif
}
