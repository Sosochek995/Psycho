#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Генератор Data/Blocks/*.json строго из docs/methodics_source.md.

Правила:
- тексты вопросов и вариантов копируются из источника дословно (без сокращений);
- ключи, матрицы, веса и интерпретации берутся только из источника;
- если блок неполный — он НЕ «доделывается», а фиксируется в docs/import_errors.md.
"""
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "docs" / "methodics_source.md"
OUT_DIR = ROOT / "Data" / "Blocks"
ERRORS_MD = ROOT / "docs" / "import_errors.md"

errors = []   # критично: блок пропущен/неполон
warnings = [] # нечёткие места, трактованы по тексту источника


def norm_ws(s: str) -> str:
    return re.sub(r"\s+", " ", s).strip()


text = SOURCE.read_text(encoding="utf-8")
lines = text.splitlines()

# ---------- разбор блоков ----------
block_starts = [i for i, l in enumerate(lines) if l.startswith("## Блок ")]
blocks = {}  # num -> dict(title, meta, sections)
for idx, start in enumerate(block_starts):
    end = block_starts[idx + 1] if idx + 1 < len(block_starts) else len(lines)
    header = lines[start]
    m = re.match(r"## Блок (\d+)\. (.+)", header)
    num = int(m.group(1))
    title = m.group(2).strip()
    body = lines[start:end]
    meta = {}
    for l in body:
        mm = re.match(r"- ([a-z_]+): (.+)", l.strip())
        if mm and not l.startswith("   "):
            meta[mm.group(1)] = mm.group(2).strip()
    sections = {}
    cur = None
    for l in body:
        sm = re.match(r"### (.+)", l)
        if sm:
            cur = sm.group(1).strip()
            sections[cur] = []
        elif cur is not None:
            sections[cur].append(l)
    blocks[num] = {"title": title, "meta": meta, "sections": sections}

assert len(blocks) == 12, f"Ожидалось 12 блоков, найдено {len(blocks)}"


def parse_instruction(b):
    return norm_ws(" ".join(l for l in b["sections"].get("Инструкция", []) if l.strip()))


def parse_numbered_items(lines_list):
    """Возвращает [(num, question_text, [(key_letter, option_text)])]."""
    items = []
    cur = None
    for l in lines_list:
        qm = re.match(r"^(\d+)\.\s+(.*)$", l)
        om = re.match(r"^\s+-\s+([абвгде])\)\s*(.*)$", l)
        dm = re.match(r"^\s+-\s+(да|нет)\s*$", l)
        if qm:
            if cur:
                items.append(cur)
            cur = [int(qm.group(1)), norm_ws(qm.group(2)), []]
        elif om and cur:
            cur[2].append((om.group(1), norm_ws(om.group(2))))
        elif dm and cur:
            cur[2].append(("да" if dm.group(1) == "да" else "нет", dm.group(1)))
        else:
            rm = re.match(r"^\s+-\s+(правая|левая)\s*$", l)
            if rm and cur:
                cur[2].append((rm.group(1), rm.group(1)))
            lm = re.match(r"^\s+-\s+([ЛП])\s*\(([^)]*)\)\s*$", l)
            if lm and cur:
                key = "l" if lm.group(1) == "Л" else "r"
                cur[2].append((key, f"{lm.group(1)} ({lm.group(2)})"))
    if cur:
        items.append(cur)
    return items


created_paths = []


def write_block(num, fname, obj):
    path = OUT_DIR / f"block_{num:02d}_{fname}.json"
    payload = json.dumps(obj, ensure_ascii=False, indent=2) + "\n"
    path.write_text(payload, encoding="utf-8")
    created_paths.append(path)
    return path


def yesno_options():
    return [{"key": "yes", "text": "да"}, {"key": "no", "text": "нет"}]


# ===================== БЛОК 1 =====================
b = blocks[1]
items = parse_numbered_items(b["sections"]["Вопросы"])
if len(items) != 50:
    errors.append(f"Блок 1: ожидалось 50 вопросов, распарсировано {len(items)}.")
key_line = norm_ws(" ".join(l for l in b["sections"]["Ключ"] if l.strip()))
key_tokens = re.findall(r"(\d+)(а|б)", key_line)
if len(key_tokens) != 50:
    errors.append(f"Блок 1: в ключе {len(key_tokens)} позиций вместо 50.")
keys = {f"q{int(n):02d}": ("a" if el == "а" else "b") for n, el in key_tokens}
questions = []
for num_q, qtext, opts in items:
    questions.append({
        "id": f"q{num_q:02d}", "text": qtext, "type": "YesNo", "required": True,
        "options": [{"key": k, "text": t} for k, t in opts],
        "tags": ["sum_key"],
    })
interp = [
    {"max": 25, "level": "Слабо", "text": "Качества лидера выражены слабо."},
    {"min": 26, "max": 35, "level": "Средне", "text": "Качества лидера выражены средне."},
    {"min": 36, "max": 40, "level": "Сильно", "text": "Лидерские качества выражены сильно."},
    {"min": 41, "level": "Склонность к диктату",
     "text": "Данный человек как лидер склонен к диктату."},
]
write_block(1, "leadership", {
    "id": b["meta"]["id"], "order": 1, "title": b["title"],
    "instruction": parse_instruction(b),
    "answerType": "YesNo", "questionCountExpected": 50,
    "questions": questions,
    "scoring": {"type": "SumKey", "maxScore": 50, "keys": keys,
                "resultFormat": "Score", "interpretations": interp},
})

# ===================== Блок 2 =====================
b = blocks[2]
items = parse_numbered_items(b["sections"]["Вопросы"])
if len(items) != 20:
    errors.append(f"Блок 2: ожидалось 20 вопросов, распарсировано {len(items)}.")
key_text = norm_ws(" ".join(l for l in b["sections"]["Ключ"] if l.strip()))
parts = key_text.split("«нет»")
yes_nums = set(int(n) for n in re.findall(r"для вопросов:?\s*([\d,\s]+?)\.", parts[0])[0].replace(" ", "").split(","))
no_nums = set(int(n) for n in re.findall(r"для вопросов:?\s*([\d,\s]+?)\.", parts[1])[0].replace(" ", "").split(","))
keys = {}
for n in sorted(yes_nums | no_nums):
    keys[f"q{n:02d}"] = "yes" if n in yes_nums else "no"
if len(keys) != 20 or yes_nums & no_nums:
    errors.append(f"Блок 2: ключ покрывает {len(keys)} вопросов из 20.")
questions = [{
    "id": f"q{n:02d}", "text": t, "type": "YesNo", "required": True,
    "options": yesno_options(), "tags": ["sum_key_weighted"],
} for n, t, _ in items]
interp = [
    {"min": 85, "max": 100, "level": "Отлично", "text": "Отлично, окружающие вас любят."},
    {"min": 75, "max": 84, "level": "Хорошо", "text": "Хорошо."},
    {"min": 65, "max": 74, "level": "Удовлетворительно", "text": "Удовлетворительно."},
    {"max": 64, "level": "Плохо", "text": "Плохо (менее 65 баллов)."},
]
write_block(2, "love", {
    "id": b["meta"]["id"], "order": 2, "title": b["title"],
    "instruction": parse_instruction(b),
    "answerType": "YesNo", "questionCountExpected": 20,
    "questions": questions,
    "scoring": {"type": "SumKeyWeighted", "maxScore": 100, "pointsPerCorrectAnswer": 5,
                "keys": keys, "resultFormat": "Score", "interpretations": interp},
})

# ===================== Блок 3 =====================
b = blocks[3]
items = parse_numbered_items(b["sections"]["Вопросы"])
if len(items) != 10:
    errors.append(f"Блок 3: ожидалось 10 вопросов, распарсировано {len(items)}.")
left_ids = ["q01", "q02", "q05", "q08", "q09"]
right_ids = ["q03", "q04", "q06", "q07", "q10"]
questions = [{
    "id": f"q{n:02d}", "text": t, "type": "Scale", "required": True,
    "options": [], "scale": {"min": 0, "max": 10, "step": 1,
                             "minLabel": "0 — полное несогласие",
                             "maxLabel": "10 — категорическое согласие"},
    "tags": ["left_hemisphere"] if f"q{n:02d}" in left_ids else ["right_hemisphere"],
} for n, t, _ in items]
interp = [
    {"condition": "oddGtBy:5", "level": "Мыслитель (логический тип)",
     "text": "Логический тип (Мыслитель). Оптимист, опирается на расчет."},
    {"condition": "evenGtOdd", "level": "Художник (художественный тип)",
     "text": "Художественный тип (Художник). Склонен к пессимизму, опирается на чувства и интуицию."},
    {"condition": "absDiffLt:5", "level": "Гармоничный (средний тип)",
     "text": "Средний тип (Гармоничный). Сочетает логику и образность."},
]
warnings.append("Блок 3: пороги источника перекрываются; применён приоритет правил: Л−П>5 → П>Л → |разница|<5.")
write_block(3, "artist_thinker", {
    "id": b["meta"]["id"], "order": 3, "title": b["title"],
    "instruction": parse_instruction(b),
    "answerType": "Scale", "questionCountExpected": 10,
    "questions": questions,
    "scoring": {"type": "HemisphereSum", "resultFormat": "TwoScales",
                "metadata": {"leftScale": "Л", "rightScale": "П"},
                "questionGroups": {"left": left_ids, "right": right_ids},
                "interpretations": interp},
})

# ===================== Блок 4 =====================
b = blocks[4]
items = parse_numbered_items(b["sections"]["Вопросы"])
if len(items) != 9:
    errors.append(f"Блок 4: ожидалось 9 вопросов, распарсировано {len(items)}.")
questions = [{
    "id": f"q{n:02d}", "text": t, "type": "Scale", "required": True,
    "options": [], "scale": {"min": 0, "max": 4, "step": 1,
                             "minLabel": "0 — совсем не про меня",
                             "maxLabel": "4 — полностью про меня"},
    "tags": ["sum"],
} for n, t, _ in items]
interp = [
    {"min": 0, "max": 12, "level": "Интроверт",
     "text": "С трудом вступает в контакт, сдержан, застенчив, ориентирован на собственные чувства."},
    {"min": 13, "max": 24, "level": "Амбаверт",
     "text": "Спокойные, ровные отношения с людьми, ответственность."},
    {"min": 25, "max": 36, "level": "Экстраверт",
     "text": "Словоохотливый, общительный оптимист, прекрасный импровизатор."},
]
warnings.append("Блок 4: источник описывает диапазон 1–12; значение 0 отнесено к «Интроверт» (нижняя граница шкалы).")
write_block(4, "extraversion", {
    "id": b["meta"]["id"], "order": 4, "title": b["title"],
    "instruction": parse_instruction(b),
    "answerType": "Scale", "questionCountExpected": 9,
    "questions": questions,
    "scoring": {"type": "SumScale", "maxScore": 36, "resultFormat": "Score",
                "interpretations": interp},
})

# ===================== Блок 5 =====================
b = blocks[5]
items = parse_numbered_items(b["sections"]["Задания"])
if len(items) != 4:
    errors.append(f"Блок 5: ожидалось 4 задания, распарсировано {len(items)}.")
questions = []
for n, t, opts in items:
    o = [{"key": k, "text": txt} for k, txt in opts]
    if len(o) != 2:
        errors.append(f"Блок 5: в задании {n} вариантов {len(o)} вместо 2.")
    questions.append({"id": f"q{n:02d}", "text": t, "type": "LeftRightChoice",
                      "required": True, "options": o, "tags": ["sequence_combo"]})
interp = []
for l in b["sections"]["Интерпретация"]:
    m = re.match(r"-\s*([ЛП]{4}):\s*(.+)", l.strip())
    if m:
        interp.append({"value": m.group(1), "level": f"Комбинация {m.group(1)}",
                       "text": norm_ws(m.group(2))})
if len(interp) != 16:
    errors.append(f"Блок 5: интерпретаций {len(interp)} вместо 16 комбинаций.")
write_block(5, "biomarkers", {
    "id": b["meta"]["id"], "order": 5, "title": b["title"],
    "instruction": parse_instruction(b),
    "answerType": "LeftRightChoice", "questionCountExpected": 4,
    "questions": questions,
    "scoring": {"type": "SequenceCombo", "resultFormat": "FourLetters",
                "interpretations": interp},
})

# ===================== Блок 6 =====================
b = blocks[6]
items = parse_numbered_items(b["sections"]["Вопросы"])
if len(items) != 6:
    errors.append(f"Блок 6: ожидалось 6 вопросов, распарсировано {len(items)}.")
fig_map = {1: "palmer_figure_1", 2: "palmer_figure_2", 3: "palmer_figure_3"}
questions = []
for n, t, opts in items:
    fm = re.match(r"Фигура\s+(\d)\.", t)
    if not fm:
        errors.append(f"Блок 6: вопрос {n} без указания фигуры.")
        continue
    fig = int(fm.group(1))
    questions.append({
        "id": f"q{n:02d}", "text": t, "type": "ImageSingleChoice", "required": True,
        "imageKey": fig_map[fig], "group": f"Фигура {fig}",
        "options": [{"key": k, "text": txt} for k, txt in opts],
        "tags": ["palmer_matrix"],
    })
matrix = {}
for l in b["sections"]["Матрица баллов"]:
    m = re.match(r"Вопрос\s+(\d+):\s*(.+)", l.strip())
    if m:
        qnum = int(m.group(1))
        row = {}
        ru_letters = "абв"
        for pair in re.findall(r"([абв])\s*=\s*(\d+)", m.group(2)):
            row["abc"[ru_letters.index(pair[0])]] = int(pair[1])
        matrix[f"q{qnum:02d}"] = row
if len(matrix) != 6:
    errors.append(f"Блок 6: матрица покрывает {len(matrix)} вопросов вместо 6.")
interp = [
    {"min": 0, "max": 16, "level": "Трудный в контактах профиль",
     "text": "Трудно в контактах с окружающими, видите в них конкурентов, возможны конфликты."},
    {"min": 17, "max": 27, "level": "Умеющий общаться профиль",
     "text": "Счастливый человек в общении, умеете разрядить обстановку, но есть границы мягкости."},
    {"min": 28, "max": 42, "level": "Гибкий профиль общения",
     "text": "Удивительно гибки в общении, но гибкость могут воспринимать как беспринципность."},
]
images = {}
for l in b["sections"].get("Изображения", []):
    m = re.match(r"-\s*(\w+):\s*(.+)", l.strip())
    if m:
        images[m.group(1)] = norm_ws(m.group(2))
write_block(6, "palmer", {
    "id": b["meta"]["id"], "order": 6, "title": b["title"],
    "instruction": parse_instruction(b),
    "answerType": "ImageSingleChoice", "questionCountExpected": 6,
    "questions": questions,
    "images": images,
    "scoring": {"type": "PalmerMatrix", "maxScore": 42, "keys": matrix,
                "resultFormat": "Score", "interpretations": interp},
})

# ===================== Блок 7 =====================
b = blocks[7]
items = parse_numbered_items(b["sections"]["Вопросы"])
if len(items) != 20:
    errors.append(f"Блок 7: ожидалось 20 вопросов, распарсировано {len(items)}.")
questions = [{
    "id": f"q{n:02d}", "text": t, "type": "Scale", "required": True,
    "options": [], "scale": {"min": 0, "max": 10, "step": 1,
                             "minLabel": "0 — полное несогласие",
                             "maxLabel": "10 — полное согласие"},
    "tags": ["leader"] if n % 2 == 1 else ["administrator"],
} for n, t, _ in items]
interp = [
    {"condition": "bothLow", "level": "Низкая управленческая активность",
     "text": "Низкая активность в управленческой деятельности."},
    {"condition": "oddGtEven", "level": "Демократ (Лидер > Администратор)",
     "text": "Опора на коллегиальные формы руководства (демократ)."},
    {"condition": "evenGtOdd", "level": "Авторитар (Администратор > Лидер)",
     "text": "Опора на административно-директивные установки (авторитар)."},
]
write_block(7, "leader_admin", {
    "id": b["meta"]["id"], "order": 7, "title": b["title"],
    "instruction": parse_instruction(b),
    "answerType": "Scale", "questionCountExpected": 20,
    "questions": questions,
    "scoring": {"type": "OddEvenSum", "maxScorePerScale": 100,
                "oddScaleName": "Лидер", "evenScaleName": "Администратор",
                "resultFormat": "TwoScales", "interpretations": interp},
})

# ===================== Блоки 8, 9 (ParameterChoice) =====================
def build_parameter_block(num, fname):
    b = blocks[num]
    sec = b["sections"]["Параметры"]
    params = []
    cur = None
    for l in sec:
        pm = re.match(r"^(\d+)\.\s+(.*)$", l)
        vm = re.match(r"^\s+-\s+(.+?):\s*(.+)$", l)
        cont = re.match(r"^\s{3,}(.+)$", l)
        if pm:
            if cur:
                params.append(cur)
            cur = [norm_ws(pm.group(2)), []]
        elif vm and cur:
            cur[1].append([norm_ws(vm.group(1)), norm_ws(vm.group(2))])
        elif cont and cur and cur[1]:
            cur[1][-1][1] += " " + norm_ws(cont.group(1))
    if cur:
        params.append(cur)
    expected = int(b["meta"].get("expected_parameter_groups_min", len(params)))
    if len(params) != expected:
        errors.append(f"Блок {num}: групп параметров {len(params)}, ожидалось {expected}.")
    questions = []
    for gi, (pname, variants) in enumerate(params, start=1):
        options = [{"key": f"p{gi:02d}_v{vi}", "text": vt, "interpretation": itp}
                   for vi, (vt, itp) in enumerate(variants, start=1)]
        if len(options) < 1:
            errors.append(f"Блок {num}: у параметра «{pname}» нет вариантов.")
        q = {
            "id": f"p{gi:02d}", "text": pname, "type": "ParameterChoice",
            "required": True, "group": pname, "options": options,
            "tags": ["qualitative_profile"],
        }
        if num == 8 and pname.startswith("Петли"):
            q["imageKey"] = "handwriting_sample"
        if num == 9 and gi == 1:
            q["imageKey"] = "signature_sample"
        questions.append(q)
    images = {}
    for l in b["sections"].get("Изображения", []):
        m = re.match(r"-\s*(\w+):\s*(.+)", l.strip())
        if m:
            images[m.group(1)] = norm_ws(m.group(2))
    interp_text = norm_ws(" ".join(l for l in b["sections"].get("Интерпретация", []) if l.strip()))
    if not interp_text:
        errors.append(f"Блок {num}: отсутствует раздел «Интерпретация».")
    write_block(num, fname, {
        "id": b["meta"]["id"], "order": num, "title": b["title"],
        "instruction": parse_instruction(b),
        "answerType": "ParameterChoice",
        "questions": questions,
        "images": images,
        "scoring": {"type": "QualitativeProfile", "resultFormat": "Text",
                    "metadata": {"profileNote": interp_text},
                    "interpretations": [
                        {"value": "Профиль", "level": "Качественный профиль",
                         "text": interp_text}]},
    })

build_parameter_block(8, "graphology")
build_parameter_block(9, "signature")

# ===================== Блок 10 =====================
b = blocks[10]
items = parse_numbered_items(b["sections"]["Вопросы"])
if len(items) != 10:
    errors.append(f"Блок 10: ожидалось 10 вопросов, распарсировано {len(items)}.")
questions = []
for n, t, opts in items:
    o = [{"key": "r" if k == "правая" else "l", "text": txt} for k, txt in opts]
    questions.append({"id": f"q{n:02d}", "text": t, "type": "LeftRightChoice",
                      "required": True, "options": o, "tags": ["count_left"]})
interp = [
    {"min": 0, "max": 2, "level": "Выраженный правша", "text": "Выраженный правша."},
    {"min": 3, "max": 5, "level": "Амбидекстр или скрытый левша",
     "text": "Амбидекстр или скрытый левша."},
    {"min": 6, "max": 8, "level": "Выраженный левша", "text": "Выраженный левша."},
    {"min": 9, "max": 10, "level": "Чистый левша", "text": "Чистый левша."},
]
write_block(10, "left_hand", {
    "id": b["meta"]["id"], "order": 10, "title": b["title"],
    "instruction": parse_instruction(b),
    "answerType": "LeftRightChoice", "questionCountExpected": 10,
    "questions": questions,
    "scoring": {"type": "CountLeft", "maxScore": 10, "resultFormat": "Score",
                "interpretations": interp},
})

# ===================== Блок 11 =====================
b = blocks[11]
items = parse_numbered_items(b["sections"]["Ситуации"])
if len(items) != 10:
    errors.append(f"Блок 11: ожидалось 10 ситуаций, распарсировано {len(items)}.")
letters = "абвгде"
weights = {"a": 0, "b": 1, "c": 3, "d": 4, "e": 2}
questions = []
for n, t, opts in items:
    if len(opts) != 5:
        errors.append(f"Блок 11: в ситуации {n} вариантов {len(opts)} вместо 5.")
    o = [{"key": "abcde"[letters.index(k)], "text": txt} for k, txt in opts]
    questions.append({"id": f"q{n:02d}", "text": t, "type": "SingleChoice",
                      "required": True, "options": o, "tags": ["formula"]})
interp = [
    {"value": "Совпадает с паспортным", "level": "Норма",
     "text": "Творческий возраст совпадает с паспортным: все в порядке."},
    {"value": "Ниже паспортного", "level": "Хорошая форма",
     "text": "Творческий возраст ниже паспортного: вы в хорошей форме, свободны от стереотипов, возможности не исчерпаны."},
    {"value": "Выше паспортного", "level": "Стандартная форма",
     "text": "Творческий возраст выше паспортного: вы успешно справляетесь со стандартными действиями, требующими четкости, но для творческой деятельности желательно, чтобы психологический возраст не опережал паспортный."},
]
write_block(11, "creative_age", {
    "id": b["meta"]["id"], "order": 11, "title": b["title"],
    "instruction": parse_instruction(b),
    "answerType": "SingleChoice", "questionCountExpected": 10,
    "questions": questions,
    "scoring": {"type": "FormulaMultiplier", "formula": "sum * 2",
                "weights": weights, "resultUnit": "years",
                "resultFormat": "Score", "interpretations": interp,
                "metadata": {"comparison": "Результат сравнивается с паспортным возрастом пользователя."}},
})

# ===================== Блок 12 =====================
b = blocks[12]
items = parse_numbered_items(b["sections"]["Вопросы"])
if len(items) != 50:
    errors.append(f"Блок 12: ожидалось 50 вопросов, распарсировано {len(items)}.")
questions = [{
    "id": f"q{n:02d}", "text": t, "type": "Scale", "required": True,
    "options": [], "scale": {"min": 0, "max": 4, "step": 1,
                             "minLabel": "0 — не согласен",
                             "maxLabel": "4 — постоянное проявление качества"},
    "tags": ["T"] if n % 2 == 1 else ["P"],
} for n, t, _ in items]
interp = [
    {"condition": "equal", "level": "Гармоничная личность",
     "text": "Гармоничная личность. Потенциал полностью обеспечивается мотивацией и реализуется в поведении."},
    {"condition": "bothLow", "level": "Спад активности",
     "text": "Снижение активности, усталость, отсутствие интереса к саморазвитию."},
    {"condition": "oddGtEven", "level": "Непризнанный гений (Т > П)",
     "text": "Есть творческие проявления, но может не хватать мотивации, амбиций или веры в себя."},
    {"condition": "evenGtOdd", "level": "Трудоголик/Карьерист (П > Т)",
     "text": "Высокая мотивация, амбиции, жажда успеха, но может не хватать гибкости, оригинальности и креативности."},
]
write_block(12, "creative_potential", {
    "id": b["meta"]["id"], "order": 12, "title": b["title"],
    "instruction": parse_instruction(b),
    "answerType": "Scale", "questionCountExpected": 50,
    "questions": questions,
    "scoring": {"type": "OddEvenSum", "maxScorePerScale": 100,
                "oddScaleName": "Т", "evenScaleName": "П",
                "resultFormat": "TwoScales", "interpretations": interp},
})

# ---------- отчёт ----------
report = {"created": [str(p.relative_to(ROOT)) for p in created_paths],
          "errors": errors, "warnings": warnings}
print(json.dumps(report, ensure_ascii=False, indent=2))

if errors:
    ERRORS_MD.write_text(
        "# Ошибки импорта методик\n\n" +
        "".join(f"- {e}\n" for e in errors) +
        "\nНеполные блоки не «доделывались» вручную согласно требованиям.\n",
        encoding="utf-8")
    sys.exit(1)
elif ERRORS_MD.exists():
    ERRORS_MD.unlink()
sys.exit(0)
