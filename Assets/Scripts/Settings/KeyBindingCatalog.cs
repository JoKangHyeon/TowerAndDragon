using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 설정창 키 항목에 노출할 액션의 목록과 순서를 담는 데이터 에셋.
/// 이 에셋은 "어떤 액션을 어떤 이름으로 보여줄지"만 정한다 — 실제 행(키 하나하나)은
/// UI_KeyBindingSection이 각 액션의 바인딩을 훑어 런타임에 만든다. 그래서 액션에 키를
/// 하나 더 붙이면 이 에셋을 고치지 않아도 설정창에 줄이 하나 늘어난다.
/// 마우스·게임패드·XR 바인딩은 UI 쪽에서 걸러지므로 여기 넣어도 표시되지 않는다.
/// </summary>
[CreateAssetMenu(fileName = "KeyBindingCatalog", menuName = "TowerAndDragon/Key Binding Catalog")]
public class KeyBindingCatalog : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        [Tooltip("설정창에 노출할 액션.")]
        public InputActionReference Action;

        [Tooltip("행 이름으로 쓸 스트링테이블 key. 값에 {0}을 넣으면 " +
                 "합성 바인딩은 방향 이름(위/아래/…)이, 그 외 여러 키를 가진 액션은 번호가 들어간다.")]
        public string DisplayLocKey;

        [Tooltip("다른 액션과 같은 키를 써도 되는 액션인지. 보정키(Shift·Ctrl)처럼 쓰이는 맥락이 달라 " +
                 "겹쳐도 문제가 없는 것만 켠다 - 켜면 설정창의 중복 키 검사에서 양방향으로 빠진다.")]
        public bool AllowsSharedKey;
    }

    [Tooltip("설정창에 보여줄 순서대로 넣는다.")]
    [SerializeField] private Entry[] _entries;

    public IReadOnlyList<Entry> Entries => _entries ?? Array.Empty<Entry>();
}
