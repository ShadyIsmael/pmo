const fs = require('node:fs');

const csvPath = 'docs/security/PERMISSIONS-TARGET-MATRIX.csv';
const seedPath = 'apps/api/src/NEXUS.Persistence/SeedDataProvider.cs';
const outputPath = 'apps/api/src/NEXUS.Modules.Identity/Application/PermissionCatalog.cs';

function parseCsv(input) {
  const rows = [];
  let row = [];
  let value = '';
  let quoted = false;
  for (let i = 0; i < input.length; i += 1) {
    const char = input[i];
    if (quoted) {
      if (char === '"' && input[i + 1] === '"') { value += '"'; i += 1; }
      else if (char === '"') quoted = false;
      else value += char;
    } else if (char === '"') quoted = true;
    else if (char === ',') { row.push(value); value = ''; }
    else if (char === '\n') { row.push(value.replace(/\r$/, '')); rows.push(row); row = []; value = ''; }
    else value += char;
  }
  if (value || row.length) { row.push(value); rows.push(row); }
  const headers = rows.shift();
  return rows.filter((item) => item.length).map((item) => Object.fromEntries(headers.map((header, i) => [header, item[i] ?? ''])));
}

const screenAr = {
  'Directory integration': 'تكامل الدليل', 'Reference data': 'البيانات المرجعية',
  'Roles and permissions': 'الأدوار والصلاحيات', 'Tenant and organization settings': 'إعدادات المستأجر والمؤسسة',
  'Users and access': 'المستخدمون والوصول', Resources: 'الموارد', Teams: 'الفرق', 'Time tracking': 'تسجيل الوقت',
  'Home / Dashboards': 'الرئيسية / لوحات المعلومات', 'Change requests': 'طلبات التغيير', Documents: 'المستندات',
  Handovers: 'تسليم الإدارة', Meetings: 'الاجتماعات', Planning: 'التخطيط', Portfolios: 'المحافظ', Programs: 'البرامج',
  'Project stakeholders': 'أصحاب المصلحة بالمشروع', 'Project status reports': 'تقارير حالة المشروع', Projects: 'المشروعات',
  'Risk management': 'إدارة المخاطر', 'Work items': 'عناصر العمل', 'Workflow administration': 'إدارة سير العمل',
  'Annual Budgets': 'الموازنات السنوية', 'Financial overview and planned payments': 'نظرة عامة مالية والمدفوعات المخططة',
  'Fiscal Year Closing': 'إقفال السنة المالية', 'Legacy financial write umbrella': 'صلاحية مالية عامة قديمة',
  'Payment Requests': 'طلبات الدفع', 'Administrative Assignments and Handovers': 'الإسنادات الإدارية وتسليم الإدارة',
  'Assignment recovery': 'استعادة الإسنادات', Reports: 'التقارير', 'Audit trail': 'سجل التدقيق',
};
const moduleAr = {
  Administration: 'الإدارة', Capacity: 'السعة', 'Dashboards and Reporting': 'لوحات المعلومات والتقارير',
  Delivery: 'تنفيذ المشاريع', 'Financial Management': 'الإدارة المالية', Governance: 'الحوكمة',
  'Portfolio & Program Management': 'إدارة المحافظ والبرامج', 'Program Management': 'إدارة البرامج',
  'Project Delivery': 'تسليم المشاريع', Reporting: 'التقارير', Security: 'الأمان',
};

const rows = parseCsv(fs.readFileSync(csvPath, 'utf8'));
console.log('parsed csv', rows.length);
const seedSource = fs.readFileSync(seedPath, 'utf8');
const previousCatalogPath = 'apps/api/src/NEXUS.Modules.Identity/Application/PermissionCatalog.cs';
const previousCatalog = fs.existsSync(previousCatalogPath) ? fs.readFileSync(previousCatalogPath, 'utf8') : '';
const seedIds = new Map();
for (const match of seedSource.matchAll(/Id\s*=\s*Guid\.Parse\("([^"]+)"\)[^\n]*?Resource\s*=\s*"([^"]+)"\s*,\s*Action\s*=\s*"([^"]+)"/g)) {
  seedIds.set(`${match[2]}.${match[3]}`, match[1]);
}
for (const match of previousCatalog.matchAll(/new\(\s*"([^"]+)"\s*,\s*"([^"]+)"\s*,\s*"([^"]+)"\s*,\s*"([^"]+)"/g)) {
  if (!seedIds.has(match[2])) seedIds.set(match[2], match[1]);
}
console.log('parsed seed ids', seedIds.size);

const retired = new Map([
  ['projects.read', 'Project.View'], ['portfolios.read', 'Portfolio.View'], ['resources.read', 'Resource.View'],
  ['projects.write', ''], ['projects.delete', 'Project.Delete'], ['portfolios.write', ''], ['resources.write', ''],
  ['reports.export', ''], ['manager.handover', ''], ['Resource.Manage', ''], ['Team.Manage', ''],
  ['Directory.Manage', ''], ['Planning.Manage', ''], ['users.write', ''], ['settings.write', ''],
  ['Program.Manage', ''], ['Portfolio.Manage', ''],
]);

const definitions = new Map();
for (const row of rows) {
  const existing = row['Existing Key'].trim();
  const proposed = row['Proposed Key'].trim();
  const module = row.Module.trim();
  const displayModule = ({ Delivery: 'Project Delivery', 'Program Management': 'Portfolio & Program Management', Security: 'Administration', 'Dashboards and Reporting': 'Reporting' })[module] || module;
  const sourceScreen = row.Screen.trim();
  const level = row.Level.trim() === 'View' ? 'View' : 'Action';
  const risk = row['Security Risk'].trim() || 'Low';
  const addDefinition = (key, isNew) => {
    if (!key || !/^[A-Za-z][A-Za-z0-9_.-]*\.[A-Za-z0-9_.-]+$/.test(key) || definitions.has(key)) return;
    const screen = key === 'finances.write' ? 'Financial overview and planned payments' : sourceScreen;
    const previous = retired.get(key);
    const isActive = previous === undefined;
    const labelEn = isActive
      ? (row['Proposed EN'].trim() || row['Existing EN'].trim() || key)
      : `Legacy permission — ${row['Existing EN'].trim() || key}`;
    const labelAr = isActive
      ? (row['Proposed AR'].trim() || row['Existing AR'].trim() || key)
      : `صلاحية قديمة — ${row['Existing AR'].trim() || key}`;
    const descriptionEn = isActive
      ? (row['Description EN'].trim() || row['Proposed EN'].trim() || key)
      : previous ? `Compatibility alias for ${previous}; retain only during the reviewed transition.` : 'Deprecated legacy capability retained for existing persisted references; do not assign to new roles.';
    const descriptionAr = isActive
      ? (row['Description AR'].trim() || row['Proposed AR'].trim() || key)
      : previous ? `صلاحية توافق قديمة مرتبطة بـ ${previous} خلال الانتقال المعتمد.` : 'صلاحية قديمة محفوظة للمراجع والسجلات الحالية، ولا تُسند إلى أدوار جديدة.';
    const parts = key.lastIndexOf('.');
    const resource = parts > 0 ? key.slice(0, parts) : key;
    const action = parts > 0 ? key.slice(parts + 1) : 'View';
    const normalizedScreen = screen.toLowerCase();
    const viewKey = rows.find((candidate) => candidate.Level.trim() === 'View' && candidate.Screen.trim().toLowerCase() === normalizedScreen && candidate.Module.trim() === module)?.['Proposed Key']?.trim()
      || rows.find((candidate) => candidate.Level.trim() === 'View' && candidate.Screen.trim().toLowerCase() === normalizedScreen)?.['Existing Key']?.trim()
      || '';
    const required = level === 'Action' && isActive && viewKey && viewKey !== key ? [viewKey] : [];
    const sensitive = ['Critical', 'High'].includes(risk) || /approve|override|delete|archive|reactivate|close|carryforward|settlement|role|permission|password/i.test(`${key} ${row.Operation}`);
    definitions.set(key, {
      Key: key,
      SeedId: seedIds.get(key) || '',
      Resource: resource,
      Action: action,
      ModuleId: displayModule.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, ''),
      ModuleNameEn: displayModule,
      ModuleNameAr: moduleAr[displayModule] || displayModule,
      ScreenId: screen.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, ''),
      ScreenNameEn: screen,
      ScreenNameAr: screenAr[screen] || screen,
      Level: level,
      LabelEn: labelEn,
      LabelAr: labelAr,
      DescriptionEn: descriptionEn,
      DescriptionAr: descriptionAr,
      Sensitivity: risk,
      IsSensitive: sensitive,
      RequiredPermissionKeys: required,
      LegacyMapping: isActive ? '' : (retired.get(key) || ''),
      IsActive: isActive,
      SourceNew: isNew,
    });
  };
  if (existing) addDefinition(existing, false);
  if (proposed) addDefinition(proposed, !existing || proposed !== existing);
}

for (const [key, extra] of [
  ['Roles.View', { module: 'Administration', screen: 'Roles and permissions', level: 'View', en: 'View Roles and Permissions', ar: 'عرض الأدوار والصلاحيات', descEn: 'Open the tenant role and permission workspace. Role data remains tenant-scoped and delegation-limited.', descAr: 'فتح مساحة الأدوار والصلاحيات الخاصة بالمستأجر مع الالتزام بنطاق المستأجر وحدود تفويض الصلاحيات.' }],
  ['ReferenceData.View', { module: 'Administration', screen: 'Reference data', level: 'View', en: 'View Reference Data', ar: 'عرض البيانات المرجعية', descEn: 'Open tenant-scoped reference data and lookup workspaces.', descAr: 'فتح مساحات البيانات المرجعية والقوائم المساندة ضمن نطاق المستأجر.' }],
  ['TenantAdministration.ManageScope', { module: 'Administration', screen: 'Tenant and organization settings', level: 'Action', en: 'Administer Tenant Scope', ar: 'إدارة نطاق المستأجر', descEn: 'Grant tenant-wide administrative management scope within this tenant only. Does not grant platform or cross-tenant authority.', descAr: 'منح نطاق الإدارة على مستوى هذا المستأجر فقط، دون صلاحيات على المنصة أو على مستأجرين آخرين.' }],
]) {
  const screenViewKey = extra.level === 'Action' ? 'settings.read' : '';
  definitions.set(key, {
    Key: key, SeedId: '', Resource: key.slice(0, key.lastIndexOf('.')), Action: key.slice(key.lastIndexOf('.') + 1),
    ModuleId: extra.module.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, ''), ModuleNameEn: extra.module, ModuleNameAr: moduleAr[extra.module],
    ScreenId: extra.screen.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, ''), ScreenNameEn: extra.screen, ScreenNameAr: screenAr[extra.screen],
    Level: extra.level, LabelEn: extra.en, LabelAr: extra.ar, DescriptionEn: extra.descEn, DescriptionAr: extra.descAr,
    Sensitivity: 'Critical', IsSensitive: true, RequiredPermissionKeys: screenViewKey ? [screenViewKey] : [], LegacyMapping: '', IsActive: true, SourceNew: true,
  });
}

if (seedIds.size < 103) throw new Error(`Expected at least 103 known seed IDs, got ${seedIds.size}`);
let nextId = Math.max(177, ...[...seedIds.values()].map((id) => Number(id.slice(5, 8)) || 0)) + 1;
for (const item of definitions.values()) {
  if (!item.SeedId) {
    const id = nextId++;
    item.SeedId = `b0000${String(id).padStart(3, '0')}-0000-0000-0000-${String(id).padStart(12, '0')}`;
  }
}
if (definitions.size !== 136) throw new Error(`Expected 136 catalog keys (103 current + 33 additions), got ${definitions.size}: ${[...definitions.keys()].filter((key) => !seedIds.has(key)).join(', ')}`);
for (const item of definitions.values()) {
  if (!item.IsActive || item.Level !== 'Action' || item.RequiredPermissionKeys.length) continue;
  const view = [...definitions.values()].find((candidate) => candidate.IsActive && candidate.Level === 'View' && candidate.ModuleId === item.ModuleId && candidate.ScreenId === item.ScreenId);
  if (view) item.RequiredPermissionKeys = [view.Key];
}
const missingViewScreens = [...definitions.values()].filter((item) => item.IsActive && item.Level === 'Action' && item.RequiredPermissionKeys.length === 0).map((item) => `${item.Key} (${item.ModuleNameEn} / ${item.ScreenNameEn})`);
console.log('built definitions', definitions.size);
for (const item of definitions.values()) if (item.RequiredPermissionKeys.some((key) => !definitions.has(key))) throw new Error(`Missing dependency for ${item.Key}`);

const string = (value) => JSON.stringify(value ?? '');
const entries = [...definitions.values()].map((item) => `        new(\n            ${string(item.SeedId)}, ${string(item.Key)}, ${string(item.Resource)}, ${string(item.Action)},\n            ${string(item.ModuleId)}, ${string(item.ModuleNameEn)}, ${string(item.ModuleNameAr)},\n            ${string(item.ScreenId)}, ${string(item.ScreenNameEn)}, ${string(item.ScreenNameAr)}, PermissionLevel.${item.Level},\n            ${string(item.LabelEn)}, ${string(item.LabelAr)}, ${string(item.DescriptionEn)}, ${string(item.DescriptionAr)},\n            ${string(item.Sensitivity)}, ${item.IsSensitive ? 'true' : 'false'}, [${item.RequiredPermissionKeys.map(string).join(', ')}], ${item.LegacyMapping ? string(item.LegacyMapping) : 'null'}, ${item.IsActive ? 'true' : 'false'}),`).join('\n');

const source = `using NEXUS.Modules.Identity.Domain;\n\nnamespace NEXUS.Modules.Identity.Application;\n\npublic enum PermissionLevel { View, Action }\n\npublic sealed record PermissionCatalogEntry(\n    string SeedId, string Key, string Resource, string Action,\n    string ModuleId, string ModuleNameEn, string ModuleNameAr,\n    string ScreenId, string ScreenNameEn, string ScreenNameAr, PermissionLevel Level,\n    string LabelEn, string LabelAr, string DescriptionEn, string DescriptionAr,\n    string Sensitivity, bool IsSensitive, IReadOnlyList<string> RequiredPermissionKeys,\n    string? LegacyMapping, bool IsActive);\n\npublic static class PermissionCatalog\n{\n    private static readonly IReadOnlyList<PermissionCatalogEntry> Catalog =\n    [\n${entries}\n    ];\n\n    public static IReadOnlyList<PermissionCatalogEntry> Entries => Catalog;\n\n    public static IReadOnlyDictionary<string, PermissionCatalogEntry> ByKey { get; } = Catalog.ToDictionary(item => item.Key, StringComparer.Ordinal);\n\n    public static IReadOnlyList<Permission> CreateSeedPermissions(Guid tenantId) => Catalog.Select(item => new Permission\n    {\n        Id = Guid.Parse(item.SeedId), TenantId = tenantId, Name = item.Key, Resource = item.Resource, Action = item.Action,\n        Description = item.DescriptionEn, CreatedBy = "System"\n    }).ToList();\n\n    public static bool HasUniqueKeys() => Catalog.Select(item => item.Key).Distinct(StringComparer.Ordinal).Count() == Catalog.Count;\n}\n`;
fs.writeFileSync(outputPath, source, 'utf8');
console.log('wrote catalog');
console.log(`Generated ${definitions.size} permission catalog entries; ${[...definitions.values()].filter((item) => item.IsActive).length} active, ${[...definitions.values()].filter((item) => item.Level === 'View' && item.IsActive).length} view, ${[...definitions.values()].filter((item) => item.Level === 'Action' && item.IsActive).length} action.`);
if (missingViewScreens.length) console.log('Actions without a screen View dependency candidate:', missingViewScreens.join('; '));
