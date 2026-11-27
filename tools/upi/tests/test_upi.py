import importlib.util
import io
import json
import math
import tempfile
import unittest
from contextlib import redirect_stdout
from pathlib import Path

ENGINE = Path(__file__).resolve().parents[1] / "upi.py"
spec = importlib.util.spec_from_file_location("upi", ENGINE)
upi = importlib.util.module_from_spec(spec)
spec.loader.exec_module(upi)

class UpITest(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.root = Path(self.tmp.name)
        (self.root / "tools" / "upi").mkdir(parents=True)
        conf = (ENGINE.parent/"upi_config.json").read_text(encoding="utf-8")
        (self.root/"tools"/"upi"/"upi_config.json").write_text(conf,encoding="utf-8")
        def put(file, content):
            p=self.root/file;p.parent.mkdir(parents=True,exist_ok=True);p.write_text(content,encoding="utf-8")
        self.put=put
        put("backend/Features/Projects/ProjectStatusService.cs", """namespace Fopx.Features.Projects;\npublic class ProjectStatusService {\n public bool UpdateStatus(int id) { return true; }\n}\n""")
        put("backend/Features/Projects/ProjectsController.cs", """using Fopx.Features.Projects;\nnamespace Fopx.Api;\npublic class ProjectsController {\n public bool UpdateStatus(int id) { return true; }\n}\n""")
        put("frontend/src/modules/projects/ProjectStatus.tsx", """import { api } from '../../shared/api';\nexport function ProjectStatus() { return null; }\n""")
        put("frontend/src/shared/api.ts", "export const api = () => 123;\n")
        put("backend/Features/Issues/IssueService.cs", "namespace Fopx.Features.Issues;\npublic class IssueService { public void CloseIssue() {} }\n")
        put("backend/Features/CostManagement/PaymentService.cs", "namespace Fopx.Features.CostManagement;\npublic class PaymentService { public void SettlePayment() {} }\n")
        put(".env", "SECRET=not-for-index\n")
        self.cfg=upi.load_config(self.root)

    def write_project_status_flow(self):
        self.put("apps/web-react/src/features/projects/components/CreateProjectSheet.tsx", """import { useUpdateProject } from \"../api/projects\";
export function ProjectSheet() {
  const updateMutation = useUpdateProject(\"project-id\");
  async function submit() {
    const statusId = \"status-target\";
    await updateMutation.mutateAsync({ statusId });
  }
  return submit;
}
""")
        self.put("apps/web-react/src/features/projects/api/projects.ts", """export function useUpdateProject(id: string) {
  return api.put(`/api/projects/${id}`, {});
}
""")
        self.put("apps/api/src/NEXUS.Modules.ProjectManagement/Api/ProjectController.cs", """[Route(\"api/projects\")]
public class ProjectController {
  private readonly IProjectService _projectService;
  [HttpPut(\"{id:guid}\")]
  public async Task<IActionResult> Update(Guid id) {
    return await _projectService.UpdateAsync(id);
  }
}
""")
        self.put("apps/api/src/NEXUS.Modules.ProjectManagement/Application/IProjectService.cs", """public interface IProjectService {
  Task UpdateAsync(Guid id);
}
""")
        self.put("apps/api/src/NEXUS.Modules.ProjectManagement/Application/ProjectService.cs", """public class ProjectService : IProjectService {
  public async Task UpdateAsync(Guid id) {
    await _context.SaveChangesAsync();
  }
}
""")
        self.put("apps/web-react/src/test/project-change-status.test.tsx", """import { fireEvent, waitFor } from \"@testing-library/react\";
const updateProject = vi.fn();
it(\"saves the canonical statusId\", async () => {
  const statusId = \"status-target\";
  fireEvent.change(statusId);
  await waitFor(() => expect(updateProject).toHaveBeenCalled());
});
""")
        self.put("apps/api/tests/NEXUS.Api.Tests/Integration/ProjectStatusChangeCatalogTests.cs", """public class ProjectStatusChangeCatalogTests {
  public async Task Updates_status_id() {
    ProjectService service = CreateService();
    await service.UpdateAsync(new UpdateProjectDto { StatusId = Guid.NewGuid() });
  }
}
""")

    def write_status_report_flow(self):
        self.put("apps/web-react/src/features/projects/ProjectGovernanceTabs.tsx", """import {
  useUpdateProjectStatusReport,
} from \"./api/projects\";
export function ProjectStatusReportsTab(projectId: string) {
  const editingReport = true;
  const update = useUpdateProjectStatusReport(projectId, \"report-id\");
  async function submit(input: object) {
    if (editingReport) await update.mutateAsync(input);
  }
  return submit;
}
""")
        self.put("apps/web-react/src/features/projects/api/projects.ts", """export function useUpdateProjectStatusReport(projectId: string, reportId: string) {
  return api.put(`/api/projects/${projectId}/status-reports/${reportId}`, {});
}
""")
        self.put("apps/api/src/NEXUS.Modules.ProjectManagement/Api/ProjectController.cs", """[Route(\"api/projects\")]
public class ProjectController {
  private readonly IProjectStatusReportService _projectStatusReportService;
  [HttpPut(\"{projectId:guid}/status-reports/{reportId:guid}\")]
  public async Task<IActionResult> UpdateStatusReport(Guid projectId, Guid reportId) {
    return await _projectStatusReportService.UpdateAsync(projectId, reportId);
  }
}
""")
        self.put("apps/api/src/NEXUS.Modules.ProjectManagement/Application/IProjectStatusReportService.cs", """public interface IProjectStatusReportService {
  Task UpdateAsync(Guid projectId, Guid reportId);
}
""")
        self.put("apps/api/src/NEXUS.Modules.ProjectManagement/Application/ProjectStatusReportService.cs", """public class ProjectStatusReportService : IProjectStatusReportService {
  public async Task UpdateAsync(Guid projectId, Guid reportId) {
    await _context.SaveChangesAsync();
  }
}
""")
        self.put("apps/api/tests/NEXUS.Api.Tests/Integration/ProjectReportingPersistenceTests.cs", """public class ProjectReportingPersistenceTests {
  public async Task Updates_draft_report() {
    ProjectStatusReportService statusReportService = CreateService();
    var editedDraft = await statusReportService.UpdateAsync(projectId, reportId);
    Assert.Equal(\"editedDraft\", editedDraft.Name);
    Assert.IsType<ProjectStatusReport>(editedDraft);
  }
}
""")

    def tearDown(self):
        self.tmp.cleanup()

    def test_init_is_project_scoped_and_outputs_docs(self):
        m,s,_=upi.scan(self.root,self.cfg,force=True)
        self.assertEqual(m["files"],6)
        sp=upi.current_snapshot(self.root)
        self.assertTrue((self.root/"docs"/"architecture"/"INDEX.md").exists())
        self.assertTrue((sp/"diagrams"/"system.mmd").exists())
        self.assertNotIn(".env",[x["path"] for x in upi.read_snapshot(self.root)["files"]])
        self.assertTrue(upi.validate(self.root,self.cfg)["ok"])

    def test_project_router_preserves_scoped_non_save_behavior(self):
        upi.scan(self.root,self.cfg,force=True)
        r=upi.route(self.root,self.cfg,"Inspect Project Status catalog",budget=16000)
        self.assertEqual(r["primary_module"],"projects")
        self.assertTrue(r["selected_files"])
        paths=[x["path"] for x in r["selected_files"]]
        self.assertTrue(all("Issues" not in x and "CostManagement" not in x for x in paths))
        self.assertTrue(all(any("status" in reason.lower() for reason in file["reasons"]) for file in r["selected_files"]), r["selected_files"])
        self.assertEqual(r["planning_state"], "focused")
        self.assertFalse(r["critical_path_coverage"]["active"] if r["critical_path_coverage"] else False)
        self.assertTrue((self.root/r["report_path"]).exists())

    def test_project_status_update_golden_covers_frontend_to_persistence_and_tests(self):
        self.write_project_status_flow()
        upi.scan(self.root,self.cfg,force=True)
        result=upi.route(self.root,self.cfg,"Fix Project Status update not saving correctly",budget=16000,write=False)
        flow=result["critical_path_coverage"]
        self.assertEqual(flow["flow_id"],"project_status_update")
        self.assertEqual(flow["coverage_percent"],100,flow)
        self.assertTrue(flow["gate"]["passed"],flow)
        self.assertEqual(flow["covered_steps"],8)
        paths={item["path"] for item in result["selected_files"]}
        self.assertEqual(paths,{
            "apps/web-react/src/features/projects/components/CreateProjectSheet.tsx",
            "apps/web-react/src/features/projects/api/projects.ts",
            "apps/api/src/NEXUS.Modules.ProjectManagement/Api/ProjectController.cs",
            "apps/api/src/NEXUS.Modules.ProjectManagement/Application/IProjectService.cs",
            "apps/api/src/NEXUS.Modules.ProjectManagement/Application/ProjectService.cs",
            "apps/web-react/src/test/project-change-status.test.tsx",
            "apps/api/tests/NEXUS.Api.Tests/Integration/ProjectStatusChangeCatalogTests.cs",
        })
        self.assertEqual(result["planning_state"],"focused-with-uncertainty")
        self.assertIn("implementation-to-contract",flow["unconfirmed_relationships"])
        self.assertTrue(all("Issues" not in path and "CostManagement" not in path for path in paths))
        self.assertTrue(all(item["status"]=="covered" for item in flow["relationships"]),flow["relationships"])
        by_relation={item["id"]:item for item in flow["relationships"]}
        self.assertEqual(by_relation["handler-to-client"]["evidence"],["useUpdateProject"])
        self.assertEqual(by_relation["client-to-endpoint"]["evidence"],["PUT /api/projects/{var} -> Update"])
        self.assertTrue(any(item["from_symbol"]=="Update" and item["to_symbol"]=="UpdateAsync" for item in upi.read_snapshot(self.root)["edges"] if item["relation"]=="CALLS_TYPED_METHOD" and item["from"].endswith("ProjectController.cs")))

    def test_status_report_update_golden_is_distinct_and_does_not_select_project_status_flow(self):
        self.write_status_report_flow()
        upi.scan(self.root,self.cfg,force=True)
        result=upi.route(self.root,self.cfg,"Fix Project Status Report update not saving correctly",budget=16000,write=False)
        flow=result["critical_path_coverage"]
        self.assertEqual(flow["flow_id"],"project_status_report_update")
        self.assertEqual(flow["coverage_percent"],100,flow)
        self.assertTrue(flow["gate"]["passed"],flow)
        self.assertEqual(flow["covered_steps"],7)
        paths={item["path"] for item in result["selected_files"]}
        self.assertIn("apps/web-react/src/features/projects/ProjectGovernanceTabs.tsx",paths)
        self.assertIn("apps/api/src/NEXUS.Modules.ProjectManagement/Application/ProjectStatusReportService.cs",paths)
        self.assertIn("apps/api/tests/NEXUS.Api.Tests/Integration/ProjectReportingPersistenceTests.cs",paths)
        self.assertNotIn("apps/web-react/src/features/projects/components/CreateProjectSheet.tsx",paths)
        self.assertNotIn("apps/api/src/NEXUS.Modules.ProjectManagement/Application/ProjectService.cs",paths)
        by_relation={item["id"]:item for item in flow["relationships"]}
        self.assertEqual(by_relation["client-to-endpoint"]["evidence"],["PUT /api/projects/{var}/status-reports/{var} -> UpdateStatusReport"])
        self.assertTrue(by_relation["handler-to-client"]["uncertain"])
        self.assertIn("handler-to-client",flow["unconfirmed_relationships"])
        self.assertEqual(len(paths),6)

    def test_critical_path_gate_blocks_missing_service_implementation(self):
        self.write_project_status_flow()
        (self.root/"apps/api/src/NEXUS.Modules.ProjectManagement/Application/ProjectService.cs").unlink()
        upi.scan(self.root,self.cfg,force=True)
        result=upi.route(self.root,self.cfg,"Fix Project Status update not saved",budget=16000,write=False)
        flow=result["critical_path_coverage"]
        self.assertFalse(flow["gate"]["passed"])
        self.assertTrue(flow["gate"]["blocks_completion"])
        self.assertIn("service-implementation",flow["missing_steps"])
        self.assertEqual(result["planning_state"],"critical-path-incomplete")

    def test_critical_path_gate_blocks_missing_relationship_without_optional_scope_expansion(self):
        self.write_status_report_flow()
        ui_path=self.root/"apps/web-react/src/features/projects/ProjectGovernanceTabs.tsx"
        ui_source=ui_path.read_text(encoding="utf-8")
        ui_path.write_text(ui_source.replace('import {\n  useUpdateProjectStatusReport,\n} from "./api/projects";', "const unrelated = true;"),encoding="utf-8")
        upi.scan(self.root,self.cfg,force=True)
        result=upi.route(self.root,self.cfg,"Fix Project Status Report update not saved",budget=16000,write=False)
        flow=result["critical_path_coverage"]
        self.assertFalse(flow["gate"]["passed"])
        self.assertIn("handler-to-client",flow["missing_relationships"])
        self.assertEqual(result["planning_state"],"critical-path-incomplete")
        self.assertEqual(len(result["selected_files"]),6)
        self.assertFalse(any("ProjectStatusReportComparison.cs" in item["path"] for item in result["selected_files"]))

    def test_route_only_critical_flow_config_change_keeps_incremental_index(self):
        upi.scan(self.root,self.cfg,force=True)
        self.cfg["critical_flows"]["project_status_update"]["title"]="Project Status save"
        _,stats,updated=upi.fresh(self.root,self.cfg)
        self.assertFalse(updated)
        self.assertEqual(stats["parsed"],0)

    def test_sensitive_settings_lockfiles_and_generated_paths_are_excluded(self):
        paths = [
            "apps/api/src/NEXUS.Api/appsettings.Development.json",
            "apps/web-react/package-lock.json",
            "apps/web-react/.role-admin-build-check/assets/index.js",
            "apps/api/src/NEXUS.Modules.ProjectManagement/.api-current/Debug/deps.json",
            "db_backups/Development.sql",
        ]
        for path in paths:
            with self.subTest(path=path):
                self.assertFalse(upi.is_safe_file(path, self.cfg))

    def test_unmatched_tests_do_not_become_fake_modules(self):
        module, basis, confidence = upi.discover_module("apps/web-react/src/test/date-formatting.test.tsx", self.cfg)
        self.assertEqual(module, "tests")
        self.assertEqual(basis, "test-file-without-module-alias")
        self.assertEqual(confidence, "low")

    def test_arabic_module_alias_and_cli_json_output(self):
        files = [{"path": "backend/Features/Projects/ProjectStatusService.cs", "module": "projects"}]
        self.assertEqual(upi.determine_module("تعديل حالة المشروع", files, self.cfg)[0], "projects")
        output = io.StringIO()
        with redirect_stdout(output):
            code = upi.main(["route", "--root", str(self.root), "--task", "تعديل حالة المشروع", "--budget", "16000", "--no-write"])
        self.assertEqual(code, 0)
        self.assertTrue(output.getvalue().isascii())
        self.assertEqual(json.loads(output.getvalue())["primary_module"], "projects")

    def test_token_estimates_are_labeled_and_bounded(self):
        tokens, method, approximate = upi.count_tokens("x" * 100, self.cfg)
        if approximate:
            self.assertEqual(method, "estimated:chars/3.5")
            self.assertEqual(tokens, math.ceil(100 / 3.5))
        else:
            self.assertTrue(method.startswith("tiktoken:"))
            self.assertGreater(tokens, 0)

    def test_change_detection_add_rename_delete(self):
        upi.scan(self.root,self.cfg,force=True)
        self.put("frontend/src/modules/projects/ProjectRiskService.ts", "export function ProjectRiskService() { return 1; }\n")
        _, stats, updated=upi.fresh(self.root,self.cfg)
        self.assertTrue(updated);self.assertEqual(stats["new"],1)
        self.assertEqual(upi.validate(self.root,self.cfg)["warnings"],[])
        self.assertTrue(any("ProjectRiskService.ts" in x["path"] for x in upi.read_snapshot(self.root)["files"]))
        before=self.root/"backend"/"Features"/"Issues"/"IssueService.cs"
        after=before.with_name("IssueTrackerService.cs")
        before.rename(after)
        _,stats,_=upi.fresh(self.root,self.cfg)
        self.assertEqual(stats["deleted"],1)
        self.assertEqual(stats["new"],1)
        self.assertEqual(upi.validate(self.root,self.cfg)["warnings"],[])

    def test_unchanged_refresh_does_not_reparse(self):
        upi.scan(self.root,self.cfg,force=True)
        original=upi.graph_edges
        def unexpected_rebuild(*args,**kwargs):
            raise AssertionError("unchanged refresh must not rebuild the dependency graph")
        upi.graph_edges=unexpected_rebuild
        try:
            _,s,updated=upi.fresh(self.root,self.cfg)
        finally:
            upi.graph_edges=original
        self.assertFalse(updated);self.assertEqual(s["parsed"],0)

    def test_stale_validation_and_auto_refresh_on_route(self):
        upi.scan(self.root,self.cfg,force=True)
        self.put("frontend/src/modules/projects/NewProjectAction.tsx","export function NewProjectAction() { return null; }\n")
        v=upi.validate(self.root,self.cfg)
        self.assertTrue(any("Stale (new)" in x for x in v["warnings"]))
        r=upi.route(self.root,self.cfg,"Update Project Status",write=False)
        self.assertTrue(r["index_updated"])
        self.assertFalse(upi.validate(self.root,self.cfg)["warnings"])

    def test_framework_linking_is_labeled_inferred(self):
        self.put("backend/Features/Projects/ProjectModel.cs", "namespace Fopx.Features.Projects;\npublic class ProjectModel {}\n")
        self.put("backend/Features/Projects/ProjectContext.cs", "namespace Fopx.Features.Projects;\npublic class ProjectContext { DbSet<ProjectModel> Projects; }\n")
        self.put("backend/Features/Projects/ApiProjectsController.cs", '[Route("api/projects")]\npublic class ApiProjectsController { [HttpGet("status")] public bool GetStatus() { return true; } }\n')
        self.put("frontend/src/modules/projects/ProjectApi.ts", "export const status = () => fetch('/api/projects/status');\n")
        upi.scan(self.root,self.cfg,force=True)
        edges=upi.read_snapshot(self.root)["edges"]
        self.assertTrue(any(x["relation"]=="PERSISTS_ENTITY" and x["confidence"].startswith("inferred") for x in edges))
        self.assertTrue(any(x["relation"]=="CALLS_API_ROUTE" and x["confidence"].startswith("inferred") for x in edges))

    def test_configuration_change_triggers_reindex(self):
        upi.scan(self.root,self.cfg,force=True)
        self.cfg["module_aliases"]["projects"].append("epicproject")
        _,stats,updated=upi.fresh(self.root,self.cfg)
        self.assertTrue(updated)
        self.assertEqual(stats["parsed"],6)

    def test_graph_version_refresh_rebuilds_relationships_without_rereading_sources(self):
        upi.scan(self.root,self.cfg,force=True)
        snapshot=upi.current_snapshot(self.root)
        manifest=json.loads((snapshot/"manifest.json").read_text(encoding="utf-8"))
        manifest["graph_version"]=0
        (snapshot/"manifest.json").write_text(json.dumps(manifest),encoding="utf-8")
        _,stats,updated=upi.fresh(self.root,self.cfg)
        self.assertTrue(updated)
        self.assertEqual(stats["mode"],"graph-refresh")
        self.assertEqual(stats["parsed"],0)
        self.assertTrue(upi.validate(self.root,self.cfg)["ok"])

    def test_dependent_files_edge_classification(self):
        upi.scan(self.root,self.cfg,force=True)
        edges=upi.read_snapshot(self.root)["edges"]
        self.assertTrue(any(e["relation"]=="IMPORTS" and e["confidence"]=="verified-static-import" for e in edges))

if __name__ == "__main__":
    unittest.main()
