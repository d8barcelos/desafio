using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderService.Domain.Entities;

namespace OrderService.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id).HasColumnName("id");
        builder.Property(u => u.Username).HasColumnName("username").HasMaxLength(64).IsRequired();
        builder.Property(u => u.PasswordHash).HasColumnName("password_hash").HasMaxLength(512).IsRequired();
        builder.Property(u => u.Role).HasColumnName("role").HasMaxLength(32).IsRequired();
        builder.Property(u => u.CustomerId).HasColumnName("customer_id");

        builder.Ignore(u => u.IsAdmin);
        builder.Ignore(u => u.IsCustomer);

        builder.HasIndex(u => u.Username).IsUnique();
    }
}
