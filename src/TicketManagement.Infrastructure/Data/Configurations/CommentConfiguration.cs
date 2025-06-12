using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketManagement.Core.Entities;

namespace TicketManagement.Infrastructure.Data.Configurations;

public class CommentConfiguration : IEntityTypeConfiguration<Comment>
{
    public void Configure(EntityTypeBuilder<Comment> builder)
    {
        builder.ToTable("Comments");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Content)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(c => c.AuthorId)
            .IsRequired()
            .HasMaxLength(100);

        // Indexes for performance
        builder.HasIndex(c => c.TicketId).HasDatabaseName("IX_Comments_TicketId");
        builder.HasIndex(c => c.CreatedAt).HasDatabaseName("IX_Comments_CreatedAt");
        builder.HasIndex(c => c.AuthorId).HasDatabaseName("IX_Comments_AuthorId");
        builder.HasIndex(c => new { c.TicketId, c.CreatedAt }).HasDatabaseName("IX_Comments_TicketId_CreatedAt");
        builder.HasIndex(c => new { c.AuthorId, c.CreatedAt }).HasDatabaseName("IX_Comments_AuthorId_CreatedAt");
    }
}