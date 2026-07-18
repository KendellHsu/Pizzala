#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Pizzala Tunnel Lab — 假後端伺服器 (軌道 A 驗證用)
---------------------------------------------------------------
目的:證明「校外手機 → pizzala.hsupingjhao.info → 本機 8787 port」
      這條路不只連得到,還能正常「傳輸資料」(GET / 檔案下載 / POST 往返)。

這是雛形(prototype),不是真後端。真後端上線時只要維持同樣的
監聽 port(8787)與 URL 結構,tunnel 設定就完全不用動 —— 這就是介面合約的意義。

為什麼不是純 `python -m http.server`:
      內建 http.server 只會做「靜態檔案伺服」,不能做 POST /api/echo 往返、
      不能回動態 JSON。這支 server.py 沿用 http.server 的 HTTPServer,只在上面
      補了幾個 API handler,啟動與心智模型跟 http.server 一樣。

用法:
      python server.py
      (或指定 port)  python server.py 8787

停止:在視窗按 Ctrl+C
"""

import json
import os
import sys
from datetime import datetime, timezone
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

# ── 設定(路徑不寫死在邏輯裡,集中在這幾行)───────────────────
PORT       = int(sys.argv[1]) if len(sys.argv) > 1 else 8787
HOST       = "0.0.0.0"                                # 監聽所有網卡(tunnel 才連得進來)
ROOT       = os.path.dirname(os.path.abspath(__file__))
PUBLIC_DIR = os.path.join(ROOT, "public")
DATA_DIR   = os.path.join(ROOT, "data")


def now_iso():
    return datetime.now(timezone.utc).astimezone().isoformat()


class Handler(BaseHTTPRequestHandler):
    server_version = "PizzalaTunnelLab/1.0"

    # ── 小工具 ───────────────────────────────────────────────
    def _send(self, body_bytes, ctype, code=200):
        self.send_response(code)
        self.send_header("Content-Type", ctype)
        self.send_header("Content-Length", str(len(body_bytes)))
        # 讓 tunnel / 瀏覽器跨來源請求不被擋(prototype 階段全開)
        self.send_header("Access-Control-Allow-Origin", "*")
        self.end_headers()
        self.wfile.write(body_bytes)

    def _send_text(self, text, ctype="text/plain; charset=utf-8", code=200):
        self._send(text.encode("utf-8"), ctype, code)

    def _send_json(self, obj, code=200):
        self._send(json.dumps(obj, ensure_ascii=False).encode("utf-8"),
                   "application/json; charset=utf-8", code)

    def _send_file(self, path, ctype):
        with open(path, "rb") as f:
            self._send(f.read(), ctype)

    def _client_ip(self):
        return self.client_address[0]

    def log_message(self, fmt, *args):
        stamp = datetime.now().strftime("%H:%M:%S")
        sys.stdout.write("  [%s] %s %s  ← %s\n"
                         % (stamp, self.command, self.path, self._client_ip()))

    # ── GET ──────────────────────────────────────────────────
    def do_GET(self):
        path = self.path.split("?", 1)[0]

        # 首頁:測試頁
        if path == "/":
            self._send_file(os.path.join(PUBLIC_DIR, "index.html"),
                            "text/html; charset=utf-8")
            return

        # 健康檢查:回 JSON,前端拿來確認「連得到 + 資料回得來」
        if path == "/api/ping":
            self._send_json({
                "ok": True,
                "service": "pizzala-tunnel-lab",
                "time": now_iso(),
                "client": self._client_ip(),
                # 有值代表經過 Cloudflare tunnel
                "via": self.headers.get("CF-Connecting-IP"),
            })
            return

        # 假的場次列表(對應軌道 B 合約的 /api/sessions)
        if path == "/api/sessions":
            self._send_file(os.path.join(DATA_DIR, "sessions.json"),
                            "application/json; charset=utf-8")
            return

        # 檔案下載測試:量大檔傳輸穩不穩
        if path == "/download/sample.bin":
            f = os.path.join(DATA_DIR, "sample.bin")
            if not os.path.exists(f):
                # 第一次跑時生成一個 1 MB 假檔
                with open(f, "wb") as fh:
                    fh.write(os.urandom(1024 * 1024))
            self._send_file(f, "application/octet-stream")
            return

        self._send_text("404 Not Found: %s" % path, code=404)

    # ── POST ─────────────────────────────────────────────────
    def do_POST(self):
        path = self.path.split("?", 1)[0]

        # 上傳往返:POST body 原樣回傳,附上收到的位元組數
        if path == "/api/echo":
            length = int(self.headers.get("Content-Length", 0))
            raw = self.rfile.read(length) if length else b""
            body = raw.decode("utf-8", errors="replace")
            self._send_json({
                "ok": True,
                "receivedBytes": len(raw),
                "echo": body,
                "serverTime": now_iso(),
            })
            return

        self._send_text("404 Not Found: %s" % path, code=404)


def main():
    httpd = ThreadingHTTPServer((HOST, PORT), Handler)
    print("")
    print("  Pizzala Tunnel Lab 假伺服器已啟動 (Python http.server)")
    print("  ------------------------------------------------")
    print("  本機自測 : http://localhost:%d/" % PORT)
    print("  對外(tunnel 指到) : http://localhost:%d" % PORT)
    print("  健康檢查 : /api/ping   (回 JSON,含伺服器時間)")
    print("  上傳往返 : /api/echo   (POST 什麼就原樣回什麼 + 大小/延遲)")
    print("  假場次   : /api/sessions")
    print("  檔案下載 : /download/sample.bin  (測大檔傳輸)")
    print("  ------------------------------------------------")
    print("  按 Ctrl+C 停止")
    print("")
    try:
        httpd.serve_forever()
    except KeyboardInterrupt:
        print("\n  已停止。")
        httpd.server_close()


if __name__ == "__main__":
    main()
