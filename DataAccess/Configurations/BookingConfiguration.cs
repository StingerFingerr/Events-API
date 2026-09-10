using Events_API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Events_API.DataAccess.Configurations;

public class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.ToTable("Bookings");
        
        builder.HasOne(b => b.Event).WithMany(b => b.Bookings).HasForeignKey(b => b.EventId);
        
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedNever();
        
        builder.Property(b => b.CreatedAt).IsRequired();
        builder.Property(b => b.Status).HasConversion<string>(); 
    }
}