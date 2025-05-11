using System;
using System.ComponentModel.DataAnnotations;

namespace WebApplication2.Models
{
    public class Todo
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(200, MinimumLength = 4)]
        public string Title { get; set; } = null!;
        
        [StringLength(500)]
        public string? Description { get; set; }
        
        public DateTime? Deadline { get; set; }
        
        [Required]
        public TodoStatus Status { get; set; }
        
        [Required]
        public TodoPriority Priority { get; set; }
        
        public DateTime CreatedAt { get; set; }
        
        public DateTime? ModifiedAt { get; set; }
        
        public bool IsCompleted { get; set; }
    }
}