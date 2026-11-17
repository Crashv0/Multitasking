"""Regenerates the winmm forwarding thunks from a winmm.dll (default: System32)."""
import re
import subprocess
import sys
from pathlib import Path

dll = sys.argv[1] if len(sys.argv) > 1 else "/mnt/c/Windows/System32/winmm.dll"
out = Path(__file__).parent
dump = subprocess.run(["objdump", "-p", dll], capture_output=True, text=True, check=True).stdout
table = dump.split("[Ordinal/Name Pointer] Table", 1)[1].split("\n\n", 1)[0]
exports = [(int(o), n) for o, n in re.findall(r"\[\s*(\d+)\]\s+(\S+)", table)]

(out / "exports.def").write_text(
    "LIBRARY winmm\nEXPORTS\n" + "".join(f"    {n}=Thunk_{n} @{o}\n" for o, n in exports))
(out / "thunks.asm").write_text(
    "EXTERN g_winmm:QWORD\n.code\n"
    + "".join(f"Thunk_{n} PROC\n    jmp QWORD PTR [g_winmm + {8 * i}]\nThunk_{n} ENDP\n"
              for i, (_, n) in enumerate(exports))
    + "END\n")
(out / "exports.inc").write_text(
    f"constexpr int kWinmmCount = {len(exports)};\n"
    "static const char* const kWinmmNames[kWinmmCount] = {\n"
    + "".join(f'    "{n}",\n' for _, n in exports) + "};\n")
print(f"{len(exports)} exports")
