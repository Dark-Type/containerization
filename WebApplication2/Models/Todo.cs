using System;
using System.ComponentModel.DataAnnotations;

namespace WebApplication2.Models
{
    public class Todo
    {
        public int Id { get; set; }

        [Required]
        [MinLength(4, ErrorMessage = "Title must be at least 4 characters long")]
        [MaxLength(200, ErrorMessage = "Title cannot exceed 200 characters")]
        public string Title { get; set; } = null!;

        [StringLength(500)] public string? Description { get; set; }

        public DateTime? Deadline { get; set; }

        [Required] public TodoStatus Status { get; set; }

        [Required] public TodoPriority Priority { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? ModifiedAt { get; set; }

        public bool IsCompleted { get; set; }
    }
}