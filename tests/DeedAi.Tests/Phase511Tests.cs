namespace DeedAi.Tests;

public sealed class Phase511Tests
{
    [Fact]
    public void Users_is_a_real_data_table_not_a_card_stack()
    {
        var page = Read("spa/src/pages/UsersPage.tsx");
        var css = Read("spa/src/styles.css");

        Assert.Contains("className=\"users-table\"", page, StringComparison.Ordinal);
        Assert.Contains("data-table=\"users\"", page, StringComparison.Ordinal);
        Assert.Contains("<table", page, StringComparison.Ordinal);
        Assert.Contains("<thead>", page, StringComparison.Ordinal);
        Assert.Contains("users-table-wrap", page, StringComparison.Ordinal);
        Assert.DoesNotContain("user-groups", page, StringComparison.Ordinal);
        Assert.DoesNotContain("user-group-toggle", page, StringComparison.Ordinal);
        Assert.DoesNotContain("user-card", page, StringComparison.Ordinal);
        Assert.DoesNotContain("card-stack", page, StringComparison.Ordinal);

        Assert.Contains(".users-table-wrap", css, StringComparison.Ordinal);
        Assert.Contains("position: sticky", css, StringComparison.Ordinal);
        Assert.Contains("--row-h: var(--table-row-h, var(--action-h))", css, StringComparison.Ordinal);
        Assert.Contains("min-height: var(--row-h)", css, StringComparison.Ordinal);
        Assert.Contains("min-height: var(--action-h)", css, StringComparison.Ordinal);
    }

    [Fact]
    public void One_filter_row_search_covers_name_email_display_name()
    {
        var page = Read("spa/src/pages/UsersPage.tsx");
        var helper = Read("spa/src/usersTable.ts");
        var css = Read("spa/src/styles.css");
        var shell = Read("spa/src/components/AppShell.tsx");

        Assert.Contains("filter-row users-filter-row", page, StringComparison.Ordinal);
        Assert.Contains("className=\"users-search\"", page, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Search users\"", page, StringComparison.Ordinal);
        Assert.Contains("placeholder=\"Search name or email\"", page, StringComparison.Ordinal);
        Assert.Contains("searchDraft", page, StringComparison.Ordinal);
        Assert.Contains("value={searchDraft}", page, StringComparison.Ordinal);
        Assert.Equal(1, Count(page, "users-search"));
        Assert.DoesNotContain("placeholder=\"Search", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("type=\"search\"", shell, StringComparison.Ordinal);

        Assert.Contains("matchesUsersSearch", helper, StringComparison.Ordinal);
        Assert.Contains("user.displayName", helper, StringComparison.Ordinal);
        Assert.Contains("user.fullName", helper, StringComparison.Ordinal);
        Assert.Contains("user.email", helper, StringComparison.Ordinal);
        Assert.Contains("max-width: 360px", css, StringComparison.Ordinal);
        Assert.Contains("input.users-search", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Column_headings_sort_and_filter_required_fields()
    {
        var page = Read("spa/src/pages/UsersPage.tsx");
        var helper = Read("spa/src/usersTable.ts");

        foreach (var heading in new[] { "Display Name", "Email", "Role", "Client(s)", "Status" })
        {
            Assert.Contains($"label=\"{heading}\"", page, StringComparison.Ordinal);
        }

        foreach (var key in new[] { "displayName", "email", "role", "clients", "status" })
        {
            Assert.Contains($"sortKey=\"{key}\"", page, StringComparison.Ordinal);
            Assert.Contains($"\"{key}\"", helper, StringComparison.Ordinal);
        }

        Assert.Contains("nextUsersSort", page, StringComparison.Ordinal);
        Assert.Contains("th-sort-affordance", page, StringComparison.Ordinal);
        Assert.Contains("aria-sort", page, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Filter Role\"", page, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Filter Client\"", page, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Filter Status\"", page, StringComparison.Ordinal);
        Assert.Contains("All Roles", page, StringComparison.Ordinal);
        Assert.Contains("All Clients", page, StringComparison.Ordinal);
        Assert.Contains("Enabled", page, StringComparison.Ordinal);
        Assert.Contains("Disabled", page, StringComparison.Ordinal);
        Assert.Contains("query.role", helper, StringComparison.Ordinal);
        Assert.Contains("user.clientIds.includes(query.client)", helper, StringComparison.Ordinal);
        Assert.Contains("query.status === \"disabled\"", helper, StringComparison.Ordinal);
    }

    [Fact]
    public void Carries_phase45_clients_confirm_sheet_and_admin_gate()
    {
        var page = Read("spa/src/pages/UsersPage.tsx");
        var helper = Read("spa/src/usersTable.ts");
        var shell = Read("spa/src/components/AppShell.tsx");

        Assert.Contains("clientColumnLabel", helper, StringComparison.Ordinal);
        Assert.Contains("names.join(\", \")", helper, StringComparison.Ordinal);
        Assert.Contains("Client column shows every assignment", helper, StringComparison.Ordinal);
        Assert.Contains("ConfirmSheet", page, StringComparison.Ordinal);
        Assert.Contains("Disable this user?", page, StringComparison.Ordinal);
        Assert.Contains("if (!canAdmin)", page, StringComparison.Ordinal);
        Assert.Contains("navigate(\"/denied\", { state: { action: \"manage users\" } })", page, StringComparison.Ordinal);
        Assert.Contains("PasswordPair", page, StringComparison.Ordinal);
        Assert.Contains("PhotoEditor", page, StringComparison.Ordinal);
        Assert.Contains("<h2>{editing === \"new\" ? \"New User\" : \"Edit User\"}</h2>", page, StringComparison.Ordinal);
        Assert.Contains("to=\"/users\"", shell, StringComparison.Ordinal);
        Assert.Contains("to=\"/software\"", shell, StringComparison.Ordinal);
        Assert.Contains("                    Systems", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("County", page, StringComparison.Ordinal);
        Assert.DoesNotContain("CAMA", page, StringComparison.Ordinal);
        Assert.DoesNotContain("County", helper, StringComparison.Ordinal);
        Assert.DoesNotContain("CAMA", helper, StringComparison.Ordinal);
    }

    [Fact]
    public void Persists_sort_filter_and_handles_empty_or_huge_lists()
    {
        var page = Read("spa/src/pages/UsersPage.tsx");
        var helper = Read("spa/src/usersTable.ts");

        Assert.Contains("useSearchParams", page, StringComparison.Ordinal);
        Assert.Contains("parseUsersTableQuery", page, StringComparison.Ordinal);
        Assert.Contains("serializeUsersTableQuery", page, StringComparison.Ordinal);
        Assert.Contains("USERS_TABLE_STORAGE_KEY", helper, StringComparison.Ordinal);
        Assert.Contains("deedai.users.table", helper, StringComparison.Ordinal);
        Assert.Contains("sessionStorage.getItem", helper, StringComparison.Ordinal);
        Assert.Contains("sessionStorage.setItem", helper, StringComparison.Ordinal);
        Assert.Contains("readStoredUsersTableQuery", page, StringComparison.Ordinal);
        Assert.Contains("No Users Yet", page, StringComparison.Ordinal);
        Assert.Contains("No Users Match", page, StringComparison.Ordinal);
        Assert.Contains("USERS_PAGE_SIZE = 50", helper, StringComparison.Ordinal);
        Assert.Contains("users-pager", page, StringComparison.Ordinal);
        Assert.Contains("table.totalPages > 1", page, StringComparison.Ordinal);
    }

    [Fact]
    public void Phase511_adds_no_ef_migration()
    {
        var migrations = Path.Combine(RepoRoot(), "src", "DeedAi.Infrastructure", "Data", "Migrations");
        Assert.False(Directory.EnumerateFiles(migrations, "*Phase511*").Any());
        Assert.False(Directory.EnumerateFiles(migrations, "*Phase5.1*").Any());
    }

    private static int Count(string text, string value)
    {
        var count = 0;
        for (var index = 0; (index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0; index += value.Length)
        {
            count++;
        }

        return count;
    }

    private static string Read(string relative)
    {
        var path = Path.Combine(RepoRoot(), relative.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(path), path);
        return File.ReadAllText(path);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DeedAi.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Could not locate DeedAi.slnx from the test output.");
    }
}
