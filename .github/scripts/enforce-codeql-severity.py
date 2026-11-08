#!/usr/bin/env python3
"""Fail CI on medium-or-higher CodeQL findings or results with unknown severity."""

from __future__ import annotations

import json
import math
import sys
from pathlib import Path
from typing import Any


THRESHOLD = 4.0


def parse_security_severity(raw: Any) -> float | None:
    if isinstance(raw, bool) or not isinstance(raw, (str, int, float)):
        return None
    try:
        score = float(raw)
    except (TypeError, ValueError):
        return None
    return score if math.isfinite(score) and 0.0 <= score <= 10.0 else None


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
            score = parse_security_severity(properties.get("security-severity"))
            if score is None:
                continue
            rule_id = rule.get("id")
            if rule_id:
                severities[rule_id] = max(score, severities.get(rule_id, score))
    return severities


def main(arguments: list[str]) -> int:
    files = sarif_files(arguments)
    if not files:
        print("ERROR: CodeQL produced no SARIF file to evaluate.", file=sys.stderr)
        return 2

    blocked_findings: list[str] = []
    for path in files:
        try:
            document = json.loads(path.read_text(encoding="utf-8"))
        except (OSError, json.JSONDecodeError) as error:
            print(f"ERROR: Could not read CodeQL SARIF {path}: {error}", file=sys.stderr)
            return 2

        runs = document.get("runs") if isinstance(document, dict) else None
        if not isinstance(runs, list) or not runs:
            print(f"ERROR: CodeQL SARIF {path} has no analysis runs.", file=sys.stderr)
            return 2

        for run in runs:
            if not isinstance(run, dict) or not isinstance(run.get("results"), list):
                print(f"ERROR: CodeQL SARIF {path} has a malformed analysis run.", file=sys.stderr)
                return 2
            severities = rule_severities(run)
            for result in run["results"]:
                if not isinstance(result, dict):
                    print(f"ERROR: CodeQL SARIF {path} contains a malformed result.", file=sys.stderr)
                    return 2
                rule_id = result.get("ruleId")
                score = severities.get(rule_id)
                if score is None:
                    result_properties = result.get("properties")
                    if isinstance(result_properties, dict):
                        score = parse_security_severity(result_properties.get("security-severity"))
                if score is None and type(result.get("ruleIndex")) is int:
                    rules = run.get("tool", {}).get("driver", {}).get("rules", [])
                    index = result["ruleIndex"]
                    if 0 <= index < len(rules):
                        rule = rules[index]
                        properties = rule.get("properties", {}) if isinstance(rule, dict) else {}
                        if isinstance(properties, dict):
                            score = parse_security_severity(properties.get("security-severity"))
                if score is None:
                    blocked_findings.append(
                        f"{path}: {rule_id or 'unknown rule'} has no valid security-severity"
                    )
                    continue
                if score < THRESHOLD:
                    continue

                location = "unknown location"
                locations = result.get("locations", [])
                if locations:
                    physical = locations[0].get("physicalLocation", {})
                    uri = physical.get("artifactLocation", {}).get("uri", "?")
                    line = physical.get("region", {}).get("startLine", "?")
                    location = f"{uri}:{line}"
                message = result.get("message", {}).get("text", "").splitlines()[0]
                blocked_findings.append(
                    f"{path}: {rule_id or 'unknown rule'} ({score:.1f}) at {location}: {message}"
                )

    if blocked_findings:
        print("CodeQL findings with medium-or-higher or unknown severity block this build:", file=sys.stderr)
        for finding in blocked_findings:
            print(f"- {finding}", file=sys.stderr)
        return 1

    print("CodeQL security threshold passed: no findings at severity 4.0 or higher.")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
