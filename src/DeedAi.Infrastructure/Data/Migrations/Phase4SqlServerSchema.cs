using Microsoft.EntityFrameworkCore.Migrations;

namespace DeedAi.Infrastructure.Data.Migrations;

/// <summary>
/// Idempotent Azure SQL DDL for Phase 4 objects. Safe to re-run when
/// <c>__EFMigrationsHistory</c> and the live schema diverge, or when a prior
/// apply created only some tables/columns. Does not create or drop Clients/Users
/// (no production data wipe). UploadedBy stays ON DELETE NO ACTION.
/// </summary>
public static class Phase4SqlServerSchema
{
    public const string Phase4AId = "20260909023000_Phase4A";
    public const string Phase4AQaId = "20260909030000_Phase4AQa";
    public const string Phase4HardeningId = "20260909022345_Phase4Hardening";
    public const string Phase4AzureRepairId = "20260909120000_Phase4AzureRepair";

    public static readonly string[] RequiredTables =
    [
        "AppPolicies",
        "SoftwareFieldMaps",
        "PropertyDefaults",
        "SoftwareClientConfigs",
        "SalesTabCodes",
        "SessionSettings",
        "OcrCleanupRules"
    ];

    public static bool IsSqlServer(MigrationBuilder migrationBuilder) =>
        string.Equals(
            migrationBuilder.ActiveProvider,
            "Microsoft.EntityFrameworkCore.SqlServer",
            StringComparison.Ordinal);

    public static void EnsureHardening(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(HardeningSql);

    public static void EnsurePhase4A(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(Phase4ASql);

    public static void EnsurePhase4AQa(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(Phase4AQaSql);

    public static void EnsureUploadedByNoAction(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(UploadedByNoActionSql);

    public static void EnsureAll(MigrationBuilder migrationBuilder)
    {
        EnsureHardening(migrationBuilder);
        EnsurePhase4A(migrationBuilder);
        EnsurePhase4AQa(migrationBuilder);
        EnsureUploadedByNoAction(migrationBuilder);
    }

    public const string HardeningSql =
        """
        IF OBJECT_ID(N'dbo.OcrCleanupRules', N'U') IS NULL
        BEGIN
            CREATE TABLE [OcrCleanupRules] (
                [Id] uniqueidentifier NOT NULL,
                [Kind] nvarchar(16) NOT NULL,
                [Value] nvarchar(64) NOT NULL,
                [IsActive] bit NOT NULL,
                [SortOrder] int NOT NULL,
                CONSTRAINT [PK_OcrCleanupRules] PRIMARY KEY ([Id]),
                CONSTRAINT [CK_OcrCleanupRules_Kind] CHECK (Kind IN (N'Trim', N'Discard'))
            );
        END
        ELSE IF OBJECT_ID(N'dbo.CK_OcrCleanupRules_Kind', N'C') IS NULL
            ALTER TABLE [OcrCleanupRules] ADD CONSTRAINT [CK_OcrCleanupRules_Kind] CHECK (Kind IN (N'Trim', N'Discard'));

        IF OBJECT_ID(N'dbo.SessionSettings', N'U') IS NULL
        BEGIN
            CREATE TABLE [SessionSettings] (
                [Id] uniqueidentifier NOT NULL,
                [IdleTimeoutMinutes] int NOT NULL,
                [UpdatedAt] datetimeoffset NOT NULL,
                CONSTRAINT [PK_SessionSettings] PRIMARY KEY ([Id])
            );
        END

        IF NOT EXISTS (
            SELECT 1 FROM sys.indexes
            WHERE name = N'IX_OcrCleanupRules_Kind_Value'
              AND object_id = OBJECT_ID(N'dbo.OcrCleanupRules'))
            CREATE UNIQUE INDEX [IX_OcrCleanupRules_Kind_Value] ON [OcrCleanupRules] ([Kind], [Value]);
        """;

    public const string Phase4ASql =
        """
        IF OBJECT_ID(N'dbo.AppPolicies', N'U') IS NULL
        BEGIN
            CREATE TABLE [AppPolicies] (
                [Id] uniqueidentifier NOT NULL,
                [SoftwarePushEnabled] bit NOT NULL,
                [SoftwareDefaultGroup] nvarchar(64) NULL,
                [SoftwareFieldDefaultsJson] nvarchar(4000) NULL,
                [UpdatedAt] datetimeoffset NOT NULL,
                CONSTRAINT [PK_AppPolicies] PRIMARY KEY ([Id])
            );
        END

        IF OBJECT_ID(N'dbo.SoftwareFieldMaps', N'U') IS NULL
        BEGIN
            CREATE TABLE [SoftwareFieldMaps] (
                [Id] uniqueidentifier NOT NULL,
                [DeedField] nvarchar(64) NOT NULL,
                [SoftwareField] nvarchar(64) NOT NULL,
                [SoftwareGroup] nvarchar(64) NULL,
                [ClientId] uniqueidentifier NULL,
                [DeedType] nvarchar(64) NULL,
                [IsActive] bit NOT NULL,
                [SortOrder] int NOT NULL,
                CONSTRAINT [PK_SoftwareFieldMaps] PRIMARY KEY ([Id])
            );
        END

        IF OBJECT_ID(N'dbo.PropertyDefaults', N'U') IS NULL
        BEGIN
            CREATE TABLE [PropertyDefaults] (
                [Id] uniqueidentifier NOT NULL,
                [Scope] nvarchar(16) NOT NULL,
                [ClientId] uniqueidentifier NULL,
                [DeedType] nvarchar(64) NULL,
                [FieldKey] nvarchar(64) NOT NULL,
                [DefaultValue] nvarchar(256) NULL,
                CONSTRAINT [PK_PropertyDefaults] PRIMARY KEY ([Id])
            );
        END

        IF OBJECT_ID(N'dbo.FK_SoftwareFieldMaps_Clients_ClientId', N'F') IS NULL
           AND OBJECT_ID(N'dbo.SoftwareFieldMaps', N'U') IS NOT NULL
           AND OBJECT_ID(N'dbo.Clients', N'U') IS NOT NULL
            ALTER TABLE [SoftwareFieldMaps] WITH CHECK ADD CONSTRAINT [FK_SoftwareFieldMaps_Clients_ClientId]
                FOREIGN KEY ([ClientId]) REFERENCES [Clients] ([Id]) ON DELETE CASCADE;

        IF OBJECT_ID(N'dbo.FK_PropertyDefaults_Clients_ClientId', N'F') IS NULL
           AND OBJECT_ID(N'dbo.PropertyDefaults', N'U') IS NOT NULL
           AND OBJECT_ID(N'dbo.Clients', N'U') IS NOT NULL
            ALTER TABLE [PropertyDefaults] WITH CHECK ADD CONSTRAINT [FK_PropertyDefaults_Clients_ClientId]
                FOREIGN KEY ([ClientId]) REFERENCES [Clients] ([Id]) ON DELETE CASCADE;

        IF NOT EXISTS (
            SELECT 1 FROM sys.indexes
            WHERE name = N'IX_SoftwareFieldMaps_DeedField_ClientId_DeedType'
              AND object_id = OBJECT_ID(N'dbo.SoftwareFieldMaps'))
            CREATE INDEX [IX_SoftwareFieldMaps_DeedField_ClientId_DeedType] ON [SoftwareFieldMaps] ([DeedField], [ClientId], [DeedType]);

        IF NOT EXISTS (
            SELECT 1 FROM sys.indexes
            WHERE name = N'IX_SoftwareFieldMaps_ClientId'
              AND object_id = OBJECT_ID(N'dbo.SoftwareFieldMaps'))
            CREATE INDEX [IX_SoftwareFieldMaps_ClientId] ON [SoftwareFieldMaps] ([ClientId]);

        IF NOT EXISTS (
            SELECT 1 FROM sys.indexes
            WHERE name = N'IX_PropertyDefaults_Scope_ClientId_DeedType_FieldKey'
              AND object_id = OBJECT_ID(N'dbo.PropertyDefaults'))
            CREATE UNIQUE INDEX [IX_PropertyDefaults_Scope_ClientId_DeedType_FieldKey] ON [PropertyDefaults] ([Scope], [ClientId], [DeedType], [FieldKey]);

        IF NOT EXISTS (
            SELECT 1 FROM sys.indexes
            WHERE name = N'IX_PropertyDefaults_ClientId'
              AND object_id = OBJECT_ID(N'dbo.PropertyDefaults'))
            CREATE INDEX [IX_PropertyDefaults_ClientId] ON [PropertyDefaults] ([ClientId]);
        """;

    public const string Phase4AQaSql =
        """
        IF COL_LENGTH(N'dbo.Documents', N'SalesTabCode') IS NULL
           AND OBJECT_ID(N'dbo.Documents', N'U') IS NOT NULL
            ALTER TABLE [Documents] ADD [SalesTabCode] nvarchar(32) NULL;

        IF OBJECT_ID(N'dbo.SoftwareClientConfigs', N'U') IS NULL
        BEGIN
            CREATE TABLE [SoftwareClientConfigs] (
                [Id] uniqueidentifier NOT NULL,
                [ClientId] uniqueidentifier NOT NULL,
                [Vendor] nvarchar(64) NULL,
                [ApiUrl] nvarchar(256) NULL,
                [GroupCode] nvarchar(32) NULL,
                [RemoveLeadingZeros] bit NOT NULL,
                [DateLabelDepth] int NOT NULL,
                [DisplaySalesTab] bit NOT NULL,
                [SendConsideration] bit NOT NULL,
                [ConsiderationThreshold] decimal(18,2) NOT NULL,
                [ResetExemptions] bit NOT NULL,
                [ResetSupplementYear] bit NOT NULL,
                [ResetSalesLetter] bit NOT NULL,
                [ResetSalesTab] bit NOT NULL,
                [ResetAgents] bit NOT NULL,
                [ResetMortgageCodes] bit NOT NULL,
                CONSTRAINT [PK_SoftwareClientConfigs] PRIMARY KEY ([Id])
            );
        END
        ELSE
        BEGIN
            IF COL_LENGTH(N'dbo.SoftwareClientConfigs', N'Vendor') IS NULL
                ALTER TABLE [SoftwareClientConfigs] ADD [Vendor] nvarchar(64) NULL;
            IF COL_LENGTH(N'dbo.SoftwareClientConfigs', N'ApiUrl') IS NULL
                ALTER TABLE [SoftwareClientConfigs] ADD [ApiUrl] nvarchar(256) NULL;
            IF COL_LENGTH(N'dbo.SoftwareClientConfigs', N'GroupCode') IS NULL
                ALTER TABLE [SoftwareClientConfigs] ADD [GroupCode] nvarchar(32) NULL;
            IF COL_LENGTH(N'dbo.SoftwareClientConfigs', N'RemoveLeadingZeros') IS NULL
                ALTER TABLE [SoftwareClientConfigs] ADD [RemoveLeadingZeros] bit NOT NULL CONSTRAINT [DF_SoftwareClientConfigs_RemoveLeadingZeros] DEFAULT CAST(0 AS bit);
            IF COL_LENGTH(N'dbo.SoftwareClientConfigs', N'DateLabelDepth') IS NULL
                ALTER TABLE [SoftwareClientConfigs] ADD [DateLabelDepth] int NOT NULL CONSTRAINT [DF_SoftwareClientConfigs_DateLabelDepth] DEFAULT 1;
            IF COL_LENGTH(N'dbo.SoftwareClientConfigs', N'DisplaySalesTab') IS NULL
                ALTER TABLE [SoftwareClientConfigs] ADD [DisplaySalesTab] bit NOT NULL CONSTRAINT [DF_SoftwareClientConfigs_DisplaySalesTab] DEFAULT CAST(0 AS bit);
            IF COL_LENGTH(N'dbo.SoftwareClientConfigs', N'SendConsideration') IS NULL
                ALTER TABLE [SoftwareClientConfigs] ADD [SendConsideration] bit NOT NULL CONSTRAINT [DF_SoftwareClientConfigs_SendConsideration] DEFAULT CAST(1 AS bit);
            IF COL_LENGTH(N'dbo.SoftwareClientConfigs', N'ConsiderationThreshold') IS NULL
                ALTER TABLE [SoftwareClientConfigs] ADD [ConsiderationThreshold] decimal(18,2) NOT NULL CONSTRAINT [DF_SoftwareClientConfigs_ConsiderationThreshold] DEFAULT 0;
            IF COL_LENGTH(N'dbo.SoftwareClientConfigs', N'ResetExemptions') IS NULL
                ALTER TABLE [SoftwareClientConfigs] ADD [ResetExemptions] bit NOT NULL CONSTRAINT [DF_SoftwareClientConfigs_ResetExemptions] DEFAULT CAST(0 AS bit);
            IF COL_LENGTH(N'dbo.SoftwareClientConfigs', N'ResetSupplementYear') IS NULL
                ALTER TABLE [SoftwareClientConfigs] ADD [ResetSupplementYear] bit NOT NULL CONSTRAINT [DF_SoftwareClientConfigs_ResetSupplementYear] DEFAULT CAST(0 AS bit);
            IF COL_LENGTH(N'dbo.SoftwareClientConfigs', N'ResetSalesLetter') IS NULL
                ALTER TABLE [SoftwareClientConfigs] ADD [ResetSalesLetter] bit NOT NULL CONSTRAINT [DF_SoftwareClientConfigs_ResetSalesLetter] DEFAULT CAST(0 AS bit);
            IF COL_LENGTH(N'dbo.SoftwareClientConfigs', N'ResetSalesTab') IS NULL
                ALTER TABLE [SoftwareClientConfigs] ADD [ResetSalesTab] bit NOT NULL CONSTRAINT [DF_SoftwareClientConfigs_ResetSalesTab] DEFAULT CAST(0 AS bit);
            IF COL_LENGTH(N'dbo.SoftwareClientConfigs', N'ResetAgents') IS NULL
                ALTER TABLE [SoftwareClientConfigs] ADD [ResetAgents] bit NOT NULL CONSTRAINT [DF_SoftwareClientConfigs_ResetAgents] DEFAULT CAST(0 AS bit);
            IF COL_LENGTH(N'dbo.SoftwareClientConfigs', N'ResetMortgageCodes') IS NULL
                ALTER TABLE [SoftwareClientConfigs] ADD [ResetMortgageCodes] bit NOT NULL CONSTRAINT [DF_SoftwareClientConfigs_ResetMortgageCodes] DEFAULT CAST(0 AS bit);
        END

        IF OBJECT_ID(N'dbo.SalesTabCodes', N'U') IS NULL
        BEGIN
            CREATE TABLE [SalesTabCodes] (
                [Id] uniqueidentifier NOT NULL,
                [ClientId] uniqueidentifier NULL,
                [Code] nvarchar(16) NOT NULL,
                [Label] nvarchar(64) NOT NULL,
                [MinConsideration] decimal(18,2) NOT NULL,
                [MaxConsideration] decimal(18,2) NULL,
                [IsActive] bit NOT NULL,
                [SortOrder] int NOT NULL,
                CONSTRAINT [PK_SalesTabCodes] PRIMARY KEY ([Id])
            );
        END

        IF OBJECT_ID(N'dbo.FK_SoftwareClientConfigs_Clients_ClientId', N'F') IS NULL
           AND OBJECT_ID(N'dbo.SoftwareClientConfigs', N'U') IS NOT NULL
           AND OBJECT_ID(N'dbo.Clients', N'U') IS NOT NULL
            ALTER TABLE [SoftwareClientConfigs] WITH CHECK ADD CONSTRAINT [FK_SoftwareClientConfigs_Clients_ClientId]
                FOREIGN KEY ([ClientId]) REFERENCES [Clients] ([Id]) ON DELETE CASCADE;

        IF OBJECT_ID(N'dbo.FK_SalesTabCodes_Clients_ClientId', N'F') IS NULL
           AND OBJECT_ID(N'dbo.SalesTabCodes', N'U') IS NOT NULL
           AND OBJECT_ID(N'dbo.Clients', N'U') IS NOT NULL
            ALTER TABLE [SalesTabCodes] WITH CHECK ADD CONSTRAINT [FK_SalesTabCodes_Clients_ClientId]
                FOREIGN KEY ([ClientId]) REFERENCES [Clients] ([Id]) ON DELETE CASCADE;

        IF NOT EXISTS (
            SELECT 1 FROM sys.indexes
            WHERE name = N'IX_SoftwareClientConfigs_ClientId'
              AND object_id = OBJECT_ID(N'dbo.SoftwareClientConfigs'))
            CREATE UNIQUE INDEX [IX_SoftwareClientConfigs_ClientId] ON [SoftwareClientConfigs] ([ClientId]);

        IF NOT EXISTS (
            SELECT 1 FROM sys.indexes
            WHERE name = N'IX_SalesTabCodes_ClientId'
              AND object_id = OBJECT_ID(N'dbo.SalesTabCodes'))
            CREATE INDEX [IX_SalesTabCodes_ClientId] ON [SalesTabCodes] ([ClientId]);

        IF NOT EXISTS (
            SELECT 1 FROM sys.indexes
            WHERE name = N'IX_SalesTabCodes_ClientId_Code'
              AND object_id = OBJECT_ID(N'dbo.SalesTabCodes'))
            CREATE UNIQUE INDEX [IX_SalesTabCodes_ClientId_Code] ON [SalesTabCodes] ([ClientId], [Code]);
        """;

    public const string UploadedByNoActionSql =
        """
        IF OBJECT_ID(N'dbo.Documents', N'U') IS NOT NULL
           AND OBJECT_ID(N'dbo.Users', N'U') IS NOT NULL
           AND COL_LENGTH(N'dbo.Documents', N'UploadedByUserId') IS NOT NULL
        BEGIN
            IF EXISTS (
                SELECT 1 FROM sys.foreign_keys
                WHERE name = N'FK_Documents_Users_UploadedByUserId'
                  AND parent_object_id = OBJECT_ID(N'dbo.Documents'))
                ALTER TABLE [Documents] DROP CONSTRAINT [FK_Documents_Users_UploadedByUserId];

            ALTER TABLE [Documents] WITH CHECK ADD CONSTRAINT [FK_Documents_Users_UploadedByUserId]
                FOREIGN KEY ([UploadedByUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION;
        END
        """;
}
