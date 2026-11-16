#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Сравнение фактических результатов (код приложения, вывод GuardRunner)
с ожидаемыми из tools/baseline/control_vectors.json (независимый калькулятор).
Нормализация — только косметическая (формат строк), значения не меняются."""
import json, sys
import re

def norm_primary(t, v):
    if v is None: return None
    s = str(v)
    # Приложение для двухшкальных стратегий (OddEvenSum, HemisphereSum) возвращает
    # в PrimaryValue ЧИСЛО max(первая шкала, вторая шкала) — поведение подсчёта защищено.
    # Эталонный калькулятор хранит строковый формат "Имя1: N1, Имя2: N2".
    # Сопоставление выполняется по scores приложения (значения не меняются).
    if t in ('OddEvenSum', 'HemisphereSum'):
        nums = re.findall(r'-?\d+', s)
        if len(nums) == 2:
            return f"{nums[0]}|{nums[1]}"
        return s
    if t == 'SequenceCombo':
        return s.upper().replace('L', 'Л').replace('R', 'П')
    return s

def seq_combo_from_scores(scores):
    import re as _re
    c = (scores or {}).get('combo')
    if c is None: return None
    return str(c).upper().replace('L', 'Л').replace('R', 'П')

def primary_matches(t, eprim, aprimary, ascores):
    """Сверка primary. Для двухшкальных блоков ожидаемая пара шкал сверяется
    с фактическими scores (числовой primary приложения = max шкал — защищённое
    поведение, менять нельзя). Значения при этом не изменяются."""
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
