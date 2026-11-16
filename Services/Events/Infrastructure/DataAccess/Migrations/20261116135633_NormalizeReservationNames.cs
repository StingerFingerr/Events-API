using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Events.Infrastructure.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeReservationNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_Reservations",
                table: "Reservations");

            migrationBuilder.RenameTable(
                name: "Reservations",
                newName: "reservations");

            migrationBuilder.RenameColumn(
                name: "State",
                table: "reservations",
                newName: "state");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "reservations",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "SeatCount",
                table: "reservations",
                newName: "seat_count");

            migrationBuilder.RenameColumn(
                name: "EventId",
                table: "reservations",
                newName: "event_id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_reservations",
                table: "reservations",
                column: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_reservations",
                table: "reservations");

            migrationBuilder.RenameTable(
                name: "reservations",
                newName: "Reservations");

            migrationBuilder.RenameColumn(
                name: "state",
                table: "Reservations",
                newName: "State");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "Reservations",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "seat_count",
                table: "Reservations",
                newName: "SeatCount");

            migrationBuilder.RenameColumn(
                name: "event_id",
                table: "Reservations",
                newName: "EventId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Reservations",
                table: "Reservations",
                column: "Id");
        }
    }
}
