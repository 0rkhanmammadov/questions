import ctypes
import io
from typing import Optional, Tuple

import mss
import pyperclip
from PIL import Image, ImageGrab

MIN_DRAG_WIDTH = 15
MIN_DRAG_HEIGHT = 10


def enable_dpi_awareness() -> None:
    try:
        ctypes.windll.shcore.SetProcessDpiAwareness(2)
    except Exception:
        try:
            ctypes.windll.user32.SetProcessDPIAware()
        except Exception:
            pass


def normalize_box(x1: int, y1: int, x2: int, y2: int) -> Optional[Tuple[int, int, int, int]]:
    left = min(int(x1), int(x2))
    top = min(int(y1), int(y2))
    width = abs(int(x2) - int(x1))
    height = abs(int(y2) - int(y1))
    if width < MIN_DRAG_WIDTH or height < MIN_DRAG_HEIGHT:
        return None
    return left, top, width, height


def image_to_png_bytes(img: Image.Image) -> bytes:
    buf = io.BytesIO()
    img.save(buf, format="PNG", optimize=True)
    return buf.getvalue()


def grab_raw_image(left: int, top: int, width: int, height: int) -> Optional[Image.Image]:
    monitor = {"left": int(left), "top": int(top), "width": int(width), "height": int(height)}
    try:
        sct_cls = getattr(mss, "MSS", mss.mss)
        with sct_cls() as sct:
            sct_img = sct.grab(monitor)
            return Image.frombytes("RGB", sct_img.size, sct_img.bgra, "raw", "BGRX")
    except Exception:
        try:
            return ImageGrab.grab(
                bbox=(int(left), int(top), int(left + width), int(top + height)),
                all_screens=True,
            )
        except Exception:
            return None


def capture_region_png(x1: int, y1: int, x2: int, y2: int) -> Optional[bytes]:
    box = normalize_box(x1, y1, x2, y2)
    if box is None:
        return None
    left, top, width, height = box
    img = grab_raw_image(left, top, width, height)
    if img is None:
        return None
    return image_to_png_bytes(img)


def capture_fullscreen_png() -> Optional[bytes]:
    try:
        user32 = ctypes.windll.user32
        sw = user32.GetSystemMetrics(0)
        sh = user32.GetSystemMetrics(1)
    except Exception:
        sw, sh = 1920, 1080
    # Exclude top browser tab bar (85px) and bottom Windows taskbar (50px)
    top = 85 if sh > 400 else 0
    height = max(200, sh - top - 50)
    img = grab_raw_image(0, top, sw, height)
    if img is None:
        return None
    return image_to_png_bytes(img)


def get_clipboard_text() -> str:
    try:
        text = pyperclip.paste()
        return (text or "").strip()
    except Exception:
        return ""
