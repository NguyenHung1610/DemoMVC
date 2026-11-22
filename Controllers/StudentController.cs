using DemoMVC.Data;
using DemoMVC.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace DemoMVC.Controllers;

[Authorize]
public class StudentController : Controller
{
    private readonly ApplicationDbContext _context;

    public StudentController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(string? search, int page = 1, int pageSize = 10)
    {
        return View(await GetStudentsPageAsync(search, page, pageSize));
    }

    public async Task<IActionResult> Table(string? search, int page = 1, int pageSize = 10)
    {
        return PartialView("_StudentTable", await GetStudentsPageAsync(search, page, pageSize));
    }

    private async Task<PagedResult<Student>> GetStudentsPageAsync(
        string? search,
        int page,
        int pageSize)
    {
        search = search?.Trim() ?? string.Empty;
        pageSize = pageSize is 10 or 20 or 50 ? pageSize : 10;

        var query = _context.Student
            .AsNoTracking()
            .Include(student => student.Department)
            .AsQueryable();

        if (search.Length > 0)
        {
            query = query.Where(student =>
                student.StudentCode.Contains(search) ||
                student.FullName.Contains(search) ||
                student.Email.Contains(search) ||
                (student.Address != null && student.Address.Contains(search)) ||
                (student.Department != null && student.Department.Name.Contains(search)));
        }

        var totalCount = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);

        var students = await query
            .OrderBy(student => student.StudentCode)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<Student>
        {
            Items = students,
            SearchTerm = search,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<IActionResult> Details(string? id)
    {
        if (id is null)
        {
            return NotFound();
        }

        var student = await _context.Student
            .AsNoTracking()
            .Include(item => item.Department)
            .FirstOrDefaultAsync(item => item.StudentCode == id);
        return student is null ? NotFound() : PartialView("_Details", student);
    }

    public async Task<IActionResult> Create()
    {
        await PopulateDepartmentsAsync();
        return PartialView("_Create", new Student { AdmissionYear = DateTime.Now.Year });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind("FullName,Address,Email,AdmissionYear,TrainingSystemCode,MajorCode,DepartmentCode")]
        Student student)
    {
        ModelState.Remove(nameof(student.StudentCode));

        if (!await _context.Departments.AnyAsync(
                department => department.DepartmentCode == student.DepartmentCode))
        {
            ModelState.AddModelError(
                nameof(student.DepartmentCode),
                "Vui lòng chọn một khoa hợp lệ.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateDepartmentsAsync();
            Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            return PartialView("_Create", student);
        }

        var prefix = $"{student.AdmissionYear % 100:00}" +
                     $"{student.TrainingSystemCode}{student.MajorCode}";
        var existingCodes = await _context.Student
            .Where(item => item.StudentCode.StartsWith(prefix))
            .Select(item => item.StudentCode)
            .ToListAsync();
        var lastSequence = existingCodes
            .Select(code => int.TryParse(code.AsSpan(prefix.Length), out var sequence)
                ? sequence
                : 0)
            .DefaultIfEmpty(0)
            .Max();

        if (lastSequence >= 9999)
        {
            ModelState.AddModelError(
                string.Empty,
                $"Mã sinh viên với tiền tố {prefix} đã vượt quá 4 chữ số thứ tự.");
            await PopulateDepartmentsAsync();
            Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            return PartialView("_Create", student);
        }

        student.StudentCode = $"{prefix}{lastSequence + 1:0000}";
        _context.Student.Add(student);
        await _context.SaveChangesAsync();

        return Json(new
        {
            message = $"Đã thêm sinh viên với mã {student.StudentCode}."
        });
    }

    public async Task<IActionResult> Edit(string? id)
    {
        if (id is null)
        {
            return NotFound();
        }

        var student = await _context.Student.FindAsync(id);
        if (student is null)
        {
            return NotFound();
        }

        await PopulateDepartmentsAsync();
        return PartialView("_Edit", student);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        string id,
        [Bind("StudentCode,FullName,Address,Email,AdmissionYear,TrainingSystemCode,MajorCode,DepartmentCode")]
        Student input)
    {
        if (id != input.StudentCode)
        {
            return NotFound();
        }

        var student = await _context.Student.FindAsync(id);
        if (student is null)
        {
            return NotFound();
        }

        if (!await _context.Departments.AnyAsync(
                department => department.DepartmentCode == input.DepartmentCode))
        {
            ModelState.AddModelError(
                nameof(input.DepartmentCode),
                "Vui lòng chọn một khoa hợp lệ.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateDepartmentsAsync();
            Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            return PartialView("_Edit", input);
        }

        student.FullName = input.FullName;
        student.Address = input.Address;
        student.Email = input.Email;
        student.DepartmentCode = input.DepartmentCode;
        await _context.SaveChangesAsync();

        return Json(new { message = "Đã cập nhật thông tin sinh viên." });
    }

    public async Task<IActionResult> Delete(string? id)
    {
        if (id is null)
        {
            return NotFound();
        }

        var student = await _context.Student
            .AsNoTracking()
            .Include(item => item.Department)
            .FirstOrDefaultAsync(item => item.StudentCode == id);
        return student is null ? NotFound() : PartialView("_Delete", student);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(string id)
    {
        var student = await _context.Student.FindAsync(id);
        if (student is null)
        {
            return NotFound();
        }

        _context.Student.Remove(student);
        await _context.SaveChangesAsync();
        return Json(new { message = "Đã xóa sinh viên." });
    }

    private async Task PopulateDepartmentsAsync()
    {
        ViewData["DepartmentCode"] = new SelectList(
            await _context.Departments
                .AsNoTracking()
                .OrderBy(department => department.Name)
                .ToListAsync(),
            nameof(Department.DepartmentCode),
            nameof(Department.Name));
    }
}
