"""Run a Python file inside the open Dev Kit editor through Unreal's official Python Remote Execution
(PythonScriptPlugin, bound to 127.0.0.1 only). Usage: python ue_exec.py <script.py> [timeout_s]
Prints the editor's Python output and exits non-zero if the command failed."""
import os, sys, time

sys.path.insert(0, r"D:\epic\CEUE5Devkit\Engine\Plugins\Experimental\PythonScriptPlugin\Content\Python")
import remote_execution as rx  # noqa: E402

script = os.path.abspath(sys.argv[1]).replace("\\", "/")
timeout = float(sys.argv[2]) if len(sys.argv) > 2 else 600.0

cfg = rx.RemoteExecutionConfig()
cfg.multicast_bind_address = "127.0.0.1"
re = rx.RemoteExecution(cfg)
re.start()
try:
    deadline = time.time() + 20
    while not re.remote_nodes and time.time() < deadline:
        time.sleep(0.25)
    if not re.remote_nodes:
        print("NO EDITOR NODE FOUND (is the Dev Kit open with remote execution enabled?)")
        sys.exit(2)
    re.open_command_connection(re.remote_nodes[0]["node_id"])
    res = re.run_command('exec(open(r"%s", encoding="utf-8").read(), {"__name__": "__main__", "__file__": r"%s"})' % (script, script),
                         exec_mode=rx.MODE_EXEC_STATEMENT)
    for o in res.get("output", []):
        print("[%s] %s" % (o.get("type"), str(o.get("output")).rstrip()))
    print("RESULT success=%s %s" % (res.get("success"), res.get("result", "")))
    sys.exit(0 if res.get("success") else 1)
finally:
    re.stop()
