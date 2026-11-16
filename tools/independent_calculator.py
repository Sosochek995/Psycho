#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Независимый минимальный калькулятор методик ZharikovApp.

ЧИТАЕТ ключи, веса, формулы, пороги и матрицы НАПРЯМУЮ из:
  - docs/methodics_source.md   (эталонный текст источника)
  - Data/Blocks/*.json         (структурированные данные)

НЕ использует код приложения (C#). Предназначен для генерации ожидаемых
значений контрольных векторов tools/baseline/control_vectors.json.

Нормализация ответов повторяет задокументированное поведение ScoringService.cs
(только как отображение "значение ответа -> балл"; логика приложения не меняется):
  yes/да/a -> a ; no/нет/b -> b ; кириллические литеры а..е -> a..e ;
  левая/л/left -> l ; правая/п/right -> r.
"""
import json
import re
import glob
import hashlib
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
SOURCE_MD = ROOT / "docs" / "methodics_source.md"
BLOCKS_GLOB = str(ROOT / "Data" / "Blocks" / "*.json")

# ---------------------------------------------------------------- утилиты ---

def norm_key(v: str):
    s = (v or "").strip().lower()
    if s in ("yes", "y", "да", "д"):
        return "a"
    if s in ("no", "n", "нет", "н"):
        return "b"
    ru = {"а": "a", "б": "b", "в": "c", "г": "d", "д": "e", "е": "f"}
    if s in ru:
        return ru[s]
    if s in ("левая", "лево", "левый", "л", "left"):
        return "l"
    if s in ("правая", "право", "правый", "п", "right"):
        return "r"
    return s


def parse_scale(v):
    try:
        return int(str(v).strip())
    except (ValueError, AttributeError):
        return 0


RU_LETTERS = "абвгдежзиклмнопрстух"  # порядок литер для букв вариантов

def letter_for_index(i: str) -> str:
    """Индекс варианта '1'/'2'... или буква 'а' -> нормализованный ключ a/b/c."""
    if i.isalpha():
        idx = RU_LETTERS.find(i.lower())
        if idx < 0:
            raise ValueError(f"неизвестная литера варианта: {i}")
        return "abcdef"[idx]
    return "abcdef"[int(i) - 1]


# ------------------------------------------------------- разбор источника ---

def parse_source_keys(md: str):
    """Ключ блоков по docs/methodics_source.md (для сверки с JSON)."""
    out = {}

    # Блок 1: "... 1а, 2а, 3б, ... 50б." в разделе Ключ
    m = re.search(r"### Ключ\n(1[аб],.*?)\n\n", md, re.S)
    if m:
        pairs = re.findall(r"(\d+)([аб])", m.group(1))
        out["block_01_leadership"] = {f"q{int(n):02d}": letter_for_index(l) for n, l in pairs}

    # Блок 2: да -> вопросы 6,13,14,20; нет -> остальные
    m_yes = re.search(r"Ответ «да» оценивается в 5 баллов для вопросов: ([\d, ]+)\.", md)
    m_no = re.search(r"Ответ «нет» оценивается в 5 баллов для вопросов: ([\d, ]+)\.", md)
    if m_yes and m_no:
        keys = {}
        for n in re.findall(r"\d+", m_yes.group(1)):
            keys[f"q{int(n):02d}"] = "a"
        for n in re.findall(r"\d+", m_no.group(1)):
            keys[f"q{int(n):02d}"] = "b"
        out["block_02_love"] = keys

    # Блок 6: матрица Пальмера "Вопрос N: а = x, б = y, в = z"
    matrix = {}
    for qn, body in re.findall(r"Вопрос (\d): ([^\n]+)", md):
        row = {}
        for letter, pts in re.findall(r"([абв]) = (\d+)", body):
            row[letter_for_index(letter)] = int(pts)
        if row:
            matrix[f"q{int(qn):02d}"] = row
    if len(matrix) == 6:
        out["block_06_palmer_matrix"] = matrix

    # Блок 11: веса из метаданных источника "a: 0  b: 1 ..." и формула "sum * 2"
    m_w = re.search(r"weights:\s*\n\s*a: (\d+)\s*\n\s*b: (\d+)\s*\n\s*c: (\d+)\s*\n\s*d: (\d+)\s*\n\s*e: (\d+)", md)
    if m_w:
        out["block_11_weights"] = dict(zip("abcde", map(int, m_w.groups())))
    m_f = re.search(r"formula: sum \* (\d+)", md)
    if m_f:
        out["block_11_multiplier"] = int(m_f.group(1))

    # Блок 5: список комбинаций интерпретации "ПППП:" и т.д.
    combos = re.findall(r"^- (Л{1}[ПЛ]{3}|П[ПЛ]{3}):", md, re.M)
    if combos:
        out["block_05_combos_count"] = len(set(combos))

    # Блок 10: подсчёт «левая»; блок 4/3/7/12 — шкальные, ключей нет.
    return out


def extract_scales_groups(md: str):
    """Группы Л/П блока 3 и имена шкал блоков 7/12 из текста источника."""
    m = re.search(r"Подсчитайте отдельно сумму баллов по строкам ([\d, ]+) \(Левое полушарие - Л\) и ([\d, ]+) \(Правое", md)
    groups = None
    if m:
        left = [f"q{int(n):02d}" for n in re.findall(r"\d+", m.group(1))]
        right = [f"q{int(n):02d}" for n in re.findall(r"\d+", m.group(2))]
        groups = {"left": left, "right": right}
    return groups


# ------------------------------------------------------------ калькуляторы --

def calc_sum_key(block, answers, weight=None):
    keys = block["scoring"]["keys"]
    w = weight if weight is not None else block["scoring"].get("pointsPerCorrectAnswer", 1)
    score = 0
    for q in block["questions"]:
        if norm_key(keys[q["id"]]) == norm_key(answers.get(q["id"], "")):
            score += w
    return score


def calc_sum_scale(block, answers):
    return sum(parse_scale(answers.get(q["id"], 0)) for q in block["questions"])


def calc_odd_even(block, answers):
    odd_name = block["scoring"]["oddScaleName"]
    even_name = block["scoring"]["evenScaleName"]
    odd = even = 0
    for q in block["questions"]:
        n = int(re.sub(r"\D", "", q["id"]))
        v = parse_scale(answers.get(q["id"], 0))
        if n % 2 == 1:
            odd += v
        else:
            even += v
    return {odd_name: odd, even_name: even}


def calc_hemisphere(block, answers):
    g = block["scoring"]["questionGroups"]
    left = sum(parse_scale(answers.get(q, 0)) for q in g["left"])
    right = sum(parse_scale(answers.get(q, 0)) for q in g["right"])
    return {"Л": left, "П": right}


def calc_formula(block, answers):
    weights = block["scoring"]["weights"]
    mult = int(re.search(r"sum \* (\d+)", block["scoring"]["formula"]).group(1))
    total = sum(weights.get(norm_key(answers.get(q["id"], "")), 0) for q in block["questions"])
    return total * mult


def calc_count_left(block, answers):
    return sum(1 for q in block["questions"] if norm_key(answers.get(q["id"], "")) == "l")


def calc_sequence(block, answers):
    letters = []
    for q in block["questions"]:
        k = norm_key(answers.get(q["id"], ""))
        letters.append({"l": "Л", "r": "П"}[k])
    return "".join(letters)


def calc_palmer(block, answers):
    matrix = block["scoring"]["keys"]
    score = 0
    for q in block["questions"]:
        row = matrix[q["id"]]
        score += row.get(norm_key(answers.get(q["id"], "")), 0)
    return score


def calc_profile(block, answers):
    parts = []
    for q in block["questions"]:
        opt = next((o for o in q["options"] if norm_key(o["key"]) == norm_key(answers.get(q["id"], ""))), None)
        label = q.get("group") or q["text"]
        interp = (opt.get("interpretation") or opt.get("text")) if opt else answers.get(q["id"], "")
        parts.append(f"{label} — {interp}")
    return "\n".join(parts)


def interpret_by_range(rules, score):
    """Правило с числовым диапазоном, первое совпадение (как MatchesScore)."""
    for r in rules:
        if r.get("min") is not None or r.get("max") is not None:
            lo = r.get("min")
            hi = r.get("max")
            if (lo is None or score >= lo) and (hi is None or score <= hi):
                return r["level"]
    return None


def interpret_by_value(rules, value):
    for r in rules:
        if r.get("value") and r["value"].lower() == (value or "").lower():
            return r["level"]
    return None


def interpret_conditions(block, scores: dict):
    """Повтор задокументированного порядка правил из источника (для ожидаемых значений)."""
    rules = block["scoring"]["interpretations"]
    vals = list(scores.values())
    first, second = vals[0], vals[1]
    low = (block["scoring"].get("maxScorePerScale") or block["scoring"].get("maxScore") or 100) * 0.3
    order = [r.get("condition") for r in rules if r.get("condition")]
    def cond_ok(c):
        name, _, arg = c.partition(":")
        th = float(arg) if arg else 0
        return {
            "oddGtEven": lambda: first > second,
            "evenGtOdd": lambda: second > first,
            "bothLow": lambda: first <= low and second <= low,
            "equal": lambda: abs(first - second) < 1e-12,
            "absDiffLt": lambda: abs(first - second) < th,
            "oddGtBy": lambda: first - second > th,
            "evenGtBy": lambda: second - first > th,
        }.get(name, lambda: False)()
    for c in order:
        if cond_ok(c):
            return next(r["level"] for r in rules if r.get("condition") == c)
    return None


# ------------------------------------------------------------- главный API --

class Calculator:
    def __init__(self):
        self.blocks = {}
        for f in sorted(glob.glob(BLOCKS_GLOB)):
            b = json.load(open(f, encoding="utf-8"))
            self.blocks[b["id"]] = b
        self.md = SOURCE_MD.read_text(encoding="utf-8")
        self.source = parse_source_keys(self.md)
        self.groups_src = extract_scales_groups(self.md)

    def expected(self, block_id: str, answers: dict):
        b = self.blocks[block_id]
        t = b["scoring"]["type"]
        rules = b["scoring"]["interpretations"]
        if t == "SumKey":
            score = calc_sum_key(b, answers, 1)
            return {"primary": score, "scores": {"score": score}, "level": interpret_by_range(rules, score)}
        if t == "SumKeyWeighted":
            score = calc_sum_key(b, answers, b["scoring"].get("pointsPerCorrectAnswer", 5))
            return {"primary": score, "scores": {"score": score}, "level": interpret_by_range(rules, score)}
        if t == "SumScale":
            score = calc_sum_scale(b, answers)
            return {"primary": score, "scores": {"score": score}, "level": interpret_by_range(rules, score)}
        if t == "OddEvenSum":
            sc = calc_odd_even(b, answers)
            return {"primary": max(sc.values()), "scores": sc, "level": interpret_conditions(b, sc)}
        if t == "HemisphereSum":
            sc = calc_hemisphere(b, answers)
            return {"primary": sc["Л"] - sc["П"], "scores": sc, "level": interpret_conditions(b, sc)}
        if t == "FormulaMultiplier":
            score = calc_formula(b, answers)
            return {"primary": score, "scores": {"score": score}, "level": None}
        if t == "CountLeft":
            score = calc_count_left(b, answers)
            return {"primary": score, "scores": {"score": score}, "level": interpret_by_range(rules, score)}
        if t == "SequenceCombo":
            combo = calc_sequence(b, answers)
            return {"primary": None, "scores": {}, "level": combo,
                    "matchLevel": interpret_by_value(rules, combo)}
        if t == "PalmerMatrix":
            score = calc_palmer(b, answers)
            return {"primary": score, "scores": {"score": score}, "level": interpret_by_range(rules, score)}
        if t == "QualitativeProfile":
            prof = calc_profile(b, answers)
            return {"primary": None, "scores": {}, "level": "Профиль", "profileHash": hashlib.sha256(prof.encode()).hexdigest()}
        raise ValueError(f"Неизвестный тип подсчёта {t}")

    def cross_check_with_source(self):
        """Сверка ключей/весов/групп JSON против docs/methodics_source.md."""
        problems = []
        for bid in ("block_01_leadership", "block_02_love"):
            src = self.source.get(bid)
            js = {q: norm_key(v) if not isinstance(v, dict) else v
                  for q, v in self.blocks[bid]["scoring"]["keys"].items()}
            js = {q: norm_key(str(v)) for q, v in js.items()}
            if src != js:
                problems.append(f"{bid}: ключи JSON расходятся с источником")
        src_m = self.source.get("block_06_palmer_matrix")
        js_m = {q: {norm_key(k): int(v) for k, v in row.items()}
                for q, row in self.blocks["block_06_palmer"]["scoring"]["keys"].items()}
        if src_m != js_m:
            problems.append("block_06_palmer: матрица JSON расходится с источником")
        src_w = self.source.get("block_11_weights")
        if src_w and {norm_key(k): v for k, v in self.blocks["block_11_creative_age"]["scoring"]["weights"].items()} != src_w:
            problems.append("block_11_creative_age: веса JSON расходятся с источником")
        if self.groups_src and self.groups_src != self.blocks["block_03_artist_thinker"]["scoring"]["questionGroups"]:
            problems.append("block_03_artist_thinker: группы Л/П расходятся с источником")
        return problems


if __name__ == "__main__":
    c = Calculator()
    print(json.dumps({"blocks": sorted(c.blocks), "sourceCheck": c.cross_check_with_source()},
                     ensure_ascii=False, indent=2))
