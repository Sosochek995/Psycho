#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Генерация файлов эталона защиты методик (tools/baseline/):
  - protected_files.json      — SHA256 защищённых файлов;
  - methodology_baseline.json — смысловой слепок 12 блоков (без косметических полей);
  - control_vectors.json      — контрольные наборы ответов и ожидаемые результаты,
                                вычисленные НЕЗАВИСИМЫМ калькулятором
                                (tools/independent_calculator.py), а не кодом приложения.

Запуск:  python3 tools/generate_baseline.py
"""
import json
import hashlib
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT / "tools"))
from independent_calculator import Calculator, norm_key  # noqa: E402

BASELINE_DIR = ROOT / "tools" / "baseline"

COSMETIC_FIELDS = {"displayLabel", "letter", "displayText", "uiHint"}

PROTECTED_CODE_FILES = [
    "Services/ScoringService.cs",
    "Services/Interfaces/IScoringService.cs",
    "Models/ScoringConfig.cs",
    "Models/Enums.cs",
    "Models/Question.cs",
    "Models/AnswerOption.cs",
    "Models/ScaleConfig.cs",
    "Models/InterpretationRule.cs",
]


def sha256_file(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def strip_cosmetic(obj):
    """Рекурсивно удаляет косметические поля (они НЕ входят в слепок)."""
    if isinstance(obj, dict):
        return {k: strip_cosmetic(v) for k, v in obj.items() if k not in COSMETIC_FIELDS}
    if isinstance(obj, list):
        return [strip_cosmetic(x) for x in obj]
    return obj


# ------------------------------------------------------------- protected ----

def build_protected_files(calc: Calculator) -> dict:
    paths = ["docs/methodics_source.md"] + PROTECTED_CODE_FILES
    # as_posix() даёт прямые слэши на любой ОС. str(Path) на Windows даёт обратные,
    # из-за чего эталон становится непортируемым: на Linux такой путь не найдётся.
    paths += sorted(p.relative_to(ROOT).as_posix() for p in (ROOT / "Data" / "Blocks").glob("*.json"))
    entries = []
    for rel in paths:
        p = ROOT / rel
        if not p.exists():
            raise FileNotFoundError(f"Защищённый файл отсутствует: {rel}")
        entries.append({"path": rel, "sha256": sha256_file(p)})
    return {"generatedBy": "tools/generate_baseline.py", "files": entries}


# -------------------------------------------------------------- baseline ----

def block_digest(b: dict) -> dict:
    questions = []
    for q in b["questions"]:
        questions.append({
            "id": q["id"],
            "text": q["text"],
            "type": q["type"],
            "required": q.get("required", True),
            "group": q.get("group"),
            "imageKey": q.get("imageKey"),
            "scale": q.get("scale"),
            "options": [{"key": o["key"], "text": o["text"],
                         "interpretation": o.get("interpretation")}
                        for o in q.get("options", [])],
        })
    scoring = strip_cosmetic(b["scoring"])
    return {
        "id": b["id"],
        "order": b["order"],
        "title": b["title"],
        "subtitle": b.get("subtitle"),
        "instruction": b["instruction"],
        "answerType": b.get("answerType"),
        "questionCount": len(b["questions"]),
        "questions": questions,
        "scoring": scoring,
        "interpretations": scoring.get("interpretations", []),
    }


def build_methodology_baseline(calc: Calculator) -> dict:
    blocks = [block_digest(calc.blocks[bid]) for bid in sorted(calc.blocks, key=lambda x: calc.blocks[x]["order"])]
    return {
        "generatedBy": "tools/generate_baseline.py",
        "note": ("Смысловой слепок защищённых данных. Косметические поля "
                 f"{sorted(COSMETIC_FIELDS)} исключены и не защищены."),
        "blockCount": len(blocks),
        "blocks": blocks,
    }


# ------------------------------------------------------ control vectors -----

def answers_for_block(b: dict, mode: str) -> dict:
    """Формирует набор ответов по режиму. Значения — внутренние ключи вариантов."""
    qs = b["questions"]
    t = b["scoring"]["type"]
    keys = b["scoring"].get("keys") or {}
    ans = {}

    def option_at(q, i):
        opts = q.get("options", [])
        if not opts:
            return None
        return opts[i % len(opts)]["key"]

    def opposite_of_key(q, want_norm):
        for o in q.get("options", []):
            if norm_key(o["key"]) != want_norm:
                return o["key"]
        return q["options"][0]["key"]

    if t in ("SumKey", "SumKeyWeighted"):
        for q in qs:
            want = norm_key(str(keys[q["id"]]))
            if mode == "match_all":
                val = next((o["key"] for o in q["options"] if norm_key(o["key"]) == want), q["options"][0]["key"])
            elif mode == "match_none":
                val = opposite_of_key(q, want)
            elif mode == "min":
                val = q["options"][0]["key"]
            elif mode == "max":
                val = q["options"][-1]["key"]
            elif mode == "alternate":
                val = option_at(q, qs.index(q))
            else:
                raise ValueError(mode)
            ans[q["id"]] = val
    elif t == "PalmerMatrix":
        order = {"min": 0, "max": 2, "match_all": 2, "match_none": 0, "alternate": None}
        idx = order[mode]
        for i, q in enumerate(qs):
            ans[q["id"]] = q["options"][i % 3 if idx is None else idx]["key"]
    elif t in ("SumScale", "OddEvenSum", "HemisphereSum"):
        sc = qs[0].get("scale") or b.get("scale") or {"min": 0, "max": 10}
        lo, hi = sc["min"], sc["max"]
        for i, q in enumerate(qs):
            s = q.get("scale") or sc
            if mode == "min" or mode == "match_none":
                v = s["min"]
            elif mode == "max" or mode == "match_all":
                v = s["max"]
            elif mode == "alternate":
                v = s["max"] if i % 2 == 0 else s["min"]
            ans[q["id"]] = str(v)
    elif t == "FormulaMultiplier":
        letter_by_weight = {"min": "a", "max": "d", "match_all": "d", "match_none": "a", "alternate": None}
        pick = letter_by_weight[mode]
        cycle = ["a", "b", "c", "d", "e"]
        for i, q in enumerate(qs):
            k = cycle[i % 5] if pick is None else pick
            ans[q["id"]] = next(o["key"] for o in q["options"] if norm_key(o["key"]) == k)
    elif t in ("CountLeft", "SequenceCombo"):
        lk = {"min": "r", "max": "l", "match_all": "l", "match_none": "r", "alternate": None}[mode]
        for i, q in enumerate(qs):
            want = (cycle_lr := ("l" if i % 2 == 0 else "r")) if lk is None else lk
            ans[q["id"]] = next(o["key"] for o in q["options"] if norm_key(o["key"]) == want)
    elif t == "QualitativeProfile":
        for i, q in enumerate(qs):
            if mode == "max" or mode == "match_all":
                j = len(q["options"]) - 1
            elif mode == "alternate":
                j = i % len(q["options"])
            else:
                j = 0
            ans[q["id"]] = q["options"][j]["key"]
    else:
        raise ValueError(f"Неизвестный тип {t}")
    return ans


def flip_one(b: dict, base: dict) -> dict:
    """Изменяет ровно один ответ относительно базового набора.

    Ищет первый вопрос, у которого есть другой вариант/значение шкалы;
    если вариативности нет (все ответы предопределены), набор не меняется.
    """
    qs = b["questions"]
    changed = dict(base)
    for q in reversed(qs):
        cur = changed[q["id"]]
        if q.get("options"):
            nxt = next((o["key"] for o in q["options"] if norm_key(o["key"]) != norm_key(cur)), None)
            if nxt is not None:
                changed[q["id"]] = nxt
                return changed
        else:  # шкала: сдвиг на 1 в пределах шкалы
            s = q["scale"]
            v = int(cur)
            nv = v - 1 if v > s["min"] else (v + 1 if v < s["max"] else None)
            if nv is not None:
                changed[q["id"]] = str(nv)
                return changed
    return changed


def build_control_vectors(calc: Calculator) -> dict:
    modes = ["min", "max", "match_all", "match_none", "alternate", "one_flipped"]
    out = {"generatedBy": "tools/generate_baseline.py (независимый калькулятор)",
           "note": "Ожидаемые значения получены tools/independent_calculator.py из "
                   "docs/methodics_source.md и Data/Blocks/*.json, без участия кода приложения.",
           "blocks": {}}
    for bid in sorted(calc.blocks, key=lambda x: calc.blocks[x]["order"]):
        b = calc.blocks[bid]
        vecs = []
        for m in modes:
            if m == "one_flipped":
                a = flip_one(b, answers_for_block(b, "match_all"))
            else:
                a = answers_for_block(b, m)
            expected = calc.expected(bid, a)
            vecs.append({"name": m, "answers": a, "expected": expected})
        out["blocks"][bid] = {"order": b["order"], "vectors": vecs}
    return out


# ------------------------------------------------------------------ main ---

def main():
    calc = Calculator()
    problems = calc.cross_check_with_source()
    if problems:
        print("ОШИБКА: расхождение JSON с docs/methodics_source.md:")
        for p in problems:
            print(" -", p)
        sys.exit(1)

    BASELINE_DIR.mkdir(parents=True, exist_ok=True)
    files = {
        "protected_files.json": build_protected_files(calc),
        "methodology_baseline.json": build_methodology_baseline(calc),
        "control_vectors.json": build_control_vectors(calc),
    }
    for name, data in files.items():
        path = BASELINE_DIR / name
        path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        print(f"создан {path.relative_to(ROOT)}")

    cv = files["control_vectors.json"]["blocks"]
    total = sum(len(x["vectors"]) for x in cv.values())
    print(f"Блоков: {len(cv)}, контрольных векторов: {total}")


if __name__ == "__main__":
    main()
