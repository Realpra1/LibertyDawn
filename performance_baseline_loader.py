"""Import helper for the hyphenated performance-baseline tool directory."""

from __future__ import annotations

import importlib.util
import sys
from pathlib import Path
from types import ModuleType


def load_builder() -> ModuleType:
    path = Path(__file__).resolve().parent / "performance-baseline" / "build_performance_map.py"
    spec = importlib.util.spec_from_file_location("cnc47_performance_map_builder", path)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"cannot load map builder: {path}")
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module
