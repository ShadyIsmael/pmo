#!/usr/bin/env python3
"""Fail CI when CodeQL reports a High or Critical security finding."""

from __future__ import annotations

import json
import sys
from pathlib import Path
from typing import Any


THRESHOLD = 7.0


def sarif_files(arguments: list[str]) -> list[Path]:
    paths: list[Path] = []
    for argument in arguments:
        candidate = Path(argument)
        if candidate.is_dir():
            paths.extend(candidate.rglob("*.sarif"))
        elif candidate.is_file() and candidate.suffix.lower() == ".sarif":
            paths.append(candidate)
    return sorted(set(paths))


def rule_severities(run: dict[str, Any]) -> dict[str, float]:
    severities: dict[str, float] = {}
    tool = run.get("tool", {})
    components = [tool.get("driver", {})]
    components.extend(tool.get("extensions", []))
    for component in components:
        for rule in component.get("rules", []):
            properties = rule.get("properties", {})
            raw = properties.get("security-severity")
            try:
                score = float(raw)
            except (TypeError, ValueError):
                continue
            rule_id = rule.get("id")
            if rule_id:
                severities[rule_id] = score
    return severities


def main(arguments: list[str]) -> int:
    files = sarif_files(arguments)
    if not files:
        print("ERROR: CodeQL produced no SARIF file to evaluate.", file=sys.stderr)
        return 2

    high_findings: list[str] = []
    for path in files:
        try:
            document = json.loads(path.read_text(encoding="utf-8"))
        except (OSError, json.JSONDecodeError) as error:
            print(f"ERROR: Could not read CodeQL SARIF {path}: {error}", file=sys.stderr)
            return 2

        for run in document.get("runs", []):
            severities = rule_severities(run)
            for result in run.get("results", []):
                rule_id = result.get("ruleId")
                score = severities.get(rule_id)
                if score is None and isinstance(result.get("ruleIndex"), int):
                    rules = run.get("tool", {}).get("driver", {}).get("rules", [])
                    index = result["ruleIndex"]
                    if 0 <= index < len(rules):
                        try:
                            score = float(rules[index].get("properties", {}).get("security-severity"))
                        except (TypeError, ValueError):
                            score = None
                if score is None or score < THRESHOLD:
                    continue

                location = "unknown location"
                locations = result.get("locations", [])
                if locations:
                    physical = locations[0].get("physicalLocation", {})
                    uri = physical.get("artifactLocation", {}).get("uri", "?")
                    line = physical.get("region", {}).get("startLine", "?")
                    location = f"{uri}:{line}"
                message = result.get("message", {}).get("text", "").splitlines()[0]
                high_findings.append(
                    f"{path}: {rule_id or 'unknown rule'} ({score:.1f}) at {location}: {message}"
                )

    if high_findings:
        print("CodeQL High/Critical security findings block this build:", file=sys.stderr)
        for finding in high_findings:
            print(f"- {finding}", file=sys.stderr)
        return 1

    print("CodeQL security threshold passed: no findings at severity 7.0 or higher.")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
