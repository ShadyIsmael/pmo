#!/usr/bin/env python3
"""Universal Project Intelligence (UPI) - stdlib-first repository index and task router.

All generated knowledge stays under <repo>/docs/architecture/. The tool never edits
application source. Parsing is conservative / heuristic, NOT a compiler or Roslyn.
"""
from __future__ import annotations

import argparse
import datetime as dt
import fnmatch
import hashlib
import json
import math
import os
from pathlib import Path
import re
import subprocess
import sys
import time
import uuid

SCHEMA = 2
ENGINE_VERSION = "1.1.0"
GRAPH_VERSION = 2
NAME = "universal-project-intelligence"
DEFAULT_EXTS = {".cs", ".ts", ".tsx", ".js", ".jsx", ".py", ".sql", ".json"}
DEFAULT_SKIP = {".git", "node_modules", "bin", "obj", "dist", ".venv", "__pycache__", "docs", ".agents"}
SECRET_NAMES = {".env", "secrets.json", "appsettings.secrets.json", "credentials.json", "id_rsa", "id_ed25519"}
IGNORED_SUFFIXES = (".min.js", ".map", ".lock", ".g.cs", ".designer.cs", ".generated.cs", ".d.ts")
STOP = {"the","a","an","and","or","to","in","of","for","with","at","by","on","fix","update","add","modify","change","implement","issue","bug","error","file","page","api","service","module","component","test","tests","project","projects","make","from","new","not","is","does","when","into","analyze","support","feature","get","set","view","create"}


def stamp():
    return dt.datetime.now(dt.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def slug(s):
    s = re.sub(r"([a-z0-9])([A-Z])", r"\1-\2", s)
    return re.sub(r"[^a-z0-9]+", "-", s.lower()).strip("-") or "other"


def words(s):
    s = re.sub(r"([a-z0-9])([A-Z])", r"\1 \2", s)
    return [v.lower() for v in re.findall(r"[\w]+", s, re.UNICODE) if len(v) > 1]


def normalize(s):
    return "".join(words(s))


def read_json(p, default=None):
    if not p.exists():
        return default
    return json.loads(p.read_text(encoding="utf-8"))


def atomic_write(p, value):
    p.parent.mkdir(parents=True, exist_ok=True)
    temp = p.with_name(p.name + ".tmp-" + uuid.uuid4().hex[:8])
    temp.write_text(value, encoding="utf-8", newline="\n")
    os.replace(temp, p)


def json_write(p, obj):
    atomic_write(p, json.dumps(obj, ensure_ascii=False, indent=2, sort_keys=True) + "\n")


def jsonl_write(p, records):
    atomic_write(p, "".join(json.dumps(x, ensure_ascii=False, sort_keys=True) + "\n" for x in records))


def jsonl_read(p):
    if not p.exists():
        return []
    return [json.loads(line) for line in p.read_text(encoding="utf-8").splitlines() if line.strip()]


def repo_root(path):
    p = Path(path).expanduser().resolve()
    if not p.is_dir():
        raise ValueError(f"Not a directory: {p}")
    return p


def load_config(root):
    local = root / "tools" / "upi" / "upi_config.json"
    packaged = Path(__file__).with_name("upi_config.json")
    cfg = read_json(local) or read_json(packaged) or {}
    cfg.setdefault("source_extensions", sorted(DEFAULT_EXTS))
    cfg.setdefault("excluded_directories", sorted(DEFAULT_SKIP))
    cfg.setdefault("module_aliases", {})
    cfg.setdefault("module_path_rules", {})
    cfg.setdefault("typescript_aliases", {})
    cfg.setdefault("shared_path_markers", ["shared", "common", "infrastructure"])
    cfg.setdefault("max_file_bytes", 3000000)
    cfg.setdefault("tokenizer", "o200k_base")
    cfg.setdefault("critical_flows", {})
    return cfg


def config_hash(cfg):
    # Routing-only configuration (for example critical flow definitions) does not
    # change per-file index metadata and must not trigger a full source reparse.
    index_keys = (
        "source_extensions", "excluded_directories", "max_file_bytes", "tokenizer",
        "module_aliases", "module_path_rules", "shared_path_markers", "typescript_aliases",
    )
    index_cfg = {key: cfg.get(key) for key in index_keys}
    return hashlib.sha256(json.dumps(index_cfg, sort_keys=True, ensure_ascii=False).encode("utf-8")).hexdigest()


def git_head(root):
    try:
        p = subprocess.run(["git", "rev-parse", "HEAD"], cwd=root, capture_output=True, text=True, timeout=3)
        return p.stdout.strip() if p.returncode == 0 else None
    except (OSError, subprocess.TimeoutExpired):
        return None


def is_safe_file(rel, cfg):
    pp = Path(rel)
    # UPI is tooling, not application architecture; do not index its own runtime/config.
    if rel.replace("\\", "/").lower().startswith("tools/upi/"):
        return False
    if pp.suffix.lower() not in set(cfg["source_extensions"]):
        return False
    parts = [s.lower() for s in pp.parts]
    if any(x in set(x.lower() for x in cfg["excluded_directories"]) for x in parts[:-1]):
        return False
    name = pp.name.lower()
    if name in SECRET_NAMES or name.startswith(".env"):
        return False
    # Project settings and dependency locks can contain credentials or huge,
    # generated inventories; neither improves a source architecture index.
    if (name.startswith("appsettings") and name.endswith(".json")) or name in {"launchsettings.json", "package-lock.json", "npm-shrinkwrap.json"}:
        return False
    if any(pp.name.lower().endswith(s) for s in IGNORED_SUFFIXES):
        return False
    if "secret" in pp.name.lower() or "credential" in pp.name.lower() or "private-key" in pp.name.lower():
        return False
    if any(x in rel.lower() for x in ("/secrets/", "/credentials/", "/.git/")):
        return False
    return True


def list_source(root, cfg):
    excluded = set(x.lower() for x in cfg["excluded_directories"])
    found = {}
    for base, dirs, filenames in os.walk(root):
        dirs[:] = sorted(x for x in dirs if x.lower() not in excluded and not (Path(base) / x).is_symlink())
        for name in sorted(filenames):
            full = Path(base) / name
            if full.is_symlink() or not full.is_file():
                continue
            rel = full.relative_to(root).as_posix()
            if not is_safe_file(rel, cfg):
                continue
            try:
                st = full.stat()
                if st.st_size > cfg["max_file_bytes"]:
                    continue
                found[rel] = {"size_bytes": st.st_size, "mtime_ns": st.st_mtime_ns}
            except OSError:
                continue
    return found


def count_tokens(text, cfg):
    try:
        import tiktoken  # Optional only; do not require it to run.
        enc = tiktoken.get_encoding(cfg["tokenizer"])
        return len(enc.encode(text)), f"tiktoken:{cfg['tokenizer']}", False
    except (ImportError, KeyError, ValueError, TypeError):
        # Not a token count. Label as approximation in every output.
        return math.ceil(len(text) / 3.5), "estimated:chars/3.5", True


def infer_kind(rel):
    n = Path(rel).name.lower()
    p = rel.lower()
    if "test" in n or "/tests/" in p or "/__tests__/" in p or n.endswith(".spec.ts"):
        return "test"
    if "migration" in p:
        return "migration"
    if "controller" in n or "/api/" in p or "endpoint" in n:
        return "api"
    if "permission" in n or "policy" in n or "authoriz" in n:
        return "permission"
    if "localiz" in n or "/i18n/" in p or "translation" in p:
        return "localization"
    if "context" in n or "repository" in n or "/persistence/" in p:
        return "persistence"
    if "entity" in n or "/entities/" in p or "/domain/" in p:
        return "domain"
    if "service" in n or "handler" in n or "usecase" in n:
        return "service"
    if n.endswith((".tsx", ".jsx", ".vue", ".razor")) or "/components/" in p or "/pages/" in p:
        return "ui"
    if n.endswith(".sql"):
        return "sql"
    if n.endswith((".json", ".csproj")):
        return "config"
    return "source"


def discover_module(rel, cfg):
    """Return owner and a short provenance; never claim high confidence from names alone."""
    lower = rel.lower()
    for module, patterns in cfg.get("module_path_rules", {}).items():
        if any(fnmatch.fnmatch(lower, pat.lower()) for pat in patterns):
            return module, "configured-path-rule", "configured"
    pp = Path(rel)
    if infer_kind(rel) == "test":
        # Test folders contain generic API/Integration tokens that otherwise
        # tie unrelated modules. Resolve test ownership from the test filename.
        test_name = normalize(pp.stem)
        test_scores = {}
        for module, aliases in cfg.get("module_aliases", {}).items():
            for alias in aliases + [module]:
                normalized = normalize(alias)
                if normalized and (test_name == normalized or test_name.startswith(normalized)):
                    test_scores[module] = max(test_scores.get(module, (0, 0)), (2, len(normalized)))
                elif len(normalized) >= 6 and normalized in test_name:
                    test_scores[module] = max(test_scores.get(module, (0, 0)), (1, len(normalized) - 1))
        if test_scores:
            top = sorted(test_scores, key=lambda module: (-test_scores[module][0], -test_scores[module][1], module))
            if len(top) > 1 and test_scores[top[0]] == test_scores[top[1]]:
                return "shared", f"ambiguous-test-name:{','.join(top[:3])}", "uncertain"
            return top[0], "test-filename-alias", "medium"
        return "tests", "test-file-without-module-alias", "low"
    parts = pp.parts
    tokens = [normalize(x) for x in parts[:-1]]
    name = normalize(pp.stem)
    aliases = {k: {normalize(x) for x in v} | {normalize(k)} for k,v in cfg["module_aliases"].items()}
    score = {}
    why = {}
    feature_parent_names = {"modules", "features", "boundedcontexts", "areas", "domains"}
    for i, part in enumerate(tokens):
        if part in feature_parent_names and i+1 < len(tokens):
            candidate = slug(parts[i+1]); score[candidate] = score.get(candidate, 0)+9; why[candidate] = "feature-directory"
    for module, al in aliases.items():
        s = 0
        for a in al:
            if not a:
                continue
            if a in tokens:
                s = max(s, 10)
            if name == a:
                s = max(s, 9)
            if name.startswith(a) and (len(a) >= 5 or name == a):
                s = max(s, 7)
            if len(a) >= 6 and a in name:
                s = max(s, 5)
        if s:
            score[module] = score.get(module, 0) + s
            why[module] = "path-and-name-heuristic"
    # Fallback from conventional Controller/Service file naming.
    if not score:
        stem = re.sub(r"(Controller|Service|Handler|Repository|Entity|Manager|Page|View|Tests?|Spec)$", "", pp.stem, flags=re.I)
        if stem and stem != pp.stem:
            candidate = slug(stem)
            if candidate not in ("base", "common", "shared", "api", "app"):
                score[candidate] = 3; why[candidate] = "filename-heuristic"
    if score:
        top = sorted(score, key=lambda k: (-score[k], k))
        if len(top)>1 and score[top[0]] == score[top[1]]:
            return "shared", f"ambiguous:{','.join(top[:3])}", "uncertain"
        return top[0], why[top[0]], "medium" if score[top[0]] >= 7 else "low"
    shared = {normalize(x) for x in cfg["shared_path_markers"]}
    if any(x in shared for x in tokens):
        return "shared", "shared-path", "medium"
    return "unclassified", "no-evidence", "uncertain"


def brace_end(lines, start):
    """Best-effort end of brace body. Falls back to declaration line."""
    depth, seen = 0, False
    for j in range(start, min(len(lines), start+1800)):
        # approximation: braces inside strings/comments may mislead; flagged in metadata
        line = lines[j]
        opened, closed = line.count("{"), line.count("}")
        if opened:
            seen = True
        depth += opened - closed
        if seen and depth <= 0:
            return j+1
        if not seen and j > start+8:
            return start+1
    return start+1


def symbols_from_text(rel, text, cfg):
    lines = text.splitlines(keepends=True)
    suffix = Path(rel).suffix.lower()
    out = []
    if suffix == ".cs":
        pat = re.compile(r"^\s*(?:(?:public|private|protected|internal|static|sealed|abstract|partial|async|virtual|override|new|readonly)\s+)*(?:(?:class|interface|record|struct|enum)\s+(?P<type>[A-Za-z_]\w*)|(?:[\w<>,?\[\].]+\s+)+(?P<func>[A-Za-z_]\w*)\s*\([^;]*\)\s*(?:\{|;|$))")
    elif suffix in {".ts", ".tsx", ".js", ".jsx"}:
        pat = re.compile(r"^\s*(?:export\s+)?(?:default\s+)?(?:async\s+)?(?:(?:class|interface|type|enum)\s+(?P<type>[A-Za-z_]\w*)|function\s+(?P<func>[A-Za-z_]\w*)\s*\(|(?:const|let)\s+(?P<const>[A-Za-z_]\w*)\s*(?::[^=]*)?=\s*(?:async\s*)?(?:\(|function))")
    elif suffix == ".py":
        pat = re.compile(r"^\s*(?:async\s+)?(?:(?:class)\s+(?P<type>[A-Za-z_]\w*)|def\s+(?P<func>[A-Za-z_]\w*))")
    else:
        return []
    for i, line in enumerate(lines):
        m = pat.search(line)
        if not m:
            continue
        name = next((x for x in m.groupdict().values() if x), None)
        if not name or name in {"if", "for", "while", "switch", "catch", "using", "return"}:
            continue
        k = "type" if m.groupdict().get("type") else "function"
        end = brace_end(lines, i) if suffix != ".py" else i+1
        if suffix == ".py":
            indent = len(line) - len(line.lstrip())
            for j in range(i+1, len(lines)):
                if lines[j].strip() and len(lines[j]) - len(lines[j].lstrip()) <= indent:
                    end = j; break
            else:
                end = len(lines)
        chunk = "".join(lines[i:end])
        tok, method, approx = count_tokens(chunk, cfg)
        out.append({"file": rel, "name": name, "kind": k, "start_line": i+1, "end_line": max(i+1,end), "tokens": tok, "token_method": method, "approximate": approx, "extraction": "heuristic-declaration-and-braces"})
    return out[:2000]


def parse_file(root, rel, stat, cfg):
    path = root / rel
    raw = path.read_bytes()
    text = raw.decode("utf-8", errors="replace")
    tcount, tmethod, approximate = count_tokens(text, cfg)
    ns = re.search(r"^\s*namespace\s+([\w.]+)\s*[;{]", text, re.M)
    usings = re.findall(r"^\s*(?:global\s+)?using\s+(?:static\s+)?([\w.]+)\s*;", text, re.M)
    imports = re.findall(r"(?:\bfrom\s*|\bimport\s*\(\s*|\brequire\s*\(\s*)[\"']([^\"']+)[\"']", text)
    if Path(rel).suffix.lower() == ".py":
        imports += re.findall(r"^\s*from\s+([\w.]+)\s+import\s+", text, re.M)
        imports += re.findall(r"^\s*import\s+([\w.]+)", text, re.M)
    # only identifiers likely to reference types; do not store source content/snippets
    references = sorted(set(re.findall(r"\b[A-Z][A-Za-z0-9_]{3,}\b", text)))[:1500]
    syms = symbols_from_text(rel, text, cfg)
    # Framework clues: source-backed strings, never interpreted as proven runtime edges.
    route_attrs = re.findall(r'\[Route\(\s*[\"\']([^\"\']+)[\"\']\s*\)\]', text)
    http_attrs = re.findall(r'\[Http(?:Get|Post|Put|Patch|Delete)\s*(?:\(\s*[\"\']([^\"\']*)[\"\'])?', text)
    controller = re.sub(r'Controller$', '', Path(rel).stem, flags=re.I).lower()
    routes = []
    for prefix in route_attrs:
        prefix = prefix.replace('[controller]',controller)
        routes.append(prefix)
        for suffix in http_attrs:
            routes.append(prefix.strip('/')+'/'+suffix.strip('/'))
    api_calls = re.findall(r'[\"\'](/?api/[a-zA-Z0-9_/{}/:$.-]+)[\"\']', text)
    dbsets = re.findall(r'\bDbSet\s*<\s*([A-Za-z_]\w*)\s*>', text)
    permissions = re.findall(r'(?:Authorize\s*\(\s*Policy\s*=\s*|RequireAuthorization\s*\()\s*[\"\']([^\"\']+)', text)
    module, rationale, confidence = discover_module(rel, cfg)
    call_sites = call_sites_from_text(text, syms)
    meta = {"path": rel, "sha256": hashlib.sha256(raw).hexdigest(), "size_bytes": stat["size_bytes"], "mtime_ns": stat["mtime_ns"],
            "tokens": tcount, "token_method": tmethod, "approximate": approximate, "lines": len(text.splitlines()),
            "kind": infer_kind(rel), "module": module, "module_basis": rationale, "module_confidence": confidence,
            "namespace": ns.group(1) if ns else None, "usings": sorted(set(usings)), "imports": sorted(set(imports)),
            "import_bindings": import_bindings_from_text(text),
            "references": references,
            "identifiers": sorted(set(re.findall(r"\b[A-Za-z_$][A-Za-z0-9_$]*\b", text)))[:5000],
            "call_sites": call_sites,
            "typed_members": typed_members_from_text(text),
            "implements": interfaces_from_text(text),
            "symbols": len(syms), "routes": sorted(set(routes)), "api_calls": sorted(set(api_calls)),
            "api_requests": api_requests_from_text(text), "endpoints": endpoints_from_text(rel, text),
            "dbsets": sorted(set(dbsets)), "permission_policies": sorted(set(permissions))}
    return meta, syms


def resolve_import(importer, name, paths, cfg):
    candidates = []
    if name.startswith("."):
        base = (Path(importer).parent / name).as_posix()
        candidates.append(os.path.normpath(base).replace("\\", "/"))
    for alias, real in cfg.get("typescript_aliases", {}).items():
        if name.startswith(alias):
            candidates.append((Path(real) / name[len(alias):]).as_posix())
    for item in candidates:
        for path in [item, *(item+ext for ext in (".ts", ".tsx", ".js", ".jsx", ".py", ".cs")), *((item+"/index"+ext) for ext in (".ts", ".tsx", ".js", ".jsx"))]:
            if path in paths:
                return path
    return None


def normalize_api_path(value):
    value = re.sub(r"\$\{[^}]+\}", "{var}", value.lower())
    value = re.sub(r"\{[^}]+\}", "{var}", value)
    value = value.split("?", 1)[0].strip("/")
    return "/" + "/".join(part for part in value.split("/") if part)


def import_bindings_from_text(text):
    bindings = []
    pattern = re.compile(r"\bimport\s+(?P<clause>[^;\n]+?)\s+from\s+[\"'](?P<source>[^\"']+)[\"']")
    for match in pattern.finditer(text):
        clause = match.group("clause").strip()
        names = []
        named = re.search(r"\{([^}]*)\}", clause)
        if named:
            for item in named.group(1).split(","):
                parts = re.split(r"\s+as\s+", item.strip())
                if parts and parts[0].strip():
                    names.append(parts[-1].strip())
        default = re.match(r"([A-Za-z_$][\w$]*)\s*(?:,|$)", clause)
        if default:
            names.append(default.group(1))
        namespace = re.search(r"\*\s+as\s+([A-Za-z_$][\w$]*)", clause)
        if namespace:
            names.append(namespace.group(1))
        bindings.append({"source": match.group("source"), "names": sorted(set(names))})
    return bindings


def call_sites_from_text(text, symbols):
    excluded = {"if", "for", "while", "switch", "catch", "using", "return", "new", "typeof", "nameof", "lock", "when"}
    pattern = re.compile(r"\b(?:(?P<receiver>[A-Za-z_$][\w$]*)\s*\.\s*)?(?P<name>[A-Za-z_$][\w$]*)\s*(?:<[^;\n()]*>)?\s*\(")
    declarations = {(item["start_line"], item["name"]) for item in symbols if item["kind"] == "function"}
    lines = text.splitlines()
    calls = []
    for match in pattern.finditer(text):
        line = text.count("\n", 0, match.start()) + 1
        name = match.group("name")
        if name in excluded or (line, name) in declarations:
            continue
        enclosing = [
            item for item in symbols
            if item["kind"] == "function" and item["start_line"] <= line <= item["end_line"]
        ]
        enclosing.sort(key=lambda item: item["end_line"] - item["start_line"])
        calls.append({
            "receiver": match.group("receiver"),
            "name": name,
            "line": line,
            "enclosing_symbol": enclosing[0]["name"] if enclosing else None,
        })
    unique = {(c["receiver"], c["name"], c["line"], c["enclosing_symbol"]): c for c in calls}
    return [unique[key] for key in sorted(unique, key=lambda item: (item[2], item[1], item[0] or ""))][:4000]


def typed_members_from_text(text):
    found = {}
    patterns = (
        re.compile(r"(?m)^\s*(?:private|public|protected|internal)\s+(?:readonly\s+)?(?P<type>[A-Z][\w.]*)\s+(?P<name>_[A-Za-z_]\w*)\s*(?:[;=])"),
        re.compile(r"\b(?P<type>[A-Z][\w.]*)\s+(?P<name>_?[a-z][A-Za-z0-9_]*)\s*(?=[,)=;])"),
    )
    for pattern in patterns:
        for match in pattern.finditer(text):
            line = text.count("\n", 0, match.start()) + 1
            item = {"name": match.group("name"), "type": match.group("type"), "line": line}
            found[(item["name"], item["type"], line)] = item
    return [found[key] for key in sorted(found)]


def interfaces_from_text(text):
    matches = []
    pattern = re.compile(r"\bclass\s+(?P<class>[A-Za-z_]\w*)\s*(?:<[^>]+>)?\s*:\s*(?P<bases>[^\n{]+)")
    for match in pattern.finditer(text):
        bases = [re.sub(r"<.*", "", value.strip()) for value in match.group("bases").split(",")]
        matches.extend({"class": match.group("class"), "interface": base} for base in bases if base.startswith("I") and len(base) > 1)
    return matches


def api_requests_from_text(text):
    pattern = re.compile(r"\bapi\.(?P<method>get|post|put|patch|delete)\s*(?:<[^>\n]*>)?\s*\(\s*(?P<quote>[\"'`])(?P<path>[^\"'`\n]+)(?P=quote)", re.I)
    return [
        {"method": match.group("method").upper(), "path": match.group("path"), "normalized_path": normalize_api_path(match.group("path")), "line": text.count("\n", 0, match.start()) + 1}
        for match in pattern.finditer(text)
    ]


def endpoints_from_text(rel, text):
    prefix_match = re.search(r"\[Route\(\s*[\"']([^\"']+)[\"']\s*\)\]", text)
    if not prefix_match:
        return []
    prefix = prefix_match.group(1)
    controller = re.sub(r"Controller$", "", Path(rel).stem, flags=re.I).lower()
    prefix = prefix.replace("[controller]", controller)
    attr_pattern = re.compile(r"\[Http(?P<method>Get|Post|Put|Patch|Delete)\s*(?:\(\s*[\"'](?P<suffix>[^\"']*)[\"']\s*\))?\s*\]", re.I)
    signature_pattern = re.compile(r"\b(?:public|private|protected|internal)\s+(?:static\s+)?(?:async\s+)?(?:[\w.<>,?\[\]]+\s+)+(?P<action>[A-Za-z_]\w*)\s*\(")
    endpoints = []
    for match in attr_pattern.finditer(text):
        following = text[match.end():match.end() + 1200]
        signature = signature_pattern.search(following)
        if not signature:
            continue
        suffix = match.group("suffix") or ""
        route = "/".join(part.strip("/") for part in (prefix, suffix) if part.strip("/"))
        endpoints.append({
            "method": match.group("method").upper(),
            "path": route,
            "normalized_path": normalize_api_path(route),
            "action": signature.group("action"),
            "line": text.count("\n", 0, match.start()) + 1,
        })
    return endpoints


def graph_edges(files, symbols, cfg):
    indexed = {f["path"]: f for f in files}
    def api_key(value):
        value = re.sub(r'\$\{[^}]+\}', '{var}', value.lower())
        value = re.sub(r'\{[^}]+\}|:[a-z_]+', '{var}', value)
        return '/'.join(x for x in value.split('?')[0].strip('/').split('/')[:2] if x)
    route_index = {}
    endpoint_index = {}
    for f in files:
        for r in f.get('routes', []):
            key = api_key(r)
            if key:
                route_index.setdefault(key,set()).add(f['path'])
        for endpoint in f.get("endpoints", []):
            endpoint_index.setdefault((endpoint["method"], endpoint["normalized_path"]), set()).add((f["path"], endpoint["action"]))
    ns_map = {}
    name_map = {}
    function_map = {}
    for f in files:
        if f["namespace"]:
            ns_map.setdefault(f["namespace"], []).append(f["path"])
        function_map[f["path"]] = set()
    for s in symbols:
        if s["kind"] == "function":
            function_map.setdefault(s["file"], set()).add(s["name"])
        if s["kind"] == "type":
            name_map.setdefault(s["name"], set()).add(s["file"])
    edges = {}
    def add(a,b,kind,confidence,evidence,from_symbol=None,to_symbol=None,allow_self=False):
        if (a == b and not allow_self) or a not in indexed or b not in indexed:
            return
        key = (a,b,kind,from_symbol,to_symbol)
        if key not in edges:
            item = {"from":a, "to":b, "relation":kind, "confidence":confidence, "evidence":evidence}
            if from_symbol:
                item["from_symbol"] = from_symbol
            if to_symbol:
                item["to_symbol"] = to_symbol
            edges[key] = item
    for f in files:
        a = f["path"]
        for imp in f["imports"]:
            b = resolve_import(a,imp,indexed,cfg)
            if b:
                add(a,b,"IMPORTS","verified-static-import",imp)
        for ns in f["usings"]:
            for candidate in ns_map.get(ns,[])[:60]:
                add(a,candidate,"USES_NAMESPACE","inferred-namespace",ns)
        # Explicit route strings and DbSet declarations provide useful cross-layer clues.
        for api in f.get('api_calls', []):
            for candidate in sorted(route_index.get(api_key(api), []))[:10]:
                add(a,candidate,"CALLS_API_ROUTE","inferred-route-prefix",api)
        for request in f.get("api_requests", []):
            for candidate, action in sorted(endpoint_index.get((request["method"], request["normalized_path"]), set())):
                add(a,candidate,"CALLS_API_ENDPOINT","source-method-and-route-match",
                    f"{request['method']} {request['normalized_path']} -> {action}",
                    from_symbol=request["method"],to_symbol=action)
        for binding in f.get("import_bindings", []):
            target = resolve_import(a, binding["source"], indexed, cfg)
            if not target:
                continue
            called = {call["name"] for call in f.get("call_sites", [])}
            for name in binding.get("names", []):
                if name in called and name in function_map.get(target, set()):
                    add(a,target,"CALLS_IMPORTED_SYMBOL","source-import-and-call",name,from_symbol=name,to_symbol=name)
        typed_members = {member["name"]: member["type"] for member in f.get("typed_members", [])}
        for call in f.get("call_sites", []):
            receiver = call.get("receiver")
            member_type = typed_members.get(receiver) if receiver else None
            if member_type:
                for candidate in sorted(name_map.get(member_type, set())):
                    if call["name"] in function_map.get(candidate, set()):
                        add(a,candidate,"CALLS_TYPED_METHOD","source-call-and-member-type",
                            f"{receiver}:{member_type}.{call['name']} at line {call['line']}",
                            from_symbol=call.get("enclosing_symbol"),to_symbol=call["name"])
            if call["name"] == "SaveChangesAsync" and call.get("enclosing_symbol") == "UpdateAsync":
                add(a,a,"PERSISTS_CHANGES","same-method-source-call",
                    f"{call['enclosing_symbol']} calls SaveChangesAsync at line {call['line']}",
                    from_symbol=call["enclosing_symbol"],to_symbol=call["name"],allow_self=True)
        for implementation in f.get("implements", []):
            for candidate in sorted(name_map.get(implementation["interface"], set())):
                if implementation["interface"] in {s["name"] for s in symbols if s["file"] == candidate and s["kind"] == "type"}:
                    add(a,candidate,"IMPLEMENTS_INTERFACE","source-class-declaration",
                        f"{implementation['class']} declares {implementation['interface']}",
                        from_symbol=implementation["class"],to_symbol=implementation["interface"])
        for entity in f.get('dbsets', []):
            for candidate in sorted(name_map.get(entity,()))[:10]:
                add(a,candidate,"PERSISTS_ENTITY","inferred-dbset-name",entity)
        # Type name usage can suggest linkage; never claim verified call relation.
        for term in f["references"]:
            for candidate in sorted(name_map.get(term,()))[:12]:
                add(a,candidate,"REFERENCES_TYPE","inferred-token-match",term)
    return sorted(edges.values(), key=lambda x:(x["from"],x["to"],x["relation"]))


def summarize(files, symbols, edges):
    members = {}
    for f in files:
        members.setdefault(f["module"], []).append(f)
    outgoing = {}
    incoming = {}
    lookup = {f["path"]: f["module"] for f in files}
    for e in edges:
        a,b = lookup[e["from"]], lookup[e["to"]]
        if a != b:
            outgoing.setdefault(a,set()).add(b)
            incoming.setdefault(b,set()).add(a)
    return {m:{"name":m,"files":sorted(f["path"] for f in fs),"file_count":len(fs),"tokens":sum(f["tokens"] for f in fs),"depends_on":sorted(outgoing.get(m,[])),"depended_on_by":sorted(incoming.get(m,[])),"kinds":{k:sum(f["kind"]==k for f in fs) for k in sorted({f["kind"] for f in fs})}} for m,fs in sorted(members.items())}


def mermaid_safe(s):
    return re.sub(r"[^a-zA-Z0-9_]", "_", s)[:60]


def diagram_system(modules, files, edges):
    lines = ["flowchart LR", "  %% Automatically generated; relationships may be inferred"]
    for m in modules:
        lines.append(f'  {mermaid_safe(m)}["{m} ({modules[m]["file_count"]} files)"]')
    files_by_path = {f["path"]: f for f in files}
    related = {(files_by_path[e["from"]]["module"], files_by_path[e["to"]]["module"]) for e in edges if files_by_path[e["from"]]["module"] != files_by_path[e["to"]]["module"]}
    for a,b in sorted(related):
        lines.append(f"  {mermaid_safe(a)} --> {mermaid_safe(b)}")
    return "\n".join(lines)+"\n"


def diagram_module(module, files, edges):
    selected = {f["path"] for f in files if f["module"] == module}
    # Per-module architecture includes capped file nodes, and external dependencies as module nodes.
    chosen = sorted(selected)[:60]
    nodes = {f:p for p,f in enumerate(chosen)}
    lines = ["flowchart LR", "  %% Module file detail capped at 60 nodes for readability"]
    for file,i in nodes.items():
        label = Path(file).name.replace('"', "'")[:40]
        lines.append(f'  f{i}["{label}"]')
    module_map = {f["path"]:f["module"] for f in files}
    external = set()
    for e in edges:
        if e["from"] not in nodes:
            continue
        if e["to"] in nodes:
            lines.append(f'  f{nodes[e["from"]]} --> f{nodes[e["to"]]}')
        elif e["to"] not in selected:
            external.add((nodes[e["from"]],module_map[e["to"]]))
    for i,mod in sorted(external):
        lines.append(f'  f{i} -.-> m_{mermaid_safe(mod)}["{mod} (external)"]')
    return "\n".join(dict.fromkeys(lines))+"\n"


def kb_root(root):
    return root / "docs" / "architecture"


def current_snapshot(root):
    kb = kb_root(root)
    cur = read_json(kb / "CURRENT.json")
    if not cur:
        return None
    name = cur.get("snapshot")
    if not isinstance(name,str) or not re.fullmatch(r"[A-Za-z0-9_-]+",name):
        raise ValueError("Invalid snapshot pointer")
    sp = kb / "snapshots" / name
    if not (sp / "manifest.json").exists():
        raise ValueError("Active snapshot missing; rerun init")
    return sp


def read_snapshot(root):
    sp = current_snapshot(root)
    if sp is None:
        return None
    return {"path":sp, "manifest": read_json(sp/"manifest.json"), "files":jsonl_read(sp/"indexes"/"files.jsonl"), "symbols":jsonl_read(sp/"indexes"/"symbols.jsonl"), "edges":jsonl_read(sp/"indexes"/"dependencies.jsonl")}


def publish(root, cfg, files, symbols, edges, stats):
    kb = kb_root(root)
    snap_name = dt.datetime.now(dt.timezone.utc).strftime("%Y%m%dT%H%M%S")+"-"+uuid.uuid4().hex[:7]
    sp = kb / "snapshots" / snap_name
    sp.mkdir(parents=True, exist_ok=False)
    files = sorted(files,key=lambda x:x["path"])
    symbols = sorted(symbols,key=lambda x:(x["file"],x["start_line"],x["name"]))
    edges = sorted(edges,key=lambda x:(x["from"],x["to"],x["relation"]))
    mods = summarize(files,symbols,edges)
    jsonl_write(sp/"indexes"/"files.jsonl",files)
    jsonl_write(sp/"indexes"/"symbols.jsonl",symbols)
    jsonl_write(sp/"indexes"/"dependencies.jsonl",edges)
    jsonl_write(sp/"indexes"/"tokens.jsonl",[{"path":f["path"],"tokens":f["tokens"],"method":f["token_method"],"approximate":f["approximate"]} for f in files])
    for m, data in mods.items():
        json_write(sp/"modules"/(slug(m)+".json"),data)
        atomic_write(sp/"diagrams"/"modules"/(slug(m)+".mmd"), diagram_module(m,files,edges))
    atomic_write(sp/"diagrams"/"system.mmd",diagram_system(mods,files,edges))
    manifest = {"schema_version": SCHEMA, "engine_version": ENGINE_VERSION, "graph_version": GRAPH_VERSION, "config_sha256": config_hash(cfg), "created_at":stamp(), "git_head":git_head(root), "analyzer":"UPI-v1.1-execution-flow-heuristic", "snapshot":snap_name,
                "files":len(files), "symbols":len(symbols), "edges":len(edges), "modules":len(mods),"scan":stats,
                "scope_note":"Index is advisory; current source is authoritative. Call and route edges are source-text evidence with explicit confidence; AST/compiler and runtime resolution are not supported."}
    json_write(sp/"manifest.json",manifest)
    # Validate complete candidate BEFORE switching active snapshot.
    if len(files) != len({f["path"] for f in files}) or any(e["from"] not in {f["path"] for f in files} or e["to"] not in {f["path"] for f in files} for e in edges):
        raise RuntimeError("Snapshot validation failed; active pointer unchanged")
    json_write(kb/"CURRENT.json", {"snapshot":snap_name,"schema_version":SCHEMA,"updated_at":stamp()})
    items = ["# Universal Project Intelligence — Architecture Index", "", f"Generated: {manifest['created_at']}", f"Active snapshot: `snapshots/{snap_name}/`", "", "**Start here after reading applicable AGENTS.md rules.** All architecture links below refer to the active snapshot. The live source code is authoritative.", "", "## Module registry", "", "| Module | Files | Indexed tokens | Dependencies |", "|---|---:|---:|---|"]
    for m, d in mods.items():
        items.append(f"| [{m}](snapshots/{snap_name}/modules/{slug(m)}.json) | {d['file_count']} | {d['tokens']:,} | {', '.join(d['depends_on']) or '—'} |")
    items += ["", "## Maps and lookup", "", f"- [System architecture](snapshots/{snap_name}/diagrams/system.mmd)", f"- [Files index](snapshots/{snap_name}/indexes/files.jsonl)", f"- [Symbols index](snapshots/{snap_name}/indexes/symbols.jsonl)", f"- [Dependency graph](snapshots/{snap_name}/indexes/dependencies.jsonl)", f"- [Token index](snapshots/{snap_name}/indexes/tokens.jsonl)", "", "## Task-first workflow", "", "```powershell", "python tools/upi/upi.py route --root . --task \"describe the task\" --budget 16000", "```", "", "The router checks index freshness and refreshes changed files before planning. Reading other modules requires a documented dependency or task requirement.", "", "## Integrity", "", f"- Indexed files: {len(files)}", f"- Indexed symbols (heuristic): {len(symbols)}", f"- Dependency edges (verified or inferred): {len(edges)}", f"- Changed files parsed this run: {stats.get('parsed',0)}", "- Token counts marked `estimated:chars/3.5` are approximations if tiktoken is unavailable.", "- Optional watcher: `python tools/upi/upi.py watch --root . --interval 5` (only runs while this command is active).", ""]
    atomic_write(kb/"INDEX.md","\n".join(items))
    return manifest


def scan(root, cfg, force=False):
    old = None if force else read_snapshot(root)
    oldfiles = {f["path"]:f for f in old["files"]} if old else {}
    oldsyms = {}
    if old:
        for s in old["symbols"]:
            oldsyms.setdefault(s["file"],[]).append(s)
    listed = list_source(root,cfg)
    changed = sorted(rel for rel,st in listed.items() if force or rel not in oldfiles or oldfiles[rel]["size_bytes"] != st["size_bytes"] or oldfiles[rel]["mtime_ns"] != st["mtime_ns"])
    removed = sorted(set(oldfiles)-set(listed))
    graph_changed=bool(old and old["manifest"].get("graph_version") != GRAPH_VERSION)
    if old and not changed and not removed and not graph_changed:
        stat={"parsed":0,"unchanged":len(listed),"deleted":0,"new":0,"changed_existing":0,"mode":"refresh"}
        return old["manifest"],stat,False
    files, symbols, errors = [],[],[]
    for rel,st in sorted(listed.items()):
        if rel in changed:
            try:
                file, syms = parse_file(root,rel,st,cfg)
            except (OSError, UnicodeError) as e:
                errors.append(f"{rel}: {e}")
                continue
        else:
            file,syms = oldfiles[rel],oldsyms.get(rel,[])
        files.append(file)
        symbols.extend(syms)
    if errors:
        raise RuntimeError("Cannot publish partial index: " + "; ".join(errors[:10]))
    edges = graph_edges(files,symbols,cfg)
    mode="init" if force or old is None else "graph-refresh" if graph_changed and not changed and not removed else "refresh"
    stat = {"parsed":len(changed),"unchanged":len(listed)-len(changed),"deleted":len(removed),"new":len(set(changed)-set(oldfiles)),"changed_existing":len(set(changed)&set(oldfiles)),"mode":mode}
    return publish(root,cfg,files,symbols,edges,stat),stat,True


def fresh(root,cfg):
    old = read_snapshot(root)
    if old is None or old["manifest"].get("schema_version") != SCHEMA or old["manifest"].get("config_sha256") != config_hash(cfg) or old["manifest"].get("engine_version") != ENGINE_VERSION:
        return scan(root,cfg,force=True)
    return scan(root,cfg,force=False)


def score_file(task, f, symbols, module, lookup):
    q = [v for v in words(task) if v not in STOP]
    # Keep project/projects as module signals even though generic in other contexts.
    query_full = normalize(task)
    stem = normalize(Path(f["path"]).stem)
    path_terms = {normalize(v) for v in Path(f["path"]).parts}
    mod = normalize(f["module"])
    pts = 0
    reasons=[]
    if f["module"] == module:
        pts += 45; reasons.append("primary-module")
    if mod and mod != "unclassified" and mod in query_full:
        pts += 15; reasons.append("module-mentioned")
    for term in q:
        n = normalize(term)
        if not n:
            continue
        if n in stem:
            pts += 12;reasons.append("filename-match:"+n)
        elif n in path_terms:
            pts += 5;reasons.append("path-match:"+n)
    for s in symbols.get(f["path"],[]):
        if any(normalize(term) in normalize(s["name"]) for term in q if len(normalize(term))>=4):
            pts += 4; reasons.append("symbol-match:"+s["name"])
            break
    if f["kind"] == "test":
        pts -= 8
    if f["module"] == "shared":
        pts -= 12
    if f["module"] not in (module,"shared"):
        pts -= 35
    return pts,sorted(set(reasons))


def determine_module(task, files, cfg):
    query = normalize(task)
    byname = set(f["module"] for f in files if f["module"] not in ("unclassified","shared"))
    scores = {}
    for module in byname:
        aliases = cfg["module_aliases"].get(module,[]) + [module]
        matches = [a for a in aliases if normalize(a) and normalize(a) in query]
        if matches:
            scores[module] = max(len(normalize(a)) for a in matches)
    # Prefer longest literal match (e.g., cost management vs project).
    if scores:
        return sorted(scores,key=lambda k:(-scores[k],k))[0],"task-explicit-alias"
    # Fallback lexical scoring over indexed filenames.
    q = set(words(task)) - STOP
    s = {}
    for f in files:
        terms = set(words(Path(f["path"]).stem))
        score = len(terms & q)
        if score:
            s[f["module"]] = s.get(f["module"],0)+score
    if s:
        return sorted(s,key=lambda k:(-s[k],k))[0],"task-filename-evidence"
    return "unclassified","no-supported-module-match"


def classify_critical_flow(task, cfg):
    query = normalize(task)
    actions = {normalize(item) for item in words(task)}
    flows = sorted(cfg.get("critical_flows", {}).items(), key=lambda item: (-item[1].get("priority", 0), item[0]))
    for flow_id, flow in flows:
        includes = any(normalize(alias) in query for alias in flow.get("match_any", []))
        excludes = any(normalize(alias) in query for alias in flow.get("exclude_any", []))
        action_match = any(normalize(action) in actions or normalize(action) in query for action in flow.get("action_any", []))
        if includes and not excludes and action_match:
            return flow_id, flow
    return None, None


def _normalized_set(values):
    return {normalize(item) for item in values}


def critical_step_matches(file, symbols, step):
    missing = []
    calls = file.get("call_sites", [])
    call_names = {call["name"] for call in calls}
    identifiers = set(file.get("identifiers", []))
    symbol_names = {symbol["name"] for symbol in symbols}
    for required in step.get("required_calls", []):
        if required not in call_names:
            missing.append("call:" + required)
    for required in step.get("required_identifiers", []):
        if required not in identifiers:
            missing.append("identifier:" + required)
    for required in step.get("required_symbols", []):
        if required not in symbol_names:
            missing.append("symbol:" + required)
    for request in step.get("required_api_requests", []):
        path = normalize_api_path(request["path"])
        if not any(item["method"] == request["method"].upper() and item["normalized_path"] == path for item in file.get("api_requests", [])):
            missing.append("api-request:" + request["method"].upper() + " " + path)
    for endpoint in step.get("required_endpoints", []):
        path = normalize_api_path(endpoint["path"])
        if not any(item["method"] == endpoint["method"].upper() and item["normalized_path"] == path and item["action"] == endpoint["action"] for item in file.get("endpoints", [])):
            missing.append("endpoint:" + endpoint["method"].upper() + " " + path + " " + endpoint["action"])
    for required in step.get("required_interfaces", []):
        if not any(item["interface"] == required for item in file.get("implements", [])):
            missing.append("implements:" + required)
    for required in step.get("required_method_calls", []):
        if not any(call.get("enclosing_symbol") == required["enclosing"] and call["name"] == required["method"] for call in calls):
            missing.append("method-call:" + required["enclosing"] + "->" + required["method"])
    for required in step.get("required_member_calls", []):
        has_member = any(item["name"] == required["receiver"] and item["type"] == required["type"] for item in file.get("typed_members", []))
        has_call = any(call.get("receiver") == required["receiver"] and call["name"] == required["method"] for call in calls)
        if not (has_member and has_call):
            missing.append("member-call:" + required["receiver"] + ":" + required["type"] + "." + required["method"])
    return not missing, missing


def evaluate_critical_flow(flow_id, flow, files, symbols_by_path, edges):
    lookup = {item["path"]: item for item in files}
    steps = []
    candidates_by_step = {}
    for step in flow.get("steps", []):
        candidates = [
            file for file in files
            if file["module"] == flow.get("module")
            and any(fnmatch.fnmatch(file["path"], pattern) for pattern in step.get("paths", []))
        ]
        matches = []
        failures = []
        for file in candidates:
            ok, missing = critical_step_matches(file, symbols_by_path.get(file["path"], []), step)
            if ok:
                matches.append(file)
            else:
                failures.extend(missing)
        chosen = matches or candidates
        candidates_by_step[step["id"]] = [file["path"] for file in chosen]
        steps.append({
            "id": step["id"],
            "required": True,
            "status": "covered" if matches else "missing",
            "matched_files": [file["path"] for file in matches],
            "candidate_files": [file["path"] for file in candidates],
            "missing_evidence": sorted(set(failures)) if not matches else [],
        })
    by_step = {item["id"]: item for item in steps}
    relationships = []
    for rule in flow.get("relations", []):
        from_files = candidates_by_step.get(rule["from"], [])
        to_files = candidates_by_step.get(rule["to"], [])
        found = [
            edge for edge in edges
            if edge["relation"] == rule["relation"]
            and edge["from"] in from_files
            and edge["to"] in to_files
            and (not rule.get("match_evidence") or rule["match_evidence"] in edge["evidence"])
            and (not rule.get("from_symbol") or rule["from_symbol"] == edge.get("from_symbol"))
            and (not rule.get("to_symbol") or rule["to_symbol"] == edge.get("to_symbol"))
        ]
        fallback_uncertainty = None
        if not found and rule["relation"] == "CALLS_IMPORTED_SYMBOL" and rule.get("match_evidence"):
            expected_call = rule["match_evidence"]
            for source_path in from_files:
                for target_path in to_files:
                    has_import = any(edge["relation"] == "IMPORTS" and edge["from"] == source_path and edge["to"] == target_path for edge in edges)
                    has_call = any(call["name"] == expected_call for call in lookup[source_path].get("call_sites", []))
                    exports_symbol = any(symbol["name"] == expected_call and symbol["kind"] == "function" for symbol in symbols_by_path.get(target_path, []))
                    if has_import and has_call and exports_symbol:
                        found.append({
                            "from": source_path,
                            "to": target_path,
                            "relation": rule["relation"],
                            "confidence": "inferred-import-call-symbol-match",
                            "evidence": f"Resolved import plus {expected_call} call and matching target function symbol",
                        })
                        fallback_uncertainty = "The import path, call name, and target function align, but the imported binding name was not indexed from this source file."
                        break
                if found:
                    break
        confidence = sorted({edge["confidence"] for edge in found})
        uncertainty = rule.get("uncertain_note") or fallback_uncertainty
        if not uncertainty and any(value.startswith("inferred") for value in confidence):
            uncertainty = "This relationship is inferred from indexed source text and was not compiler-resolved."
        relationships.append({
            "id": rule["id"],
            "from_step": rule["from"],
            "to_step": rule["to"],
            "relation": rule["relation"],
            "required": bool(rule.get("required", True)),
            "status": "covered" if found else "missing",
            "evidence": [edge["evidence"] for edge in found[:5]],
            "confidence": confidence,
            "uncertain": bool(found and uncertainty),
            "uncertainty": uncertainty if found else None,
        })
    covered_steps = sum(item["status"] == "covered" for item in steps)
    required_steps = sum(item.get("required", True) for item in steps)
    missing_relationships = [item["id"] for item in relationships if item["required"] and item["status"] != "covered"]
    missing_steps = [item["id"] for item in steps if item["required"] and item["status"] != "covered"]
    unconfirmed = [item["id"] for item in relationships if item["uncertain"]]
    complete = not missing_steps and not missing_relationships
    return {
        "active": True,
        "flow_id": flow_id,
        "title": flow.get("title", flow_id),
        "module": flow.get("module"),
        "complete": complete,
        "coverage_percent": round(covered_steps * 100 / required_steps) if required_steps else 100,
        "covered_steps": covered_steps,
        "required_steps": required_steps,
        "steps": steps,
        "relationships": relationships,
        "unconfirmed_relationships": unconfirmed,
        "missing_steps": missing_steps,
        "missing_relationships": missing_relationships,
        "gate": {
            "passed": complete,
            "blocks_completion": not complete,
            "reason": "All critical steps and required source relationships are indexed." if complete else "Critical path evidence is missing; scope must not be declared complete.",
        },
        "_candidate_paths_by_step": candidates_by_step,
    }


def critical_ranges_for_file(root, file, flow, symbols_by_path):
    path = file["path"]
    ranges = []
    reasons = []
    for step in flow.get("steps", []):
        if not any(fnmatch.fnmatch(path, pattern) for pattern in step.get("paths", [])):
            continue
        syms = symbols_by_path.get(path, [])
        wanted = set(step.get("read_symbols", []))
        for symbol in syms:
            if symbol["name"] in wanted:
                ranges.append([symbol["start_line"], symbol["end_line"]])
        wanted_calls = set(step.get("read_calls", []))
        if wanted_calls:
            try:
                lines = (root / path).read_text(encoding="utf-8", errors="replace").splitlines()
            except OSError:
                lines = []
            context = int(step.get("context_lines", 8))
            anchor_names = set(step.get("read_anchor_calls", []))
            anchors = [call["line"] for call in file.get("call_sites", []) if call["name"] in anchor_names]
            anchor_distance = int(step.get("read_anchor_distance", 200))
            for call in file.get("call_sites", []):
                expected_enclosing = step.get("read_call_enclosing")
                if call["name"] in wanted_calls and (not expected_enclosing or call.get("enclosing_symbol") == expected_enclosing):
                    if anchors and min(abs(call["line"] - anchor) for anchor in anchors) > anchor_distance:
                        continue
                    ranges.append([max(1, call["line"] - context), min(len(lines) or file["lines"], call["line"] + context)])
        if wanted or wanted_calls:
            reasons.append("critical-path-step:" + step["id"])
    if not ranges:
        return [[1, file["lines"]]], reasons
    ranges.sort()
    merged = []
    for start, end in ranges:
        if merged and start <= merged[-1][1] + 1:
            merged[-1][1] = max(merged[-1][1], end)
        else:
            merged.append([start, end])
    return merged, reasons


def token_cost_for_ranges(root, file, ranges, cfg):
    if ranges == [[1, file["lines"]]]:
        return file["tokens"], file["token_method"], file["approximate"]
    try:
        lines = (root / file["path"]).read_text(encoding="utf-8", errors="replace").splitlines(keepends=True)
    except OSError:
        return file["tokens"], file["token_method"], file["approximate"]
    chunks = ["".join(lines[max(0, start - 1):min(len(lines), end)]) for start, end in ranges]
    return count_tokens("\n".join(chunks), cfg)


def route(root,cfg,task,budget=16000,write=True,max_files=30):
    manifest,stat,updated=fresh(root,cfg)
    snap=read_snapshot(root)
    files=snap["files"]
    sym_by_path={}
    for s in snap["symbols"]:
        sym_by_path.setdefault(s["file"],[]).append(s)
    module,reason=determine_module(task,files,cfg)
    flow_id,flow=classify_critical_flow(task,cfg)
    if flow and flow.get("module") != module:
        flow_id,flow=None,None
    lookup={f["path"]:f for f in files}
    coverage=evaluate_critical_flow(flow_id,flow,files,sym_by_path,snap["edges"]) if flow else None
    critical_by_path={}
    critical_order=[]
    if coverage:
        for step in coverage["steps"]:
            candidates=step["matched_files"] or step["candidate_files"]
            for path in candidates:
                critical_by_path.setdefault(path,[]).append(step["id"])
                if path not in critical_order:
                    critical_order.append(path)
    scored=[]
    for f in files:
        pts,why=score_file(task,f,sym_by_path,module,{})
        if pts > 0:
            scored.append((pts,f,why))
    scored.sort(key=lambda x:(-x[0],x[1]["path"]))
    # Never load all files of a module just because the module itself matched.
    # A file must have task-specific filename or symbol evidence; otherwise
    # the router returns a small discovery fallback and asks the agent to inspect
    # the module manifest before expanding.
    primary=[(p,f,w) for p,f,w in scored if f["module"] == module and any(r.startswith(("filename-match:", "symbol-match:")) for r in w)]
    if not primary:
        primary=[(p,f,w+["low-confidence-module-fallback"]) for p,f,w in scored if f["module"] == module][:3]
    if coverage:
        critical=[]
        for path in critical_order:
            file=lookup.get(path)
            if not file or file["module"] != module:
                continue
            pts,why=score_file(task,file,sym_by_path,module,{})
            why=sorted(set(why+["critical-path-step:"+step for step in critical_by_path[path]]))
            critical.append((pts+1000,file,why))
        primary=critical
    else:
        primary=primary[:max_files]
    selected=[]
    source_est=0
    # Soft reading budget: reserve 40% for instructions, tools, reasoning and follow-up.
    source_cap=round(budget*.60)
    qterms=[normalize(x) for x in words(task) if len(normalize(x))>=4 and x not in STOP]
    for pts,f,why in primary:
        is_critical=f["path"] in critical_by_path if coverage else False
        if source_est >= source_cap and selected and not is_critical:
            break
        if is_critical:
            ranges,_=critical_ranges_for_file(root,f,flow,sym_by_path)
            cost,token_method,approximate=token_cost_for_ranges(root,f,ranges,cfg)
            granularity="critical-path"
        else:
            matches=[s for s in sym_by_path.get(f["path"],[]) if any(t in normalize(s["name"]) for t in qterms)]
            # Symbol chunks may overlap. Use cached source counts only after path coverage is decided.
            chunks=sorted(matches,key=lambda s:(s["start_line"],s["end_line"]))[:5]
            nonoverlap = all(chunks[i]["end_line"] < chunks[i+1]["start_line"] for i in range(len(chunks)-1))
            if chunks and nonoverlap and f["tokens"]>sum(c["tokens"] for c in chunks)*1.3:
                cost=sum(c["tokens"] for c in chunks)
                granularity="symbols"
                ranges=[[s["start_line"],s["end_line"]] for s in chunks]
            else:
                cost=f["tokens"];granularity="full-file";ranges=[[1,f["lines"]]]
            token_method=f["token_method"];approximate=f["approximate"]
        if source_est+cost > source_cap and selected and not is_critical:
            # Mark overflow; don't silently omit all huge files.
            continue
        selected.append({"path":f["path"],"kind":f["kind"],"module":f["module"],"score":pts,"reasons":why,"read_mode":granularity,"line_ranges":ranges,"reading_tokens":cost,"token_method":token_method,"approximate":approximate})
        source_est+=cost
    selected_paths={x["path"] for x in selected}
    reverse=[];cross=[]
    for e in snap["edges"]:
        if e["to"] in selected_paths and e["from"] not in selected_paths:
            reverse.append(e)
        if e["from"] in selected_paths and e["to"] not in selected_paths:
            cross.append(e)
    # Do not automatically open dependent modules: present relevant edges for awareness.
    awareness=[]
    for e in cross[:30]+reverse[:30]:
        other=e["to"] if e["from"] in selected_paths else e["from"]
        if other not in selected_paths:
            awareness.append({"path":other,"module":lookup[other]["module"],"relation":e["relation"],"confidence":e["confidence"],"evidence":e["evidence"]})
    awareness=list({x["path"]+x["relation"]:x for x in awareness}.values())[:30]
    tests=[f["path"] for f in files if f["kind"]=="test" and f["module"]==module]
    if coverage:
        critical_tests=[]
        for step in coverage["steps"]:
            if "test" in step["id"]:
                critical_tests.extend(step["matched_files"] or step["candidate_files"])
        tests=list(dict.fromkeys(critical_tests+[path for _,file,_ in sorted(scored,key=lambda item:(-item[0],item[1]["path"])) for path in [file["path"]] if file["kind"]=="test" and file["module"]==module]))
    tests=tests[:30]
    excluded=sorted({f["module"] for f in files if f["module"] not in (module,"shared")})
    symbol_count=sum(1 for symbol in snap["symbols"] if symbol["file"] in selected_paths and any(symbol["start_line"] <= end and symbol["end_line"] >= start for item in selected if item["path"]==symbol["file"] for start,end in item["line_ranges"]))
    if coverage:
        coverage.pop("_candidate_paths_by_step",None)
        coverage["selected_critical_files"]=[path for path in selected_paths if path in critical_by_path]
        coverage["selected_critical_file_count"]=len(coverage["selected_critical_files"])
        coverage["selected_symbol_count"]=symbol_count
        coverage["selected_reading_tokens"]=sum(item["reading_tokens"] for item in selected if item["path"] in critical_by_path)
    if coverage and not coverage["gate"]["passed"]:
        planning_state="critical-path-incomplete"
    elif source_est>source_cap:
        planning_state="budget-overflow"
    elif coverage and coverage["unconfirmed_relationships"]:
        planning_state="focused-with-uncertainty"
    else:
        planning_state="needs-targeted-discovery" if not selected or all("low-confidence-module-fallback" in f["reasons"] for f in selected) else "focused"
    out={"task":task,"created_at":stamp(),"snapshot":snap["manifest"]["snapshot"],"primary_module":module,"module_selection_basis":reason,
         "selected_files":selected,"dependency_awareness_only":awareness,"related_tests_candidates":tests,"nonprimary_modules_not_read_by_router":excluded,
         "critical_path_coverage":coverage,
         "selection_summary":{"files":len(selected),"symbols":symbol_count,"tokens":source_est,"critical_path_coverage_percent":coverage["coverage_percent"] if coverage else None},
         "token_plan":{"source_reading_tokens":source_est,"reserved_non_source_budget":budget-source_cap,"available_source_budget":source_cap,"requested_total_budget":budget,
                       "estimation_order":"Critical-path coverage is evaluated before reading-cost calculation and optional file ranking.",
                       "warning":"An estimate of selected source reading, NOT full task execution or model hidden reasoning. Some counts may be approximate."},
         "planning_state":planning_state,
         "limits":["Router never prevents an agent from manually opening other files; enforce via agent instructions and audit.","C# and TS relationships are source-text heuristics; compiler-resolved calls and runtime dependency-injection resolution are not proven.","Unknown/unclassified files may require manual targeted inspection.","Index sources excluded secrets and build outputs."],
         "refresh":stat,"index_updated":updated}
    if write:
        target=kb_root(root)/"task-scopes"/(dt.datetime.now(dt.timezone.utc).strftime("%Y%m%dT%H%M%S")+"-"+slug(task)[:50]+"-"+uuid.uuid4().hex[:5])
        json_write(target.with_suffix(".json"),out)
        lines=["# UPI Task Context Manifest", "",f"Task: **{task}**",f"Primary module: **{module}** ({reason})",f"Architecture snapshot: `{out['snapshot']}`", "", "## Selected source reading", "", "| File | Range(s) | Tokens | Why |", "|---|---|---:|---|"]
        for x in selected:
            lines.append(f"| `{x['path']}` | {', '.join(f'{a}-{b}' for a,b in x['line_ranges'])} | {x['reading_tokens']} | {', '.join(x['reasons'])} |")
        lines += ["", "## Dependency awareness (not automatically read)", ""]
        for x in awareness:
            lines.append(f"- `{x['path']}` — {x['relation']} ({x['confidence']}; {x['evidence']})")
        lines += ["", "## Critical path coverage", ""]
        if coverage:
            lines.append(f"- Gate: **{'PASS' if coverage['gate']['passed'] else 'BLOCKED'}**; {coverage['covered_steps']}/{coverage['required_steps']} steps ({coverage['coverage_percent']}%)")
            for step in coverage["steps"]:
                lines.append(f"- `{step['id']}`: {step['status']}; {', '.join(step['matched_files'] or step['candidate_files']) or 'no indexed file'}")
            for relationship in coverage["relationships"]:
                suffix = " — UNCERTAIN: " + relationship["uncertainty"] if relationship.get("uncertainty") else ""
                lines.append(f"- Relationship `{relationship['id']}`: {relationship['status']} ({', '.join(relationship['confidence']) or 'no source evidence'}){suffix}")
        else:
            lines.append("- Not applicable to this task.")
        lines += ["", "## Budget", "", f"- Initial selected source: **{source_est}** tokens (method varies per file)",f"- Requested planning budget: **{budget}** tokens",f"- Other-context allowance: **{budget-source_cap}** tokens", "", "Critical-path coverage is assessed before reading-cost calculation and optional file ranking. The predicted number is NOT total actual task token consumption; expand scope with documented reasons only.", ""]
        atomic_write(target.with_suffix(".md"),"\n".join(lines))
        out["report_path"]=target.with_suffix(".md").relative_to(root).as_posix()
    return out


def validate(root,cfg,check_freshness=True):
    snap=read_snapshot(root)
    if snap is None:
        return {"ok":False,"errors":["No index found; run init"],"warnings":[]}
    errors=[]; warnings=[]
    lookup={x["path"]:x for x in snap["files"]}
    if len(lookup) != len(snap["files"]):
        errors.append("Duplicate file paths")
    if snap["manifest"].get("schema_version")!=SCHEMA:
        errors.append("Invalid schema version")
    if snap["manifest"].get("config_sha256") != config_hash(cfg):
        warnings.append("Configuration has changed; refresh required")
    if snap["manifest"].get("engine_version") != ENGINE_VERSION:
        warnings.append("Analyzer version changed; refresh required")
    if snap["manifest"].get("graph_version") != GRAPH_VERSION:
        warnings.append("Dependency graph version changed; refresh required")
    for e in snap["edges"]:
        if e["from"] not in lookup or e["to"] not in lookup:
            errors.append("Dangling graph edge")
            break
    for s in snap["symbols"]:
        if s["file"] not in lookup:
            errors.append("Orphaned symbol")
            break
    if check_freshness:
        listed=list_source(root,cfg)
        for path,f in lookup.items():
            if path not in listed:
                warnings.append("Stale (deleted): "+path)
            elif listed[path]["mtime_ns"]!=f["mtime_ns"] or listed[path]["size_bytes"]!=f["size_bytes"]:
                warnings.append("Stale (modified): "+path)
        for path in set(listed)-set(lookup):
            warnings.append("Stale (new): "+path)
    return {"ok":not errors,"errors":errors,"warnings":warnings[:100],"files":len(lookup),"symbols":len(snap["symbols"]),"edges":len(snap["edges"]),"snapshot":snap["manifest"]["snapshot"]}


def main(argv=None):
    p=argparse.ArgumentParser(prog="upi",description="Universal Project Intelligence — persistent architecture index and token-aware task router")
    p.add_argument("command",choices=["init","refresh","route","validate","status","watch","explain"])
    p.add_argument("--root",default=".",help="Repository root")
    p.add_argument("--task",help="Natural-language task for ROUTE")
    p.add_argument("--module",help="Module for EXPLAIN")
    p.add_argument("--budget",type=int,default=16000,help="Illustrative total context token budget")
    p.add_argument("--max-files",type=int,default=30)
    p.add_argument("--interval",type=float,default=5.0,help="Watcher polling interval seconds")
    p.add_argument("--once",action="store_true",help="Watcher: one pass then exit")
    p.add_argument("--no-write",action="store_true",help="Router: do not save task report")
    args=p.parse_args(argv)
    try:
        root=repo_root(args.root)
        cfg=load_config(root)
        if args.command in ("init","refresh"):
            manifest,stats,updated=scan(root,cfg,force=(args.command=="init")) if args.command=="init" else fresh(root,cfg)
            print(json.dumps({"updated":updated,"manifest":manifest,"scan":stats},indent=2))
        elif args.command=="route":
            if not args.task:
                p.error("--task is required for route")
            if args.budget < 100:
                p.error("--budget must be >=100")
            # JSON stdout must remain encodable in Windows legacy code pages;
            # task manifests themselves are written as UTF-8.
            print(json.dumps(route(root,cfg,args.task,args.budget,write=not args.no_write,max_files=args.max_files),ensure_ascii=True,indent=2))
        elif args.command=="validate":
            result=validate(root,cfg)
            print(json.dumps(result,indent=2))
            if not result["ok"]:
                return 2
            if result["warnings"]:
                return 3
        elif args.command=="status":
            result=validate(root,cfg)
            print(json.dumps(result,indent=2))
        elif args.command=="explain":
            _,_,_=fresh(root,cfg)
            snap=current_snapshot(root)
            if not args.module:
                p.error("--module is required for explain")
            f=snap/"modules"/(slug(args.module)+".json")
            if not f.exists():
                raise ValueError(f"Unknown module: {args.module}")
            print(f.read_text(encoding="utf-8"))
            print("Diagram:",snap/"diagrams"/"modules"/(slug(args.module)+".mmd"))
        elif args.command=="watch":
            print(f"Watching {root} every {args.interval}s; Ctrl+C to stop. No background daemon is installed.",flush=True)
            while True:
                m,stat,updated=fresh(root,cfg)
                if updated:
                    print(json.dumps({"time":stamp(),"updated":True,"scan":stat}),flush=True)
                if args.once:
                    break
                time.sleep(max(.5,args.interval))
    except KeyboardInterrupt:
        return 0
    except (RuntimeError,ValueError,OSError, json.JSONDecodeError) as e:
        print("UPI error:",str(e),file=sys.stderr)
        return 2
    return 0

if __name__=="__main__":
    sys.exit(main())
