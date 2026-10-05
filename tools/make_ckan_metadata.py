"""Legacy entry point: metadata is now maintained directly in NetKAN."""
from pathlib import Path
path = Path(__file__).resolve().parents[2] / "NetKAN" / "NetKAN" / "NivenRingworld.netkan"
print("Edit and submit the authoritative NetKAN file (no local metadata generated):")
print(path)
print(path.read_text(encoding="utf-8"))
