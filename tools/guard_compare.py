#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Сравнение фактических результатов (код приложения, вывод GuardRunner)
с ожидаемыми из tools/baseline/control_vectors.json (независимый калькулятор).

Для QualitativeProfile (блоки 8, 9) маркер 'Профиль' не является содержательным
результатом. Проверяется фактическое содержание профиля: в summary должны
присутствовать интерпретации всех выбранных вариантов. Это строже прежней проверки.
"""
import json, sys
import re

def norm_primary(t, v):
    if v is None: return None
    s = str(v)
    if t in ('OddEvenSum', 'HemisphereSum'):
        nums = re.findall(r'-?\d+', s)
        if len(nums) == 2:
            return f"{nums[0]}|{nums[1]}"
        return s
    if t == 'SequenceCombo':
        return s.upper().replace('L', 'Л').replace('R', 'П')
    return s

def seq_combo_from_scores(scores):
    c = (scores or {}).get('combo')
    if c is None: return None
    return str(c).upper().replace('L', 'Л').replace('R', 'П')

def primary_matches(t, eprim, aprimary, ascores):
    en = norm_primary(t, eprim)
    if t == 'SequenceCombo':
        ac = seq_combo_from_scores(ascores)
        an = norm_primary(t, aprimary)
        return str(en) == str(ac) or str(en) == str(an)
    if t in ('OddEvenSum', 'HemisphereSum') and '|' in str(en):
        e1, e2 = str(en).split('|')
        vals = [str(int(float(v))) for v in (ascores or {}).values()]
        return len(vals) == 2 and vals[0] == e1 and vals[1] == e2
    an = norm_primary(t, aprimary)
    return str(en) == str(an)

def norm_level(v):
    if v is None: return ''
    s = str(v).strip()
    s = re.sub(r'\s*[（(].*?[)）]\s*', '', s)
    s = s.replace('"', '').replace("'", '')
    s = s.lower().strip()
    # Косметический префикс эталона: "Комбинация ПППП" и "ПППП" — одно значение.
    # Сама комбинация букв продолжает сравниваться точно.
    if s.startswith('комбинация '):
        s = s[len('комбинация '):].strip()
    return s
    
def eq_level(a, b):
    return norm_level(a) == norm_level(b)

def check_profile(bid, vec, a):
    """Содержательная проверка качественного профиля."""
    summ = str(a.get('summary') or '')
    if not summ.strip():
        return "summary пуст — профиль не сформирован"
    block = json.load(open(f'Data/Blocks/{bid}.json', encoding='utf-8'))
    qmap = {q['id']: q for q in block.get('questions', [])}
    missing = []
    for qid, ans in (vec.get('answers') or {}).items():
        q = qmap.get(qid)
        if not q: continue
        opt = next((o for o in q.get('options', []) if o.get('key') == ans), None)
        if opt is None: continue
        text = (opt.get('interpretation') or opt.get('text') or '').strip()
        if text and text not in summ:
            missing.append(qid)
    if missing:
        return "в summary нет интерпретаций для: " + ", ".join(missing)
    return None

def main(exp_path, act_path):
    exp = json.load(open(exp_path, encoding='utf-8'))['blocks']
    act = json.load(open(act_path, encoding='utf-8'))
    types = {}
    for bid in exp:
        d = json.load(open(f'Data/Blocks/{bid}.json', encoding='utf-8'))
        types[bid] = d['scoring']['type']
    passed = failed = 0; mismatches = []
    for bid, bd in exp.items():
        t = types[bid]
        for vec in bd['vectors']:
            name = vec['name']; e = vec['expected']
            a = act.get(bid, {}).get(name)
            def fail(msg):
                nonlocal failed
                failed += 1; mismatches.append(f"{bid}/{name}: {msg}")
            if a is None: fail("нет результата от кода приложения"); continue
            if not a.get('ok'): fail(f"исключение: {a.get('error')}"); continue
            if t == 'QualitativeProfile':
                problem = check_profile(bid, vec, a)
                if problem: fail(problem); continue
                passed += 1; continue
            if not primary_matches(t, e['primary'], a.get('primary'), a.get('scores')):
                fail(f"primary: ожидалось {e['primary']!r}, получено {a.get('primary')!r} scores={a.get('scores')!r}"); continue
            el = e.get('level'); al = a.get('level')
            if not eq_level(el, al):
                fail(f"level: ожидалось {el!r}, получено {al!r}"); continue
            passed += 1
    print(json.dumps({'passed': passed, 'failed': failed, 'mismatches': mismatches}, ensure_ascii=False, indent=1))
    return 0 if failed == 0 else 1

if __name__ == '__main__':
    sys.exit(main(sys.argv[1], sys.argv[2]))
