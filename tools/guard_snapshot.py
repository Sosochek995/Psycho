#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Снимок смыслового слепка методик из Data/Blocks/*.json.
Формат полностью совместим с tools/baseline/methodology_baseline.json.
Косметические поля (displayLabel, letter и т.п.) в слепок НЕ входят."""
import json, sys, os

COSMETIC = {"displayLabel", "letter"}  # не защищены

def strip_cosmetic(obj):
    if isinstance(obj, dict):
        return {k: strip_cosmetic(v) for k, v in obj.items() if k not in COSMETIC}
    if isinstance(obj, list):
        return [strip_cosmetic(x) for x in obj]
    return obj

def snapshot(blocks_dir="Data/Blocks"):
    files = sorted(f for f in os.listdir(blocks_dir) if f.startswith("block_") and f.endswith(".json"))
    blocks = []
    for f in files:
        d = json.load(open(os.path.join(blocks_dir, f), encoding="utf-8"))
        scoring = d.get("scoring", {}) or {}
        interps = scoring.get("interpretations", d.get("interpretations", [])) or []
        questions = []
        for q in d.get("questions", []):
            questions.append({
                "id": q.get("id"),
                "text": q.get("text"),
                "type": q.get("type"),
                "required": q.get("required", True),
                "group": q.get("group"),
                "imageKey": q.get("imageKey"),
                "scale": strip_cosmetic(q.get("scale")),
                "options": [
                    {"key": o.get("key"), "text": o.get("text"),
                     "interpretation": o.get("interpretation")}
                    for o in (q.get("options") or [])
                ],
            })
        prot_scoring = {k: strip_cosmetic(v) for k, v in scoring.items() if k != "interpretations"}
        blocks.append({
            "id": d.get("id"),
            "order": d.get("order"),
            "title": d.get("title"),
            "subtitle": d.get("subtitle"),
            "instruction": d.get("instruction"),
            "answerType": d.get("answerType"),
            "questionCount": len(d.get("questions", [])),
            "questions": questions,
            "scoring": prot_scoring,
            "interpretations": strip_cosmetic(interps),
        })
    return {"blocks": blocks}

if __name__ == "__main__":
    out = sys.argv[1] if len(sys.argv) > 1 else "-"
    data = snapshot(sys.argv[2] if len(sys.argv) > 2 else "Data/Blocks")
    txt = json.dumps(data, ensure_ascii=False, indent=2, sort_keys=True)
    if out == "-":
        print(txt)
    else:
        open(out, "w", encoding="utf-8").write(txt)
