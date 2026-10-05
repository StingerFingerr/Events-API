using Events_API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Events_API.DataAccess.Configurations;

public class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.ToTable("bookings");
        
        builder.HasOne(b => b.Event).WithMany(b => b.Bookings).HasForeignKey(b => b.EventId);
        
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedNever();
        builder.Property(b => b.Id).HasColumnName("id");
        builder.Property(b => b.EventId).HasColumnName("event_id");
        
        builder.Property(b => b.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(b => b.ProcessedAt).HasColumnName("processed_at");
        builder.Property(b => b.Status).HasColumnName("status").HasConversion<string>();
    }
}
