using Events_API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Events_API.DataAccess.Configurations;

public class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.ToTable("Events");
        
        builder.HasMany(e => e.Bookings).WithOne(b => b.Event).HasForeignKey(b => b.EventId);
        
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        
        builder.Property(e => e.Title).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(500);
        builder.Property(e => e.StartAt).IsRequired();
        builder.Property(e => e.EndAt).IsRequired();
        builder.Property(e => e.TotalSeats).IsRequired();
    }
}