#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Сравнение фактических результатов (код приложения, вывод GuardRunner)
с ожидаемыми из tools/baseline/control_vectors.json (независимый калькулятор).
Нормализация — только косметическая (формат строк), значения не меняются."""
import json, sys

def norm_primary(t, v):
    if v is None: return None
    s = str(v)
    # "Л: 50, П: 50" <-> "Л:50 П:50" <-> "50/50"
    if t in ('OddEvenSum', 'HemisphereSum'):
        import re
        m = re.findall(r'([^:,/\s]+):\s*(-?\d+)|(-?\d+)\s*/\s*(-?\d+)', s)
        parts = re.split(r'[;,]', s)
        nums = re.findall(r'-?\d+', s)
        names = re.findall(r'[А-Яа-яЁёA-Za-z]+(?=\s*:)', s)
        if len(nums) == 2:
            if names: return f"{names[0]}:{nums[0]}|{names[-1] if len(names)>1 else names[0]}:{nums[1]}"
            return f"??:{nums[0]}|??:{nums[1]}"
        return s
    if t == 'SequenceCombo':
        return s.upper().replace('L', 'Л').replace('R', 'П')
    return s

def norm_level(v):
    if v is None: return ''
    s = str(v).strip()
    # убрать числовые хвосты вида "(Лидер > Администратор)" и кавычки
    import re
    s = re.sub(r'\s*[（(].*?[)）]\s*', '', s)
    s = s.replace('"', '').replace("'", '')
    # эквивалентные формулировки уровней (косметика)
    aliases = [
        ('Равновесие', 'Равные шкалы'), ('равные', 'Равные'),
    ]
    return s.lower()

def eq_level(a, b):
    a, b = norm_level(a), norm_level(b)
    if a == b: return True
    # «Т = П» vs «Равновесие» etc.
    return False

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
            ep = norm_primary(t, e['primary']); ap = norm_primary(t, a.get('primary'))
            if str(ep) != str(ap):
                fail(f"primary: ожидалось {e['primary']!r}, получено {a.get('primary')!r}"); continue
            el = e.get('level'); al = a.get('level')
            if not eq_level(el, al):
                fail(f"level: ожидалось {el!r}, получено {al!r}"); continue
            passed += 1
    print(json.dumps({'passed': passed, 'failed': failed, 'mismatches': mismatches}, ensure_ascii=False, indent=1))
    return 0 if failed == 0 else 1

if __name__ == '__main__':
    sys.exit(main(sys.argv[1], sys.argv[2]))
