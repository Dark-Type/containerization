using Microsoft.EntityFrameworkCore;
using WebApplication2.Models;

namespace WebApplication2.Data
{
    public class TodoContext : DbContext
    {
        public TodoContext(DbContextOptions<TodoContext> options) : base(options)
        {
        }

        public DbSet<Todo> Todos { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);


            modelBuilder.Entity<Todo>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("Todos_pkey");

                entity.Property(e => e.Status)
                    .HasConversion<string>();

                entity.Property(e => e.Priority)
                    .HasConversion<string>();

                entity.Property(e => e.Title)
                    .IsRequired()
                    .HasMaxLength(200);

                entity.Property(e => e.Description)
                    .HasMaxLength(500)
                    .IsRequired(false);

                entity.Property(e => e.CreatedAt)
                    .HasDefaultValueSql("CURRENT_DATE")
                    .HasColumnType("date");

                entity.Property(e => e.Deadline)
                    .HasColumnType("date");

                entity.Property(e => e.ModifiedAt)
                    .HasColumnType("date");

                entity.Property(e => e.IsCompleted)
                    .HasDefaultValue(false);
            });
        }
    }
}