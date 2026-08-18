# template.html + sim.js + bundle.json -> Docs/BalanceReport/BalanceSimulator.html
# 에셋이 바뀌면 build_bundle.py를 먼저 돌린 뒤 이걸 돌린다.
import io, os, sys, json
import os as _os
_HERE = _os.path.dirname(_os.path.abspath(__file__))
_REPO = _os.path.abspath(_os.path.join(_HERE, "..", "..", ".."))


sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8')
HERE = _HERE
OUT = _os.path.join(_HERE, "..", "BalanceSimulator.html")

tpl = open(os.path.join(HERE, 'template.html'), encoding='utf-8').read()
sim = open(os.path.join(HERE, 'sim.js'), encoding='utf-8').read()
bundle = open(os.path.join(HERE, 'bundle.json'), encoding='utf-8').read()

# <script type="application/json"> 안에서 </script>가 조기 종료시키지 않게 막는다.
bundle = bundle.replace('</', '<\\/')

assert '/*__BUNDLE__*/' in tpl and '/*__SIM__*/' in tpl
html = tpl.replace('/*__BUNDLE__*/', bundle).replace('/*__SIM__*/', sim)

os.makedirs(os.path.dirname(OUT), exist_ok=True)
open(OUT, 'w', encoding='utf-8', newline='\n').write(html)
print('wrote', OUT, len(html), 'chars')
