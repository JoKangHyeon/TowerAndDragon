#!/usr/bin/env python3
"""BalanceSimulator.html이 내보낸 balance-changes.json을 Unity 에셋에 반영한다.

기본은 드라이런이다. 무엇이 바뀌는지 diff로 보여주고 끝난다.
실제로 쓰려면 --apply를 붙인다.

    python Docs/BalanceReport/apply_balance.py balance-changes.json
    python Docs/BalanceReport/apply_balance.py balance-changes.json --apply

안전장치
  - 숫자 필드만 건드린다. 화이트리스트에 없는 필드는 거부한다.
  - guid, fileID, 참조는 절대 건드리지 않는다.
  - 스칼라 필드는 파일 안에서 정확히 1번 나올 때만 수정한다(여러 개면 중단).
  - 파일의 기존 줄바꿈(LF/CRLF)을 그대로 유지한다.
  - Unity 에디터가 켜져 있으면 경고한다. 반영 전에 Unity를 닫을 것.
  - --apply 시 원본을 .bak으로 남긴다.
"""
from __future__ import annotations

import argparse
import json
import os
import re
import shutil
import subprocess
import sys

# 이 스크립트가 손대도 되는 숫자 필드. 여기 없는 이름은 전부 거부한다.
SCALAR_FIELDS = {
    "_amount", "_maxHealth", "_shieldAmount", "_moveSpeed",
    "_interval", "_range", "_areaRadius",
    "_populationCapacity", "_reviveDelay", "_maxPopulation",
    "_bonusRatio", "_foodUpkeepPerPopulation", "_baselineCellYield",
    "_diagonalBonusFactor", "_healPerDay", "_researchPointCost",
    "_spawnCount", "_spawnInterval", "_durationSeconds",
    "_damagePerTick", "_tickIntervalSeconds", "_speedMultiplier",
}
# "필드[자원]" 형태로 리스트 안의 Amount를 바꾸는 것들
LIST_FIELDS = {"_buildCost", "_initialResources", "_resourceCost"}

RESOURCE_TYPE = {
    "Food": 1, "Wood": 2, "Stone": 4, "FlameHeart": 8,
    "SnowCrystal": 16, "TimeSand": 32, "PhilosopherStone": 64,
}

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))


class Refused(Exception):
    """안전장치에 걸린 변경. 파일은 건드리지 않는다."""


def read_text(path: str) -> tuple[str, str]:
    """파일을 읽고 (내용, 줄바꿈) 을 돌려준다. 줄바꿈은 나중에 그대로 되돌린다."""
    with open(path, "rb") as fh:
        raw = fh.read()
    crlf = raw.count(b"\r\n")
    lf = raw.count(b"\n") - crlf
    newline = "\r\n" if crlf > lf else "\n"
    return raw.decode("utf-8").replace("\r\n", "\n"), newline


def write_text(path: str, text: str, newline: str) -> None:
    with open(path, "wb") as fh:
        fh.write(text.replace("\n", newline).encode("utf-8"))


def format_number(new_value: float, old_token: str) -> str:
    """원본이 정수 표기였으면 정수로, 소수 표기였으면 소수로 맞춘다."""
    if "." not in old_token and float(new_value).is_integer():
        return str(int(new_value))
    text = f"{float(new_value):.6f}".rstrip("0").rstrip(".")
    return text or "0"


def patch_scalar(text: str, field: str, new_value: float) -> tuple[str, str]:
    if field not in SCALAR_FIELDS:
        raise Refused(f"화이트리스트에 없는 필드: {field}")
    pattern = re.compile(rf"^(\s*{re.escape(field)}: )(-?[\d.]+)\s*$", re.M)
    hits = list(pattern.finditer(text))
    if not hits:
        raise Refused(f"{field} 를 찾지 못했다")
    if len(hits) > 1:
        raise Refused(f"{field} 가 {len(hits)}번 나온다 — 어느 것인지 확정할 수 없어 건너뛴다")
    hit = hits[0]
    old = hit.group(2)
    replacement = hit.group(1) + format_number(new_value, old)
    return text[: hit.start()] + replacement + text[hit.end():], old


def patch_list_amount(text: str, field: str, resource: str, new_value: float) -> tuple[str, str]:
    if field not in LIST_FIELDS:
        raise Refused(f"화이트리스트에 없는 리스트 필드: {field}")
    if resource not in RESOURCE_TYPE:
        raise Refused(f"모르는 자원 이름: {resource}")
    type_id = RESOURCE_TYPE[resource]

    # YAML 시퀀스는 키와 같은 들여쓰기에서 "- "로 시작하고, 이어지는 줄은 더 깊게 들여쓴다.
    block = re.search(rf"^(\s*){re.escape(field)}:\s*$\n((?:\1[- ].*\n)*)", text, re.M)
    if not block:
        raise Refused(f"{field} 블록을 찾지 못했다")
    body = block.group(2)
    entry = re.search(rf"(- Type: {type_id}\s*\n\s*Amount: )(-?[\d.]+)", body)
    if not entry:
        raise Refused(f"{field} 안에 {resource}(Type {type_id}) 항목이 없다")
    old = entry.group(2)
    new_body = body[: entry.start()] + entry.group(1) + format_number(new_value, old) + body[entry.end():]
    return text[: block.start(2)] + new_body + text[block.end(2):], old


def unity_running() -> bool:
    try:
        out = subprocess.run(
            ["tasklist", "/FI", "IMAGENAME eq Unity.exe", "/NH"],
            capture_output=True, text=True, timeout=10,
        ).stdout
        return "Unity.exe" in out
    except Exception:
        return False


def main() -> int:
    ap = argparse.ArgumentParser(description="밸런스 시뮬레이터 변경분을 Unity 에셋에 반영한다.")
    ap.add_argument("changes", help="BalanceSimulator.html이 내보낸 JSON")
    ap.add_argument("--apply", action="store_true", help="실제로 파일을 수정한다(기본은 드라이런)")
    ap.add_argument("--repo", default=REPO, help="프로젝트 루트")
    args = ap.parse_args()

    sys.stdout.reconfigure(encoding="utf-8")

    with open(args.changes, encoding="utf-8") as fh:
        payload = json.load(fh)

    changes = payload.get("changes", [])
    skipped = payload.get("unappliable", [])
    if not changes:
        print("반영할 항목이 없다.")
        return 0

    # 가드는 "이 프로젝트"에 쓸 때만 의미가 있다. 다른 디렉터리(샌드박스)면 건너뛴다.
    targets_project = os.path.normcase(os.path.abspath(args.repo)) == os.path.normcase(REPO)
    if args.apply and targets_project and unity_running():
        print("Unity 에디터가 실행 중이다. 에셋을 덮어쓰면 에디터가 메모리 값으로 되돌린다.")
        print("Unity를 닫고 다시 실행할 것.")
        return 2

    # 파일 단위로 묶어 한 번만 읽고 쓴다
    by_file: dict[str, list[dict]] = {}
    for change in changes:
        by_file.setdefault(change["asset"], []).append(change)

    planned, refused = [], []
    edited: dict[str, tuple[str, str]] = {}

    for rel, items in sorted(by_file.items()):
        path = os.path.join(args.repo, rel.replace("/", os.sep))
        if not os.path.isfile(path):
            refused.append((rel, "-", "파일이 없다"))
            continue
        text, newline = read_text(path)
        for item in items:
            field, new_value, label = item["field"], item["to"], item.get("label", "")
            try:
                match = re.fullmatch(r"([A-Za-z_]+)\[([A-Za-z]+)\]", field)
                if match:
                    text, old = patch_list_amount(text, match.group(1), match.group(2), new_value)
                else:
                    text, old = patch_scalar(text, field, new_value)
                planned.append((rel, field, old, format_number(new_value, old), label))
            except Refused as exc:
                refused.append((rel, field, str(exc)))
        edited[path] = (text, newline)

    width = max((len(r) for r, *_ in planned), default=10)
    print(f"{'적용 예정' if not args.apply else '적용'} — {len(planned)}건\n")
    for rel, field, old, new, label in planned:
        print(f"  {rel:<{width}}  {field:<28} {old:>10} → {new:<10}  {label}")

    if refused:
        print(f"\n건너뜀 — {len(refused)}건 (파일은 건드리지 않았다)")
        for rel, field, why in refused:
            print(f"  {rel}  {field}: {why}")

    if skipped:
        print(f"\n수동 반영 필요 — {len(skipped)}건")
        for item in skipped:
            print(f"  {item['label']}: {item['from']} → {item['to']}  ({item['reason']})")

    if not args.apply:
        print("\n드라이런이다. 실제로 반영하려면 --apply 를 붙일 것.")
        return 0

    for path, (text, newline) in edited.items():
        shutil.copy2(path, path + ".bak")
        write_text(path, text, newline)
    print(f"\n{len(edited)}개 파일 수정. 원본은 .bak 으로 남겼다.")
    print("Unity를 열어 값이 맞는지 확인한 뒤 .bak 파일을 지울 것.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
