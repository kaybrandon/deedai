using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using DeedAi.Domain;
using DeedAi.Domain.Entities;
using DeedAi.Domain.Ocr;
using DeedAi.Infrastructure;
using DeedAi.Infrastructure.Data;
using DeedAi.Infrastructure.Data.Migrations;
using DeedAi.Infrastructure.Email;
using DeedAi.Infrastructure.Ocr;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DeedAi.Tests;

public sealed class Phase48EmailTests
{
    private const string TestSendGridKey = "sg-test-configured-0000";
    private const string TestSmtpPassword = "smtp-pass-configured";

    [Fact]
    public async Task Admin_can_switch_mode_and_status_hides_secrets()
    {
        await using var factory = TestAppFactory.Create(extraSettings: SmtpKv());
        var client = await Authed(factory, DatabaseSeeder.AdminEmail);

        var get = await client.GetAsync("/api/settings/email");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var body = await get.Content.ReadAsStringAsync();
        AssertNoSecrets(body);
        using (var json = JsonDocument.Parse(body))
        {
            Assert.Equal(EmailModes.SendGrid, json.RootElement.GetProperty("mode").GetString());
            Assert.True(json.RootElement.GetProperty("configured").GetBoolean());
            Assert.True(json.RootElement.GetProperty("sendGridConfigured").GetBoolean());
            Assert.Equal("0000", json.RootElement.GetProperty("sendGridKeyLast4").GetString());
            Assert.True(json.RootElement.GetProperty("smtpHostConfigured").GetBoolean());
            Assert.Equal("smtp.example.test", json.RootElement.GetProperty("smtpHost").GetString());
            Assert.Equal(587, json.RootElement.GetProperty("smtpPort").GetInt32());
            Assert.True(json.RootElement.GetProperty("smtpTls").GetBoolean());
            Assert.True(json.RootElement.GetProperty("smtpUsernameConfigured").GetBoolean());
            Assert.True(json.RootElement.GetProperty("smtpPasswordConfigured").GetBoolean());
            Assert.True(json.RootElement.GetProperty("verifyRequired").GetBoolean());
        }

        var put = await client.PutAsync("/api/settings/email", TestAppFactory.Json(
            """{"mode":"Smtp","fromName":"Deed AI Mail","fromAddress":"noreply@bisconsultants.com","verifyRequired":true}"""));
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var saved = await put.Content.ReadAsStringAsync();
        AssertNoSecrets(saved);
        using var savedJson = JsonDocument.Parse(saved);
        Assert.Equal(EmailModes.Smtp, savedJson.RootElement.GetProperty("mode").GetString());
        Assert.True(savedJson.RootElement.GetProperty("configured").GetBoolean());
        Assert.Equal("Deed AI Mail", savedJson.RootElement.GetProperty("fromName").GetString());
    }

    [Theory]
    [InlineData("viewer@bisconsultants.com")]
    [InlineData("editor@bisconsultants.com")]
    [InlineData("uploader@bisconsultants.com")]
    public async Task Non_admin_cannot_read_or_change_email_settings(string email)
    {
        await using var factory = TestAppFactory.Create();
        var client = await Authed(factory, email);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/settings/email")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsync("/api/settings/email", TestAppFactory.Json(
            """{"mode":"Smtp","fromName":"X","fromAddress":"noreply@bisconsultants.com","verifyRequired":false}"""))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/settings/email/test", TestAppFactory.Json(
            """{"to":"ops@bisconsultants.com"}"""))).StatusCode);
    }

    [Fact]
    public async Task Test_send_passes_when_active_mode_is_configured()
    {
        await using var factory = TestAppFactory.Create();
        var recorder = factory.GetRequiredService<RecordingEmailSender>();
        recorder.Clear();
        var client = await Authed(factory, DatabaseSeeder.AdminEmail);

        var response = await client.PostAsync("/api/settings/email/test", TestAppFactory.Json("""{"to":"ops@bisconsultants.com"}"""));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        AssertNoSecrets(body);
        using var json = JsonDocument.Parse(body);
        Assert.True(json.RootElement.GetProperty("passed").GetBoolean());
        Assert.Contains("Pass", json.RootElement.GetProperty("message").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains(recorder.Sent, x => x.To == "ops@bisconsultants.com" && x.Subject.Contains("test", StringComparison.OrdinalIgnoreCase));

        var status = await (await client.GetAsync("/api/settings/email")).Content.ReadAsStringAsync();
        using var statusJson = JsonDocument.Parse(status);
        Assert.False(statusJson.RootElement.GetProperty("lastSuccessAt").ValueKind is JsonValueKind.Null);
    }

    [Fact]
    public async Task Test_send_and_forgot_fail_closed_when_unconfigured()
    {
        await using var factory = UnconfiguredFactory();
        var recorder = factory.GetRequiredService<RecordingEmailSender>();
        recorder.Clear();
        var client = await Authed(factory, DatabaseSeeder.AdminEmail);

        var test = await client.PostAsync("/api/settings/email/test", TestAppFactory.Json("""{"to":"ops@bisconsultants.com"}"""));
        Assert.Equal(HttpStatusCode.OK, test.StatusCode);
        var testBody = await test.Content.ReadAsStringAsync();
        AssertNoSecrets(testBody);
        using (var json = JsonDocument.Parse(testBody))
        {
            Assert.False(json.RootElement.GetProperty("passed").GetBoolean());
            Assert.Contains("not configured", json.RootElement.GetProperty("message").GetString(), StringComparison.OrdinalIgnoreCase);
        }

        var forgot = await factory.CreateJsonClient().PostAsync(
            "/api/auth/forgot-password",
            TestAppFactory.Json("""{"email":"viewer@bisconsultants.com"}"""));
        Assert.Equal(HttpStatusCode.OK, forgot.StatusCode);
        Assert.Contains("If that email is on file", await forgot.Content.ReadAsStringAsync());
        Assert.DoesNotContain(recorder.Sent, x => x.To == "viewer@bisconsultants.com");
        Assert.DoesNotContain(recorder.Sent, x => x.To == "ops@bisconsultants.com");
    }

    [Fact]
    public async Task Unverified_user_is_gated_until_token_verify_and_disabled_stays_blocked()
    {
        await using var factory = TestAppFactory.Create();
        var recorder = factory.GetRequiredService<RecordingEmailSender>();
        var admin = await Authed(factory, DatabaseSeeder.AdminEmail);
        var create = await admin.PostAsync("/api/admin/users", TestAppFactory.Json(
            """{"email":"pat@bisconsultants.com","displayName":"Pat","fullName":"Pat Viewer","role":"Viewer","password":"ChangeMe!2","isActive":true,"clientIds":[]}"""));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        using (var created = JsonDocument.Parse(await create.Content.ReadAsStringAsync()))
        {
            Assert.False(created.RootElement.GetProperty("emailVerified").GetBoolean());
        }

        var login = factory.CreateJsonClient();
        var blocked = await login.PostAsync("/api/auth/login", TestAppFactory.Json("""{"email":"pat@bisconsultants.com","password":"ChangeMe!2"}"""));
        Assert.Equal(HttpStatusCode.Unauthorized, blocked.StatusCode);
        Assert.Contains("Verify your email", await blocked.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        var mail = recorder.Sent.First(x => x.To == "pat@bisconsultants.com" && x.Subject.Contains("Verify", StringComparison.OrdinalIgnoreCase));
        var token = ExtractVerifyToken(mail.TextBody);
        var verify = await login.PostAsync("/api/auth/verify-email", TestAppFactory.Json("{\"token\":\"" + token + "\"}"));
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);

        var tokenValue = await factory.LoginAsync(login, "pat@bisconsultants.com", "ChangeMe!2");
        Assert.False(string.IsNullOrWhiteSpace(tokenValue));

        Guid userId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DeedAiDbContext>();
            var user = await db.Users.SingleAsync(x => x.Email == "pat@bisconsultants.com");
            userId = user.Id;
            Assert.True(user.EmailVerified);
            user.IsActive = false;
            await db.SaveChangesAsync();
        }

        var disabled = await factory.CreateJsonClient().PostAsync(
            "/api/auth/login",
            TestAppFactory.Json("""{"email":"pat@bisconsultants.com","password":"ChangeMe!2"}"""));
        Assert.Equal(HttpStatusCode.Unauthorized, disabled.StatusCode);
        Assert.Contains("disabled", await disabled.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        recorder.Clear();
        var resend = await admin.PostAsync($"/api/admin/users/{userId}/resend-verification", null);
        Assert.Equal(HttpStatusCode.BadRequest, resend.StatusCode);
        Assert.Contains("already verified", await resend.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Verify_required_off_lets_unverified_sign_in_and_admin_can_resend()
    {
        await using var factory = TestAppFactory.Create();
        var recorder = factory.GetRequiredService<RecordingEmailSender>();
        var admin = await Authed(factory, DatabaseSeeder.AdminEmail);
        await admin.PutAsync("/api/settings/email", TestAppFactory.Json(
            """{"mode":"SendGrid","fromName":"Deed AI","fromAddress":"noreply@bisconsultants.com","verifyRequired":false}"""));

        var create = await admin.PostAsync("/api/admin/users", TestAppFactory.Json(
            """{"email":"lee@bisconsultants.com","displayName":"Lee","fullName":"Lee Viewer","role":"Viewer","password":"ChangeMe!2","isActive":true,"clientIds":[]}"""));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        using var created = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var id = created.RootElement.GetProperty("id").GetGuid();
        Assert.False(created.RootElement.GetProperty("emailVerified").GetBoolean());

        var allowed = await factory.LoginAsync(factory.CreateJsonClient(), "lee@bisconsultants.com", "ChangeMe!2");
        Assert.False(string.IsNullOrWhiteSpace(allowed));

        recorder.Clear();
        var resend = await admin.PostAsync($"/api/admin/users/{id}/resend-verification", null);
        Assert.Equal(HttpStatusCode.OK, resend.StatusCode);
        Assert.Contains(recorder.Sent, x => x.To == "lee@bisconsultants.com" && x.Subject.Contains("Verify", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Ocr_notify_skips_when_unconfigured()
    {
        await using var factory = UnconfiguredFactory();
        var recorder = factory.GetRequiredService<RecordingEmailSender>();
        recorder.Clear();

        Guid documentId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DeedAiDbContext>();
            var blobs = scope.ServiceProvider.GetRequiredService<DeedAi.Domain.Abstractions.IBlobStorage>();
            documentId = Guid.NewGuid();
            var blobPath = $"deeds/notify-{documentId:N}.pdf";
            db.Documents.Add(new Document
            {
                Id = documentId,
                Name = "Notify_ready.pdf",
                ClientId = DatabaseSeeder.AcmeId,
                Status = DocumentStatuses.Queued,
                BlobPath = blobPath,
                AssigneeUserId = DatabaseSeeder.EditorId,
                UploadedByUserId = DatabaseSeeder.UploaderId,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
            await using var pdf = new MemoryStream("%PDF-1.4 test"u8.ToArray());
            await blobs.UploadAsync(blobPath, pdf, "application/pdf", CancellationToken.None);
        }

        using (var scope = factory.Services.CreateScope())
        {
            var processor = scope.ServiceProvider.GetRequiredService<OcrProcessor>();
            var document = await scope.ServiceProvider.GetRequiredService<DeedAiDbContext>().Documents.AsNoTracking().FirstAsync(x => x.Id == documentId);
            await processor.ProcessAsync(new OcrQueueDelivery
            {
                Job = new OcrJobMessage { DocumentId = documentId, BlobPath = document.BlobPath },
                MessageId = "n48",
                PopReceipt = "r",
                DequeueCount = 1
            }, CancellationToken.None);
        }

        Assert.DoesNotContain(recorder.Sent, x => x.To == "editor@bisconsultants.com");
    }

    [Fact]
    public void Smtp_and_sendgrid_secrets_read_kv_names_only()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["SendGrid__ApiKey"] = "sg-from-kv",
            ["Smtp__Host"] = "smtp.example.test",
            ["Smtp__Port"] = "587",
            ["Smtp__UseTls"] = "true",
            ["Smtp__Username"] = "kv-user",
            ["Smtp__Password"] = "kv-pass",
            ["Smtp__TimeoutSeconds"] = "45"
        }).Build();

        Assert.Equal("sg-from-kv", DependencyInjection.FirstValue(config, "SendGridApiKey", "SendGrid:ApiKey", "SendGrid__ApiKey"));
        var kv = EmailKv.Read(config);
        Assert.True(kv.SendGridConfigured);
        Assert.Equal("m-kv", kv.SendGridKeyLast4);
        Assert.True(kv.SmtpConfigured);
        Assert.Equal("smtp.example.test", kv.SmtpHost);
        Assert.Equal(587, kv.SmtpPort);
        Assert.True(kv.SmtpTls);
        Assert.Equal(45, kv.SmtpTimeoutSeconds);
        Assert.True(kv.ConfiguredFor(EmailModes.SendGrid));
        Assert.True(kv.ConfiguredFor(EmailModes.Smtp));
        Assert.False(kv.ConfiguredFor(EmailModes.SendGrid) && kv.SendGridKeyLast4 == "sg-from-kv");
    }

    [Fact]
    public void Phase48_sql_server_up_is_guarded_and_has_designer()
    {
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        var up = typeof(Phase48AdminEmail).GetMethod("Up", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(up);
        up!.Invoke(new Phase48AdminEmail(), [builder]);
        Assert.Empty(builder.Operations.OfType<CreateTableOperation>());
        Assert.Empty(builder.Operations.OfType<AddColumnOperation>());
        var sql = string.Join('\n', builder.Operations.OfType<SqlOperation>().Select(x => x.Sql));
        Assert.Contains("IF COL_LENGTH(N'dbo.Users', N'EmailVerified') IS NULL", sql, StringComparison.Ordinal);
        Assert.Contains("IF OBJECT_ID(N'dbo.EmailSettings', N'U') IS NULL", sql, StringComparison.Ordinal);
        Assert.Contains("IF OBJECT_ID(N'dbo.EmailVerificationTokens', N'U') IS NULL", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("sg-", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", sql, StringComparison.OrdinalIgnoreCase);

        var designer = typeof(Phase48AdminEmail);
        Assert.NotNull(designer.GetCustomAttributes(typeof(MigrationAttribute), false).SingleOrDefault());
        Assert.NotNull(designer.GetCustomAttributes(typeof(Microsoft.EntityFrameworkCore.Infrastructure.DbContextAttribute), false).SingleOrDefault());
        Assert.NotNull(designer.GetMethod("BuildTargetModel", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic));
    }

    [Fact]
    public void Placeholder_keys_are_not_configured()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["SendGridApiKey"] = "PLACEHOLDER",
            ["SmtpHost"] = "PLACEHOLDER",
            ["SmtpUsername"] = "PLACEHOLDER",
            ["SmtpPassword"] = "PLACEHOLDER"
        }).Build();
        var kv = EmailKv.Read(config);
        Assert.False(kv.SendGridConfigured);
        Assert.False(kv.SmtpConfigured);
        Assert.Null(kv.SendGridKeyLast4);
    }

    private static TestAppFactory UnconfiguredFactory() =>
        TestAppFactory.Create(extraSettings: new Dictionary<string, string?>
        {
            ["SendGridApiKey"] = "",
            ["SendGrid:ApiKey"] = "",
            ["SendGrid__ApiKey"] = ""
        });

    private static Dictionary<string, string?> SmtpKv() => new()
    {
        ["SmtpHost"] = "smtp.example.test",
        ["SmtpPort"] = "587",
        ["SmtpTls"] = "true",
        ["SmtpUsername"] = "smtp-user",
        ["SmtpPassword"] = TestSmtpPassword,
        ["SmtpTimeoutSeconds"] = "30"
    };

    private static void AssertNoSecrets(string body)
    {
        Assert.DoesNotContain(TestSendGridKey, body, StringComparison.Ordinal);
        Assert.DoesNotContain(TestSmtpPassword, body, StringComparison.Ordinal);
        Assert.DoesNotContain("smtp-user", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Bearer ", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PLACEHOLDER", body, StringComparison.Ordinal);
    }

    private static string ExtractVerifyToken(string body)
    {
        const string marker = "Verify token: ";
        var start = body.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, body);
        return body[(start + marker.Length)..].Split('\n', '\r')[0].Trim();
    }

    private static async Task<HttpClient> Authed(TestAppFactory factory, string email)
    {
        var client = factory.CreateJsonClient();
        var token = await factory.LoginAsync(client, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
