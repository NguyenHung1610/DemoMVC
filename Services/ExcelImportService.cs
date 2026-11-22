using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using DemoMVC.Data;
using DemoMVC.Models;
using DocumentFormat.OpenXml.Packaging;
using Microsoft.EntityFrameworkCore;

namespace DemoMVC.Services;

public sealed class ExcelImportService
{
    public const int MaxImportRows = 10_000;

    public static readonly IReadOnlyList<string> ImportHeaders =
    [
        "FullName",
        "Email",
        "Address",
        "AdmissionYear",
        "TrainingSystemCode",
        "MajorCode",
        "DepartmentCode"
    ];

    private readonly ApplicationDbContext _context;

    public ExcelImportService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ExcelImportResult> PreviewAsync(
        Stream file,
        CancellationToken cancellationToken = default)
    {
        var parsed = await ParseAsync(file, cancellationToken);
        return parsed.Result;
    }

    public async Task<ExcelImportResult> ImportAsync(
        Stream file,
        CancellationToken cancellationToken = default)
    {
        var parsed = await ParseAsync(file, cancellationToken);
        if (!parsed.Result.IsValid)
        {
            return parsed.Result;
        }

        if (parsed.Students.Count > 0)
        {
            _context.Student.AddRange(parsed.Students);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return parsed.Result;
    }

    public static byte[] CreateTemplate()
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Students");
        for (var column = 0; column < ImportHeaders.Count; column++)
        {
            worksheet.Cell(1, column + 1).Value = ImportHeaders[column];
        }

        worksheet.Range(1, 1, 1, ImportHeaders.Count).Style.Font.Bold = true;
        worksheet.Columns(1, ImportHeaders.Count).AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private async Task<ParsedImport> ParseAsync(
        Stream file,
        CancellationToken cancellationToken)
    {
        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(file);
        }
        catch (Exception exception) when (
            exception is IOException or InvalidDataException or FormatException or
                ArgumentException or System.Xml.XmlException or OpenXmlPackageException)
        {
            return ParsedImport.Invalid("File không phải là workbook Excel hợp lệ.");
        }

        using (workbook)
        {
            var worksheet = workbook.Worksheets.FirstOrDefault();
            if (worksheet is null)
            {
                return ParsedImport.Invalid("File Excel không có worksheet dữ liệu.");
            }

            var header = worksheet.Row(1);
            for (var column = 0; column < ImportHeaders.Count; column++)
            {
                if (!string.Equals(
                        header.Cell(column + 1).GetString().Trim(),
                        ImportHeaders[column],
                        StringComparison.OrdinalIgnoreCase))
                {
                    return ParsedImport.Invalid(
                        "Dòng tiêu đề phải gồm đúng thứ tự các cột: " +
                        string.Join(", ", ImportHeaders) + ".");
                }
            }

            var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
            if (lastRow - 1 > MaxImportRows)
            {
                return ParsedImport.Invalid("File chỉ được chứa tối đa 10.000 dòng dữ liệu.");
            }

            var result = new ExcelImportResult();
            var errorsByRow = new Dictionary<int, List<ExcelImportError>>();
            var parsedRows = new List<(ExcelImportPreviewRow Preview, Student Student)>();
            var departmentCodes = await _context.Departments
                .AsNoTracking()
                .Select(department => department.DepartmentCode)
                .ToDictionaryAsync(code => code, StringComparer.OrdinalIgnoreCase, cancellationToken);
            var existingEmails = await _context.Student
                .AsNoTracking()
                .Select(student => student.Email)
                .ToListAsync(cancellationToken);
            var seenEmails = new HashSet<string>(existingEmails, StringComparer.OrdinalIgnoreCase);

            for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var row = worksheet.Row(rowNumber);
                if (Enumerable.Range(1, ImportHeaders.Count)
                    .All(column => string.IsNullOrWhiteSpace(row.Cell(column).GetString())))
                {
                    continue;
                }

                result.TotalRows++;
                var student = new Student
                {
                    FullName = row.Cell(1).GetString().Trim(),
                    Email = row.Cell(2).GetString().Trim(),
                    Address = NullIfWhiteSpace(row.Cell(3).GetString()),
                    TrainingSystemCode = row.Cell(5).GetString().Trim(),
                    MajorCode = row.Cell(6).GetString().Trim(),
                    DepartmentCode = row.Cell(7).GetString().Trim()
                };
                int? admissionYear = null;
                var yearCell = row.Cell(4);
                if (!yearCell.TryGetValue<int>(out var parsedYear) &&
                    !int.TryParse(
                        yearCell.GetString().Trim(),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out parsedYear))
                {
                    AddError(errorsByRow, rowNumber, "AdmissionYear", "năm nhập học không hợp lệ");
                }
                else
                {
                    admissionYear = parsedYear;
                    student.AdmissionYear = parsedYear;
                    if (parsedYear is < 2000 or > 2099)
                    {
                        AddError(
                            errorsByRow,
                            rowNumber,
                            "AdmissionYear",
                            "năm nhập học phải từ 2000 đến 2099");
                    }
                }

                if (string.IsNullOrWhiteSpace(student.FullName))
                {
                    AddError(errorsByRow, rowNumber, "FullName", "thiếu họ tên");
                }
                else if (student.FullName.Length > 100)
                {
                    AddError(errorsByRow, rowNumber, "FullName", "họ tên không được vượt quá 100 ký tự");
                }

                if (string.IsNullOrWhiteSpace(student.Email))
                {
                    AddError(errorsByRow, rowNumber, "Email", "thiếu email");
                }
                else if (student.Email.Length > 254 ||
                         !new EmailAddressAttribute().IsValid(student.Email))
                {
                    AddError(errorsByRow, rowNumber, "Email", "email không hợp lệ");
                }
                else if (seenEmails.Contains(student.Email))
                {
                    AddError(errorsByRow, rowNumber, "Email", "email đã tồn tại hoặc bị trùng trong file");
                }

                if (!Regex.IsMatch(student.TrainingSystemCode, @"^\d{2}$"))
                {
                    AddError(
                        errorsByRow,
                        rowNumber,
                        "TrainingSystemCode",
                        "mã hệ đào tạo phải gồm 2 chữ số");
                }

                if (!Regex.IsMatch(student.MajorCode, @"^\d{2}$"))
                {
                    AddError(errorsByRow, rowNumber, "MajorCode", "mã ngành phải gồm 2 chữ số");
                }

                if (string.IsNullOrWhiteSpace(student.DepartmentCode))
                {
                    AddError(errorsByRow, rowNumber, "DepartmentCode", "thiếu mã khoa");
                }
                else if (!departmentCodes.TryGetValue(student.DepartmentCode, out var departmentCode))
                {
                    AddError(
                        errorsByRow,
                        rowNumber,
                        "DepartmentCode",
                        $"sai mã khoa '{student.DepartmentCode}'");
                }
                else
                {
                    student.DepartmentCode = departmentCode;
                }

                var preview = new ExcelImportPreviewRow
                {
                    RowNumber = rowNumber,
                    FullName = student.FullName,
                    Email = student.Email,
                    Address = student.Address,
                    AdmissionYear = admissionYear,
                    TrainingSystemCode = student.TrainingSystemCode,
                    MajorCode = student.MajorCode,
                    DepartmentCode = student.DepartmentCode
                };
                parsedRows.Add((preview, student));

                if (!errorsByRow.ContainsKey(rowNumber) &&
                    !string.IsNullOrWhiteSpace(student.Email))
                {
                    seenEmails.Add(student.Email);
                }
            }

            var existingStudentCodes = await _context.Student
                .AsNoTracking()
                .Select(student => student.StudentCode)
                .ToListAsync(cancellationToken);
            var nextSequenceByPrefix = new Dictionary<string, int>(StringComparer.Ordinal);
            var studentsToImport = new List<Student>();

            foreach (var (preview, student) in parsedRows)
            {
                if (errorsByRow.ContainsKey(preview.RowNumber))
                {
                    continue;
                }

                var prefix = CreateStudentCodePrefix(student);
                if (!nextSequenceByPrefix.TryGetValue(prefix, out var lastSequence))
                {
                    lastSequence = existingStudentCodes
                        .Where(code => code.StartsWith(prefix, StringComparison.Ordinal))
                        .Select(code => code.Substring(prefix.Length))
                        .Where(suffix => suffix.Length == 4 && suffix.All(char.IsDigit))
                        .Select(suffix => int.TryParse(
                            suffix,
                            NumberStyles.None,
                            CultureInfo.InvariantCulture,
                            out var sequence) ? sequence : 0)
                        .DefaultIfEmpty(0)
                        .Max();
                }

                if (lastSequence >= 9999)
                {
                    AddError(
                        errorsByRow,
                        preview.RowNumber,
                        "StudentCode",
                        $"mã sinh viên với tiền tố {prefix} đã vượt giới hạn 9999");
                    continue;
                }

                var nextSequence = lastSequence + 1;
                student.StudentCode = $"{prefix}{nextSequence:0000}";
                preview.StudentCode = student.StudentCode;
                preview.IsValid = true;
                nextSequenceByPrefix[prefix] = nextSequence;
                studentsToImport.Add(student);
            }

            result.SuccessfulRows = studentsToImport.Count;
            result.Errors = errorsByRow
                .OrderBy(item => item.Key)
                .SelectMany(item => item.Value)
                .ToList();
            result.PreviewRows = parsedRows.Select(item =>
            {
                item.Preview.IsValid = !errorsByRow.ContainsKey(item.Preview.RowNumber);
                return item.Preview;
            }).ToList();

            return new ParsedImport(result, studentsToImport);
        }
    }

    private static void AddError(
        IDictionary<int, List<ExcelImportError>> errorsByRow,
        int rowNumber,
        string column,
        string reason)
    {
        if (!errorsByRow.TryGetValue(rowNumber, out var rowErrors))
        {
            rowErrors = [];
            errorsByRow.Add(rowNumber, rowErrors);
        }

        rowErrors.Add(new ExcelImportError
        {
            RowNumber = rowNumber,
            Column = column,
            Reason = reason
        });
    }

    private static string CreateStudentCodePrefix(Student student) =>
        $"{student.AdmissionYear % 100:00}{student.TrainingSystemCode}{student.MajorCode}";

    private static string? NullIfWhiteSpace(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record ParsedImport(ExcelImportResult Result, List<Student> Students)
    {
        public static ParsedImport Invalid(string message) =>
            new(new ExcelImportResult { IsValid = false, Message = message }, []);
    }
}
