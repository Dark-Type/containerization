using System.ComponentModel.DataAnnotations;

namespace WebApplication2.Models;

public class Todo
{
    public int Id { get; init; }

    [Required] [MaxLength(500)] public string Description { get; set; } = string.Empty;

    public bool IsCompleted { get; set; }
}