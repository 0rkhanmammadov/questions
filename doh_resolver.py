"""
DNS-over-HTTPS (DoH) Resolver for stealth & firewall bypass.
Bypasses local network DNS filtering (Port 53) by querying
encrypted DoH endpoints (Port 443) directly via raw IPs.
"""

import json
import logging
import socket
import ssl
import threading
import time
import urllib.request
from typing import Dict, List, Optional, Tuple

logger = logging.getLogger(__name__)

# Direct Anycast IP endpoints (No DNS lookup needed to connect to these!)
DOH_SERVERS: List[Tuple[str, Dict[str, str]]] = [
    ("https://1.1.1.1/dns-query", {"accept": "application/dns-json"}),
    ("https://1.0.0.1/dns-query", {"accept": "application/dns-json"}),
    ("https://8.8.8.8/resolve", {}),
    ("https://8.8.4.4/resolve", {}),
]

_ORIG_GETADDRINFO = socket.getaddrinfo
_DNS_CACHE: Dict[str, Tuple[str, float]] = {}
_CACHE_LOCK = threading.Lock()
_DOH_ENABLED = False


def _is_ip_address(host: str) -> bool:
    if not host:
        return True
    if host.lower() in ("localhost", "127.0.0.1", "::1"):
        return True
    parts = host.split(".")
    if len(parts) == 4 and all(p.isdigit() and 0 <= int(p) <= 255 for p in parts):
        return True
    return False


def resolve_doh(hostname: str, timeout: float = 3.5) -> Optional[str]:
    """Resolve a domain to an IPv4 address using DoH over encrypted HTTPS."""
    if _is_ip_address(hostname):
        return hostname

    now = time.time()
    with _CACHE_LOCK:
        if hostname in _DNS_CACHE:
            cached_ip, exp = _DNS_CACHE[hostname]
            if now < exp:
                return cached_ip

    ctx = ssl.create_default_context()
    ctx.check_hostname = False
    ctx.verify_mode = ssl.CERT_NONE

    for base_url, base_headers in DOH_SERVERS:
        url = f"{base_url}?name={hostname}&type=A"
        try:
            req = urllib.request.Request(
                url,
                headers={"User-Agent": "Mozilla/5.0", **base_headers}
            )
            with urllib.request.urlopen(req, context=ctx, timeout=timeout) as res:
                if res.status != 200:
                    continue
                data = json.loads(res.read().decode("utf-8"))
                answers = data.get("Answer") or []
                for ans in answers:
                    if ans.get("type") == 1:  # Type 1 = A record (IPv4)
                        ip = ans.get("data", "").strip()
                        if ip and _is_ip_address(ip):
                            ttl = min(max(ans.get("TTL", 300), 60), 1800)
                            with _CACHE_LOCK:
                                _DNS_CACHE[hostname] = (ip, now + ttl)
                            return ip
        except Exception:
            continue

    return None


def _patched_getaddrinfo(host, port, family=0, type=0, proto=0, flags=0):
    if not _DOH_ENABLED or not host or _is_ip_address(str(host)):
        return _ORIG_GETADDRINFO(host, port, family, type, proto, flags)

    resolved_ip = resolve_doh(str(host))
    if resolved_ip:
        try:
            return _ORIG_GETADDRINFO(resolved_ip, port, family, type, proto, flags)
        except Exception:
            pass

    return _ORIG_GETADDRINFO(host, port, family, type, proto, flags)


def enable_doh() -> None:
    """Activate global DoH resolver for all Python network connections."""
    global _DOH_ENABLED
    if not _DOH_ENABLED:
        _DOH_ENABLED = True
        socket.getaddrinfo = _patched_getaddrinfo


def disable_doh() -> None:
    """Restore standard OS DNS resolver."""
    global _DOH_ENABLED
    if _DOH_ENABLED:
        _DOH_ENABLED = False
        socket.getaddrinfo = _ORIG_GETADDRINFO


def is_doh_enabled() -> bool:
    return _DOH_ENABLED
