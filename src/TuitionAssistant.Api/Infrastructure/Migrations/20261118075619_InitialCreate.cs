using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TuitionAssistant.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "batch_imports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    uploaded_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    file_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    total_rows = table.Column<int>(type: "integer", nullable: false),
                    succeeded = table.Column<int>(type: "integer", nullable: false),
                    failed = table.Column<int>(type: "integer", nullable: false),
                    created = table.Column<int>(type: "integer", nullable: false),
                    updated = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_batch_imports", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "daily_query_counters",
                columns: table => new
                {
                    student_no = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    day_utc = table.Column<DateOnly>(type: "date", nullable: false),
                    count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_daily_query_counters", x => new { x.student_no, x.day_utc });
                });

            migrationBuilder.CreateTable(
                name: "students",
                columns: table => new
                {
                    student_no = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    full_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_students", x => x.student_no);
                });

            migrationBuilder.CreateTable(
                name: "tuition_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_no = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    term = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    amount_cents = table.Column<long>(type: "bigint", nullable: false),
                    paid_cents = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false, defaultValue: "USD"),
                    concurrency_stamp = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tuition_records", x => x.id);
                    table.CheckConstraint("ck_tuition_records_amount_non_negative", "amount_cents >= 0");
                    table.CheckConstraint("ck_tuition_records_paid_range", "paid_cents >= 0 AND paid_cents <= amount_cents");
                    table.ForeignKey(
                        name: "FK_tuition_records_students_student_no",
                        column: x => x.student_no,
                        principalTable: "students",
                        principalColumn: "student_no",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    username = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    student_no = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                    table.ForeignKey(
                        name: "FK_users_students_student_no",
                        column: x => x.student_no,
                        principalTable: "students",
                        principalColumn: "student_no",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "payments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    transaction_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    tuition_record_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_no = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    term = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    amount_cents = table.Column<long>(type: "bigint", nullable: false),
                    currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    channel = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    performed_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    recorded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payments", x => x.id);
                    table.CheckConstraint("ck_payments_amount_positive", "amount_cents > 0");
                    table.ForeignKey(
                        name: "FK_payments_tuition_records_tuition_record_id",
                        column: x => x.tuition_record_id,
                        principalTable: "tuition_records",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_payments_student_no",
                table: "payments",
                column: "student_no");

            migrationBuilder.CreateIndex(
                name: "IX_payments_transaction_id",
                table: "payments",
                column: "transaction_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payments_tuition_record_id",
                table: "payments",
                column: "tuition_record_id");

            migrationBuilder.CreateIndex(
                name: "IX_tuition_records_student_no_term",
                table: "tuition_records",
                columns: new[] { "student_no", "term" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tuition_records_term",
                table: "tuition_records",
                column: "term");

            migrationBuilder.CreateIndex(
                name: "IX_users_student_no",
                table: "users",
                column: "student_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_username",
                table: "users",
                column: "username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "batch_imports");

            migrationBuilder.DropTable(
                name: "daily_query_counters");

            migrationBuilder.DropTable(
                name: "payments");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "tuition_records");

            migrationBuilder.DropTable(
                name: "students");
        }
    }
}
