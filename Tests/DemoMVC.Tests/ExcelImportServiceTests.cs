using ClosedXML.Excel;
using DemoMVC.Data;
using DemoMVC.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DemoMVC.Tests;

public sealed class ExcelImportServiceTests
{
    [Fact]
    public async Task ImportAsync_SkipsBlankRowsAndGeneratesSequentialCodes()
    {
        await using var database = await CreateDatabaseAsync();
        var service = new ExcelImportService(database.Context);
        using var workbook = CreateWorkbook(
            ("Nguyen Van A", "a@example.com", "Ha Noi", "2026", "21", "05", "CNTT"),
            ("", "", "", "", "", "", ""),
            ("Tran Thi B", "b@example.com", "", "2026", "21", "05", "CNTT"));

        var result = await service.ImportAsync(workbook);

        Assert.Equal(2, result.TotalRows);
        Assert.Equal(2, result.SuccessfulRows);
        Assert.Equal(0, result.ErrorRows);
        var students = await database.Context.Student
            .OrderBy(student => student.StudentCode)
            .ToListAsync();
        Assert.Equal(["2621050001", "2621050002"], students.Select(student => student.StudentCode));
    }

    [Fact]
    public async Task ImportAsync_ReportsInvalidValuesAndDuplicateEmailsByColumn()
    {
        await using var database = await CreateDatabaseAsync();
        var service = new ExcelImportService(database.Context);
        using var workbook = CreateWorkbook(
            ("Valid Name", "same@example.com", "", "2026", "21", "05", "CNTT"),
            ("Second Name", "SAME@example.com", "", "1800", "2", "05", "NOPE"));

        var result = await service.ImportAsync(workbook);

        Assert.Equal(2, result.TotalRows);
        Assert.Equal(1, result.SuccessfulRows);
        Assert.Equal(1, result.ErrorRows);
        Assert.Contains(result.Errors, error =>
            error.RowNumber == 3 &&
            error.Column == "Email" &&
            error.Reason.Contains("trùng", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Errors, error => error.Column == "AdmissionYear");
        Assert.Contains(result.Errors, error => error.Column == "TrainingSystemCode");
        Assert.Contains(result.Errors, error => error.Column == "DepartmentCode");
        Assert.Single(await database.Context.Student.ToListAsync());
    }

    [Fact]
    public async Task PreviewAsync_ValidatesAndGeneratesPreviewWithoutSaving()
    {
        await using var database = await CreateDatabaseAsync();
        var service = new ExcelImportService(database.Context);
        using var workbook = CreateWorkbook(
            ("Preview Student", "preview@example.com", "", "2026", "21", "05", "CNTT"));

        var result = await service.PreviewAsync(workbook);

        Assert.Equal(1, result.TotalRows);
        Assert.Equal(1, result.SuccessfulRows);
        Assert.Equal(2, Assert.Single(result.PreviewRows).RowNumber);
        Assert.Equal("2621050001", result.PreviewRows[0].StudentCode);
        Assert.Empty(await database.Context.Student.ToListAsync());
    }

    private static MemoryStream CreateWorkbook(
        params (string FullName, string Email, string Address, string Year, string System, string Major, string Department)[] rows)
    {
        var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Students");
        for (var column = 0; column < ExcelImportService.ImportHeaders.Count; column++)
        {
            worksheet.Cell(1, column + 1).Value = ExcelImportService.ImportHeaders[column];
        }

        for (var rowIndex = 0; rowIndex < rows.Length; rowIndex++)
        {
            var row = rows[rowIndex];
            var values = new[]
            {
                row.FullName, row.Email, row.Address, row.Year,
                row.System, row.Major, row.Department
            };
            for (var column = 0; column < values.Length; column++)
            {
                worksheet.Cell(rowIndex + 2, column + 1).Value = values[column];
            }
        }

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        workbook.Dispose();
        stream.Position = 0;
        return stream;
    }

    private static async Task<TestDatabase> CreateDatabaseAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;
        var context = new ApplicationDbContext(options);
        await context.Database.EnsureCreatedAsync();
        return new TestDatabase(connection, context);
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        public TestDatabase(SqliteConnection connection, ApplicationDbContext context)
        {
            Connection = connection;
            Context = context;
        }

        public SqliteConnection Connection { get; }
        public ApplicationDbContext Context { get; }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }
}
