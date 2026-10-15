using Events_API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Events_API.DataAccess.Configurations;

public class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.ToTable("events");
        
        builder.HasMany(e => e.Bookings).WithOne(b => b.Event).HasForeignKey(b => b.EventId);
        
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Id).HasColumnName("id");
        
        builder.Property(e => e.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
        builder.Property(e => e.Description).HasColumnName("description").HasMaxLength(500);
        builder.Property(e => e.StartAt).HasColumnName("start_at").IsRequired();
        builder.Property(e => e.EndAt).HasColumnName("end_at").IsRequired();
        builder.Property(e => e.TotalSeats).HasColumnName("total_seats").IsRequired();
        builder.Property(e => e.AvailableSeats).HasColumnName("available_seats").IsRequired();
    }
}
