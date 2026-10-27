using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace DemoMVC.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Departments",
                columns: table => new
                {
                    DepartmentCode = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    DepartmentCode1 = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Departments", x => x.DepartmentCode);
                    table.ForeignKey(
                        name: "FK_Departments_Departments_DepartmentCode1",
                        column: x => x.DepartmentCode1,
                        principalTable: "Departments",
                        principalColumn: "DepartmentCode");
                });

            migrationBuilder.CreateTable(
                name: "Student",
                columns: table => new
                {
                    StudentCode = table.Column<string>(type: "TEXT", nullable: false),
                    FullName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Address = table.Column<string>(type: "TEXT", nullable: true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 254, nullable: false),
                    AdmissionYear = table.Column<int>(type: "INTEGER", nullable: false),
                    TrainingSystemCode = table.Column<string>(type: "TEXT", nullable: false),
                    MajorCode = table.Column<string>(type: "TEXT", nullable: false),
                    DepartmentCode = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Student", x => x.StudentCode);
                    table.ForeignKey(
                        name: "FK_Student_Departments_DepartmentCode",
                        column: x => x.DepartmentCode,
                        principalTable: "Departments",
                        principalColumn: "DepartmentCode",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Departments",
                columns: new[] { "DepartmentCode", "DepartmentCode1", "Name" },
                values: new object[,]
                {
                    { "CD", null, "Khoa Cơ - Điện" },
                    { "CNTT", null, "Khoa Công nghệ thông tin" },
                    { "DC", null, "Khoa Địa chất" },
                    { "DK", null, "Khoa Dầu khí" },
                    { "GDQP", null, "Khoa Giáo dục quốc phòng" },
                    { "KHCB", null, "Khoa Khoa học cơ bản" },
                    { "KT", null, "Khoa Kinh tế và Quản trị kinh doanh" },
                    { "LLCT", null, "Khoa Lý luận chính trị" },
                    { "M", null, "Khoa Mỏ" },
                    { "MT", null, "Khoa Môi trường" },
                    { "TDB", null, "Khoa Trắc địa - Bản đồ và Quản lý đất đai" },
                    { "XD", null, "Khoa Xây dựng" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Departments_DepartmentCode1",
                table: "Departments",
                column: "DepartmentCode1");

            migrationBuilder.CreateIndex(
                name: "IX_Student_DepartmentCode",
                table: "Student",
                column: "DepartmentCode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Student");

            migrationBuilder.DropTable(
                name: "Departments");
        }
    }
}
