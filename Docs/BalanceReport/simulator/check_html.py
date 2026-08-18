# 빌드된 HTML을 정적 검사한다: JSON 파싱 / 스크립트 문법 / 토큰 누락 / 미해결 플레이스홀더
import io, os, re, sys, json, subprocess, tempfile
import os as _os
_HERE = _os.path.dirname(_os.path.abspath(__file__))
_REPO = _os.path.abspath(_os.path.join(_HERE, "..", "..", ".."))


sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
SRC = _os.path.join(_HERE, "..", "BalanceSimulator.html")
html = open(SRC, encoding='utf-8').read()
fail = []

# 1) 플레이스홀더 잔존
for ph in ('__BUNDLE__', '__SIM__'):
    if ph in html:
        fail.append(f'플레이스홀더 미치환: {ph}')

# 2) 금지 요소 (Artifact는 head/body 스켈레톤을 씌운다)
for tag in ('<!doctype', '<html', '<head', '<body'):
    if tag in html.lower():
        fail.append(f'금지 태그 포함: {tag}')

# 3) 외부 리소스 (CSP 차단 대상)
for m in re.finditer(r'(?:src|href)\s*=\s*["\'](https?:)?//', html):
    fail.append('외부 리소스 참조: ' + html[m.start():m.start() + 60])

# 4) 스크립트 추출
blocks = re.findall(r'<script([^>]*)>(.*?)</script>', html, re.S)
print(f'script 블록 {len(blocks)}개')
tmp = tempfile.mkdtemp()
for i, (attrs, body) in enumerate(blocks):
    if 'application/json' in attrs:
        try:
            data = json.loads(body)
            print(f'  [{i}] JSON OK — 최상위 키 {len(data)}개, {len(body)}바이트')
            for k in ('towers', 'monsters', 'waves', 'chunks', 'profiles', 'research',
                      'economy', 'production', 'portalTerrain', 'conquestDuration'):
                if k not in data:
                    fail.append(f'번들에 {k} 없음')
        except Exception as e:
            fail.append(f'[{i}] JSON 파싱 실패: {e}')
        continue
    path = os.path.join(tmp, f's{i}.js')
    open(path, 'w', encoding='utf-8').write(body)
    r = subprocess.run(['node', '--check', path], capture_output=True, text=True, encoding='utf-8')
    if r.returncode != 0:
        fail.append(f'[{i}] JS 문법 오류:\n{r.stderr[:900]}')
    else:
        print(f'  [{i}] JS 문법 OK — {len(body)}바이트')

# 5) CSS 변수: 사용했는데 정의 안 된 것
used = set(re.findall(r'var\(--([a-z0-9-]+)\)', html))
css = '\n'.join(re.findall(r'<style>(.*?)</style>', html, re.S))
declared = set(re.findall(r'(?<!var\()--([a-z0-9-]+)\s*:', css))
missing = used - declared
if missing:
    fail.append('정의되지 않은 CSS 변수: ' + ', '.join(sorted(missing)))
else:
    print(f'CSS 변수 OK — 사용 {len(used)}개 전부 정의됨')

# 6) 라이트/다크 토큰 대칭
for block in ('@media (prefers-color-scheme:dark)', ':root[data-theme="dark"]', ':root[data-theme="light"]'):
    if block not in html:
        fail.append(f'테마 블록 누락: {block}')

# 7) UI가 참조하는 id가 마크업에 있는지
ids_in_markup = set(re.findall(r'\bid="([^"]+)"', html))
ids_used = set(re.findall(r"getElementById\('([^']+)'\)", html))
gap = ids_used - ids_in_markup
if gap:
    fail.append('getElementById 대상 없음: ' + ', '.join(sorted(gap)))
else:
    print(f'id 참조 OK — {len(ids_used)}개')

# 8) title / favicon 준비
if '<title>' not in html:
    fail.append('<title> 없음')

print()
if fail:
    print('=== 실패 ===')
    for f in fail:
        print(' -', f)
    sys.exit(1)
print('=== 정적 검사 전부 통과 ===')
