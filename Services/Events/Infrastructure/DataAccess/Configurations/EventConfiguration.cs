using Events.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Events.Infrastructure.DataAccess.Configurations;

public class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.ToTable("events");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Id).HasColumnName("id");

        builder.Property(e => e.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
        builder.Property(e => e.Description).HasColumnName("description").HasMaxLength(500);
        builder.Property(e => e.StartAt).HasColumnName("start_at").IsRequired();
        builder.Property(e => e.EndAt).HasColumnName("end_at").IsRequired();
        builder.Property(e => e.TotalSeats).HasColumnName("total_seats").IsRequired();
        builder.Property(e => e.HeldSeats).HasColumnName("held_seats").IsConcurrencyToken();
        builder.Property(e => e.AvailableSeats).HasColumnName("available_seats").IsConcurrencyToken().IsRequired();
    }
}
