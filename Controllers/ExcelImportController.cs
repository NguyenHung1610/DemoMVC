using DemoMVC.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DemoMVC.Controllers;

[Authorize]
public sealed class ExcelImportController : Controller
{
    private const long MaxImportFileSize = 10 * 1024 * 1024;
    private readonly ExcelImportService _excelImportService;

    public ExcelImportController(ExcelImportService excelImportService)
    {
        _excelImportService = excelImportService;
    }

    [HttpGet]
    public IActionResult Index() => RedirectToAction("Index", "Student");

    [HttpGet]
    public IActionResult DownloadTemplate()
    {
        return File(
            ExcelImportService.CreateTemplate(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "StudentImportTemplate.xlsx");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(11 * 1024 * 1024)]
    public async Task<IActionResult> Preview(IFormFile? file, CancellationToken cancellationToken)
    {
        var validation = ValidateFile(file);
        if (validation is not null)
        {
            return BadRequest(new { message = validation });
        }

        await using var stream = file!.OpenReadStream();
        var result = await _excelImportService.PreviewAsync(stream, cancellationToken);
        return result.IsValid ? Json(result) : BadRequest(new { message = result.Message });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(11 * 1024 * 1024)]
    public async Task<IActionResult> Import(IFormFile? file, CancellationToken cancellationToken)
    {
        var validation = ValidateFile(file);
        if (validation is not null)
        {
            return BadRequest(new { message = validation });
        }

        await using var stream = file!.OpenReadStream();
        var result = await _excelImportService.ImportAsync(stream, cancellationToken);
        return result.IsValid ? Json(result) : BadRequest(new { message = result.Message });
    }

    private static string? ValidateFile(IFormFile? file)
    {
        if (file is null || file.Length == 0)
        {
            return "Vui lòng chọn file Excel không rỗng.";
        }

        if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return "Chỉ chấp nhận file có định dạng .xlsx.";
        }

        if (file.Length > MaxImportFileSize)
        {
            return "Kích thước file không được vượt quá 10 MB.";
        }

        return null;
    }
}
