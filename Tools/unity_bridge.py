"""
Minimal client for the MCP-for-Unity editor bridge (127.0.0.1:6401, length-framed JSON), for when the MCP server
itself won't start.

    python Tools/unity_bridge.py code <file.cs>            run C# with execute_code (prints the result)
    python Tools/unity_bridge.py raw <type> '<json>'       any tool, e.g. raw read_console '{"action":"get","types":["error"]}'
    python Tools/unity_bridge.py shot <out.png> '<json>'   manage_camera screenshot, image saved to out.png
"""
import base64
import json
import socket
import struct
import sys

PORT = 6401


def recv_exact(s, n):
    buf = b""
    while len(buf) < n:
        chunk = s.recv(n - len(buf))
        if not chunk:
            raise ConnectionError("bridge closed the connection")
        buf += chunk
    return buf


def call(kind, params, timeout=180):
    s = socket.create_connection(("127.0.0.1", PORT), timeout=timeout)
    try:
        hello = b""
        while not hello.endswith(b"\n"):
            hello += recv_exact(s, 1)
        payload = json.dumps({"type": kind, "params": params}).encode("utf-8")
        s.sendall(struct.pack(">Q", len(payload)) + payload)
        n = struct.unpack(">Q", recv_exact(s, 8))[0]
        return json.loads(recv_exact(s, n).decode("utf-8"))
    finally:
        s.close()


def find_image(obj):
    if isinstance(obj, dict):
        for k, v in obj.items():
            if isinstance(v, str) and len(v) > 2000 and k.lower() in ("image", "imagebase64", "image_base64", "base64", "data"):
                return v
            r = find_image(v)
            if r:
                return r
    elif isinstance(obj, list):
        for v in obj:
            r = find_image(v)
            if r:
                return r
    return None


def strip_big(obj):
    if isinstance(obj, dict):
        return {k: ("<%d chars>" % len(v) if isinstance(v, str) and len(v) > 2000 else strip_big(v)) for k, v in obj.items()}
    if isinstance(obj, list):
        return [strip_big(v) for v in obj]
    return obj


def main():
    mode = sys.argv[1]
    if mode == "code":
        code = open(sys.argv[2], encoding="utf-8").read()
        res = call("execute_code", {"action": "execute", "code": code})
    elif mode == "raw":
        res = call(sys.argv[2], json.loads(sys.argv[3]) if len(sys.argv) > 3 else {})
    elif mode == "shot":
        params = {"action": "screenshot", "include_image": True}
        if len(sys.argv) > 3:
            params.update(json.loads(sys.argv[3]))
        res = call("manage_camera", params)
        img = find_image(res)
        if img:
            if "," in img[:64]:
                img = img.split(",", 1)[1]
            open(sys.argv[2], "wb").write(base64.b64decode(img))
            print("saved", sys.argv[2])
        res = strip_big(res)
    else:
        raise SystemExit("unknown mode " + mode)
    print(json.dumps(res, indent=1, ensure_ascii=False)[:20000])


if __name__ == "__main__" and sys.argv[1] != "wait":
    main()


def wait_ready(limit=240):
    """Waits out a recompile / domain reload: the bridge drops, comes back, and reports not compiling."""
    import time
    t0 = time.time()
    time.sleep(3)
    while time.time() - t0 < limit:
        try:
            r = call("execute_code", {"action": "execute", "code": "return UnityEditor.EditorApplication.isCompiling || UnityEditor.EditorApplication.isUpdating;"}, timeout=20)
            busy = r.get("result", {}).get("data", {}).get("result")
            if busy is False:
                return time.time() - t0
        except Exception:
            pass
        time.sleep(2)
    raise TimeoutError("Unity did not come back")


if __name__ == "__main__" and len(sys.argv) > 1 and sys.argv[1] == "wait":
    print("ready after %.0fs" % wait_ready())
