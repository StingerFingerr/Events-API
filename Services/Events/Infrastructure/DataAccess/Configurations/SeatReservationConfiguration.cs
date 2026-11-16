using Events.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Events.Infrastructure.DataAccess.Configurations;

public class SeatReservationConfiguration : IEntityTypeConfiguration<SeatReservation>
{
    public void Configure(EntityTypeBuilder<SeatReservation> builder)
    {
        builder.ToTable("reservations");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.EventId).HasColumnName("event_id");
        builder.Property(r => r.SeatCount).HasColumnName("seat_count");
        builder.Property(r => r.State).HasColumnName("state").HasConversion<string>().IsConcurrencyToken();
    }
}
