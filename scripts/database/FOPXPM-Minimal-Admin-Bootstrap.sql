/*
FOPX PM core minimal Development Admin bootstrap (SQL Server)
Prepared from the three exact sources supplied in the referenced chat.
PermissionCatalog: B7A51FD4D3B7ADE2D13E90A7DAB5A59EAD4C419A84E6EE845BDC1AE767B7458B
ProtectedTenantAdminPolicy: D3010559C5D257E4BC24EB9FECFD837C4739122BE9129146773CC02D3FA5FD45
SeedDataProvider: 5B95396B321C547F7EF2D243DCE1173F29AACAA7A91A1785A0047A1DD418B1FB

Source-derived Tenant Admin grants: 47 protected keys, 102 approved supplementary entries (deduplicated), and 2 approved Project Settings entries.
This core script intentionally omits the additional approved reference-catalog rows
created by the full EF --seed-minimal-only helper path; the user-supplied schema in
this request covers the identity, tenant, and organization tables only.

This file does not apply migrations, write to __EFMigrationsHistory, delete data,
or create demo/business records. It writes only the Tenant, Organization, Permissions,
Roles, RolePermissions, Users, and UserRoles rows defined below. It refuses to run
unless connected to SQL Server Shady / database NexusPmo and all other application
tables are empty. Permission scope is not a persisted column in the supplied schema;
the catalog scope below is used only for validation.

Before first execution, replace the password-hash placeholder with a hash produced
locally by the application's ASP.NET Core PasswordHasher<User> (same call as
SeedDataProvider: HashPassword(new User(), securePassword)). Use a one-time password
of at least 12 characters, keep the plaintext out of this file, and keep the Admin
change-password flag enabled. The script rejects an unfilled placeholder when it
needs to create the Admin. A rerun against the exact existing Admin does not reset it.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF UPPER(CONVERT(nvarchar(128), SERVERPROPERTY('ServerName'))) <> N'SHADY'
    THROW 51000, 'Refusing to run: connected SQL Server is not Shady.', 1;
IF UPPER(DB_NAME()) <> N'NEXUSPMO'
    THROW 51001, 'Refusing to run: select the NexusPmo database in SSMS first.', 1;
IF CONVERT(nvarchar(60), DATABASEPROPERTYEX(DB_NAME(), 'Status')) <> N'ONLINE'
    THROW 51002, 'Refusing to run: NexusPmo is not ONLINE.', 1;
IF @@TRANCOUNT <> 0
    THROW 51003, 'Refusing to run inside an existing transaction.', 1;

DECLARE @TenantId uniqueidentifier = CONVERT(uniqueidentifier, N'11111111-1111-1111-1111-111111111111');
DECLARE @OrganizationId uniqueidentifier = CONVERT(uniqueidentifier, N'90000001-0000-0000-0000-000000000001');
DECLARE @AdminUserId uniqueidentifier = CONVERT(uniqueidentifier, N'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa');
DECLARE @AdminEmail nvarchar(512) = N'admin@nexus.com';
DECLARE @AdminPasswordHash nvarchar(max) = N'__REPLACE_WITH_ASPNETCORE_PASSWORDHASH__';
DECLARE @BootstrapPasswordExpiryHours int = 24;
DECLARE @NowUtc datetime2(7) = SYSUTCDATETIME();

IF @BootstrapPasswordExpiryHours NOT BETWEEN 1 AND 168
    THROW 51004, 'Temporary password expiry must be between 1 and 168 hours.', 1;

DECLARE @PermissionSeed TABLE (
    PermissionId uniqueidentifier NOT NULL PRIMARY KEY,
    PermissionName nvarchar(200) NOT NULL UNIQUE,
    Resource nvarchar(200) NOT NULL,
    [Action] nvarchar(100) NOT NULL,
    Description nvarchar(1000) NULL,
    PermissionScope nvarchar(32) NOT NULL,
    UNIQUE (Resource, [Action])
);
INSERT @PermissionSeed (PermissionId, PermissionName, Resource, [Action], Description, PermissionScope)
VALUES
    (CONVERT(uniqueidentifier, N'b0000169-0000-0000-0000-000000000169'), N'Audit.View', N'Audit', N'View', N'Read the tenant audit trail and governance activity log', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000107-0000-0000-0000-000000000107'), N'ChangeRequest.Approve', N'ChangeRequest', N'Approve', N'Approve or reject project change requests', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000106-0000-0000-0000-000000000106'), N'ChangeRequest.Manage', N'ChangeRequest', N'Manage', N'Create, update, and cancel project change requests', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000105-0000-0000-0000-000000000105'), N'ChangeRequest.View', N'ChangeRequest', N'View', N'View project change requests', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000154-0000-0000-0000-000000000154'), N'Directory.ExecuteSync', N'Directory', N'ExecuteSync', N'Execute selected directory synchronization actions', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000155-0000-0000-0000-000000000155'), N'Directory.ManageMappings', N'Directory', N'ManageMappings', N'Manage directory field, group, and organization mappings', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000156-0000-0000-0000-000000000156'), N'Directory.ManageProvisioningPolicy', N'Directory', N'ManageProvisioningPolicy', N'Manage directory provisioning policy', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000153-0000-0000-0000-000000000153'), N'Directory.PreviewSync', N'Directory', N'PreviewSync', N'Preview directory synchronization', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000152-0000-0000-0000-000000000152'), N'Directory.TestConnection', N'Directory', N'TestConnection', N'Test directory connectivity', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000150-0000-0000-0000-000000000150'), N'Directory.View', N'Directory', N'View', N'View directory integration configuration and sync history', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000137-0000-0000-0000-000000000137'), N'Document.Archive', N'Document', N'Archive', N'Archive documents', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000135-0000-0000-0000-000000000135'), N'Document.Create', N'Document', N'Create', N'Upload documents', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000136-0000-0000-0000-000000000136'), N'Document.Edit', N'Document', N'Edit', N'Upload document versions', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000138-0000-0000-0000-000000000138'), N'Document.TemplateManage', N'Document', N'TemplateManage', N'Manage reusable document templates', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000134-0000-0000-0000-000000000134'), N'Document.View', N'Document', N'View', N'View documents and templates', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000228-0000-0000-0000-000000000228'), N'ProjectFinance.View', N'ProjectFinance', N'View', N'Open the Project Finance navigation shell. Each finance workspace has its own data View permission and remains separately scoped.', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000244-0000-0000-0000-000000000244'), N'Budget.View', N'Budget', N'View', N'View legacy budget records within authorized tenant and project or portfolio management scope.', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000245-0000-0000-0000-000000000245'), N'Budget.Create', N'Budget', N'Create', N'Create legacy budget records within authorized project or portfolio scope.', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000246-0000-0000-0000-000000000246'), N'Budget.Edit', N'Budget', N'Edit', N'Edit legacy budget records within authorized project or portfolio scope.', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000247-0000-0000-0000-000000000247'), N'Budget.Delete', N'Budget', N'Delete', N'Delete legacy budget records within authorized project or portfolio scope.', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000248-0000-0000-0000-000000000248'), N'BudgetRevision.Create', N'BudgetRevision', N'Create', N'Create a budget revision draft for a budget in the actor''s authorized project or portfolio scope.', N'Project'),
    (CONVERT(uniqueidentifier, N'b0000249-0000-0000-0000-000000000249'), N'BudgetRevision.EditDraft', N'BudgetRevision', N'EditDraft', N'Edit a draft budget revision within the actor''s authorized project or portfolio scope.', N'Project'),
    (CONVERT(uniqueidentifier, N'b0000250-0000-0000-0000-000000000250'), N'BudgetRevision.SubmitForReview', N'BudgetRevision', N'SubmitForReview', N'Submit a draft budget revision into the review workflow within authorized scope.', N'Project'),
    (CONVERT(uniqueidentifier, N'b0000251-0000-0000-0000-000000000251'), N'BudgetRevision.Cancel', N'BudgetRevision', N'Cancel', N'Cancel an eligible budget revision under workflow-state rules and authorized project scope.', N'Project'),
    (CONVERT(uniqueidentifier, N'b0000252-0000-0000-0000-000000000252'), N'BudgetRevision.Delete', N'BudgetRevision', N'Delete', N'Delete an eligible draft budget revision within authorized project scope.', N'Project'),
    (CONVERT(uniqueidentifier, N'b0000253-0000-0000-0000-000000000253'), N'Cost.View', N'Cost', N'View', N'View cost entries within authorized project management scope.', N'Project'),
    (CONVERT(uniqueidentifier, N'b0000254-0000-0000-0000-000000000254'), N'Cost.Create', N'Cost', N'Create', N'Create cost entries within authorized project management scope.', N'Project'),
    (CONVERT(uniqueidentifier, N'b0000255-0000-0000-0000-000000000255'), N'Cost.Edit', N'Cost', N'Edit', N'Edit cost entries within authorized project management scope.', N'Project'),
    (CONVERT(uniqueidentifier, N'b0000256-0000-0000-0000-000000000256'), N'Cost.Delete', N'Cost', N'Delete', N'Delete cost entries within authorized project management scope.', N'Project'),
    (CONVERT(uniqueidentifier, N'b0000269-0000-0000-0000-000000000269'), N'CostUtilization.Export', N'CostUtilization', N'Export', N'Export Cost & Utilization data within the current Tenant and the user''s authorized management scope. The export endpoint also requires Resource.View; resource cost-rate fields remain redacted unless Resource.ViewCostRate is effective.', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000257-0000-0000-0000-000000000257'), N'Commitment.View', N'Commitment', N'View', N'View purchase commitments within authorized project management scope.', N'Project'),
    (CONVERT(uniqueidentifier, N'b0000258-0000-0000-0000-000000000258'), N'Commitment.Create', N'Commitment', N'Create', N'Create purchase commitments within authorized project management scope.', N'Project'),
    (CONVERT(uniqueidentifier, N'b0000259-0000-0000-0000-000000000259'), N'Commitment.Edit', N'Commitment', N'Edit', N'Edit eligible purchase commitments within authorized project management scope.', N'Project'),
    (CONVERT(uniqueidentifier, N'b0000260-0000-0000-0000-000000000260'), N'Commitment.Close', N'Commitment', N'Close', N'Close purchase commitments within authorized project management scope.', N'Project'),
    (CONVERT(uniqueidentifier, N'b0000261-0000-0000-0000-000000000261'), N'Commitment.Cancel', N'Commitment', N'Cancel', N'Cancel eligible purchase commitments within authorized project management scope.', N'Project'),
    (CONVERT(uniqueidentifier, N'b0000262-0000-0000-0000-000000000262'), N'Commitment.Delete', N'Commitment', N'Delete', N'Delete eligible purchase commitments within authorized project management scope.', N'Project'),
    (CONVERT(uniqueidentifier, N'b0000263-0000-0000-0000-000000000263'), N'PaymentObligation.View', N'PaymentObligation', N'View', N'View payment obligations and their settlements within authorized project scope.', N'Project'),
    (CONVERT(uniqueidentifier, N'b0000264-0000-0000-0000-000000000264'), N'PaymentObligation.Create', N'PaymentObligation', N'Create', N'Create a planned payment obligation within authorized project scope.', N'Project'),
    (CONVERT(uniqueidentifier, N'b0000265-0000-0000-0000-000000000265'), N'PaymentObligation.Edit', N'PaymentObligation', N'Edit', N'Edit an eligible payment obligation within authorized project scope.', N'Project'),
    (CONVERT(uniqueidentifier, N'b0000266-0000-0000-0000-000000000266'), N'PaymentObligation.Delete', N'PaymentObligation', N'Delete', N'Delete an eligible payment obligation without settlements within authorized project scope.', N'Project'),
    (CONVERT(uniqueidentifier, N'b0000267-0000-0000-0000-000000000267'), N'PaymentObligationSettlement.Record', N'PaymentObligationSettlement', N'Record', N'Record an actual settlement against a planned payment obligation within authorized tenant, project, and fiscal-year scope.', N'Project'),
    (CONVERT(uniqueidentifier, N'b0000215-0000-0000-0000-000000000215'), N'BudgetRevision.View', N'BudgetRevision', N'View', N'View budget revisions within authorized Tenant and Project management scope.', N'Project'),
    (CONVERT(uniqueidentifier, N'b0000216-0000-0000-0000-000000000216'), N'BudgetRevision.Approve', N'BudgetRevision', N'Approve', N'Approve or reject a submitted budget revision. The requester cannot decide their own revision.', N'Project'),
    (CONVERT(uniqueidentifier, N'b0000167-0000-0000-0000-000000000167'), N'Governance.AssignmentRecovery.Initiate', N'Governance.AssignmentRecovery', N'Initiate', N'Initiate initial manager assignments through the existing approval workflow', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000168-0000-0000-0000-000000000168'), N'Governance.AssignmentRecovery.Manage', N'Governance.AssignmentRecovery', N'Manage', N'Reconcile governance references, assign project sponsors, and initiate administrative handovers', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000166-0000-0000-0000-000000000166'), N'Governance.AssignmentRecovery.View', N'Governance.AssignmentRecovery', N'View', N'Discover tenant entities with missing governance assignments', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000149-0000-0000-0000-000000000149'), N'Handover.AdminInitiate', N'Handover', N'AdminInitiate', N'Initiate administrative handovers and initial manager assignments', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000110-0000-0000-0000-000000000110'), N'Handover.Approve', N'Handover', N'Approve', N'Accept or reject project and manager handovers', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000109-0000-0000-0000-000000000109'), N'Handover.Manage', N'Handover', N'Manage', N'Request or cancel project and manager handovers', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000108-0000-0000-0000-000000000108'), N'Handover.View', N'Handover', N'View', N'View project and manager handovers', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000128-0000-0000-0000-000000000128'), N'Meeting.Create', N'Meeting', N'Create', N'Create meetings', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000131-0000-0000-0000-000000000131'), N'Meeting.Delete', N'Meeting', N'Delete', N'Delete meetings', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000129-0000-0000-0000-000000000129'), N'Meeting.Edit', N'Meeting', N'Edit', N'Edit meetings and attendees', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000130-0000-0000-0000-000000000130'), N'Meeting.Manage', N'Meeting', N'Manage', N'Manage minutes, comments, and work item links', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000127-0000-0000-0000-000000000127'), N'Meeting.View', N'Meeting', N'View', N'View meetings and meeting minutes', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000171-0000-0000-0000-000000000171'), N'PaymentSettlement.Record', N'PaymentSettlement', N'Record', N'Record actual project payment settlements within the actor''s authorized project scope', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000118-0000-0000-0000-000000000118'), N'Planning.Lifecycle', N'Planning', N'Lifecycle', N'Manage planning working and baseline lifecycle', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000116-0000-0000-0000-000000000116'), N'Planning.View', N'Planning', N'View', N'View project planning data', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000119-0000-0000-0000-000000000119'), N'Portfolio.View', N'Portfolio', N'View', N'View portfolios', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000121-0000-0000-0000-000000000121'), N'Program.View', N'Program', N'View', N'View programs', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000102-0000-0000-0000-000000000102'), N'Project.Create', N'Project', N'Create', N'Create projects', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000104-0000-0000-0000-000000000104'), N'Project.Delete', N'Project', N'Delete', N'Delete projects', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000103-0000-0000-0000-000000000103'), N'Project.Edit', N'Project', N'Edit', N'Edit project data', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000101-0000-0000-0000-000000000101'), N'Project.View', N'Project', N'View', N'View projects', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000170-0000-0000-0000-000000000170'), N'ProjectAnnualBudget.Approve', N'ProjectAnnualBudget', N'Approve', N'Decide an assigned budget review by approving or returning for changes; retain scope and separation-of-duties checks.', N'Project'),
    (CONVERT(uniqueidentifier, N'b0000176-0000-0000-0000-000000000176'), N'ProjectAnnualBudget.CarryForward', N'ProjectAnnualBudget', N'CarryForward', N'Authorize explicit free-balance and outstanding-commitment carry forward during Project year-end closing', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000174-0000-0000-0000-000000000174'), N'ProjectAnnualBudget.Close', N'ProjectAnnualBudget', N'Close', N'Prepare, submit, and cancel Project fiscal-year closing statements within the actor''s authorized project scope', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000175-0000-0000-0000-000000000175'), N'ProjectAnnualBudget.CloseApprove', N'ProjectAnnualBudget', N'CloseApprove', N'Approve or reject an assigned Project fiscal-year closing statement', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000177-0000-0000-0000-000000000177'), N'ProjectApproval.Override', N'ProjectApproval', N'Override', N'Apply a reasoned exception to one approval submission only; does not change persistent Project Settings.', N'Project'),
    (CONVERT(uniqueidentifier, N'b0000173-0000-0000-0000-000000000173'), N'ProjectPaymentReversal.Approve', N'ProjectPaymentReversal', N'Approve', N'Approve or reject project Payment Request commitment reversals assigned within the actor''s authorized project scope', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000172-0000-0000-0000-000000000172'), N'ProjectPaymentReversal.Create', N'ProjectPaymentReversal', N'Create', N'Create and submit approved project Payment Request commitment reversals', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000237-0000-0000-0000-000000000237'), N'Report.View', N'Report', N'View', N'View reports within verified tenant and organization query scope.', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000017-0000-0000-0000-000000000017'), N'Resource.Assign', N'Resource', N'Assign', N'Assign resources to projects', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000024-0000-0000-0000-000000000024'), N'Resource.Create', N'Resource', N'Create', N'Create resources and vendors', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000026-0000-0000-0000-000000000026'), N'Resource.Delete', N'Resource', N'Delete', N'Deactivate or delete resources and vendors', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000025-0000-0000-0000-000000000025'), N'Resource.Edit', N'Resource', N'Edit', N'Edit resources and vendors', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000022-0000-0000-0000-000000000022'), N'Resource.EditCostRate', N'Resource', N'EditCostRate', N'Edit resource cost rates', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000016-0000-0000-0000-000000000016'), N'Resource.View', N'Resource', N'View', N'View resources', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000021-0000-0000-0000-000000000021'), N'Resource.ViewCostRate', N'Resource', N'ViewCostRate', N'View resource cost rates', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000112-0000-0000-0000-000000000112'), N'Risk.Create', N'Risk', N'Create', N'Create project risks', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000114-0000-0000-0000-000000000114'), N'Risk.Delete', N'Risk', N'Delete', N'Delete risk children', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000113-0000-0000-0000-000000000113'), N'Risk.Edit', N'Risk', N'Edit', N'Edit project risks and follow-through', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000115-0000-0000-0000-000000000115'), N'Risk.Mitigate', N'Risk', N'Mitigate', N'Transition and mitigate project risks', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000111-0000-0000-0000-000000000111'), N'Risk.View', N'Risk', N'View', N'View project risks', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000238-0000-0000-0000-000000000238'), N'Settings.View', N'Settings', N'View', N'View tenant and organization settings records within authorized tenant and management scope.', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000133-0000-0000-0000-000000000133'), N'Stakeholders.Manage', N'Stakeholders', N'Manage', N'Manage project stakeholders', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000132-0000-0000-0000-000000000132'), N'Stakeholders.Read', N'Stakeholders', N'Read', N'View project stakeholders', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000158-0000-0000-0000-000000000158'), N'StatusReport.Create', N'StatusReport', N'Create', N'Create draft project status reports', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000159-0000-0000-0000-000000000159'), N'StatusReport.Edit', N'StatusReport', N'Edit', N'Edit draft project status reports', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000164-0000-0000-0000-000000000164'), N'StatusReport.Export', N'StatusReport', N'Export', N'Export a project status report', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000165-0000-0000-0000-000000000165'), N'StatusReport.ManageConfiguration', N'StatusReport', N'ManageConfiguration', N'Configure official project reporting scope', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000163-0000-0000-0000-000000000163'), N'StatusReport.Publish', N'StatusReport', N'Publish', N'Publish a Draft status report and capture its immutable project snapshot', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000157-0000-0000-0000-000000000157'), N'StatusReport.View', N'StatusReport', N'View', N'View project status reports and performance', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000031-0000-0000-0000-000000000031'), N'Team.Create', N'Team', N'Create', N'Create teams', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000033-0000-0000-0000-000000000033'), N'Team.Delete', N'Team', N'Delete', N'Deactivate or delete teams', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000032-0000-0000-0000-000000000032'), N'Team.Edit', N'Team', N'Edit', N'Edit teams and manage their members', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000030-0000-0000-0000-000000000030'), N'Team.View', N'Team', N'View', N'View teams and their members', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000143-0000-0000-0000-000000000143'), N'TimeTracking.ManageOwn', N'TimeTracking', N'ManageOwn', N'Create, update, and delete own manual time entries', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000145-0000-0000-0000-000000000145'), N'TimeTracking.ManageTeam', N'TimeTracking', N'ManageTeam', N'Manage team manual time entries within management scope', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000142-0000-0000-0000-000000000142'), N'TimeTracking.ViewOwn', N'TimeTracking', N'ViewOwn', N'View own manual time entries', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000144-0000-0000-0000-000000000144'), N'TimeTracking.ViewTeam', N'TimeTracking', N'ViewTeam', N'View team manual time entries within management scope', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000239-0000-0000-0000-000000000239'), N'Users.View', N'Users', N'View', N'View users and access records within authorized tenant and management scope.', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000240-0000-0000-0000-000000000240'), N'Users.ResetPassword', N'Users', N'ResetPassword', N'Generate a one-time temporary password for a local user', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000140-0000-0000-0000-000000000140'), N'Workflow.Manage', N'Workflow', N'Manage', N'Edit workflow drafts', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000141-0000-0000-0000-000000000141'), N'Workflow.Publish', N'Workflow', N'Publish', N'Publish and retire workflow versions', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000139-0000-0000-0000-000000000139'), N'Workflow.View', N'Workflow', N'View', N'View workflow definitions and versions', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000124-0000-0000-0000-000000000124'), N'WorkItem.Create', N'WorkItem', N'Create', N'Create task and issue work items', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000126-0000-0000-0000-000000000126'), N'WorkItem.Delete', N'WorkItem', N'Delete', N'Delete task and issue work items', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000125-0000-0000-0000-000000000125'), N'WorkItem.Edit', N'WorkItem', N'Edit', N'Edit task and issue work items', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000123-0000-0000-0000-000000000123'), N'WorkItem.View', N'WorkItem', N'View', N'View task and issue work items', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000178-0000-0000-0000-000000000178'), N'ProjectAnnualBudget.View', N'ProjectAnnualBudget', N'View', N'Open the annual-budget workspace within tenant and project scope. Review-only users see assigned records only.', N'Project'),
    (CONVERT(uniqueidentifier, N'b0000179-0000-0000-0000-000000000179'), N'ProjectPaymentRequest.View', N'ProjectPaymentRequest', N'View', N'View requests and approval history in tenant/project scope; reviewer-only lists are assignment-filtered.', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000180-0000-0000-0000-000000000180'), N'ProjectAnnualBudgetClosing.View', N'ProjectAnnualBudgetClosing', N'View', N'View closings in tenant/project scope; reviewer-only lists are assignment-filtered.', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000233-0000-0000-0000-000000000233'), N'ProjectSettlement.View', N'ProjectSettlement', N'View', N'View Project settlement, reversal, and planned-payment records within authorized Project scope. Assigned reviewers retain access only to their assigned workflow record.', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000235-0000-0000-0000-000000000235'), N'ProjectFinancialAuditTrail.View', N'ProjectFinancialAuditTrail', N'View', N'View immutable financial audit records within authorized Project scope. This permission never allows changing or deleting audit history.', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000268-0000-0000-0000-000000000268'), N'ProjectFinancialFoundation.View', N'ProjectFinancialFoundation', N'View', N'Read the Project financial foundation summary, including the lifetime ceiling and source-backed actual-paid and outstanding-obligation totals when records are comparable. Legacy approved-payment allocation and available-budget fields may be null. Does not grant create, edit, approve, or export capabilities.', N'Project'),
    (CONVERT(uniqueidentifier, N'b0000270-0000-0000-0000-000000000270'), N'ProgramFinancialRollup.View', N'ProgramFinancialRollup', N'View', N'Read a Program financial rollup and its paged Project breakdown using only Projects within the current Tenant and authorized Program/descendant Project management scope. Does not grant Project workflow actions or unscoped Project access.', N'Program'),
    (CONVERT(uniqueidentifier, N'b0000271-0000-0000-0000-000000000271'), N'PortfolioFinancialRollup.View', N'PortfolioFinancialRollup', N'View', N'Read a Portfolio financial rollup and its paged Project breakdown using only Projects within the current Tenant and authorized Portfolio/descendant Program and Project management scope. Does not grant Project workflow actions or unscoped Project access.', N'Portfolio'),
    (CONVERT(uniqueidentifier, N'b0000181-0000-0000-0000-000000000181'), N'Dashboard.View', N'Dashboard', N'View', N'Open the dashboard workspace. Each widget data API continues to enforce its own permission and tenant/organization scope.', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000182-0000-0000-0000-000000000182'), N'ProjectAnnualBudget.Create', N'ProjectAnnualBudget', N'Create', N'Create an annual-budget draft within project and fiscal-year scope.', N'Project'),
    (CONVERT(uniqueidentifier, N'b0000183-0000-0000-0000-000000000183'), N'ProjectAnnualBudget.EditDraft', N'ProjectAnnualBudget', N'EditDraft', N'Edit an eligible draft before submission.', N'Project'),
    (CONVERT(uniqueidentifier, N'b0000184-0000-0000-0000-000000000184'), N'ProjectAnnualBudget.SubmitForReview', N'ProjectAnnualBudget', N'SubmitForReview', N'Submit an eligible draft to its configured review workflow.', N'Project'),
    (CONVERT(uniqueidentifier, N'b0000185-0000-0000-0000-000000000185'), N'ProjectAnnualBudget.ReviseApproved', N'ProjectAnnualBudget', N'ReviseApproved', N'Submit an eligible revision to an approved budget.', N'Project'),
    (CONVERT(uniqueidentifier, N'b0000186-0000-0000-0000-000000000186'), N'ProjectAnnualBudget.ManageApprovalConfiguration', N'ProjectAnnualBudget', N'ManageApprovalConfiguration', N'Change persistent Project Settings approval defaults; distinct from a one-submission override.', N'Project'),
    (CONVERT(uniqueidentifier, N'b0000187-0000-0000-0000-000000000187'), N'ProjectPaymentRequest.Create', N'ProjectPaymentRequest', N'Create', N'Create a request draft within project scope and currency rules.', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000188-0000-0000-0000-000000000188'), N'ProjectPaymentRequest.EditDraft', N'ProjectPaymentRequest', N'EditDraft', N'Edit an eligible request draft before submission.', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000189-0000-0000-0000-000000000189'), N'ProjectPaymentRequest.Submit', N'ProjectPaymentRequest', N'Submit', N'Submit an eligible request to its approval workflow.', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000190-0000-0000-0000-000000000190'), N'ProjectPaymentRequest.Cancel', N'ProjectPaymentRequest', N'Cancel', N'Cancel an eligible request under workflow-state rules; distinct from reversing settlement.', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000191-0000-0000-0000-000000000191'), N'ProjectPaymentRequest.Approve', N'ProjectPaymentRequest', N'Approve', N'Decide only an assigned request; active team membership, project scope, exact reviewer assignment, and separation of duties remain required.', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000192-0000-0000-0000-000000000192'), N'Program.CreateAcrossPortfolios', N'Program', N'CreateAcrossPortfolios', N'Allow creation outside native Portfolio assignment only within eligible tenant and organization scope; still requires Program.Create.', N'Organization'),
    (CONVERT(uniqueidentifier, N'b0000193-0000-0000-0000-000000000193'), N'Users.ManageAccounts', N'Users', N'ManageAccounts', N'Create users and update profile details. Account activation/deactivation requires Users.SetStatus. Excludes role assignment, permission overrides, and password reset.', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000217-0000-0000-0000-000000000217'), N'Users.SetStatus', N'Users', N'SetStatus', N'Activate or deactivate a tenant user account. This does not grant profile editing, role assignment, direct permission overrides, or password reset.', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000195-0000-0000-0000-000000000195'), N'Users.AssignRoles', N'Users', N'AssignRoles', N'Assign/remove tenant roles; changes effective access and require audit.', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000196-0000-0000-0000-000000000196'), N'UserPermissions.Manage', N'UserPermissions', N'Manage', N'Add/remove per-user Allow/Deny overrides with audit and delegation limits.', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000197-0000-0000-0000-000000000197'), N'Settings.ManageTenantConfiguration', N'Settings', N'ManageTenantConfiguration', N'Update tenant settings permitted by typed definitions; platform settings remain separate.', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000199-0000-0000-0000-000000000199'), N'Program.Create', N'Program', N'Create', N'Create a Program under an eligible Portfolio. Native assignment is required unless the explicit cross-Portfolio capability is also granted.', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000200-0000-0000-0000-000000000200'), N'Program.Edit', N'Program', N'Edit', N'Edit an existing Program within native management scope.', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000201-0000-0000-0000-000000000201'), N'Program.Archive', N'Program', N'Archive', N'Archive a Program within native management scope.', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000202-0000-0000-0000-000000000202'), N'Program.Reactivate', N'Program', N'Reactivate', N'Reactivate an archived Program within native management scope.', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000203-0000-0000-0000-000000000203'), N'Portfolio.Create', N'Portfolio', N'Create', N'Create a Portfolio within tenant and organization authority.', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000204-0000-0000-0000-000000000204'), N'Portfolio.Edit', N'Portfolio', N'Edit', N'Edit a Portfolio within tenant and organization authority.', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000205-0000-0000-0000-000000000205'), N'Portfolio.Archive', N'Portfolio', N'Archive', N'Archive a Portfolio subject to tenant, organization, and lifecycle checks.', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000206-0000-0000-0000-000000000206'), N'Portfolio.Reactivate', N'Portfolio', N'Reactivate', N'Reactivate an archived Portfolio subject to tenant, organization, and lifecycle checks.', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000207-0000-0000-0000-000000000207'), N'ProjectFinancialCeiling.ApproveChange', N'ProjectFinancialCeiling', N'ApproveChange', N'Qualify approval of a change request that alters the approved lifetime ceiling. Also requires the normal ChangeRequest.Approve decision capability, exact workflow assignment, project scope, and separation of duties.', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000212-0000-0000-0000-000000000212'), N'Roles.View', N'Roles', N'View', N'Open the tenant role and permission workspace. Role data remains tenant-scoped and delegation-limited.', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000213-0000-0000-0000-000000000213'), N'ReferenceData.View', N'ReferenceData', N'View', N'Open tenant-scoped reference data and lookup workspaces.', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000225-0000-0000-0000-000000000225'), N'ReferenceData.Create', N'ReferenceData', N'Create', N'Create supported tenant-managed reference data, including currencies, vendor categories, work-item statuses, and Project Roles.', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000226-0000-0000-0000-000000000226'), N'ReferenceData.Edit', N'ReferenceData', N'Edit', N'Edit supported tenant-managed reference data while preserving category-specific immutability and usage rules.', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000227-0000-0000-0000-000000000227'), N'ReferenceData.Delete', N'ReferenceData', N'Delete', N'Soft-delete currencies or deactivate supported lookup values where existing business rules permit. Referenced or immutable values remain protected.', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000214-0000-0000-0000-000000000214'), N'TenantAdministration.ManageScope', N'TenantAdministration', N'ManageScope', N'Grant tenant-wide administrative management scope within this tenant only. Does not grant platform or cross-tenant authority.', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000208-0000-0000-0000-000000000208'), N'Roles.Create', N'Roles', N'Create', N'Create a tenant-scoped role. Assigning permissions also requires Roles.ConfigurePermissions and may not exceed the actor''s delegated permissions.', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000209-0000-0000-0000-000000000209'), N'Roles.EditDetails', N'Roles', N'EditDetails', N'Change a role''s name, description, and category without changing its permission grants.', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000210-0000-0000-0000-000000000210'), N'Roles.ConfigurePermissions', N'Roles', N'ConfigurePermissions', N'Assign or remove permissions within the actor''s effective delegation authority; never permits self-escalation.', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000211-0000-0000-0000-000000000211'), N'Roles.Delete', N'Roles', N'Delete', N'Delete an unused tenant role. System roles and roles assigned to users cannot be deleted.', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000241-0000-0000-0000-000000000241'), N'Issue.View', N'Issue', N'View', N'Read tenant issues within the user''s assigned project scope.', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000242-0000-0000-0000-000000000242'), N'Plugin.View', N'Plugin', N'View', N'Read the current tenant''s plugin registry and activation metadata.', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000236-0000-0000-0000-000000000236'), N'Directory.ManageConnection', N'Directory', N'ManageConnection', N'Create and update directory connections and synchronization scope within the current tenant.', N'Tenant'),
    (CONVERT(uniqueidentifier, N'b0000243-0000-0000-0000-000000000243'), N'Planning.Edit', N'Planning', N'Edit', N'Create and edit plan activities, dependencies, assignments, and calendars within project scope.', N'Project'),
    (CONVERT(uniqueidentifier, N'b0000272-0000-0000-0000-000000000272'), N'ProjectSettings.View', N'ProjectSettings', N'View', N'Open Project Settings within the actor''s authorized Tenant and Project scope. This permission does not allow saving changes or replace permissions required for protected settings.', N'Project'),
    (CONVERT(uniqueidentifier, N'b0000273-0000-0000-0000-000000000273'), N'ProjectSettings.Edit', N'ProjectSettings', N'Edit', N'Edit Project Settings within the actor''s authorized Tenant and Project scope. Financial approval defaults and other protected settings continue to require their specific permissions.', N'Project');

DECLARE @RoleSeed TABLE (
    RoleId uniqueidentifier NOT NULL PRIMARY KEY,
    SystemRoleKey nvarchar(64) NOT NULL UNIQUE,
    Name nvarchar(200) NOT NULL,
    Description nvarchar(1000) NULL,
    Category nvarchar(200) NULL
);
INSERT @RoleSeed (RoleId, SystemRoleKey, Name, Description, Category)
VALUES
    (CONVERT(uniqueidentifier, N'a0000001-0000-0000-0000-000000000001'), N'tenant.admin', N'admin', N'Tenant-scoped administration and approved PMO capabilities', N'System'),
    (CONVERT(uniqueidentifier, N'a0000002-0000-0000-0000-000000000002'), N'tenant.executive', N'executive', N'Executive leadership access', N'Leadership'),
    (CONVERT(uniqueidentifier, N'a0000003-0000-0000-0000-000000000003'), N'tenant.portfolio-manager', N'portfolio.manager', N'Portfolio management access', N'Management'),
    (CONVERT(uniqueidentifier, N'a0000004-0000-0000-0000-000000000004'), N'tenant.project-manager', N'project.manager', N'Project management access', N'Management'),
    (CONVERT(uniqueidentifier, N'a0000005-0000-0000-0000-000000000005'), N'tenant.team-member', N'team.member', N'Team member access', N'Operations'),
    (CONVERT(uniqueidentifier, N'a0000006-0000-0000-0000-000000000006'), N'tenant.pmo-admin', N'pmo.admin', N'PMO administration access', N'PMO'),
    (CONVERT(uniqueidentifier, N'a0000007-0000-0000-0000-000000000007'), N'tenant.finance-controller', N'finance.controller', N'Financial management access', N'Finance'),
    (CONVERT(uniqueidentifier, N'a0000008-0000-0000-0000-000000000008'), N'tenant.resource-manager', N'resource.manager', N'Resource management access', N'Management');

DECLARE @RolePermissionSeed TABLE (
    SystemRoleKey nvarchar(64) NOT NULL,
    PermissionId uniqueidentifier NOT NULL,
    PermissionName nvarchar(200) NOT NULL,
    GrantClass nvarchar(40) NOT NULL,
    PermissionScope nvarchar(32) NOT NULL,
    PRIMARY KEY (SystemRoleKey, PermissionId)
);
INSERT @RolePermissionSeed (SystemRoleKey, PermissionId, PermissionName, GrantClass, PermissionScope)
VALUES
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000169-0000-0000-0000-000000000169'), N'Audit.View', N'ProtectedBaseline', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000154-0000-0000-0000-000000000154'), N'Directory.ExecuteSync', N'ProtectedBaseline', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000155-0000-0000-0000-000000000155'), N'Directory.ManageMappings', N'ProtectedBaseline', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000156-0000-0000-0000-000000000156'), N'Directory.ManageProvisioningPolicy', N'ProtectedBaseline', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000153-0000-0000-0000-000000000153'), N'Directory.PreviewSync', N'ProtectedBaseline', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000152-0000-0000-0000-000000000152'), N'Directory.TestConnection', N'ProtectedBaseline', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000150-0000-0000-0000-000000000150'), N'Directory.View', N'ProtectedBaseline', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000167-0000-0000-0000-000000000167'), N'Governance.AssignmentRecovery.Initiate', N'ProtectedBaseline', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000168-0000-0000-0000-000000000168'), N'Governance.AssignmentRecovery.Manage', N'ProtectedBaseline', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000166-0000-0000-0000-000000000166'), N'Governance.AssignmentRecovery.View', N'ProtectedBaseline', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000238-0000-0000-0000-000000000238'), N'Settings.View', N'ProtectedBaseline', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000239-0000-0000-0000-000000000239'), N'Users.View', N'ProtectedBaseline', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000240-0000-0000-0000-000000000240'), N'Users.ResetPassword', N'ProtectedBaseline', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000140-0000-0000-0000-000000000140'), N'Workflow.Manage', N'ProtectedBaseline', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000141-0000-0000-0000-000000000141'), N'Workflow.Publish', N'ProtectedBaseline', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000139-0000-0000-0000-000000000139'), N'Workflow.View', N'ProtectedBaseline', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000193-0000-0000-0000-000000000193'), N'Users.ManageAccounts', N'ProtectedBaseline', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000217-0000-0000-0000-000000000217'), N'Users.SetStatus', N'ProtectedBaseline', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000195-0000-0000-0000-000000000195'), N'Users.AssignRoles', N'ProtectedBaseline', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000196-0000-0000-0000-000000000196'), N'UserPermissions.Manage', N'ProtectedBaseline', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000197-0000-0000-0000-000000000197'), N'Settings.ManageTenantConfiguration', N'ProtectedBaseline', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000212-0000-0000-0000-000000000212'), N'Roles.View', N'ProtectedBaseline', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000213-0000-0000-0000-000000000213'), N'ReferenceData.View', N'ProtectedBaseline', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000225-0000-0000-0000-000000000225'), N'ReferenceData.Create', N'ProtectedBaseline', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000226-0000-0000-0000-000000000226'), N'ReferenceData.Edit', N'ProtectedBaseline', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000227-0000-0000-0000-000000000227'), N'ReferenceData.Delete', N'ProtectedBaseline', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000214-0000-0000-0000-000000000214'), N'TenantAdministration.ManageScope', N'ProtectedBaseline', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000208-0000-0000-0000-000000000208'), N'Roles.Create', N'ProtectedBaseline', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000209-0000-0000-0000-000000000209'), N'Roles.EditDetails', N'ProtectedBaseline', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000210-0000-0000-0000-000000000210'), N'Roles.ConfigurePermissions', N'ProtectedBaseline', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000211-0000-0000-0000-000000000211'), N'Roles.Delete', N'ProtectedBaseline', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000242-0000-0000-0000-000000000242'), N'Plugin.View', N'ProtectedBaseline', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000236-0000-0000-0000-000000000236'), N'Directory.ManageConnection', N'ProtectedBaseline', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000119-0000-0000-0000-000000000119'), N'Portfolio.View', N'ProtectedBaseline', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000203-0000-0000-0000-000000000203'), N'Portfolio.Create', N'ProtectedBaseline', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000204-0000-0000-0000-000000000204'), N'Portfolio.Edit', N'ProtectedBaseline', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000205-0000-0000-0000-000000000205'), N'Portfolio.Archive', N'ProtectedBaseline', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000206-0000-0000-0000-000000000206'), N'Portfolio.Reactivate', N'ProtectedBaseline', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000121-0000-0000-0000-000000000121'), N'Program.View', N'ProtectedBaseline', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000199-0000-0000-0000-000000000199'), N'Program.Create', N'ProtectedBaseline', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000200-0000-0000-0000-000000000200'), N'Program.Edit', N'ProtectedBaseline', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000201-0000-0000-0000-000000000201'), N'Program.Archive', N'ProtectedBaseline', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000202-0000-0000-0000-000000000202'), N'Program.Reactivate', N'ProtectedBaseline', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000108-0000-0000-0000-000000000108'), N'Handover.View', N'ProtectedBaseline', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000109-0000-0000-0000-000000000109'), N'Handover.Manage', N'ProtectedBaseline', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000110-0000-0000-0000-000000000110'), N'Handover.Approve', N'ProtectedBaseline', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000149-0000-0000-0000-000000000149'), N'Handover.AdminInitiate', N'ProtectedBaseline', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000215-0000-0000-0000-000000000215'), N'BudgetRevision.View', N'ApprovedSupplementary', N'Project'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000107-0000-0000-0000-000000000107'), N'ChangeRequest.Approve', N'ApprovedSupplementary', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000106-0000-0000-0000-000000000106'), N'ChangeRequest.Manage', N'ApprovedSupplementary', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000105-0000-0000-0000-000000000105'), N'ChangeRequest.View', N'ApprovedSupplementary', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000181-0000-0000-0000-000000000181'), N'Dashboard.View', N'ApprovedSupplementary', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000137-0000-0000-0000-000000000137'), N'Document.Archive', N'ApprovedSupplementary', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000135-0000-0000-0000-000000000135'), N'Document.Create', N'ApprovedSupplementary', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000136-0000-0000-0000-000000000136'), N'Document.Edit', N'ApprovedSupplementary', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000138-0000-0000-0000-000000000138'), N'Document.TemplateManage', N'ApprovedSupplementary', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000134-0000-0000-0000-000000000134'), N'Document.View', N'ApprovedSupplementary', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000228-0000-0000-0000-000000000228'), N'ProjectFinance.View', N'ApprovedSupplementary', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000128-0000-0000-0000-000000000128'), N'Meeting.Create', N'ApprovedSupplementary', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000131-0000-0000-0000-000000000131'), N'Meeting.Delete', N'ApprovedSupplementary', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000129-0000-0000-0000-000000000129'), N'Meeting.Edit', N'ApprovedSupplementary', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000130-0000-0000-0000-000000000130'), N'Meeting.Manage', N'ApprovedSupplementary', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000127-0000-0000-0000-000000000127'), N'Meeting.View', N'ApprovedSupplementary', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000118-0000-0000-0000-000000000118'), N'Planning.Lifecycle', N'ApprovedSupplementary', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000116-0000-0000-0000-000000000116'), N'Planning.View', N'ApprovedSupplementary', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000102-0000-0000-0000-000000000102'), N'Project.Create', N'ApprovedSupplementary', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000104-0000-0000-0000-000000000104'), N'Project.Delete', N'ApprovedSupplementary', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000103-0000-0000-0000-000000000103'), N'Project.Edit', N'ApprovedSupplementary', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000101-0000-0000-0000-000000000101'), N'Project.View', N'ApprovedSupplementary', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000180-0000-0000-0000-000000000180'), N'ProjectAnnualBudgetClosing.View', N'ApprovedSupplementary', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000190-0000-0000-0000-000000000190'), N'ProjectPaymentRequest.Cancel', N'ApprovedSupplementary', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000187-0000-0000-0000-000000000187'), N'ProjectPaymentRequest.Create', N'ApprovedSupplementary', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000188-0000-0000-0000-000000000188'), N'ProjectPaymentRequest.EditDraft', N'ApprovedSupplementary', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000189-0000-0000-0000-000000000189'), N'ProjectPaymentRequest.Submit', N'ApprovedSupplementary', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000179-0000-0000-0000-000000000179'), N'ProjectPaymentRequest.View', N'ApprovedSupplementary', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000237-0000-0000-0000-000000000237'), N'Report.View', N'ApprovedSupplementary', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000017-0000-0000-0000-000000000017'), N'Resource.Assign', N'ApprovedSupplementary', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000024-0000-0000-0000-000000000024'), N'Resource.Create', N'ApprovedSupplementary', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000026-0000-0000-0000-000000000026'), N'Resource.Delete', N'ApprovedSupplementary', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000025-0000-0000-0000-000000000025'), N'Resource.Edit', N'ApprovedSupplementary', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000022-0000-0000-0000-000000000022'), N'Resource.EditCostRate', N'ApprovedSupplementary', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000016-0000-0000-0000-000000000016'), N'Resource.View', N'ApprovedSupplementary', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000021-0000-0000-0000-000000000021'), N'Resource.ViewCostRate', N'ApprovedSupplementary', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000112-0000-0000-0000-000000000112'), N'Risk.Create', N'ApprovedSupplementary', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000114-0000-0000-0000-000000000114'), N'Risk.Delete', N'ApprovedSupplementary', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000113-0000-0000-0000-000000000113'), N'Risk.Edit', N'ApprovedSupplementary', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000115-0000-0000-0000-000000000115'), N'Risk.Mitigate', N'ApprovedSupplementary', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000111-0000-0000-0000-000000000111'), N'Risk.View', N'ApprovedSupplementary', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000133-0000-0000-0000-000000000133'), N'Stakeholders.Manage', N'ApprovedSupplementary', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000132-0000-0000-0000-000000000132'), N'Stakeholders.Read', N'ApprovedSupplementary', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000158-0000-0000-0000-000000000158'), N'StatusReport.Create', N'ApprovedSupplementary', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000159-0000-0000-0000-000000000159'), N'StatusReport.Edit', N'ApprovedSupplementary', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000164-0000-0000-0000-000000000164'), N'StatusReport.Export', N'ApprovedSupplementary', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000165-0000-0000-0000-000000000165'), N'StatusReport.ManageConfiguration', N'ApprovedSupplementary', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000163-0000-0000-0000-000000000163'), N'StatusReport.Publish', N'ApprovedSupplementary', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000157-0000-0000-0000-000000000157'), N'StatusReport.View', N'ApprovedSupplementary', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000031-0000-0000-0000-000000000031'), N'Team.Create', N'ApprovedSupplementary', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000033-0000-0000-0000-000000000033'), N'Team.Delete', N'ApprovedSupplementary', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000032-0000-0000-0000-000000000032'), N'Team.Edit', N'ApprovedSupplementary', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000030-0000-0000-0000-000000000030'), N'Team.View', N'ApprovedSupplementary', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000143-0000-0000-0000-000000000143'), N'TimeTracking.ManageOwn', N'ApprovedSupplementary', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000145-0000-0000-0000-000000000145'), N'TimeTracking.ManageTeam', N'ApprovedSupplementary', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000142-0000-0000-0000-000000000142'), N'TimeTracking.ViewOwn', N'ApprovedSupplementary', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000144-0000-0000-0000-000000000144'), N'TimeTracking.ViewTeam', N'ApprovedSupplementary', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000124-0000-0000-0000-000000000124'), N'WorkItem.Create', N'ApprovedSupplementary', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000126-0000-0000-0000-000000000126'), N'WorkItem.Delete', N'ApprovedSupplementary', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000125-0000-0000-0000-000000000125'), N'WorkItem.Edit', N'ApprovedSupplementary', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000123-0000-0000-0000-000000000123'), N'WorkItem.View', N'ApprovedSupplementary', N'Tenant'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000273-0000-0000-0000-000000000273'), N'ProjectSettings.Edit', N'ApprovedProjectSettings', N'Project'),
    (N'tenant.admin', CONVERT(uniqueidentifier, N'b0000272-0000-0000-0000-000000000272'), N'ProjectSettings.View', N'ApprovedProjectSettings', N'Project'),
    (N'tenant.executive', CONVERT(uniqueidentifier, N'b0000101-0000-0000-0000-000000000101'), N'Project.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.executive', CONVERT(uniqueidentifier, N'b0000104-0000-0000-0000-000000000104'), N'Project.Delete', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.executive', CONVERT(uniqueidentifier, N'b0000119-0000-0000-0000-000000000119'), N'Portfolio.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.executive', CONVERT(uniqueidentifier, N'b0000016-0000-0000-0000-000000000016'), N'Resource.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.executive', CONVERT(uniqueidentifier, N'b0000239-0000-0000-0000-000000000239'), N'Users.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.executive', CONVERT(uniqueidentifier, N'b0000228-0000-0000-0000-000000000228'), N'ProjectFinance.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.executive', CONVERT(uniqueidentifier, N'b0000237-0000-0000-0000-000000000237'), N'Report.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.executive', CONVERT(uniqueidentifier, N'b0000017-0000-0000-0000-000000000017'), N'Resource.Assign', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.executive', CONVERT(uniqueidentifier, N'b0000021-0000-0000-0000-000000000021'), N'Resource.ViewCostRate', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.executive', CONVERT(uniqueidentifier, N'b0000022-0000-0000-0000-000000000022'), N'Resource.EditCostRate', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.executive', CONVERT(uniqueidentifier, N'b0000102-0000-0000-0000-000000000102'), N'Project.Create', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.executive', CONVERT(uniqueidentifier, N'b0000103-0000-0000-0000-000000000103'), N'Project.Edit', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.executive', CONVERT(uniqueidentifier, N'b0000132-0000-0000-0000-000000000132'), N'Stakeholders.Read', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.executive', CONVERT(uniqueidentifier, N'b0000133-0000-0000-0000-000000000133'), N'Stakeholders.Manage', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.executive', CONVERT(uniqueidentifier, N'b0000105-0000-0000-0000-000000000105'), N'ChangeRequest.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.executive', CONVERT(uniqueidentifier, N'b0000106-0000-0000-0000-000000000106'), N'ChangeRequest.Manage', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.executive', CONVERT(uniqueidentifier, N'b0000107-0000-0000-0000-000000000107'), N'ChangeRequest.Approve', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.executive', CONVERT(uniqueidentifier, N'b0000108-0000-0000-0000-000000000108'), N'Handover.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.executive', CONVERT(uniqueidentifier, N'b0000109-0000-0000-0000-000000000109'), N'Handover.Manage', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.executive', CONVERT(uniqueidentifier, N'b0000110-0000-0000-0000-000000000110'), N'Handover.Approve', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.executive', CONVERT(uniqueidentifier, N'b0000111-0000-0000-0000-000000000111'), N'Risk.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.executive', CONVERT(uniqueidentifier, N'b0000112-0000-0000-0000-000000000112'), N'Risk.Create', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.executive', CONVERT(uniqueidentifier, N'b0000113-0000-0000-0000-000000000113'), N'Risk.Edit', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.executive', CONVERT(uniqueidentifier, N'b0000114-0000-0000-0000-000000000114'), N'Risk.Delete', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.executive', CONVERT(uniqueidentifier, N'b0000115-0000-0000-0000-000000000115'), N'Risk.Mitigate', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.executive', CONVERT(uniqueidentifier, N'b0000116-0000-0000-0000-000000000116'), N'Planning.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.executive', CONVERT(uniqueidentifier, N'b0000243-0000-0000-0000-000000000243'), N'Planning.Edit', N'SystemRoleSeed', N'Project'),
    (N'tenant.executive', CONVERT(uniqueidentifier, N'b0000118-0000-0000-0000-000000000118'), N'Planning.Lifecycle', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.executive', CONVERT(uniqueidentifier, N'b0000203-0000-0000-0000-000000000203'), N'Portfolio.Create', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.executive', CONVERT(uniqueidentifier, N'b0000204-0000-0000-0000-000000000204'), N'Portfolio.Edit', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.executive', CONVERT(uniqueidentifier, N'b0000205-0000-0000-0000-000000000205'), N'Portfolio.Archive', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.executive', CONVERT(uniqueidentifier, N'b0000206-0000-0000-0000-000000000206'), N'Portfolio.Reactivate', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.executive', CONVERT(uniqueidentifier, N'b0000121-0000-0000-0000-000000000121'), N'Program.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.executive', CONVERT(uniqueidentifier, N'b0000199-0000-0000-0000-000000000199'), N'Program.Create', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.executive', CONVERT(uniqueidentifier, N'b0000200-0000-0000-0000-000000000200'), N'Program.Edit', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.executive', CONVERT(uniqueidentifier, N'b0000201-0000-0000-0000-000000000201'), N'Program.Archive', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.executive', CONVERT(uniqueidentifier, N'b0000202-0000-0000-0000-000000000202'), N'Program.Reactivate', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.executive', CONVERT(uniqueidentifier, N'b0000123-0000-0000-0000-000000000123'), N'WorkItem.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.executive', CONVERT(uniqueidentifier, N'b0000142-0000-0000-0000-000000000142'), N'TimeTracking.ViewOwn', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.executive', CONVERT(uniqueidentifier, N'b0000143-0000-0000-0000-000000000143'), N'TimeTracking.ManageOwn', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.executive', CONVERT(uniqueidentifier, N'b0000144-0000-0000-0000-000000000144'), N'TimeTracking.ViewTeam', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.executive', CONVERT(uniqueidentifier, N'b0000145-0000-0000-0000-000000000145'), N'TimeTracking.ManageTeam', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.executive', CONVERT(uniqueidentifier, N'b0000157-0000-0000-0000-000000000157'), N'StatusReport.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.executive', CONVERT(uniqueidentifier, N'b0000164-0000-0000-0000-000000000164'), N'StatusReport.Export', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.executive', CONVERT(uniqueidentifier, N'b0000127-0000-0000-0000-000000000127'), N'Meeting.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.executive', CONVERT(uniqueidentifier, N'b0000134-0000-0000-0000-000000000134'), N'Document.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.executive', CONVERT(uniqueidentifier, N'b0000241-0000-0000-0000-000000000241'), N'Issue.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.portfolio-manager', CONVERT(uniqueidentifier, N'b0000119-0000-0000-0000-000000000119'), N'Portfolio.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.portfolio-manager', CONVERT(uniqueidentifier, N'b0000101-0000-0000-0000-000000000101'), N'Project.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.portfolio-manager', CONVERT(uniqueidentifier, N'b0000237-0000-0000-0000-000000000237'), N'Report.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.portfolio-manager', CONVERT(uniqueidentifier, N'b0000103-0000-0000-0000-000000000103'), N'Project.Edit', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.portfolio-manager', CONVERT(uniqueidentifier, N'b0000132-0000-0000-0000-000000000132'), N'Stakeholders.Read', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.portfolio-manager', CONVERT(uniqueidentifier, N'b0000133-0000-0000-0000-000000000133'), N'Stakeholders.Manage', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.portfolio-manager', CONVERT(uniqueidentifier, N'b0000105-0000-0000-0000-000000000105'), N'ChangeRequest.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.portfolio-manager', CONVERT(uniqueidentifier, N'b0000108-0000-0000-0000-000000000108'), N'Handover.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.portfolio-manager', CONVERT(uniqueidentifier, N'b0000109-0000-0000-0000-000000000109'), N'Handover.Manage', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.portfolio-manager', CONVERT(uniqueidentifier, N'b0000110-0000-0000-0000-000000000110'), N'Handover.Approve', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.portfolio-manager', CONVERT(uniqueidentifier, N'b0000116-0000-0000-0000-000000000116'), N'Planning.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.portfolio-manager', CONVERT(uniqueidentifier, N'b0000111-0000-0000-0000-000000000111'), N'Risk.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.portfolio-manager', CONVERT(uniqueidentifier, N'b0000203-0000-0000-0000-000000000203'), N'Portfolio.Create', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.portfolio-manager', CONVERT(uniqueidentifier, N'b0000204-0000-0000-0000-000000000204'), N'Portfolio.Edit', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.portfolio-manager', CONVERT(uniqueidentifier, N'b0000205-0000-0000-0000-000000000205'), N'Portfolio.Archive', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.portfolio-manager', CONVERT(uniqueidentifier, N'b0000206-0000-0000-0000-000000000206'), N'Portfolio.Reactivate', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.portfolio-manager', CONVERT(uniqueidentifier, N'b0000121-0000-0000-0000-000000000121'), N'Program.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.portfolio-manager', CONVERT(uniqueidentifier, N'b0000199-0000-0000-0000-000000000199'), N'Program.Create', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.portfolio-manager', CONVERT(uniqueidentifier, N'b0000200-0000-0000-0000-000000000200'), N'Program.Edit', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.portfolio-manager', CONVERT(uniqueidentifier, N'b0000201-0000-0000-0000-000000000201'), N'Program.Archive', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.portfolio-manager', CONVERT(uniqueidentifier, N'b0000202-0000-0000-0000-000000000202'), N'Program.Reactivate', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.portfolio-manager', CONVERT(uniqueidentifier, N'b0000123-0000-0000-0000-000000000123'), N'WorkItem.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.portfolio-manager', CONVERT(uniqueidentifier, N'b0000124-0000-0000-0000-000000000124'), N'WorkItem.Create', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.portfolio-manager', CONVERT(uniqueidentifier, N'b0000125-0000-0000-0000-000000000125'), N'WorkItem.Edit', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.portfolio-manager', CONVERT(uniqueidentifier, N'b0000142-0000-0000-0000-000000000142'), N'TimeTracking.ViewOwn', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.portfolio-manager', CONVERT(uniqueidentifier, N'b0000143-0000-0000-0000-000000000143'), N'TimeTracking.ManageOwn', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.portfolio-manager', CONVERT(uniqueidentifier, N'b0000144-0000-0000-0000-000000000144'), N'TimeTracking.ViewTeam', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.portfolio-manager', CONVERT(uniqueidentifier, N'b0000145-0000-0000-0000-000000000145'), N'TimeTracking.ManageTeam', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.portfolio-manager', CONVERT(uniqueidentifier, N'b0000157-0000-0000-0000-000000000157'), N'StatusReport.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.portfolio-manager', CONVERT(uniqueidentifier, N'b0000164-0000-0000-0000-000000000164'), N'StatusReport.Export', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.portfolio-manager', CONVERT(uniqueidentifier, N'b0000127-0000-0000-0000-000000000127'), N'Meeting.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.portfolio-manager', CONVERT(uniqueidentifier, N'b0000128-0000-0000-0000-000000000128'), N'Meeting.Create', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.portfolio-manager', CONVERT(uniqueidentifier, N'b0000129-0000-0000-0000-000000000129'), N'Meeting.Edit', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.portfolio-manager', CONVERT(uniqueidentifier, N'b0000134-0000-0000-0000-000000000134'), N'Document.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.portfolio-manager', CONVERT(uniqueidentifier, N'b0000135-0000-0000-0000-000000000135'), N'Document.Create', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.portfolio-manager', CONVERT(uniqueidentifier, N'b0000136-0000-0000-0000-000000000136'), N'Document.Edit', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.portfolio-manager', CONVERT(uniqueidentifier, N'b0000241-0000-0000-0000-000000000241'), N'Issue.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.project-manager', CONVERT(uniqueidentifier, N'b0000101-0000-0000-0000-000000000101'), N'Project.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.project-manager', CONVERT(uniqueidentifier, N'b0000016-0000-0000-0000-000000000016'), N'Resource.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.project-manager', CONVERT(uniqueidentifier, N'b0000237-0000-0000-0000-000000000237'), N'Report.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.project-manager', CONVERT(uniqueidentifier, N'b0000017-0000-0000-0000-000000000017'), N'Resource.Assign', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.project-manager', CONVERT(uniqueidentifier, N'b0000024-0000-0000-0000-000000000024'), N'Resource.Create', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.project-manager', CONVERT(uniqueidentifier, N'b0000025-0000-0000-0000-000000000025'), N'Resource.Edit', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.project-manager', CONVERT(uniqueidentifier, N'b0000102-0000-0000-0000-000000000102'), N'Project.Create', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.project-manager', CONVERT(uniqueidentifier, N'b0000103-0000-0000-0000-000000000103'), N'Project.Edit', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.project-manager', CONVERT(uniqueidentifier, N'b0000104-0000-0000-0000-000000000104'), N'Project.Delete', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.project-manager', CONVERT(uniqueidentifier, N'b0000132-0000-0000-0000-000000000132'), N'Stakeholders.Read', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.project-manager', CONVERT(uniqueidentifier, N'b0000133-0000-0000-0000-000000000133'), N'Stakeholders.Manage', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.project-manager', CONVERT(uniqueidentifier, N'b0000105-0000-0000-0000-000000000105'), N'ChangeRequest.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.project-manager', CONVERT(uniqueidentifier, N'b0000106-0000-0000-0000-000000000106'), N'ChangeRequest.Manage', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.project-manager', CONVERT(uniqueidentifier, N'b0000108-0000-0000-0000-000000000108'), N'Handover.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.project-manager', CONVERT(uniqueidentifier, N'b0000109-0000-0000-0000-000000000109'), N'Handover.Manage', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.project-manager', CONVERT(uniqueidentifier, N'b0000110-0000-0000-0000-000000000110'), N'Handover.Approve', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.project-manager', CONVERT(uniqueidentifier, N'b0000111-0000-0000-0000-000000000111'), N'Risk.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.project-manager', CONVERT(uniqueidentifier, N'b0000112-0000-0000-0000-000000000112'), N'Risk.Create', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.project-manager', CONVERT(uniqueidentifier, N'b0000113-0000-0000-0000-000000000113'), N'Risk.Edit', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.project-manager', CONVERT(uniqueidentifier, N'b0000114-0000-0000-0000-000000000114'), N'Risk.Delete', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.project-manager', CONVERT(uniqueidentifier, N'b0000115-0000-0000-0000-000000000115'), N'Risk.Mitigate', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.project-manager', CONVERT(uniqueidentifier, N'b0000116-0000-0000-0000-000000000116'), N'Planning.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.project-manager', CONVERT(uniqueidentifier, N'b0000243-0000-0000-0000-000000000243'), N'Planning.Edit', N'SystemRoleSeed', N'Project'),
    (N'tenant.project-manager', CONVERT(uniqueidentifier, N'b0000121-0000-0000-0000-000000000121'), N'Program.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.project-manager', CONVERT(uniqueidentifier, N'b0000123-0000-0000-0000-000000000123'), N'WorkItem.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.project-manager', CONVERT(uniqueidentifier, N'b0000124-0000-0000-0000-000000000124'), N'WorkItem.Create', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.project-manager', CONVERT(uniqueidentifier, N'b0000125-0000-0000-0000-000000000125'), N'WorkItem.Edit', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.project-manager', CONVERT(uniqueidentifier, N'b0000142-0000-0000-0000-000000000142'), N'TimeTracking.ViewOwn', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.project-manager', CONVERT(uniqueidentifier, N'b0000143-0000-0000-0000-000000000143'), N'TimeTracking.ManageOwn', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.project-manager', CONVERT(uniqueidentifier, N'b0000144-0000-0000-0000-000000000144'), N'TimeTracking.ViewTeam', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.project-manager', CONVERT(uniqueidentifier, N'b0000145-0000-0000-0000-000000000145'), N'TimeTracking.ManageTeam', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.project-manager', CONVERT(uniqueidentifier, N'b0000157-0000-0000-0000-000000000157'), N'StatusReport.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.project-manager', CONVERT(uniqueidentifier, N'b0000158-0000-0000-0000-000000000158'), N'StatusReport.Create', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.project-manager', CONVERT(uniqueidentifier, N'b0000159-0000-0000-0000-000000000159'), N'StatusReport.Edit', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.project-manager', CONVERT(uniqueidentifier, N'b0000164-0000-0000-0000-000000000164'), N'StatusReport.Export', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.project-manager', CONVERT(uniqueidentifier, N'b0000165-0000-0000-0000-000000000165'), N'StatusReport.ManageConfiguration', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.project-manager', CONVERT(uniqueidentifier, N'b0000127-0000-0000-0000-000000000127'), N'Meeting.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.project-manager', CONVERT(uniqueidentifier, N'b0000128-0000-0000-0000-000000000128'), N'Meeting.Create', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.project-manager', CONVERT(uniqueidentifier, N'b0000129-0000-0000-0000-000000000129'), N'Meeting.Edit', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.project-manager', CONVERT(uniqueidentifier, N'b0000130-0000-0000-0000-000000000130'), N'Meeting.Manage', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.project-manager', CONVERT(uniqueidentifier, N'b0000131-0000-0000-0000-000000000131'), N'Meeting.Delete', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.project-manager', CONVERT(uniqueidentifier, N'b0000134-0000-0000-0000-000000000134'), N'Document.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.project-manager', CONVERT(uniqueidentifier, N'b0000135-0000-0000-0000-000000000135'), N'Document.Create', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.project-manager', CONVERT(uniqueidentifier, N'b0000136-0000-0000-0000-000000000136'), N'Document.Edit', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.project-manager', CONVERT(uniqueidentifier, N'b0000241-0000-0000-0000-000000000241'), N'Issue.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.team-member', CONVERT(uniqueidentifier, N'b0000101-0000-0000-0000-000000000101'), N'Project.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.team-member', CONVERT(uniqueidentifier, N'b0000132-0000-0000-0000-000000000132'), N'Stakeholders.Read', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.team-member', CONVERT(uniqueidentifier, N'b0000111-0000-0000-0000-000000000111'), N'Risk.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.team-member', CONVERT(uniqueidentifier, N'b0000116-0000-0000-0000-000000000116'), N'Planning.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.team-member', CONVERT(uniqueidentifier, N'b0000119-0000-0000-0000-000000000119'), N'Portfolio.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.team-member', CONVERT(uniqueidentifier, N'b0000121-0000-0000-0000-000000000121'), N'Program.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.team-member', CONVERT(uniqueidentifier, N'b0000123-0000-0000-0000-000000000123'), N'WorkItem.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.team-member', CONVERT(uniqueidentifier, N'b0000124-0000-0000-0000-000000000124'), N'WorkItem.Create', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.team-member', CONVERT(uniqueidentifier, N'b0000125-0000-0000-0000-000000000125'), N'WorkItem.Edit', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.team-member', CONVERT(uniqueidentifier, N'b0000142-0000-0000-0000-000000000142'), N'TimeTracking.ViewOwn', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.team-member', CONVERT(uniqueidentifier, N'b0000143-0000-0000-0000-000000000143'), N'TimeTracking.ManageOwn', N'SystemRoleSeed', N'Tenant');

INSERT @RolePermissionSeed (SystemRoleKey, PermissionId, PermissionName, GrantClass, PermissionScope)
VALUES
    (N'tenant.team-member', CONVERT(uniqueidentifier, N'b0000127-0000-0000-0000-000000000127'), N'Meeting.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.team-member', CONVERT(uniqueidentifier, N'b0000134-0000-0000-0000-000000000134'), N'Document.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.team-member', CONVERT(uniqueidentifier, N'b0000135-0000-0000-0000-000000000135'), N'Document.Create', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.team-member', CONVERT(uniqueidentifier, N'b0000241-0000-0000-0000-000000000241'), N'Issue.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.pmo-admin', CONVERT(uniqueidentifier, N'b0000101-0000-0000-0000-000000000101'), N'Project.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.pmo-admin', CONVERT(uniqueidentifier, N'b0000104-0000-0000-0000-000000000104'), N'Project.Delete', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.pmo-admin', CONVERT(uniqueidentifier, N'b0000119-0000-0000-0000-000000000119'), N'Portfolio.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.pmo-admin', CONVERT(uniqueidentifier, N'b0000016-0000-0000-0000-000000000016'), N'Resource.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.pmo-admin', CONVERT(uniqueidentifier, N'b0000239-0000-0000-0000-000000000239'), N'Users.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.pmo-admin', CONVERT(uniqueidentifier, N'b0000228-0000-0000-0000-000000000228'), N'ProjectFinance.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.pmo-admin', CONVERT(uniqueidentifier, N'b0000237-0000-0000-0000-000000000237'), N'Report.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.pmo-admin', CONVERT(uniqueidentifier, N'b0000238-0000-0000-0000-000000000238'), N'Settings.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.pmo-admin', CONVERT(uniqueidentifier, N'b0000017-0000-0000-0000-000000000017'), N'Resource.Assign', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.pmo-admin', CONVERT(uniqueidentifier, N'b0000021-0000-0000-0000-000000000021'), N'Resource.ViewCostRate', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.pmo-admin', CONVERT(uniqueidentifier, N'b0000022-0000-0000-0000-000000000022'), N'Resource.EditCostRate', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.pmo-admin', CONVERT(uniqueidentifier, N'b0000102-0000-0000-0000-000000000102'), N'Project.Create', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.pmo-admin', CONVERT(uniqueidentifier, N'b0000103-0000-0000-0000-000000000103'), N'Project.Edit', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.pmo-admin', CONVERT(uniqueidentifier, N'b0000132-0000-0000-0000-000000000132'), N'Stakeholders.Read', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.pmo-admin', CONVERT(uniqueidentifier, N'b0000133-0000-0000-0000-000000000133'), N'Stakeholders.Manage', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.pmo-admin', CONVERT(uniqueidentifier, N'b0000105-0000-0000-0000-000000000105'), N'ChangeRequest.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.pmo-admin', CONVERT(uniqueidentifier, N'b0000106-0000-0000-0000-000000000106'), N'ChangeRequest.Manage', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.pmo-admin', CONVERT(uniqueidentifier, N'b0000107-0000-0000-0000-000000000107'), N'ChangeRequest.Approve', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.pmo-admin', CONVERT(uniqueidentifier, N'b0000108-0000-0000-0000-000000000108'), N'Handover.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.pmo-admin', CONVERT(uniqueidentifier, N'b0000109-0000-0000-0000-000000000109'), N'Handover.Manage', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.pmo-admin', CONVERT(uniqueidentifier, N'b0000110-0000-0000-0000-000000000110'), N'Handover.Approve', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.pmo-admin', CONVERT(uniqueidentifier, N'b0000149-0000-0000-0000-000000000149'), N'Handover.AdminInitiate', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.pmo-admin', CONVERT(uniqueidentifier, N'b0000166-0000-0000-0000-000000000166'), N'Governance.AssignmentRecovery.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.pmo-admin', CONVERT(uniqueidentifier, N'b0000167-0000-0000-0000-000000000167'), N'Governance.AssignmentRecovery.Initiate', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.pmo-admin', CONVERT(uniqueidentifier, N'b0000168-0000-0000-0000-000000000168'), N'Governance.AssignmentRecovery.Manage', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.pmo-admin', CONVERT(uniqueidentifier, N'b0000111-0000-0000-0000-000000000111'), N'Risk.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.pmo-admin', CONVERT(uniqueidentifier, N'b0000112-0000-0000-0000-000000000112'), N'Risk.Create', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.pmo-admin', CONVERT(uniqueidentifier, N'b0000113-0000-0000-0000-000000000113'), N'Risk.Edit', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.pmo-admin', CONVERT(uniqueidentifier, N'b0000114-0000-0000-0000-000000000114'), N'Risk.Delete', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.pmo-admin', CONVERT(uniqueidentifier, N'b0000115-0000-0000-0000-000000000115'), N'Risk.Mitigate', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.pmo-admin', CONVERT(uniqueidentifier, N'b0000116-0000-0000-0000-000000000116'), N'Planning.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.pmo-admin', CONVERT(uniqueidentifier, N'b0000243-0000-0000-0000-000000000243'), N'Planning.Edit', N'SystemRoleSeed', N'Project'),
    (N'tenant.pmo-admin', CONVERT(uniqueidentifier, N'b0000118-0000-0000-0000-000000000118'), N'Planning.Lifecycle', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.pmo-admin', CONVERT(uniqueidentifier, N'b0000203-0000-0000-0000-000000000203'), N'Portfolio.Create', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.pmo-admin', CONVERT(uniqueidentifier, N'b0000204-0000-0000-0000-000000000204'), N'Portfolio.Edit', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.pmo-admin', CONVERT(uniqueidentifier, N'b0000205-0000-0000-0000-000000000205'), N'Portfolio.Archive', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.pmo-admin', CONVERT(uniqueidentifier, N'b0000206-0000-0000-0000-000000000206'), N'Portfolio.Reactivate', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.pmo-admin', CONVERT(uniqueidentifier, N'b0000121-0000-0000-0000-000000000121'), N'Program.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.pmo-admin', CONVERT(uniqueidentifier, N'b0000199-0000-0000-0000-000000000199'), N'Program.Create', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.pmo-admin', CONVERT(uniqueidentifier, N'b0000200-0000-0000-0000-000000000200'), N'Program.Edit', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.pmo-admin', CONVERT(uniqueidentifier, N'b0000201-0000-0000-0000-000000000201'), N'Program.Archive', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.pmo-admin', CONVERT(uniqueidentifier, N'b0000202-0000-0000-0000-000000000202'), N'Program.Reactivate', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.pmo-admin', CONVERT(uniqueidentifier, N'b0000123-0000-0000-0000-000000000123'), N'WorkItem.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.pmo-admin', CONVERT(uniqueidentifier, N'b0000124-0000-0000-0000-000000000124'), N'WorkItem.Create', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.pmo-admin', CONVERT(uniqueidentifier, N'b0000125-0000-0000-0000-000000000125'), N'WorkItem.Edit', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.pmo-admin', CONVERT(uniqueidentifier, N'b0000126-0000-0000-0000-000000000126'), N'WorkItem.Delete', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.pmo-admin', CONVERT(uniqueidentifier, N'b0000139-0000-0000-0000-000000000139'), N'Workflow.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.pmo-admin', CONVERT(uniqueidentifier, N'b0000140-0000-0000-0000-000000000140'), N'Workflow.Manage', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.pmo-admin', CONVERT(uniqueidentifier, N'b0000141-0000-0000-0000-000000000141'), N'Workflow.Publish', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.pmo-admin', CONVERT(uniqueidentifier, N'b0000142-0000-0000-0000-000000000142'), N'TimeTracking.ViewOwn', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.pmo-admin', CONVERT(uniqueidentifier, N'b0000143-0000-0000-0000-000000000143'), N'TimeTracking.ManageOwn', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.pmo-admin', CONVERT(uniqueidentifier, N'b0000144-0000-0000-0000-000000000144'), N'TimeTracking.ViewTeam', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.pmo-admin', CONVERT(uniqueidentifier, N'b0000145-0000-0000-0000-000000000145'), N'TimeTracking.ManageTeam', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.pmo-admin', CONVERT(uniqueidentifier, N'b0000157-0000-0000-0000-000000000157'), N'StatusReport.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.pmo-admin', CONVERT(uniqueidentifier, N'b0000158-0000-0000-0000-000000000158'), N'StatusReport.Create', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.pmo-admin', CONVERT(uniqueidentifier, N'b0000159-0000-0000-0000-000000000159'), N'StatusReport.Edit', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.pmo-admin', CONVERT(uniqueidentifier, N'b0000163-0000-0000-0000-000000000163'), N'StatusReport.Publish', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.pmo-admin', CONVERT(uniqueidentifier, N'b0000164-0000-0000-0000-000000000164'), N'StatusReport.Export', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.pmo-admin', CONVERT(uniqueidentifier, N'b0000165-0000-0000-0000-000000000165'), N'StatusReport.ManageConfiguration', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.pmo-admin', CONVERT(uniqueidentifier, N'b0000169-0000-0000-0000-000000000169'), N'Audit.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.pmo-admin', CONVERT(uniqueidentifier, N'b0000127-0000-0000-0000-000000000127'), N'Meeting.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.pmo-admin', CONVERT(uniqueidentifier, N'b0000128-0000-0000-0000-000000000128'), N'Meeting.Create', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.pmo-admin', CONVERT(uniqueidentifier, N'b0000129-0000-0000-0000-000000000129'), N'Meeting.Edit', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.pmo-admin', CONVERT(uniqueidentifier, N'b0000130-0000-0000-0000-000000000130'), N'Meeting.Manage', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.pmo-admin', CONVERT(uniqueidentifier, N'b0000131-0000-0000-0000-000000000131'), N'Meeting.Delete', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.pmo-admin', CONVERT(uniqueidentifier, N'b0000134-0000-0000-0000-000000000134'), N'Document.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.pmo-admin', CONVERT(uniqueidentifier, N'b0000135-0000-0000-0000-000000000135'), N'Document.Create', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.pmo-admin', CONVERT(uniqueidentifier, N'b0000136-0000-0000-0000-000000000136'), N'Document.Edit', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.pmo-admin', CONVERT(uniqueidentifier, N'b0000137-0000-0000-0000-000000000137'), N'Document.Archive', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.pmo-admin', CONVERT(uniqueidentifier, N'b0000138-0000-0000-0000-000000000138'), N'Document.TemplateManage', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.pmo-admin', CONVERT(uniqueidentifier, N'b0000241-0000-0000-0000-000000000241'), N'Issue.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.pmo-admin', CONVERT(uniqueidentifier, N'b0000242-0000-0000-0000-000000000242'), N'Plugin.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.finance-controller', CONVERT(uniqueidentifier, N'b0000228-0000-0000-0000-000000000228'), N'ProjectFinance.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.finance-controller', CONVERT(uniqueidentifier, N'b0000244-0000-0000-0000-000000000244'), N'Budget.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.finance-controller', CONVERT(uniqueidentifier, N'b0000245-0000-0000-0000-000000000245'), N'Budget.Create', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.finance-controller', CONVERT(uniqueidentifier, N'b0000246-0000-0000-0000-000000000246'), N'Budget.Edit', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.finance-controller', CONVERT(uniqueidentifier, N'b0000247-0000-0000-0000-000000000247'), N'Budget.Delete', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.finance-controller', CONVERT(uniqueidentifier, N'b0000215-0000-0000-0000-000000000215'), N'BudgetRevision.View', N'SystemRoleSeed', N'Project'),
    (N'tenant.finance-controller', CONVERT(uniqueidentifier, N'b0000248-0000-0000-0000-000000000248'), N'BudgetRevision.Create', N'SystemRoleSeed', N'Project'),
    (N'tenant.finance-controller', CONVERT(uniqueidentifier, N'b0000249-0000-0000-0000-000000000249'), N'BudgetRevision.EditDraft', N'SystemRoleSeed', N'Project'),
    (N'tenant.finance-controller', CONVERT(uniqueidentifier, N'b0000250-0000-0000-0000-000000000250'), N'BudgetRevision.SubmitForReview', N'SystemRoleSeed', N'Project'),
    (N'tenant.finance-controller', CONVERT(uniqueidentifier, N'b0000216-0000-0000-0000-000000000216'), N'BudgetRevision.Approve', N'SystemRoleSeed', N'Project'),
    (N'tenant.finance-controller', CONVERT(uniqueidentifier, N'b0000251-0000-0000-0000-000000000251'), N'BudgetRevision.Cancel', N'SystemRoleSeed', N'Project'),
    (N'tenant.finance-controller', CONVERT(uniqueidentifier, N'b0000252-0000-0000-0000-000000000252'), N'BudgetRevision.Delete', N'SystemRoleSeed', N'Project'),
    (N'tenant.finance-controller', CONVERT(uniqueidentifier, N'b0000253-0000-0000-0000-000000000253'), N'Cost.View', N'SystemRoleSeed', N'Project'),
    (N'tenant.finance-controller', CONVERT(uniqueidentifier, N'b0000254-0000-0000-0000-000000000254'), N'Cost.Create', N'SystemRoleSeed', N'Project'),
    (N'tenant.finance-controller', CONVERT(uniqueidentifier, N'b0000255-0000-0000-0000-000000000255'), N'Cost.Edit', N'SystemRoleSeed', N'Project'),
    (N'tenant.finance-controller', CONVERT(uniqueidentifier, N'b0000256-0000-0000-0000-000000000256'), N'Cost.Delete', N'SystemRoleSeed', N'Project'),
    (N'tenant.finance-controller', CONVERT(uniqueidentifier, N'b0000257-0000-0000-0000-000000000257'), N'Commitment.View', N'SystemRoleSeed', N'Project'),
    (N'tenant.finance-controller', CONVERT(uniqueidentifier, N'b0000258-0000-0000-0000-000000000258'), N'Commitment.Create', N'SystemRoleSeed', N'Project'),
    (N'tenant.finance-controller', CONVERT(uniqueidentifier, N'b0000259-0000-0000-0000-000000000259'), N'Commitment.Edit', N'SystemRoleSeed', N'Project'),
    (N'tenant.finance-controller', CONVERT(uniqueidentifier, N'b0000260-0000-0000-0000-000000000260'), N'Commitment.Close', N'SystemRoleSeed', N'Project'),
    (N'tenant.finance-controller', CONVERT(uniqueidentifier, N'b0000261-0000-0000-0000-000000000261'), N'Commitment.Cancel', N'SystemRoleSeed', N'Project'),
    (N'tenant.finance-controller', CONVERT(uniqueidentifier, N'b0000262-0000-0000-0000-000000000262'), N'Commitment.Delete', N'SystemRoleSeed', N'Project'),
    (N'tenant.finance-controller', CONVERT(uniqueidentifier, N'b0000263-0000-0000-0000-000000000263'), N'PaymentObligation.View', N'SystemRoleSeed', N'Project'),
    (N'tenant.finance-controller', CONVERT(uniqueidentifier, N'b0000264-0000-0000-0000-000000000264'), N'PaymentObligation.Create', N'SystemRoleSeed', N'Project'),
    (N'tenant.finance-controller', CONVERT(uniqueidentifier, N'b0000265-0000-0000-0000-000000000265'), N'PaymentObligation.Edit', N'SystemRoleSeed', N'Project'),
    (N'tenant.finance-controller', CONVERT(uniqueidentifier, N'b0000266-0000-0000-0000-000000000266'), N'PaymentObligation.Delete', N'SystemRoleSeed', N'Project'),
    (N'tenant.finance-controller', CONVERT(uniqueidentifier, N'b0000267-0000-0000-0000-000000000267'), N'PaymentObligationSettlement.Record', N'SystemRoleSeed', N'Project'),
    (N'tenant.finance-controller', CONVERT(uniqueidentifier, N'b0000237-0000-0000-0000-000000000237'), N'Report.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.finance-controller', CONVERT(uniqueidentifier, N'b0000101-0000-0000-0000-000000000101'), N'Project.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.finance-controller', CONVERT(uniqueidentifier, N'b0000132-0000-0000-0000-000000000132'), N'Stakeholders.Read', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.finance-controller', CONVERT(uniqueidentifier, N'b0000119-0000-0000-0000-000000000119'), N'Portfolio.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.finance-controller', CONVERT(uniqueidentifier, N'b0000121-0000-0000-0000-000000000121'), N'Program.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.finance-controller', CONVERT(uniqueidentifier, N'b0000134-0000-0000-0000-000000000134'), N'Document.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.resource-manager', CONVERT(uniqueidentifier, N'b0000016-0000-0000-0000-000000000016'), N'Resource.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.resource-manager', CONVERT(uniqueidentifier, N'b0000101-0000-0000-0000-000000000101'), N'Project.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.resource-manager', CONVERT(uniqueidentifier, N'b0000017-0000-0000-0000-000000000017'), N'Resource.Assign', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.resource-manager', CONVERT(uniqueidentifier, N'b0000021-0000-0000-0000-000000000021'), N'Resource.ViewCostRate', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.resource-manager', CONVERT(uniqueidentifier, N'b0000022-0000-0000-0000-000000000022'), N'Resource.EditCostRate', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.resource-manager', CONVERT(uniqueidentifier, N'b0000132-0000-0000-0000-000000000132'), N'Stakeholders.Read', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.resource-manager', CONVERT(uniqueidentifier, N'b0000111-0000-0000-0000-000000000111'), N'Risk.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.resource-manager', CONVERT(uniqueidentifier, N'b0000116-0000-0000-0000-000000000116'), N'Planning.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.resource-manager', CONVERT(uniqueidentifier, N'b0000123-0000-0000-0000-000000000123'), N'WorkItem.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.resource-manager', CONVERT(uniqueidentifier, N'b0000142-0000-0000-0000-000000000142'), N'TimeTracking.ViewOwn', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.resource-manager', CONVERT(uniqueidentifier, N'b0000143-0000-0000-0000-000000000143'), N'TimeTracking.ManageOwn', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.resource-manager', CONVERT(uniqueidentifier, N'b0000144-0000-0000-0000-000000000144'), N'TimeTracking.ViewTeam', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.resource-manager', CONVERT(uniqueidentifier, N'b0000145-0000-0000-0000-000000000145'), N'TimeTracking.ManageTeam', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.resource-manager', CONVERT(uniqueidentifier, N'b0000127-0000-0000-0000-000000000127'), N'Meeting.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.resource-manager', CONVERT(uniqueidentifier, N'b0000134-0000-0000-0000-000000000134'), N'Document.View', N'SystemRoleSeed', N'Tenant'),
    (N'tenant.resource-manager', CONVERT(uniqueidentifier, N'b0000241-0000-0000-0000-000000000241'), N'Issue.View', N'SystemRoleSeed', N'Tenant');

DECLARE @RequiredColumns TABLE (
    TableName sysname NOT NULL,
    ColumnName sysname NOT NULL,
    TypeName sysname NOT NULL,
    IsNullable bit NOT NULL,
    PRIMARY KEY (TableName, ColumnName)
);
INSERT @RequiredColumns (TableName, ColumnName, TypeName, IsNullable)
VALUES
    (N'Tenants', N'Id', N'uniqueidentifier', 0),
    (N'Tenants', N'Code', N'nvarchar', 0),
    (N'Tenants', N'DisplayName', N'nvarchar', 0),
    (N'Tenants', N'Status', N'nvarchar', 0),
    (N'Tenants', N'CreatedAt', N'datetime2', 0),
    (N'Tenants', N'CreatedBy', N'nvarchar', 0),
    (N'Tenants', N'RowVersion', N'timestamp', 0),
    (N'Organizations', N'Id', N'uniqueidentifier', 0),
    (N'Organizations', N'Name', N'nvarchar', 0),
    (N'Organizations', N'Code', N'nvarchar', 1),
    (N'Organizations', N'Description', N'nvarchar', 1),
    (N'Organizations', N'IsActive', N'bit', 0),
    (N'Organizations', N'TenantId', N'uniqueidentifier', 0),
    (N'Organizations', N'CreatedAt', N'datetime2', 0),
    (N'Organizations', N'CreatedBy', N'nvarchar', 0),
    (N'Organizations', N'IsDeleted', N'bit', 0),
    (N'Organizations', N'RowVersion', N'varbinary', 0),
    (N'Permissions', N'Id', N'uniqueidentifier', 0),
    (N'Permissions', N'Name', N'nvarchar', 0),
    (N'Permissions', N'Description', N'nvarchar', 1),
    (N'Permissions', N'Resource', N'nvarchar', 0),
    (N'Permissions', N'Action', N'nvarchar', 0),
    (N'Permissions', N'TenantId', N'uniqueidentifier', 0),
    (N'Permissions', N'CreatedAt', N'datetime2', 0),
    (N'Permissions', N'CreatedBy', N'nvarchar', 0),
    (N'Permissions', N'IsDeleted', N'bit', 0),
    (N'Permissions', N'RowVersion', N'timestamp', 0),
    (N'Roles', N'Id', N'uniqueidentifier', 0),
    (N'Roles', N'Name', N'nvarchar', 0),
    (N'Roles', N'Description', N'nvarchar', 1),
    (N'Roles', N'Category', N'nvarchar', 1),
    (N'Roles', N'IsSystem', N'bit', 0),
    (N'Roles', N'TenantId', N'uniqueidentifier', 0),
    (N'Roles', N'CreatedAt', N'datetime2', 0),
    (N'Roles', N'CreatedBy', N'nvarchar', 0),
    (N'Roles', N'IsDeleted', N'bit', 0),
    (N'Roles', N'RowVersion', N'timestamp', 0),
    (N'Roles', N'SystemRoleKey', N'nvarchar', 1),
    (N'RolePermissions', N'PermissionsId', N'uniqueidentifier', 0),
    (N'RolePermissions', N'RolesId', N'uniqueidentifier', 0),
    (N'Users', N'Id', N'uniqueidentifier', 0),
    (N'Users', N'Email', N'nvarchar', 0),
    (N'Users', N'PasswordHash', N'nvarchar', 1),
    (N'Users', N'AuthenticationSource', N'nvarchar', 0),
    (N'Users', N'MustChangePassword', N'bit', 0),
    (N'Users', N'TemporaryPasswordExpiresAt', N'datetime2', 1),
    (N'Users', N'PasswordChangedAt', N'datetime2', 1),
    (N'Users', N'FirstName', N'nvarchar', 0),
    (N'Users', N'LastName', N'nvarchar', 0),
    (N'Users', N'IsActive', N'bit', 0),
    (N'Users', N'TenantId', N'uniqueidentifier', 0),
    (N'Users', N'CreatedAt', N'datetime2', 0),
    (N'Users', N'CreatedBy', N'nvarchar', 0),
    (N'Users', N'IsDeleted', N'bit', 0),
    (N'Users', N'RowVersion', N'timestamp', 0),
    (N'UserRoles', N'Id', N'uniqueidentifier', 0),
    (N'UserRoles', N'UserId', N'uniqueidentifier', 0),
    (N'UserRoles', N'RoleId', N'uniqueidentifier', 0),
    (N'UserRoles', N'TenantId', N'uniqueidentifier', 0),
    (N'UserRoles', N'CreatedAt', N'datetime2', 0),
    (N'UserRoles', N'CreatedBy', N'nvarchar', 0),
    (N'UserRoles', N'IsDeleted', N'bit', 0),
    (N'UserRoles', N'RowVersion', N'varbinary', 0);

DECLARE @ForbiddenAdminGrantNames TABLE (PermissionName nvarchar(200) NOT NULL PRIMARY KEY);
INSERT @ForbiddenAdminGrantNames (PermissionName)
VALUES
    (N'PaymentObligationSettlement.Record'),
    (N'BudgetRevision.Approve'),
    (N'ProjectAnnualBudget.Approve'),
    (N'ProjectApproval.Override'),
    (N'ProjectAnnualBudget.ManageApprovalConfiguration'),
    (N'ProjectPaymentRequest.Approve');

IF (SELECT COUNT(*) FROM @PermissionSeed) <> 160
    THROW 51005, 'Embedded active permission catalog count is not 160.', 1;
IF (SELECT COUNT(*) FROM @RoleSeed) <> 8
    THROW 51006, 'Embedded system role count is not eight.', 1;
IF NOT EXISTS (SELECT 1 FROM @RoleSeed WHERE SystemRoleKey = N'tenant.admin' AND RoleId = CONVERT(uniqueidentifier, N'a0000001-0000-0000-0000-000000000001'))
    THROW 51007, 'Protected Tenant Admin stable identity is missing.', 1;
IF (SELECT COUNT(*) FROM @RolePermissionSeed WHERE GrantClass = N'ProtectedBaseline' AND SystemRoleKey = N'tenant.admin') <> 47
    THROW 51008, 'Protected Tenant Admin baseline count differs from the exact uploaded catalog and policy.', 1;
IF (SELECT COUNT(*) FROM @RolePermissionSeed WHERE GrantClass = N'ApprovedProjectSettings' AND SystemRoleKey = N'tenant.admin') <> 2
    THROW 51009, 'Approved Project Settings grant count is not two.', 1;
IF EXISTS (
    SELECT 1 FROM @RolePermissionSeed
    WHERE SystemRoleKey = N'tenant.admin'
      AND PermissionScope NOT IN (N'Tenant', N'Project')
)
    THROW 51010, 'Tenant Admin grant contains an unsupported permission scope.', 1;
IF EXISTS (
    SELECT 1 FROM @RolePermissionSeed
    WHERE SystemRoleKey = N'tenant.admin'
      AND PermissionScope = N'Project'
      AND PermissionName NOT IN (N'BudgetRevision.View', N'ProjectSettings.Edit', N'ProjectSettings.View')
)
    THROW 51011, 'Tenant Admin has a Project-scoped grant outside the source allowlist.', 1;
IF (SELECT COUNT(*) FROM @RolePermissionSeed WHERE SystemRoleKey = N'tenant.admin' AND PermissionScope = N'Project') <> 3
    THROW 51037, 'Tenant Admin Project-scoped grant count differs from the exact source allowlist.', 1;
IF EXISTS (
    SELECT 1 FROM @RolePermissionSeed g
    JOIN @ForbiddenAdminGrantNames f ON f.PermissionName = g.PermissionName
    WHERE g.SystemRoleKey = N'tenant.admin'
)
    THROW 51012, 'Tenant Admin includes a financial approval, settlement, or approval-configuration grant.', 1;

IF EXISTS (
    SELECT 1
    FROM @RequiredColumns required
    LEFT JOIN sys.tables t ON t.name = required.TableName
    LEFT JOIN sys.schemas s ON s.schema_id = t.schema_id AND s.name = N'dbo'
    LEFT JOIN sys.columns c ON c.object_id = t.object_id AND c.name = required.ColumnName
    LEFT JOIN sys.types ty ON ty.user_type_id = c.user_type_id
    WHERE t.object_id IS NULL OR s.schema_id IS NULL OR c.column_id IS NULL
       OR ty.name <> required.TypeName OR c.is_nullable <> required.IsNullable
)
    THROW 51013, 'Required dbo schema differs from the user-supplied table schema; no data was written.', 1;

DECLARE @NonCoreTables TABLE (SchemaName sysname NOT NULL, TableName sysname NOT NULL, PRIMARY KEY (SchemaName, TableName));
INSERT @NonCoreTables (SchemaName, TableName)
SELECT s.name, t.name
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE t.is_ms_shipped = 0
  AND NOT (s.name = N'dbo' AND t.name IN (N'__EFMigrationsHistory', N'Tenants', N'Organizations', N'Permissions', N'Roles', N'RolePermissions', N'Users', N'UserRoles'));
DECLARE @NonEmptyTables TABLE (SchemaName sysname NOT NULL, TableName sysname NOT NULL, PRIMARY KEY (SchemaName, TableName));
DECLARE @NonEmptyTablesAfter TABLE (SchemaName sysname NOT NULL, TableName sysname NOT NULL, PRIMARY KEY (SchemaName, TableName));
DECLARE @ScanSchema sysname, @ScanTable sysname, @HasRows bit, @ScanSql nvarchar(max);
DECLARE @TableScan CURSOR;
SET @TableScan = CURSOR LOCAL FAST_FORWARD FOR SELECT SchemaName, TableName FROM @NonCoreTables ORDER BY SchemaName, TableName;

SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @LockResult int;
    EXEC @LockResult = sys.sp_getapplock
        @Resource = N'FOPXPM:MinimalAdminBootstrap:11111111111111111111111111111111',
        @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000;
    IF @LockResult < 0 THROW 51014, 'Could not acquire the bootstrap transaction lock.', 1;

    OPEN @TableScan;
    FETCH NEXT FROM @TableScan INTO @ScanSchema, @ScanTable;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @HasRows = 0;
        SET @ScanSql = N'SELECT @HasRows = CASE WHEN EXISTS (SELECT 1 FROM ' + QUOTENAME(@ScanSchema) + N'.' + QUOTENAME(@ScanTable) + N') THEN 1 ELSE 0 END;';
        EXEC sys.sp_executesql @ScanSql, N'@HasRows bit OUTPUT', @HasRows OUTPUT;
        IF @HasRows = 1 INSERT @NonEmptyTables (SchemaName, TableName) VALUES (@ScanSchema, @ScanTable);
        FETCH NEXT FROM @TableScan INTO @ScanSchema, @ScanTable;
    END;
    CLOSE @TableScan;
    IF EXISTS (SELECT 1 FROM @NonEmptyTables)
    BEGIN
        DECLARE @NonEmptyMessage nvarchar(2048) = N'Refusing to seed a database with existing non-core records: ';
        SELECT @NonEmptyMessage = @NonEmptyMessage + STRING_AGG(CONVERT(nvarchar(max), QUOTENAME(SchemaName) + N'.' + QUOTENAME(TableName)), N', ')
        FROM @NonEmptyTables;
        THROW 51015, @NonEmptyMessage, 1;
    END;

    IF EXISTS (SELECT 1 FROM dbo.Tenants WITH (UPDLOCK, HOLDLOCK) WHERE Id <> @TenantId OR Code <> N'FOPX-DEMO')
        THROW 51016, 'Unexpected Tenant row exists.', 1;
    IF EXISTS (SELECT 1 FROM dbo.Tenants WHERE Id = @TenantId AND (Code <> N'FOPX-DEMO' OR DisplayName <> N'FOPX Development Demo Tenant' OR Status <> N'Active' OR CreatedAt <> CONVERT(datetime2(7), N'2026-11-02T00:00:00') OR CreatedBy <> N'Development seed'))
        THROW 51017, 'Existing canonical Tenant conflicts with the seed source.', 1;

    IF EXISTS (SELECT 1 FROM dbo.Organizations WITH (UPDLOCK, HOLDLOCK) WHERE Id <> @OrganizationId OR TenantId <> @TenantId)
        THROW 51018, 'Unexpected Organization row exists.', 1;
    IF EXISTS (SELECT 1 FROM dbo.Organizations WHERE Id = @OrganizationId AND (Name <> N'NEXUS Group' OR Code <> N'NEXUS' OR Description <> N'Enterprise operating organization' OR TenantId <> @TenantId OR IsActive <> 1 OR IsDeleted <> 0))
        THROW 51019, 'Existing structural Organization conflicts with the seed source.', 1;

    IF EXISTS (
        SELECT 1 FROM dbo.Permissions p WITH (UPDLOCK, HOLDLOCK)
        WHERE NOT EXISTS (SELECT 1 FROM @PermissionSeed s WHERE s.PermissionId = p.Id AND p.TenantId = @TenantId
            AND s.PermissionName = p.Name AND s.Resource = p.Resource AND s.[Action] = p.[Action] AND ISNULL(s.Description, N'') = ISNULL(p.Description, N'') AND p.IsDeleted = 0)
    )
        THROW 51020, 'Unexpected or non-canonical Permission row exists.', 1;
    IF EXISTS (
        SELECT 1 FROM @PermissionSeed s
        WHERE EXISTS (SELECT 1 FROM dbo.Permissions p WHERE p.TenantId = @TenantId AND p.Resource = s.Resource AND p.[Action] = s.[Action] AND p.Id <> s.PermissionId)
    )
        THROW 51021, 'A canonical permission key is already stored with a different stable ID.', 1;

    IF EXISTS (
        SELECT 1 FROM dbo.Roles r WITH (UPDLOCK, HOLDLOCK)
        WHERE NOT EXISTS (SELECT 1 FROM @RoleSeed s WHERE s.RoleId = r.Id AND s.SystemRoleKey = r.SystemRoleKey
            AND s.Name = r.Name AND ISNULL(s.Description, N'') = ISNULL(r.Description, N'')
            AND ISNULL(s.Category, N'') = ISNULL(r.Category, N'') AND r.TenantId = @TenantId AND r.IsSystem = 1 AND r.IsDeleted = 0)
    )
        THROW 51022, 'Unexpected or non-canonical Role row exists.', 1;
    IF EXISTS (
        SELECT 1 FROM @RoleSeed s
        WHERE EXISTS (SELECT 1 FROM dbo.Roles r WHERE r.TenantId = @TenantId AND r.SystemRoleKey = s.SystemRoleKey AND r.Id <> s.RoleId)
           OR EXISTS (SELECT 1 FROM dbo.Roles r WHERE r.TenantId = @TenantId AND r.Name = s.Name AND r.Id <> s.RoleId)
    )
        THROW 51023, 'A system role key or name is already stored with a different stable ID.', 1;

    IF EXISTS (SELECT 1 FROM dbo.Users WITH (UPDLOCK, HOLDLOCK) WHERE Id <> @AdminUserId OR Email <> @AdminEmail)
        THROW 51024, 'Unexpected User row exists; this script never removes or replaces users.', 1;
    IF EXISTS (SELECT 1 FROM dbo.Users WHERE Id = @AdminUserId AND (Email <> @AdminEmail OR TenantId <> @TenantId OR AuthenticationSource <> N'Local' OR FirstName <> N'Alex' OR LastName <> N'Admin' OR IsActive <> 1 OR IsDeleted <> 0 OR MustChangePassword <> 1 OR PasswordHash IS NULL OR DATALENGTH(PasswordHash) = 0))
        THROW 51025, 'Existing Admin row is not the canonical, active, password-change-required account.', 1;
    IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Id = @AdminUserId)
       AND (@AdminPasswordHash IS NULL OR @AdminPasswordHash = N'' OR @AdminPasswordHash = N'__REPLACE_WITH_ASPNETCORE_PASSWORDHASH__')
        THROW 51026, 'Generate an ASP.NET Core PasswordHasher<User> hash locally and replace the script placeholder before first execution.', 1;

    IF EXISTS (
        SELECT 1 FROM dbo.RolePermissions rp WITH (UPDLOCK, HOLDLOCK)
        WHERE NOT EXISTS (SELECT 1 FROM @RolePermissionSeed s JOIN @RoleSeed r ON r.SystemRoleKey = s.SystemRoleKey
            WHERE rp.PermissionsId = s.PermissionId AND rp.RolesId = r.RoleId)
    )
        THROW 51027, 'Unexpected RolePermissions link exists; this script will not remove grants.', 1;
    IF EXISTS (
        SELECT 1 FROM dbo.UserRoles ur WITH (UPDLOCK, HOLDLOCK)
        WHERE NOT (ur.Id = CONVERT(uniqueidentifier, N'f1000001-0000-0000-0000-000000000001') AND ur.UserId = @AdminUserId
            AND ur.RoleId = (SELECT RoleId FROM @RoleSeed WHERE SystemRoleKey = N'tenant.admin')
            AND ur.TenantId = @TenantId AND ur.IsDeleted = 0)
    )
        THROW 51028, 'Unexpected UserRole row exists.', 1;

    IF NOT EXISTS (SELECT 1 FROM dbo.Tenants WHERE Id = @TenantId)
        INSERT dbo.Tenants (Id, Code, DisplayName, Status, CreatedAt, CreatedBy)
        VALUES (@TenantId, N'FOPX-DEMO', N'FOPX Development Demo Tenant', N'Active', CONVERT(datetime2(7), N'2026-11-02T00:00:00'), N'Development seed');

    IF NOT EXISTS (SELECT 1 FROM dbo.Organizations WHERE Id = @OrganizationId)
        INSERT dbo.Organizations (Id, Name, Code, Description, IsActive, TenantId, CreatedAt, CreatedBy, IsDeleted, RowVersion)
        VALUES (@OrganizationId, N'NEXUS Group', N'NEXUS', N'Enterprise operating organization', 1, @TenantId, @NowUtc, N'System', 0, 0x);

    INSERT dbo.Permissions (Id, Name, Description, Resource, [Action], TenantId, CreatedAt, CreatedBy, IsDeleted)
    SELECT s.PermissionId, s.PermissionName, s.Description, s.Resource, s.[Action], @TenantId, @NowUtc, N'System', 0
    FROM @PermissionSeed s
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Permissions p WHERE p.Id = s.PermissionId);

    INSERT dbo.Roles (Id, Name, Description, Category, IsSystem, TenantId, CreatedAt, CreatedBy, IsDeleted, SystemRoleKey)
    SELECT s.RoleId, s.Name, s.Description, s.Category, 1, @TenantId, @NowUtc, N'System', 0, s.SystemRoleKey
    FROM @RoleSeed s
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Roles r WHERE r.Id = s.RoleId);

    INSERT dbo.RolePermissions (PermissionsId, RolesId)
    SELECT s.PermissionId, r.RoleId
    FROM @RolePermissionSeed s
    JOIN @RoleSeed r ON r.SystemRoleKey = s.SystemRoleKey
    WHERE NOT EXISTS (SELECT 1 FROM dbo.RolePermissions rp WHERE rp.PermissionsId = s.PermissionId AND rp.RolesId = r.RoleId);

    IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Id = @AdminUserId)
        INSERT dbo.Users (Id, Email, PasswordHash, AuthenticationSource, MustChangePassword, TemporaryPasswordExpiresAt, PasswordChangedAt,
                          FirstName, LastName, IsActive, TenantId, CreatedAt, CreatedBy, IsDeleted)
        VALUES (@AdminUserId, @AdminEmail, @AdminPasswordHash, N'Local', 1, DATEADD(hour, @BootstrapPasswordExpiryHours, @NowUtc), @NowUtc,
                N'Alex', N'Admin', 1, @TenantId, @NowUtc, N'System', 0);

    IF NOT EXISTS (SELECT 1 FROM dbo.UserRoles WHERE UserId = @AdminUserId AND RoleId = (SELECT RoleId FROM @RoleSeed WHERE SystemRoleKey = N'tenant.admin'))
        INSERT dbo.UserRoles (Id, UserId, RoleId, TenantId, CreatedAt, CreatedBy, IsDeleted, RowVersion)
        VALUES (CONVERT(uniqueidentifier, N'f1000001-0000-0000-0000-000000000001'), @AdminUserId,
                (SELECT RoleId FROM @RoleSeed WHERE SystemRoleKey = N'tenant.admin'), @TenantId, @NowUtc, N'System', 0, 0x);

    IF (SELECT COUNT(*) FROM dbo.Tenants) <> 1
       OR NOT EXISTS (SELECT 1 FROM dbo.Tenants WHERE Id = @TenantId AND Code = N'FOPX-DEMO' AND DisplayName = N'FOPX Development Demo Tenant' AND Status = N'Active')
        THROW 51029, 'Post-check failed: canonical Tenant count/content.', 1;
    IF (SELECT COUNT(*) FROM dbo.Organizations) <> 1
       OR NOT EXISTS (SELECT 1 FROM dbo.Organizations WHERE Id = @OrganizationId AND TenantId = @TenantId AND Code = N'NEXUS' AND Name = N'NEXUS Group' AND IsActive = 1 AND IsDeleted = 0)
        THROW 51030, 'Post-check failed: structural Organization count/content.', 1;
    IF (SELECT COUNT(*) FROM dbo.Permissions) <> 160
       OR EXISTS (SELECT 1 FROM @PermissionSeed s LEFT JOIN dbo.Permissions p ON p.Id = s.PermissionId
            WHERE p.Id IS NULL OR p.TenantId <> @TenantId OR p.Name <> s.PermissionName OR p.Resource <> s.Resource OR p.[Action] <> s.[Action] OR ISNULL(p.Description, N'') <> ISNULL(s.Description, N'') OR p.IsDeleted <> 0)
        THROW 51031, 'Post-check failed: the persisted active permission catalog is not the exact 160-row source catalog.', 1;
    IF (SELECT COUNT(*) FROM dbo.Roles) <> 8
       OR EXISTS (SELECT 1 FROM @RoleSeed s LEFT JOIN dbo.Roles r ON r.Id = s.RoleId
            WHERE r.Id IS NULL OR r.TenantId <> @TenantId OR r.SystemRoleKey <> s.SystemRoleKey OR r.Name <> s.Name OR r.IsSystem <> 1 OR r.IsDeleted <> 0)
        THROW 51032, 'Post-check failed: the persisted system-role set is not the exact eight-role seed.', 1;
    IF (SELECT COUNT(*) FROM dbo.RolePermissions) <> (SELECT COUNT(*) FROM @RolePermissionSeed)
       OR EXISTS (SELECT 1 FROM @RolePermissionSeed s JOIN @RoleSeed r ON r.SystemRoleKey = s.SystemRoleKey
            LEFT JOIN dbo.RolePermissions rp ON rp.PermissionsId = s.PermissionId AND rp.RolesId = r.RoleId
            WHERE rp.PermissionsId IS NULL)
        THROW 51033, 'Post-check failed: role grants do not match the source mappings.', 1;
    IF (SELECT COUNT(*) FROM dbo.Users) <> 1
       OR NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Id = @AdminUserId AND Email = @AdminEmail AND TenantId = @TenantId
            AND AuthenticationSource = N'Local' AND IsActive = 1 AND IsDeleted = 0 AND MustChangePassword = 1
            AND PasswordHash IS NOT NULL AND DATALENGTH(PasswordHash) > 0)
        THROW 51034, 'Post-check failed: exactly one active canonical Admin with a nonempty PasswordHash is required; SQL cannot validate its algorithm.', 1;
    IF (SELECT COUNT(*) FROM dbo.UserRoles) <> 1
       OR NOT EXISTS (SELECT 1 FROM dbo.UserRoles WHERE Id = CONVERT(uniqueidentifier, N'f1000001-0000-0000-0000-000000000001')
            AND UserId = @AdminUserId AND RoleId = (SELECT RoleId FROM @RoleSeed WHERE SystemRoleKey = N'tenant.admin')
            AND TenantId = @TenantId AND IsDeleted = 0)
        THROW 51035, 'Post-check failed: the Admin must have exactly one active Tenant Admin membership.', 1;

    OPEN @TableScan;
    FETCH NEXT FROM @TableScan INTO @ScanSchema, @ScanTable;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @HasRows = 0;
        SET @ScanSql = N'SELECT @HasRows = CASE WHEN EXISTS (SELECT 1 FROM ' + QUOTENAME(@ScanSchema) + N'.' + QUOTENAME(@ScanTable) + N') THEN 1 ELSE 0 END;';
        EXEC sys.sp_executesql @ScanSql, N'@HasRows bit OUTPUT', @HasRows OUTPUT;
        IF @HasRows = 1 INSERT @NonEmptyTablesAfter (SchemaName, TableName) VALUES (@ScanSchema, @ScanTable);
        FETCH NEXT FROM @TableScan INTO @ScanSchema, @ScanTable;
    END;
    CLOSE @TableScan;
    IF EXISTS (SELECT 1 FROM @NonEmptyTablesAfter)
        THROW 51036, 'Post-check failed: a non-core table contains rows; inspect triggers or concurrent writes.', 1;

    COMMIT TRANSACTION;
    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
    DEALLOCATE @TableScan;

    SELECT
        N'Bootstrap complete' AS Result,
        CAST(SERVERPROPERTY('ServerName') AS nvarchar(128)) AS ServerName,
        DB_NAME() AS DatabaseName,
        DB_ID() AS DatabaseId,
        (SELECT COUNT(*) FROM dbo.Tenants) AS TenantCount,
        (SELECT COUNT(*) FROM dbo.Organizations) AS OrganizationCount,
        (SELECT COUNT(*) FROM dbo.Permissions) AS PermissionCount,
        (SELECT COUNT(*) FROM dbo.Roles) AS SystemRoleCount,
        (SELECT COUNT(*) FROM dbo.RolePermissions) AS RolePermissionCount,
        (SELECT COUNT(*) FROM dbo.Users WHERE IsActive = 1 AND IsDeleted = 0) AS ActiveUserCount,
        (SELECT COUNT(*) FROM dbo.UserRoles WHERE IsDeleted = 0) AS ActiveUserRoleCount,
        N'No EF migrations were modified' AS MigrationHistory;
END TRY
BEGIN CATCH
    IF CURSOR_STATUS('variable', '@TableScan') >= 0 CLOSE @TableScan;
    IF CURSOR_STATUS('variable', '@TableScan') > -3 DEALLOCATE @TableScan;
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;
    THROW;
END CATCH;
