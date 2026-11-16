#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Независимый калькулятор контрольных векторов.
Читает ключи/веса/формулы/пороги напрямую из Data/Blocks/*.json
(файлы импортированы из docs/methodics_source.md). НЕ использует код приложения.
Латинские значения в keys (a/b/c/l/r) соответствуют вариантам по порядку —
зафиксированное документированное соответствие, содержимое не меняется."""
import json, os, sys

MAP = {'a': 'а', 'b': 'б', 'c': 'в', 'd': 'г', 'e': 'д'}

def norm(v):  # нормализация внутреннего значения ключа к значению варианта
    return MAP.get(str(v), str(v))

def calc(block, answers):
    sc = block['scoring']; qs = block['questions']
    qids = [q['id'] for q in qs]
    t = sc['type']
    if t == 'SumKey':
        s = sum(1 for q in qids if norm(sc['keys'][q]) == answers[q]); prim = s; scores = {'score': s}
    elif t == 'SumKeyWeighted':
        w = sc.get('pointsPerCorrectAnswer', 5)
        s = w * sum(1 for q in qids if norm(sc['keys'][q]) == answers[q]); prim = s; scores = {'score': s}
    elif t == 'SumScale':
        s = sum(float(answers[q]) for q in qids); prim = int(s); scores = {'score': int(s)}
    elif t == 'OddEvenSum':
        odd = sum(float(answers[q]) for i, q in enumerate(qids, 1) if i % 2 == 1)
        even = sum(float(answers[q]) for i, q in enumerate(qids, 1) if i % 2 == 0)
        n1 = sc.get('oddScaleName', 'Odd'); n2 = sc.get('evenScaleName', 'Even')
        scores = {n1: int(odd), n2: int(even)}
        prim = f"{n1}: {int(odd)}, {n2}: {int(even)}"
    elif t == 'HemisphereSum':
        g = sc['questionGroups']
        L = sum(float(answers[q]) for q in g['left']); R = sum(float(answers[q]) for q in g['right'])
        scores = {'Л': int(L), 'П': int(R)}; prim = f"Л: {int(L)}, П: {int(R)}"
    elif t == 'FormulaMultiplier':
        w = sc['weights']; mult = 2
        s = sum(w[str(answers[q])] for q in qids); prim = int(s * mult); scores = {'score': int(s * mult)}
    elif t == 'CountLeft':
        c = sum(1 for q in qids if answers[q] == 'l'); prim = c; scores = {'leftCount': c}
    elif t == 'SequenceCombo':
        combo = ''.join(norm(answers[q]) for q in qids)
        prim = combo.upper().replace('L', 'Л').replace('R', 'П'); scores = {'combo': prim}
    elif t == 'PalmerMatrix':
        m = sc['keys']; s = 0
        for q in qs:
            idx = [o['key'] for o in q['options']].index(answers[q['id']])
            s += m[q['id']][['a','b','c','d','e'][idx]]
        prim = s; scores = {'score': s}
    elif t == 'QualitativeProfile':
        lines = []
        for q in qs:
            sel = answers[q['id']]
            opt = next(o for o in q['options'] if o['key'] == sel)
            lines.append(f"{q['text']}: {opt.get('interpretation','')}")
        return 'Профиль', {}, '\n'.join(lines), 'Качественный профиль'
    else:
        raise ValueError('unknown scoring type: ' + t)
    lvl = pick_level(t, sc, prim, scores)
    return prim, scores, None, lvl

def pick_level(t, sc, prim, scores):
    interps = sc.get('interpretations', [])
    for r in interps:
        cond = r.get('condition')
        if cond:
            vals = list(scores.values())
            a, b = vals[0], vals[1]
            if cond.startswith('oddGtBy:'):
                d = float(cond.split(':')[1])
                if a - b > d: return r['level']
            elif cond == 'evenGtOdd':
                if b > a: return r['level']
            elif cond == 'oddGtEven':
                if a > b: return r['level']
            elif cond.startswith('absDiffLt:'):
                d = float(cond.split(':')[1])
                if abs(a - b) < d: return r['level']
            elif cond == 'bothLow':
                if a <= 30 and b <= 30: return r['level']
            elif cond == 'equal':
                if a == b: return r['level']
            continue
        if 'value' in r:
            if str(r['value']) == str(prim): return r['level']
            continue
        mn = r.get('min'); mx = r.get('max')
        pv = prim if isinstance(prim, (int, float)) else None
        if pv is None: continue
        if (mn is None or pv >= mn) and (mx is None or pv <= mx): return r['level']
    # fallback для двухшкальных без условий
    vals = list(scores.values())
    if len(vals) == 2:
        a, b = vals[0], vals[1]; n1, n2 = list(scores.keys())
        if a == b: return f'{n1} = {n2}'
        return f'{n1} > {n2}' if a > b else f'{n2} > {n1}'
    return ''

def build_vectors(block):
    sc = block['scoring']; qs = block['questions']; t = sc['type']
    qids = [q['id'] for q in qs]
    def keys_of(q): return [o['key'] for o in q.get('options', [])]
    sets = {}
    if t in ('SumKey', 'SumKeyWeighted'):
        key = {}
        for q in qs:
            kv = norm(sc['keys'][q['id']])
            kk = keys_of(q)
            key[q['id']] = kv if kv in kk else kk[0]
        sets['all_min'] = {q['id']: keys_of(q)[0] for q in qs}
        sets['all_max'] = {q['id']: keys_of(q)[-1] for q in qs}
        sets['match_key'] = dict(key)
        sets['match_none'] = {q['id']: ([x for x in keys_of(q) if x != key[q['id']]] or [key[q['id']]])[0] for q in qs}
        sets['alternating'] = {q['id']: (key[q['id']] if i % 2 == 0 else ([x for x in keys_of(q) if x != key[q['id']]] or [key[q['id']]])[0]) for i, q in enumerate(qs)}
        one = dict(key); fq = qs[0]['id']
        one[fq] = ([x for x in keys_of(qs[0]) if x != key[fq]] or [key[fq]])[0]
        sets['one_changed'] = one
    elif t == 'SumScale':
        mn = str(qs[0]['scale']['min']); mx = str(qs[0]['scale']['max'])
        sets['all_min'] = {q: mn for q in qids}
        sets['all_max'] = {q: mx for q in qids}
        sets['alternating'] = {q: (mn if i % 2 == 0 else mx) for i, q in enumerate(qids)}
        mid = str((int(mn) + int(mx)) // 2); sets['mid'] = {q: mid for q in qids}
        one = dict(sets['mid']); one[qids[0]] = mn; sets['one_changed'] = one
    elif t in ('OddEvenSum', 'HemisphereSum'):
        smax = str(qs[0].get('scale', {}).get('max', 10)); smin = str(qs[0].get('scale', {}).get('min', 0))
        sets['all_min'] = {q: smin for q in qids}
        sets['all_max'] = {q: smax for q in qids}
        sets['alternating'] = {q: (smax if i % 2 == 0 else smin) for i, q in enumerate(qids)}
        mid = str((int(smin) + int(smax)) // 2)
        one = {q: mid for q in qids}; one[qids[0]] = smin; sets['one_changed'] = one
    elif t == 'FormulaMultiplier':
        ws = sorted(sc['weights'].items(), key=lambda x: x[1])
        sets['all_min'] = {q: ws[0][0] for q in qids}
        sets['all_max'] = {q: ws[-1][0] for q in qids}
        sets['alternating'] = {q: ws[i % len(ws)][0] for i, q in enumerate(qids)}
        one = dict(sets['all_min']); one[qids[0]] = ws[-1][0]; sets['one_changed'] = one
    elif t in ('CountLeft', 'SequenceCombo'):
        kk = keys_of(qs[0]); l = [x for x in kk if x == 'l'] or kk[-1:]; r = [x for x in kk if x == 'r'] or kk[:1]
        sets['all_min'] = {q: r[0] for q in qids}   # минимум левых
        sets['all_max'] = {q: l[0] for q in qids}   # максимум левых
        sets['alternating'] = {q: (l[0] if i % 2 == 0 else r[0]) for i, q in enumerate(qids)}
        one = dict(sets['all_min']); one[qids[0]] = l[0]; sets['one_changed'] = one
    elif t == 'PalmerMatrix':
        m = sc['keys']
        def val_for(qid, ans): 
            kk = keys_of(next(x for x in qs if x['id'] == qid)); return m[qid][['a','b','c','d','e'][kk.index(ans)]]
        sets['all_min'] = {q['id']: min(keys_of(q), key=lambda a: val_for(q['id'], a)) for q in qs}
        sets['all_max'] = {q['id']: max(keys_of(q), key=lambda a: val_for(q['id'], a)) for q in qs}
        sets['alternating'] = {q['id']: keys_of(q)[i % len(keys_of(q))] for i, q in enumerate(qs)}
        one = dict(sets['all_min']); one[qs[0]['id']] = keys_of(qs[0])[1]; sets['one_changed'] = one
    elif t == 'QualitativeProfile':
        sets['all_first'] = {q['id']: keys_of(q)[0] for q in qs}
        sets['all_last'] = {q['id']: keys_of(q)[-1] for q in qs}
        sets['alternating'] = {q['id']: (keys_of(q)[0] if i % 2 == 0 else keys_of(q)[-1]) for i, q in enumerate(qs)}
        one = dict(sets['all_first']); one[qs[0]['id']] = keys_of(qs[0])[-1]; sets['one_changed'] = one
    else:
        raise ValueError(t)
    vecs = []
    for name, ans in sets.items():
        prim, scores, detail, lvl = calc(block, ans)
        v = {'name': name, 'answers': ans, 'expected': {'primary': prim, 'scores': scores, 'level': lvl}}
        if detail is not None: v['expected']['details'] = detail
        vecs.append(v)
    return vecs

if __name__ == '__main__':
    out_path = sys.argv[1] if len(sys.argv) > 1 else 'tools/baseline/control_vectors.json'
    blocks_dir = sys.argv[2] if len(sys.argv) > 2 else 'Data/Blocks'
    out = {'generatedBy': 'independent calculator tools/guard_vectors.py (reads keys/weights/formulas directly from Data/Blocks/*.json imported from docs/methodics_source.md)',
           'note': 'Ожидаемые значения вычислены НЕ кодом приложения. Латинские значения в keys блоков соответствуют вариантам ответов по порядку (a->первый, b->второй, c->третий).',
           'blocks': {}}
    total = 0
    for f in sorted(os.listdir(blocks_dir)):
        if not (f.startswith('block_') and f.endswith('.json')): continue
        blk = json.load(open(os.path.join(blocks_dir, f), encoding='utf-8'))
        vecs = build_vectors(blk)
        out['blocks'][blk['id']] = {'order': blk['order'], 'vectors': vecs}
        total += len(vecs)
    json.dump(out, open(out_path, 'w', encoding='utf-8'), ensure_ascii=False, indent=1)
    print(f'total vectors: {total}')
