#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Снимок смыслового слепка методик.

ВАЖНО: использует ТЕ ЖЕ функции, что и generate_baseline.py.
Только так сравнение "текущее vs эталон" вообще возможно:
одинаковые косметические поля, одинаковая структура, одинаковая сериализация.
Собственной копии логики слепка здесь быть не должно.
"""
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT / "tools"))

# Единственный источник истины о том, что входит в слепок.
from generate_baseline import block_digest, COSMETIC_FIELDS  # noqa: E402


def snapshot(blocks_dir: Path) -> dict:
    """Собирает слепок из Data/Blocks/*.json в формате эталона."""
    blocks = []
    for f in sorted(blocks_dir.glob("block_*.json")):
        raw = json.loads(f.read_text(encoding="utf-8-sig"))
        blocks.append(block_digest(raw))
    blocks.sort(key=lambda b: b["order"])
    return {
        "generatedBy": "tools/guard_snapshot.py",
        "note": ("Смысловой слепок защищённых данных. Косметические поля "
                 f"{sorted(COSMETIC_FIELDS)} исключены и не защищены."),
        "blockCount": len(blocks),
        "blocks": blocks,
    }


if __name__ == "__main__":
    out = sys.argv[1] if len(sys.argv) > 1 else "-"
    src = Path(sys.argv[2]) if len(sys.argv) > 2 else ROOT / "Data" / "Blocks"
    data = snapshot(src)
    text = json.dumps(data, ensure_ascii=False, indent=2) + "\n"
    if out == "-":
        print(text)
    else:
        Path(out).write_text(text, encoding="utf-8")
