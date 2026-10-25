import pytest
from pathlib import Path
from local_db import search_local_db

SAMPLE_DB = """
# Nümunə Sual Bazası
1. Kompüterin əsas hesablama və idarəetmə mərkəzi hansı mikrosxemdir?
Cavab: A) Prosessor (CPU)
---
2. İnformasiyanın ən kiçik ölçü vahidi nə adlanır?
* Bit
---
3. Azərbaycanın ən böyük gölü hansıdır?
Cavab: Sarısu
---
4. Hansı qurğu daxiletmə qurğusu sayılır?
A) Monitor
B) Printer
Cavab: Klaviatur
---
5. Python proqramlaşdırma dilində siyahı necə yaradılır? | [ ] mötərizələri ilə
"""

@pytest.fixture
def db_file(tmp_path: Path) -> Path:
    f = tmp_path / "baza.txt"
    f.write_text(SAMPLE_DB.strip(), encoding="utf-8")
    return f


def test_bm25_exact_match(db_file: Path):
    ans = search_local_db("İnformasiyanın ən kiçik ölçü vahidi nə adlanır?", db_file)
    assert ans == "Bit"


def test_bm25_with_ocr_noise_and_options(db_file: Path):
    # Screen capture includes options A, B, C, D and question number
    captured = (
        "2. Informasiyanin en kicik olcu vahidi ne adlanir?\n"
        "A) Bayt  B) Bit  C) Kilobayt  D) Meqabayt"
    )
    ans = search_local_db(captured, db_file)
    assert ans == "Bit"


def test_bm25_word_order_variation(db_file: Path):
    # Words rearranged, stop words omitted
    query = "idarəetmə hesablama mərkəzi hansı kompüter mikrosxemdir"
    ans = search_local_db(query, db_file)
    assert ans == "A) Prosessor (CPU)"


def test_bm25_pipe_format(db_file: Path):
    query = "Python dilində siyahı yaratmaq"
    ans = search_local_db(query, db_file)
    assert ans == "[ ] mötərizələri ilə"


def test_bm25_no_false_positive(db_file: Path):
    # Completely unrelated question should return None
    ans = search_local_db("Fransanın paytaxtı haradır?", db_file)
    assert ans is None
