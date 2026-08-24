using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HostelSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Rooms: composite for availability query ──
            // WHERE HostelId = ? AND IsAvailable = 1 AND CurrentOccupancy < Capacity
            migrationBuilder.CreateIndex(
                name: "IX_Rooms_HostelId_IsAvailable_CurrentOccupancy",
                table: "Rooms",
                columns: new[] { "HostelId", "IsAvailable", "CurrentOccupancy" });

            // ── RoomApplications ──
            migrationBuilder.CreateIndex(
                name: "IX_RoomApplications_Status",
                table: "RoomApplications",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_RoomApplications_StudentId_Status",
                table: "RoomApplications",
                columns: new[] { "StudentId", "Status" });

            // ── RoomAllocations ──
            migrationBuilder.CreateIndex(
                name: "IX_RoomAllocations_StudentId_IsActive",
                table: "RoomAllocations",
                columns: new[] { "StudentId", "IsActive" });

            // ── Payments ──
            // IX_Payments_StudentId already exists from InitialCreate; keep idempotent check
            // Only add Status index (StudentId unique already covered by FK index)
            migrationBuilder.CreateIndex(
                name: "IX_Payments_Status",
                table: "Payments",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Rooms_HostelId_IsAvailable_CurrentOccupancy",
                table: "Rooms");

            migrationBuilder.DropIndex(
                name: "IX_RoomApplications_Status",
                table: "RoomApplications");

            migrationBuilder.DropIndex(
                name: "IX_RoomApplications_StudentId_Status",
                table: "RoomApplications");

            migrationBuilder.DropIndex(
                name: "IX_RoomAllocations_StudentId_IsActive",
                table: "RoomAllocations");

            migrationBuilder.DropIndex(
                name: "IX_Payments_Status",
                table: "Payments");
        }
    }
}
