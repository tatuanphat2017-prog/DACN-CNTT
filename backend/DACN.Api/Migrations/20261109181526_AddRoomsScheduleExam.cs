using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DACN.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomsScheduleExam : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "ExamDate",
                table: "CourseSections",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExamRoomId",
                table: "CourseSections",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "ExamStartTime",
                table: "CourseSections",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PeriodCount",
                table: "CourseSections",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RoomId",
                table: "CourseSections",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StartPeriod",
                table: "CourseSections",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Weekday",
                table: "CourseSections",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Rooms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Building = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Floor = table.Column<int>(type: "int", nullable: false),
                    Capacity = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rooms", x => x.Id);
                    table.CheckConstraint("CK_Rooms_Capacity", "[Capacity] > 0");
                    table.CheckConstraint("CK_Rooms_Floor", "[Floor] >= 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_CourseSections_ExamRoomId",
                table: "CourseSections",
                column: "ExamRoomId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseSections_RoomId",
                table: "CourseSections",
                column: "RoomId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CourseSections_Schedule",
                table: "CourseSections",
                sql: "([Weekday] IS NULL AND [StartPeriod] IS NULL AND [PeriodCount] IS NULL AND [RoomId] IS NULL) OR ([Weekday] BETWEEN 2 AND 8 AND [StartPeriod] BETWEEN 1 AND 12 AND [PeriodCount] >= 1 AND [StartPeriod] + [PeriodCount] - 1 <= 12 AND [RoomId] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_Code",
                table: "Rooms",
                column: "Code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CourseSections_Rooms_ExamRoomId",
                table: "CourseSections",
                column: "ExamRoomId",
                principalTable: "Rooms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CourseSections_Rooms_RoomId",
                table: "CourseSections",
                column: "RoomId",
                principalTable: "Rooms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CourseSections_Rooms_ExamRoomId",
                table: "CourseSections");

            migrationBuilder.DropForeignKey(
                name: "FK_CourseSections_Rooms_RoomId",
                table: "CourseSections");

            migrationBuilder.DropTable(
                name: "Rooms");

            migrationBuilder.DropIndex(
                name: "IX_CourseSections_ExamRoomId",
                table: "CourseSections");

            migrationBuilder.DropIndex(
                name: "IX_CourseSections_RoomId",
                table: "CourseSections");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CourseSections_Schedule",
                table: "CourseSections");

            migrationBuilder.DropColumn(
                name: "ExamDate",
                table: "CourseSections");

            migrationBuilder.DropColumn(
                name: "ExamRoomId",
                table: "CourseSections");

            migrationBuilder.DropColumn(
                name: "ExamStartTime",
                table: "CourseSections");

            migrationBuilder.DropColumn(
                name: "PeriodCount",
                table: "CourseSections");

            migrationBuilder.DropColumn(
                name: "RoomId",
                table: "CourseSections");

            migrationBuilder.DropColumn(
                name: "StartPeriod",
                table: "CourseSections");

            migrationBuilder.DropColumn(
                name: "Weekday",
                table: "CourseSections");
        }
    }
}
