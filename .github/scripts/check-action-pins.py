#!/usr/bin/env python3
"""Fail if a workflow references an external GitHub Action by a mutable ref."""

from __future__ import annotations

import sys
from pathlib import Path


ROOT = Path.cwd() if __file__ in ("<stdin>", "-") else Path(__file__).resolve().parents[2]
WORKFLOWS = ROOT / ".github" / "workflows"


def is_hexadecimal(value: str, length: int) -> bool:
    return len(value) == length and all(character in "0123456789abcdefABCDEF" for character in value)


def check_workflows() -> list[str]:
    errors: list[str] = []
    files = sorted((*WORKFLOWS.glob("*.yml"), *WORKFLOWS.glob("*.yaml")))
    if not files:
        return [f"{WORKFLOWS}: no workflow files found"]

    for workflow in files:
        try:
            lines = workflow.read_text(encoding="utf-8").splitlines()
        except (OSError, UnicodeError) as error:
            errors.append(f"{workflow.relative_to(ROOT)}: cannot read workflow: {error}")
            continue

        for line_number, line in enumerate(lines, start=1):
            field = line.strip()
            if not field.startswith("uses:"):
                continue
            value = field[len("uses:"):].strip()
            if " #" in value:
                value = value.split(" #", 1)[0].rstrip()
            if len(value) >= 2 and value[0] == value[-1] and value[0] in "\"'":
                value = value[1:-1]

            if value.startswith("./"):
                continue

            if value.startswith("docker://"):
                image, separator, digest = value.rpartition("@")
                if (
                    not separator
                    or not image.removeprefix("docker://")
                    or not digest.startswith("sha256:")
                    or not is_hexadecimal(digest.removeprefix("sha256:"), 64)
                ):
                    errors.append(
                        f"{workflow.relative_to(ROOT)}:{line_number}: "
                        f"container action must use an immutable SHA-256 digest: {value!r}"
                    )
                continue

            repository, separator, reference = value.rpartition("@")
            valid_repository = (
                separator
                and "/" in repository
                and not any(character.isspace() for character in repository)
            )
            if not valid_repository or not is_hexadecimal(reference, 40):
                errors.append(
                    f"{workflow.relative_to(ROOT)}:{line_number}: "
                    f"external action must use a full 40-character commit SHA: {value!r}"
                )

    return errors


def main() -> int:
    errors = check_workflows()
    if errors:
        for error in errors:
            print(f"ERROR: {error}", file=sys.stderr)
        return 1
    print("GitHub Actions pin check passed.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
