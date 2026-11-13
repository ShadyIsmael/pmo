import re
import csv
import json

# Read the PermissionCatalog.cs file
with open(r'apps\api\src\NEXUS.Modules.Identity\Application\PermissionCatalog.cs', 'r', encoding='utf-8') as f:
    content = f.read()

# Find the Catalog array content
start_marker = 'private static readonly IReadOnlyList<PermissionCatalogEntry> Catalog ='
start = content.find(start_marker)
if start == -1:
    start = content.find('private static readonly IReadOnlyList<PermissionCatalogEntry> Catalog')

# Find the closing bracket for the Catalog array
# We need to find the matching closing bracket
bracket_count = 0
catalog_start = content.find('[', start)
catalog_end = catalog_start
for i, char in enumerate(content[catalog_start:], catalog_start):
    if char == '[':
        bracket_count += 1
    elif char == ']':
        bracket_count -= 1
        if bracket_count == 0:
            catalog_end = i + 1
            break

catalog_content = content[catalog_start:catalog_end]

# Extract all new(...) entries - each entry starts with 'new(' and has a SeedId
# Pattern to match each entry
entry_pattern = r'new\s*\(\s*"([^"]+)"\s*,\s*"([^"]+)"\s*,\s*"([^"]+)"\s*,\s*"([^"]+)"\s*,\s*"([^"]+)"\s*,\s*"([^"]+)"\s*,\s*"([^"]+)"\s*,\s*"([^"]+)"\s*,\s*"([^"]+)"\s*,\s*PermissionLevel\.(\w+)\s*,\s*"([^"]+)"\s*,\s*"([^"]+)"\s*,\s*"([^"]+)"\s*,\s*"([^"]+)"\s*,\s*"([^"]+)"\s*,\s*(true|false)\s*,\s*\[([^\]]*)\]\s*,\s*(true|false)(?:\s*,\s*PermissionScope\.(\w+))?(?:\s*,\s*IsAssignable:\s*(true|false))?(?:\s*,\s*IsTenantAdminBaseline:\s*(true|false))?\s*\)'

entries = re.findall(entry_pattern, catalog_content, re.MULTILINE | re.DOTALL)

print(f'Found {len(entries)} entries')

# Also try a simpler approach - find all SeedIds
seed_ids = re.findall(r'"(b000\d{12})"', catalog_content)
print(f'Seed IDs found: {len(seed_ids)}')

# Let's also try to find each entry by looking for the pattern
# Each entry has: SeedId, Key, Resource, Action, ModuleId, ModuleNameEn, ModuleNameAr, ScreenId, ScreenNameEn, ScreenNameAr, PermissionLevel, LabelEn, LabelAr, DescriptionEn, DescriptionAr, Sensitivity, IsSensitive, RequiredPermissionKeys[], IsActive, [Scope], [IsAssignable], [IsTenantAdminBaseline]

# Write results to CSV
with open(r'docs\security\permission-audit\PERMISSION-CATALOG-INVENTORY.csv', 'w', newline='', encoding='utf-8') as csvfile:
    writer = csv.writer(csvfile)
    writer.writerow([
        'SeedId', 'CanonicalKey', 'Resource', 'Action', 'ModuleId', 'ModuleNameEn', 'ModuleNameAr',
        'ScreenId', 'ScreenNameEn', 'ScreenNameAr', 'PermissionLevel', 'LabelEn', 'LabelAr',
        'DescriptionEn', 'DescriptionAr', 'Sensitivity', 'IsSensitive', 'RequiredPermissionKeys',
        'IsActive', 'Scope', 'IsAssignable', 'IsTenantAdminBaseline', 'SourceFile', 'SourceLine'
    ])
    
    for i, entry in enumerate(entries):
        seed_id, key, resource, action, module_id, module_name_en, module_name_ar, screen_id, screen_name_en, screen_name_ar, perm_level, label_en, label_ar, desc_en, desc_ar, sensitivity, is_sensitive, req_keys_str, is_active, scope, is_assignable, is_baseline = entry
        
        # Parse required permission keys
        req_keys = [k.strip().strip('"') for k in req_keys_str.split(',') if k.strip()] if req_keys_str.strip() else []
        
        # Default values
        scope = scope or 'Tenant'
        is_assignable = is_assignable if is_assignable != '' else 'true'
        is_baseline = is_baseline if is_baseline != '' else 'false'
        
        writer.writerow([
            seed_id, key, resource, action, module_id, module_name_en, module_name_ar,
            screen_id, screen_name_en, screen_name_ar, perm_level, label_en, label_ar,
            desc_en, desc_ar, sensitivity, is_sensitive, ';'.join(req_keys),
            is_active, scope, is_assignable, is_baseline,
            'apps/api/src/NEXUS.Modules.Identity/Application/PermissionCatalog.cs', f'Line ~{27 + i * 12}'
        ])

print('CSV written successfully')

# Also print summary
active_count = sum(1 for e in entries if e[18] == 'true')
inactive_count = len(entries) - active_count
view_count = sum(1 for e in entries if e[10] == 'View')
action_count = sum(1 for e in entries if e[10] == 'Action')

print(f'\nSummary:')
print(f'Total entries: {len(entries)}')
print(f'Active: {active_count}')
print(f'Inactive: {inactive_count}')
print(f'View: {view_count}')
print(f'Action: {action_count}')

# Module breakdown
modules = {}
for e in entries:
    mod = e[4]
    modules[mod] = modules.get(mod, 0) + 1

print('\nModule breakdown:')
for mod, count in sorted(modules.items()):
    print(f'  {mod}: {count}')

# Scope breakdown
scopes = {}
for e in entries:
    scope = e[19] or 'Tenant'
    scopes[scope] = scopes.get(scope, 0) + 1

print('\nScope breakdown:')
for scope, count in sorted(scopes.items()):
    print(f'  {scope}: {count}')

# Baseline count
baseline_count = sum(1 for e in entries if (e[21] or 'false') == 'true')
print(f'\nIsTenantAdminBaseline: {baseline_count}')

# Duplicate check
keys = [e[1] for e in entries]
duplicates = [k for k in set(keys) if keys.count(k) > 1]
print(f'\nDuplicate canonical keys: {len(duplicates)}')
for d in duplicates:
    print(f'  {d}')