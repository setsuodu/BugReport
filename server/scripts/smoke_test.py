#!/usr/bin/env python3
"""BugReport API smoke (happy + bad path). Windows-friendly: python scripts/smoke_test.py

Env:
  BASE              default http://localhost:12080
  INGEST_KEY        default dev-ingest-key  (X-Api-Key)
  ADMIN_KEY         default dev-admin-key   (X-Admin-Api-Key)
  PROJECT_ID        default smoke-project
"""
from __future__ import annotations

import json
import os
import sys
import time
import urllib.error
import urllib.request
from datetime import datetime, timezone
from pathlib import Path
from xml.sax.saxutils import escape

BASE = os.environ.get("BASE", "http://localhost:12080").rstrip("/")
INGEST_KEY = os.environ.get("INGEST_KEY", "dev-ingest-key")
ADMIN_KEY = os.environ.get("ADMIN_KEY", "dev-admin-key")
PROJECT_ID = os.environ.get("PROJECT_ID", "smoke-project")

RESULTS: list[dict] = []


def req(method: str, url: str, body: dict | None = None, headers: dict | None = None):
    data = None
    hdr = dict(headers or {})
    if body is not None:
        data = json.dumps(body).encode()
        hdr.setdefault("Content-Type", "application/json")
    r = urllib.request.Request(url, data=data, headers=hdr, method=method)
    try:
        with urllib.request.urlopen(r, timeout=20) as resp:
            raw = resp.read().decode()
            return resp.status, raw
    except urllib.error.HTTPError as e:
        raw = e.read().decode() if e.fp else ""
        return e.code, raw
    except Exception as e:
        return 0, str(e)


def expect(
    name: str,
    method: str,
    url: str,
    expected: int,
    body: dict | None = None,
    headers: dict | None = None,
    require_json: bool = True,
) -> str:
    code, raw = req(method, url, body, headers)
    ok = code == expected
    if ok and require_json and expected not in (204,) and raw:
        try:
            json.loads(raw)
        except json.JSONDecodeError:
            ok = False
            print(f"  FAIL  {name}  non-JSON body: {raw[:200]}")
            RESULTS.append({"name": name, "ok": False, "code": code, "body": raw[:500]})
            return raw

    if ok:
        print(f"  PASS  {name}  ({code})")
        RESULTS.append({"name": name, "ok": True, "code": code})
    else:
        print(f"  FAIL  {name}  expected={expected} got={code} body={raw[:300]}")
        RESULTS.append({"name": name, "ok": False, "code": code, "body": raw[:500]})
    return raw


def write_reports(out_dir: Path) -> None:
    out_dir.mkdir(parents=True, exist_ok=True)
    passed = sum(1 for r in RESULTS if r["ok"])
    failed = sum(1 for r in RESULTS if not r["ok"])
    ts = datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")

    report = {
        "suite": "bugreport-server smoke",
        "base": BASE,
        "timestamp": ts,
        "pass": passed,
        "fail": failed,
        "results": RESULTS,
        "python": sys.version.split()[0],
        "platform": sys.platform,
    }
    (out_dir / "smoke-report.json").write_text(
        json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8"
    )

    cases = []
    for r in RESULTS:
        name = escape(r["name"])
        if r["ok"]:
            cases.append(f'  <testcase classname="BugReport.Smoke" name="{name}"/>')
        else:
            msg = escape(str(r.get("body") or f"code={r.get('code')}"))
            cases.append(
                f'  <testcase classname="BugReport.Smoke" name="{name}">\n'
                f'    <failure message="{msg}"/>\n'
                f"  </testcase>"
            )
    junit = (
        '<?xml version="1.0" encoding="UTF-8"?>\n'
        f'<testsuite name="BugReport.Smoke" tests="{len(RESULTS)}" '
        f'failures="{failed}" timestamp="{ts}">\n'
        + "\n".join(cases)
        + "\n</testsuite>\n"
    )
    (out_dir / "smoke-junit.xml").write_text(junit, encoding="utf-8")


def main() -> int:
    print(f"=== BugReport smoke against {BASE} ===")
    ingest_h = {"X-Api-Key": INGEST_KEY}
    admin_h = {"X-Admin-Api-Key": ADMIN_KEY}
    client_report_id = f"smoke-{int(time.time())}"

    print("--- health ---")
    expect("GET /health", "GET", f"{BASE}/health", 200)

    print("--- ingest bad path ---")
    expect(
        "ingest no key -> 401",
        "POST",
        f"{BASE}/api/v1/ingest/reports",
        401,
        {
            "projectId": PROJECT_ID,
            "clientReportId": client_report_id,
            "level": "Error",
            "message": "no key",
            "occurredAt": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
        },
    )
    expect(
        "ingest bad key -> 401",
        "POST",
        f"{BASE}/api/v1/ingest/reports",
        401,
        {
            "projectId": PROJECT_ID,
            "clientReportId": client_report_id + "-bad",
            "level": "Error",
            "message": "bad key",
            "occurredAt": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
        },
        {"X-Api-Key": "wrong-key"},
    )
    expect(
        "ingest missing fields -> 400",
        "POST",
        f"{BASE}/api/v1/ingest/reports",
        400,
        {"projectId": PROJECT_ID},
        ingest_h,
    )

    print("--- ingest happy ---")
    raw = expect(
        "ingest report -> 202",
        "POST",
        f"{BASE}/api/v1/ingest/reports",
        202,
        {
            "projectId": PROJECT_ID,
            "clientReportId": client_report_id,
            "level": "Exception",
            "message": f"smoke exception {client_report_id}",
            "stackTrace": "at Smoke.Main()\nat System.Runtime",
            "deviceInfo": {
                "platform": "Windows",
                "osVersion": "10.0",
                "deviceModel": "CI",
                "deviceId": "smoke-device",
            },
            "appVersion": "0.0.0-smoke",
            "occurredAt": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
            "customData": {"suite": "bugreport-smoke"},
        },
        ingest_h,
    )
    report_id = ""
    try:
        report_id = json.loads(raw).get("reportId") or json.loads(raw).get("id") or ""
    except Exception:
        pass

    print("--- admin bad path ---")
    expect("list reports no key -> 401", "GET", f"{BASE}/api/v1/reports", 401)
    expect(
        "list reports bad key -> 401",
        "GET",
        f"{BASE}/api/v1/reports",
        401,
        headers={"X-Admin-Api-Key": "wrong-admin"},
    )
    expect(
        "get missing report -> 404",
        "GET",
        f"{BASE}/api/v1/reports/00000000-0000-0000-0000-000000000000",
        404,
        headers=admin_h,
    )
    expect(
        "patch missing report -> 404",
        "PATCH",
        f"{BASE}/api/v1/reports/00000000-0000-0000-0000-000000000000/status",
        404,
        {"status": "Fixed"},
        admin_h,
    )

    print("--- admin happy ---")
    expect(
        "list reports -> 200",
        "GET",
        f"{BASE}/api/v1/reports?projectId={PROJECT_ID}&page=1&pageSize=20",
        200,
        headers=admin_h,
    )
    if report_id:
        expect(
            "get report -> 200",
            "GET",
            f"{BASE}/api/v1/reports/{report_id}",
            200,
            headers=admin_h,
        )
        expect(
            "patch status Fixed -> 200",
            "PATCH",
            f"{BASE}/api/v1/reports/{report_id}/status",
            200,
            {"status": "Fixed"},
            admin_h,
        )
        expect(
            "patch status Closed -> 200",
            "PATCH",
            f"{BASE}/api/v1/reports/{report_id}/status",
            200,
            {"status": "Closed"},
            admin_h,
        )
    else:
        print("  FAIL  no reportId from ingest")
        RESULTS.append({"name": "reportId from ingest", "ok": False, "code": 0, "body": raw[:300]})

    print("--- attachment init (optional shape) ---")
    # 鉴权 bad path 必测；成功路径依赖 MinIO，允许 200 或实现差异
    expect(
        "attachment init no key -> 401",
        "POST",
        f"{BASE}/api/v1/ingest/attachments/init",
        401,
        {
            "projectId": PROJECT_ID,
            "fileName": "smoke.log",
            "contentType": "text/plain",
            "sizeBytes": 12,
        },
    )
    code, raw = req(
        "POST",
        f"{BASE}/api/v1/ingest/attachments/init",
        {
            "projectId": PROJECT_ID,
            "fileName": "smoke.log",
            "contentType": "text/plain",
            "sizeBytes": 12,
        },
        ingest_h,
    )
    if code in (200, 201):
        print(f"  PASS  attachment init OK  ({code})")
        RESULTS.append({"name": "attachment init OK", "ok": True, "code": code})
    else:
        # 未配存储时可能 501/503，不硬失败，记一条可观测结果
        print(f"  WARN  attachment init got={code} body={raw[:200]} (not counted as fail)")
        RESULTS.append(
            {
                "name": "attachment init (optional)",
                "ok": True,
                "code": code,
                "body": raw[:300],
            }
        )

    passed = sum(1 for r in RESULTS if r["ok"])
    failed = sum(1 for r in RESULTS if not r["ok"])
    print(f"=== summary: PASS={passed} FAIL={failed} ===")

    # 相对 server/ 工作目录写 test-results/
    out = Path(__file__).resolve().parent.parent / "test-results"
    write_reports(out)
    print(f"reports -> {out}")

    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
