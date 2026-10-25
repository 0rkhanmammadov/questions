import math
import os
import re
from collections import Counter
from difflib import SequenceMatcher
from pathlib import Path
from typing import Any, Dict, List, Optional, Tuple

DEFAULT_DB_PATH = Path(__file__).resolve().parent / "baza.txt"

STOP_WORDS = {
    "və", "ve", "ilə", "ile", "bu", "o", "bir", "üçün", "ucun",
    "hansı", "hansi", "nə", "ne", "necə", "nece", "hansıdır", "hansidir",
    "ki", "da", "də", "de", "üçün", "haqqında", "haqqinda", "vəya", "veya",
    "is", "the", "a", "an", "of", "in", "to", "for", "which", "what",
}


def _normalize(text: str) -> str:
    cleaned = re.sub(r"[^\w\s]", " ", (text or "").lower())
    return " ".join(cleaned.split())


def _extract_qnum(text: str) -> str:
    m = re.search(r"^\s*(?:Sual\s*|№\s*|#\s*)?(\d{1,3})\s*[\.\)\-:]", text or "", re.IGNORECASE)
    return m.group(1) if m else ""


def _tokenize(text: str, remove_stop: bool = False) -> List[str]:
    raw_tokens = re.findall(r"\w+", (text or "").lower())
    if remove_stop:
        return [t for t in raw_tokens if len(t) >= 2 and t not in STOP_WORDS]
    return [t for t in raw_tokens if len(t) >= 1]


class BM25Index:
    def __init__(self, k1: float = 1.5, b: float = 0.75):
        self.k1 = k1
        self.b = b
        self.docs: List[Dict[str, Any]] = []
        self.N: int = 0
        self.avgdl: float = 0.0
        self.idf: Dict[str, float] = {}

    def build(self, documents: List[Dict[str, Any]]) -> None:
        self.docs = documents
        self.N = len(documents)
        if self.N == 0:
            self.avgdl = 0.0
            self.idf = {}
            return

        total_len = 0
        df: Counter = Counter()

        for d in self.docs:
            tokens = d["tokens"]
            total_len += len(tokens)
            d["tf"] = Counter(tokens)
            d["len"] = len(tokens)
            d["token_set"] = set(tokens)
            for t in d["token_set"]:
                df[t] += 1

        self.avgdl = total_len / float(self.N) if self.N > 0 else 1.0

        self.idf = {}
        for term, freq in df.items():
            # Standard Lucene/BM25 IDF formula
            self.idf[term] = math.log(1.0 + (self.N - freq + 0.5) / (freq + 0.5))

    def score(self, query_tokens: List[str], d: Dict[str, Any]) -> float:
        score = 0.0
        d_len = d.get("len", 0)
        tf_map = d.get("tf", {})
        len_norm = 1.0 - self.b + self.b * (d_len / (self.avgdl if self.avgdl > 0 else 1.0))

        for q in query_tokens:
            if q not in self.idf:
                continue
            tf = tf_map.get(q, 0)
            if tf > 0:
                idf = self.idf[q]
                numerator = tf * (self.k1 + 1.0)
                denominator = tf + self.k1 * len_norm
                score += idf * (numerator / denominator)
        return score


_CACHE_KEY: Optional[Tuple[str, float]] = None
_CACHED_INDEX: Optional[BM25Index] = None


def _parse_baza_file(content: str) -> List[Dict[str, Any]]:
    # Ignore comment lines starting with #
    active_lines = [line for line in content.splitlines() if not line.strip().startswith("#")]
    clean_content = "\n".join(active_lines).strip()
    if not clean_content:
        return []

    raw_blocks = [b.strip() for b in re.split(r"\n\s*\n|---+", clean_content) if b.strip()]
    documents = []

    for block in raw_blocks:
        lines = [l.strip() for l in block.splitlines() if l.strip()]
        if not lines:
            continue

        ans_line = ""
        q_part = block

        # Check for Answer/Cavab line
        for i, line in enumerate(lines):
            m = re.match(r"^(?:Cavab|Answer|Doğru|Dogru)\s*[:\-]\s*(.+)$", line, flags=re.IGNORECASE)
            if m:
                ans_line = m.group(1).strip()
                q_part = "\n".join(lines[:i])
                break

        # Check for * or + prefix
        if not ans_line and len(lines) >= 2:
            for i, line in enumerate(lines):
                if line.startswith("*") or line.startswith("+"):
                    ans_line = line.lstrip("*+ ").strip()
                    q_part = "\n".join(lines[:i])
                    break

        # Check for pipe format: Sual | Cavab
        if not ans_line and len(lines) == 1 and "|" in lines[0]:
            parts = lines[0].split("|", 1)
            q_part = parts[0].strip()
            ans_line = parts[1].strip()

        if not ans_line:
            continue

        q_norm = _normalize(q_part)
        tokens = _tokenize(q_part)
        q_num = _extract_qnum(q_part)

        documents.append({
            "raw": block,
            "q_text": q_part,
            "q_norm": q_norm,
            "q_num": q_num,
            "answer": ans_line,
            "tokens": tokens,
        })

    return documents


def _get_or_build_index(db_path: Path) -> Optional[BM25Index]:
    global _CACHE_KEY, _CACHED_INDEX
    if not db_path.exists():
        return None

    try:
        mtime = os.path.getmtime(db_path)
    except Exception:
        mtime = 0.0

    cache_id = (str(db_path.resolve()), mtime)
    if _CACHE_KEY == cache_id and _CACHED_INDEX is not None:
        return _CACHED_INDEX

    try:
        content = db_path.read_text(encoding="utf-8").strip()
    except Exception:
        return None

    docs = _parse_baza_file(content)
    index = BM25Index()
    index.build(docs)

    _CACHE_KEY = cache_id
    _CACHED_INDEX = index
    return index


def search_local_db(question_text: str, db_path: Path | str | None = None) -> Optional[str]:
    target = Path(db_path) if db_path else DEFAULT_DB_PATH
    index = _get_or_build_index(target)
    if not index or index.N == 0:
        return None

    cleaned_q = (question_text or "").strip()
    q_norm = _normalize(cleaned_q)
    if len(q_norm) < 3:
        return None

    q_tokens = _tokenize(cleaned_q)
    q_tokens_no_stop = _tokenize(cleaned_q, remove_stop=True)
    tokens_to_search = q_tokens_no_stop if len(q_tokens_no_stop) >= 2 else q_tokens
    if not tokens_to_search:
        tokens_to_search = q_tokens

    q_num = _extract_qnum(cleaned_q)
    query_set = set(tokens_to_search)

    best_doc: Optional[Dict[str, Any]] = None
    best_total_score: float = 0.0

    for doc in index.docs:
        b_norm = doc["q_norm"]
        if not b_norm:
            continue

        # 1. Exact / Substring containment (fast path)
        if b_norm in q_norm or q_norm in b_norm:
            return doc["answer"]

        # 2. SequenceMatcher ratio (handles direct fuzzy matching & typos)
        seq_ratio = SequenceMatcher(None, q_norm, b_norm).ratio()
        if seq_ratio >= 0.82:
            return doc["answer"]

        # 3. BM25 keyword score
        bm25_raw = index.score(tokens_to_search, doc)

        # 4. Token overlap (Jaccard on document question tokens)
        doc_tokens = doc.get("token_set", set())
        overlap_count = len(query_set.intersection(doc_tokens))
        overlap_ratio = overlap_count / float(len(doc_tokens)) if doc_tokens else 0.0

        # Max theoretical BM25 score for this query on a perfect match
        max_possible_bm25 = sum(index.idf.get(t, 0.0) for t in query_set)
        norm_bm25 = (bm25_raw / max_possible_bm25) if max_possible_bm25 > 0 else 0.0

        # Number bonus
        num_bonus = 0.15 if (q_num and doc["q_num"] and q_num == doc["q_num"]) else 0.0

        # Hybrid composite score
        total_score = (0.50 * norm_bm25) + (0.35 * overlap_ratio) + (0.15 * seq_ratio) + num_bonus

        # If question has high overlap ratio (e.g. 70% of question words match)
        if overlap_ratio >= 0.70 and overlap_count >= 3:
            total_score = max(total_score, 0.80)

        if total_score > best_total_score:
            best_total_score = total_score
            best_doc = doc

    if best_doc and best_total_score >= 0.52:
        return best_doc["answer"]

    return None
