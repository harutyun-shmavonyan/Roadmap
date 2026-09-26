using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Roadmap.Api.Migrations
{
    /// <inheritdoc />
    public partial class Courses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "lesson_templates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Sections = table.Column<string>(type: "jsonb", nullable: false),
                    ExerciseKinds = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lesson_templates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "courses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Subtitle = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    DescriptionMd = table.Column<string>(type: "text", nullable: true),
                    CapstoneMd = table.Column<string>(type: "text", nullable: true),
                    TemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    TargetHoursPerWeek = table.Column<double>(type: "double precision", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Metadata = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_courses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_courses_lesson_templates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "lesson_templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "course_resources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    LessonId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Url = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    NoteMd = table.Column<string>(type: "text", nullable: true),
                    Position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_course_resources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_course_resources_courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "progress_events",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    StageId = table.Column<Guid>(type: "uuid", nullable: true),
                    LessonId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExerciseId = table.Column<Guid>(type: "uuid", nullable: true),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Payload = table.Column<string>(type: "jsonb", nullable: false),
                    Actor = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_progress_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_progress_events_courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "stages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    SummaryMd = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    TargetWeeks = table.Column<double>(type: "double precision", nullable: true),
                    Metadata = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_stages_courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "lessons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    SummaryMd = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    EstimatedHours = table.Column<double>(type: "double precision", nullable: true),
                    Metadata = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lessons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_lessons_stages_StageId",
                        column: x => x.StageId,
                        principalTable: "stages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "exercises",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LessonId = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionKind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    PromptMd = table.Column<string>(type: "text", nullable: false),
                    ReferenceMd = table.Column<string>(type: "text", nullable: true),
                    MaxScore = table.Column<double>(type: "double precision", nullable: false),
                    Weight = table.Column<double>(type: "double precision", nullable: false),
                    Required = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exercises", x => x.Id);
                    table.ForeignKey(
                        name: "FK_exercises_lessons_LessonId",
                        column: x => x.LessonId,
                        principalTable: "lessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "lesson_sections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LessonId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ContentMd = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lesson_sections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_lesson_sections_lessons_LessonId",
                        column: x => x.LessonId,
                        principalTable: "lessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "submissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExerciseId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptNo = table.Column<int>(type: "integer", nullable: false),
                    ContentMd = table.Column<string>(type: "text", nullable: true),
                    Links = table.Column<string>(type: "jsonb", nullable: false),
                    SubmittedBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_submissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_submissions_exercises_ExerciseId",
                        column: x => x.ExerciseId,
                        principalTable: "exercises",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "grades",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Score = table.Column<double>(type: "double precision", nullable: false),
                    MaxScore = table.Column<double>(type: "double precision", nullable: false),
                    FeedbackMd = table.Column<string>(type: "text", nullable: true),
                    Rubric = table.Column<string>(type: "jsonb", nullable: true),
                    GradedBy = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    GradedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SupersedesGradeId = table.Column<Guid>(type: "uuid", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_grades", x => x.Id);
                    table.ForeignKey(
                        name: "FK_grades_submissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "submissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_course_resources_CourseId_LessonId",
                table: "course_resources",
                columns: new[] { "CourseId", "LessonId" });

            migrationBuilder.CreateIndex(
                name: "IX_courses_Slug",
                table: "courses",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_courses_TemplateId",
                table: "courses",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_exercises_LessonId_Position",
                table: "exercises",
                columns: new[] { "LessonId", "Position" });

            migrationBuilder.CreateIndex(
                name: "IX_grades_IdempotencyKey",
                table: "grades",
                column: "IdempotencyKey");

            migrationBuilder.CreateIndex(
                name: "IX_grades_SubmissionId_GradedAt",
                table: "grades",
                columns: new[] { "SubmissionId", "GradedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_lesson_sections_LessonId_Kind",
                table: "lesson_sections",
                columns: new[] { "LessonId", "Kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_lesson_templates_Name",
                table: "lesson_templates",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_lessons_StageId_Position",
                table: "lessons",
                columns: new[] { "StageId", "Position" });

            migrationBuilder.CreateIndex(
                name: "IX_progress_events_CourseId_CreatedAt",
                table: "progress_events",
                columns: new[] { "CourseId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_stages_CourseId_Code",
                table: "stages",
                columns: new[] { "CourseId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_stages_CourseId_Position",
                table: "stages",
                columns: new[] { "CourseId", "Position" });

            migrationBuilder.CreateIndex(
                name: "IX_submissions_ExerciseId_AttemptNo",
                table: "submissions",
                columns: new[] { "ExerciseId", "AttemptNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_submissions_IdempotencyKey",
                table: "submissions",
                column: "IdempotencyKey");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "course_resources");

            migrationBuilder.DropTable(
                name: "grades");

            migrationBuilder.DropTable(
                name: "lesson_sections");

            migrationBuilder.DropTable(
                name: "progress_events");

            migrationBuilder.DropTable(
                name: "submissions");

            migrationBuilder.DropTable(
                name: "exercises");

            migrationBuilder.DropTable(
                name: "lessons");

            migrationBuilder.DropTable(
                name: "stages");

            migrationBuilder.DropTable(
                name: "courses");

            migrationBuilder.DropTable(
                name: "lesson_templates");
        }
    }
}
