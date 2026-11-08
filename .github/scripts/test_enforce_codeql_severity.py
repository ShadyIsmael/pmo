"""Focused tests for the CI CodeQL SARIF severity gate."""

import contextlib
import importlib.util
import io
import json
import unittest
from pathlib import Path
from unittest import mock


SCRIPT = Path(__file__).with_name("enforce-codeql-severity.py")
SPEC = importlib.util.spec_from_file_location("enforce_codeql_severity", SCRIPT)
GATE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(GATE)
SARIF_PATH = Path("results.sarif")


def report(severity):
    properties = {} if severity is None else {"security-severity": severity}
    return {
        "runs": [{
            "tool": {"driver": {"rules": [{"id": "test/rule", "properties": properties}]}},
            "results": [{"ruleId": "test/rule", "message": {"text": "Test finding"}}],
        }],
    }


class CodeQLSeverityGateTests(unittest.TestCase):
    def evaluate(self, contents):
        stdout, stderr = io.StringIO(), io.StringIO()
        with mock.patch.object(GATE, "sarif_files", return_value=[SARIF_PATH]), \
                mock.patch.object(Path, "read_text", return_value=contents), \
                contextlib.redirect_stdout(stdout), contextlib.redirect_stderr(stderr):
            status = GATE.main(["results"])
        return status, stdout.getvalue(), stderr.getvalue()

    def test_medium_blocks_and_low_passes(self):
        for score, expected in (("4.0", 1), ("7.0", 1), ("3.9", 0), ("0.0", 0)):
            with self.subTest(score=score):
                status, _, _ = self.evaluate(json.dumps(report(score)))
                self.assertEqual(status, expected)

    def test_unscored_or_invalid_severity_blocks(self):
        for score in (None, "NaN", "Infinity", "-1", "11", True, "unknown"):
            with self.subTest(score=score):
                status, _, stderr = self.evaluate(json.dumps(report(score)))
                self.assertEqual(status, 1)
                self.assertIn("no valid security-severity", stderr)

    def test_malformed_sarif_fails_closed(self):
        for contents in ("{", "{}", '{"runs":[]}', '{"runs":[{}]}',
                         '{"runs":[{"results":[null]}]}'):
            with self.subTest(contents=contents):
                status, _, stderr = self.evaluate(contents)
                self.assertEqual(status, 2)
                self.assertIn("ERROR:", stderr)

    def test_missing_sarif_fails_closed(self):
        stderr = io.StringIO()
        with mock.patch.object(GATE, "sarif_files", return_value=[]), \
                contextlib.redirect_stderr(stderr):
            self.assertEqual(GATE.main(["missing"]), 2)
        self.assertIn("no SARIF file", stderr.getvalue())


if __name__ == "__main__":
    unittest.main()
