using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 세이브 슬롯 목록의 한 줄. 표시할 값은 전부 UI_LoadGameWindow가 Setup으로 채운다
/// (UI_ClaimListSlot과 같은 구조 - 슬롯은 자기 데이터를 스스로 찾지 않는다).
///
/// 썸네일만은 예외적으로 이 슬롯이 소유한다. SaveSlotQuery.TryLoadThumbnail이 호출할 때마다
/// Texture2D를 새로 만들기 때문에, 만든 쪽이 아니라 붙인 쪽이 해제 시점을 알 수 있다.
/// </summary>
public class UI_SaveSlotItem : MonoBehaviour
{
    [Tooltip("슬롯 전체를 덮는 버튼. 클릭하면 그 슬롯을 이어한다.")]
    [SerializeField] private Button _selectButton;

    [Tooltip("슬롯 번호 라벨.")]
    [SerializeField] private TMP_Text _slotNumberText;

    [Tooltip("저장 시각 라벨. 비어 있거나 손상된 슬롯이면 그 사유를 대신 보여 준다.")]
    [SerializeField] private TMP_Text _timestampText;

    [Tooltip("일차 라벨.")]
    [SerializeField] private TMP_Text _dayText;

    [Tooltip("자동저장 슬롯 표시. 자동저장이 아니면 숨긴다.")]
    [SerializeField] private GameObject _autoSaveBadge;

    [Tooltip("점령 현황 썸네일. 썸네일이 없으면 숨긴다.")]
    [SerializeField] private Image _thumbnailImage;

    [Tooltip("슬롯 삭제 버튼. 빈 슬롯에서는 숨긴다.")]
    [SerializeField] private Button _deleteButton;

    private int _slotIndex = SavePaths.INVALID_SLOT_INDEX;
    private Action<int> _selected;
    private Action<int> _deleted;

    // 이 슬롯이 붙여 놓은 썸네일. 다시 그릴 때 텍스처까지 같이 파괴해야 새지 않는다.
    private Sprite _thumbnail;

    private void Awake()
    {
        if (_selectButton != null)
        {
            _selectButton.onClick.AddListener(HandleSelected);
        }

        if (_deleteButton != null)
        {
            _deleteButton.onClick.AddListener(HandleDeleted);
        }
    }

    private void OnDestroy()
    {
        ReleaseThumbnail();
    }

    /// <summary>
    /// 슬롯 한 줄을 그린다. 선택 가능 여부는 창이 계산해서 내려준다 - 같은 슬롯이라도
    /// 저장 모드냐 불러오기 모드냐에 따라 판정이 달라지는데, 그 규칙은 창이 알고 슬롯은 모른다.
    /// 덮어쓰기·삭제 확인은 이 줄이 아니라 창의 확인 팝업(UI_SlotConfirmPopup)이 맡는다.
    /// </summary>
    public void Setup(
        SaveSlotInfo info,
        bool isSelectable,
        Action<int> onSelected,
        Action<int> onDeleted)
    {
        _slotIndex = info.SlotIndex;
        _selected = onSelected;
        _deleted = onDeleted;

        if (_slotNumberText != null)
        {
            _slotNumberText.text = string.Format(
                StringTable.GetString(TitleLocKeys.LOAD_SLOT_NUMBER), info.SlotIndex);
        }

        RenderState(info, isSelectable);
        RenderThumbnail(info);
    }

    /// <summary>썸네일 텍스처를 해제한다. 창을 닫을 때 목록 전체에 대해 호출한다.</summary>
    public void ReleaseThumbnail()
    {
        if (_thumbnail == null)
        {
            return;
        }

        if (_thumbnailImage != null)
        {
            _thumbnailImage.sprite = null;
        }

        // Sprite.Create가 만든 스프라이트는 텍스처를 소유하지 않으므로 둘 다 지워야 한다.
        Destroy(_thumbnail.texture);
        Destroy(_thumbnail);
        _thumbnail = null;
    }

    private void RenderState(SaveSlotInfo info, bool isSelectable)
    {
        // 표시(뱃지·일차)는 슬롯에 데이터가 있느냐로 갈리고, 선택 가능 여부는 창이 정한다.
        // 저장 모드에서는 빈 슬롯도 눌러야 하므로 둘을 같은 값으로 묶으면 안 된다.
        bool hasData = !info.IsEmpty && !info.IsCorrupted;

        if (_selectButton != null)
        {
            _selectButton.interactable = isSelectable;
        }

        // 빈 슬롯은 지울 것이 없다. 손상된 슬롯은 로드는 막되 삭제는 열어 둔다.
        if (_deleteButton != null)
        {
            _deleteButton.gameObject.SetActive(!info.IsEmpty);
        }

        if (_autoSaveBadge != null)
        {
            _autoSaveBadge.SetActive(hasData && info.IsAutoSave);
        }

        if (_timestampText != null)
        {
            _timestampText.text = ResolveTimestampLabel(info);
        }

        if (_dayText != null)
        {
            _dayText.gameObject.SetActive(hasData);

            if (hasData)
            {
                _dayText.text = string.Format(
                    StringTable.GetString(SaveLocKeys.SLOT_DAY_LABEL), info.DayNumber);
            }
        }
    }

    private static string ResolveTimestampLabel(SaveSlotInfo info)
    {
        if (info.IsEmpty)
        {
            return StringTable.GetString(SaveLocKeys.SLOT_EMPTY);
        }

        if (info.IsCorrupted)
        {
            return StringTable.GetString(SaveLocKeys.SLOT_CORRUPTED);
        }

        return SaveTimestampFormatter.ToLastSavedLabel(info.SavedAtUtc);
    }

    private void RenderThumbnail(SaveSlotInfo info)
    {
        ReleaseThumbnail();

        if (!WiringGuard.Require(_thumbnailImage, nameof(_thumbnailImage), this))
        {
            return;
        }

        bool hasThumbnail =
            info.HasThumbnail && SaveSlotQuery.TryLoadThumbnail(info.SlotIndex, out _thumbnail);

        _thumbnailImage.gameObject.SetActive(hasThumbnail);

        if (hasThumbnail)
        {
            _thumbnailImage.sprite = _thumbnail;
        }
    }

    private void HandleSelected()
    {
        SoundManager.Play(SoundId.UiButtonClick);
        _selected?.Invoke(_slotIndex);
    }

    private void HandleDeleted()
    {
        SoundManager.Play(SoundId.UiButtonClick);
        _deleted?.Invoke(_slotIndex);
    }
}
