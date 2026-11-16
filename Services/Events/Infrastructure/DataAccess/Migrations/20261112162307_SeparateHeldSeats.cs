using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Events.Infrastructure.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class SeparateHeldSeats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "held_seats",
                table: "events",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Before this migration a hold had already been deducted from available_seats.
            migrationBuilder.Sql("""
                UPDATE events AS e
                SET held_seats = holds.count, available_seats = e.available_seats + holds.count
                FROM (SELECT "EventId", SUM("SeatCount")::integer AS count
                      FROM "Reservations" WHERE "State" = 'Held' GROUP BY "EventId") AS holds
                WHERE holds."EventId" = e.id;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE events SET available_seats = available_seats - held_seats");
            migrationBuilder.DropColumn(
                name: "held_seats",
                table: "events");
        }
    }
}
