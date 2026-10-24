import re
from difflib import SequenceMatcher
from pathlib import Path
from typing import Optional

DEFAULT_DB_PATH = Path(__file__).resolve().parent / "baza.txt"


def _normalize(text: str) -> str:
    cleaned = re.sub(r"[^\w\s]", " ", (text or "").lower())
    return " ".join(cleaned.split())


def search_local_db(question_text: str, db_path: Path | str | None = None) -> Optional[str]:
    target = Path(db_path) if db_path else DEFAULT_DB_PATH
    if not target.exists():
        return None
    try:
        raw = target.read_text(encoding="utf-8").strip()
    except Exception:
        return None
    if not raw:
        return None

    # Ignore comment lines starting with #
    active_lines = [line for line in raw.splitlines() if not line.strip().startswith("#")]
    content = "\n".join(active_lines).strip()
    if not content:
        return None

    blocks = [b.strip() for b in re.split(r"\n\s*\n|---+", content) if b.strip()]
    if not blocks:
        return None

    q_norm = _normalize(question_text)
    if len(q_norm) < 4:
        return None

    best_score = 0.0
    best_answer = None

    for block in blocks:
        lines = [l.strip() for l in block.splitlines() if l.strip()]
        if not lines:
            continue
        # Check if last line is marked as "Cavab: ..." or "ANSWER: ..."
        ans_line = ""
        q_part = block
        for i, line in enumerate(lines):
            m = re.match(r"^(?:Cavab|Answer|Doğru|Dogru)\s*[:\-]\s*(.+)$", line, flags=re.IGNORECASE)
            if m:
                ans_line = m.group(1).strip()
                q_part = "\n".join(lines[:i])
                break
        if not ans_line and len(lines) >= 2:
            # Also support "Question | Answer" on single line or last line starting with *
            for line in lines:
                if line.startswith("*") or line.startswith("+"):
                    ans_line = line.lstrip("*+ ").strip()
                    break
        if not ans_line:
            continue

        b_norm = _normalize(q_part)
        if not b_norm:
            continue

        score = SequenceMatcher(None, q_norm, b_norm).ratio()
        # Also check substring containment for when OCR captures question + options
        if b_norm in q_norm or q_norm in b_norm:
            score = max(score, 0.85)

        if score > best_score:
            best_score = score
            best_answer = ans_line

    if best_score >= 0.65 and best_answer:
        return best_answer
    return None
