namespace DemoMVC.Models;

public sealed class ExcelImportResult
{
    public int TotalRows { get; set; }

    public int SuccessfulRows { get; set; }

    public int ErrorRows => Errors.Select(error => error.RowNumber).Distinct().Count();

    public List<ExcelImportError> Errors { get; set; } = [];

    public List<ExcelImportPreviewRow> PreviewRows { get; set; } = [];

    public string? Message { get; set; }

    public bool IsValid { get; set; } = true;
}

public sealed class ExcelImportError
{
    public int RowNumber { get; set; }

    public string Column { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;
}

public sealed class ExcelImportPreviewRow
{
    public int RowNumber { get; set; }

    public string? StudentCode { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? Address { get; set; }

    public int? AdmissionYear { get; set; }

    public string TrainingSystemCode { get; set; } = string.Empty;

    public string MajorCode { get; set; } = string.Empty;

    public string DepartmentCode { get; set; } = string.Empty;

    public bool IsValid { get; set; }
}
