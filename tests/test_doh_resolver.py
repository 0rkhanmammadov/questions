import socket
from unittest.mock import patch
import pytest

from doh_resolver import (
    _ORIG_GETADDRINFO,
    _is_ip_address,
    disable_doh,
    enable_doh,
    is_doh_enabled,
    resolve_doh,
)


def test_is_ip_address():
    assert _is_ip_address("1.1.1.1")
    assert _is_ip_address("8.8.8.8")
    assert _is_ip_address("127.0.0.1")
    assert _is_ip_address("localhost")
    assert not _is_ip_address("generativelanguage.googleapis.com")
    assert not _is_ip_address("google.com")


def test_resolve_doh_mock():
    with patch("urllib.request.urlopen") as mock_open:
        mock_res = mock_open.return_value.__enter__.return_value
        mock_res.status = 200
        mock_res.read.return_value = b'{"Status": 0, "Answer": [{"name": "example.com", "type": 1, "TTL": 300, "data": "93.184.216.34"}]}'

        ip = resolve_doh("test-domain.com")
        assert ip == "93.184.216.34"


def test_enable_disable_doh():
    disable_doh()
    assert not is_doh_enabled()
    assert socket.getaddrinfo == _ORIG_GETADDRINFO

    enable_doh()
    assert is_doh_enabled()
    assert socket.getaddrinfo != _ORIG_GETADDRINFO
