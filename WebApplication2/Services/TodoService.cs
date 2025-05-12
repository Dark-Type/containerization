using System;
using System.Globalization;
using System.Text.RegularExpressions;
using WebApplication2.Models;

namespace WebApplication2.Services
{
    public class TodoService : ITodoService
    {
        public Todo ProcessTodoMacros(Todo todo)
        {
            ProcessPriorityMacro(todo);
            ProcessDeadlineMacro(todo);
            return todo;
        }

        public void UpdateTodoStatus(Todo todo)
        {
            if (todo.Status == TodoStatus.Completed || todo.Status == TodoStatus.Late)
                return;

            if (todo.Deadline.HasValue && DateTime.UtcNow > todo.Deadline.Value)
            {
                todo.Status = TodoStatus.Overdue;
            }
            else
            {
                todo.Status = TodoStatus.Active;
            }
        }

        public void MarkAsCompleted(Todo todo)
        {
            bool isOverdue = todo.Deadline.HasValue &&
                             todo.Deadline.Value.Date < DateTime.UtcNow.Date;

            if (isOverdue)
            {
                todo.Status = TodoStatus.Late;
            }
            else
            {
                todo.Status = TodoStatus.Completed;
            }

            todo.ModifiedAt = DateTime.UtcNow;
        }
        public void UpdateTodoStatus(Todo todo, TodoStatus previousStatus = TodoStatus.Active, DateTime? previousDeadline = null)
        {
            if (previousStatus == TodoStatus.Late && todo.Status == TodoStatus.Late)
            {
                if (todo.Deadline.HasValue && todo.Deadline.Value.Date >= DateTime.UtcNow.Date)
                {
                    todo.Status = TodoStatus.Completed;
                    return;
                }
            }

            if (todo.Status == TodoStatus.Active || todo.Status == TodoStatus.Overdue)
            {
                if (todo.Deadline.HasValue && todo.Deadline.Value.Date < DateTime.UtcNow.Date)
                {
                    todo.Status = TodoStatus.Overdue;
                }
                else
                {
                    todo.Status = TodoStatus.Active;
                }
            }
            else if (todo.Status == TodoStatus.Completed && todo.Deadline.HasValue && 
                     todo.Deadline.Value.Date < DateTime.UtcNow.Date)
            {
                todo.Status = TodoStatus.Late;
            }
        }
        public void MarkAsIncomplete(Todo todo)
        {
            todo.Status = todo.Deadline.HasValue && DateTime.UtcNow > todo.Deadline.Value
                ? TodoStatus.Overdue
                : TodoStatus.Active;

            todo.ModifiedAt = DateTime.UtcNow;
        }

        private void ProcessPriorityMacro(Todo todo)
        {
            string title = todo.Title;
            var priorityMatch = Regex.Match(title, @"!([1-4])");

            if (priorityMatch.Success)
            {
                string priority = priorityMatch.Groups[1].Value;
                todo.Priority = priority switch
                {
                    "1" => TodoPriority.Critical,
                    "2" => TodoPriority.High,
                    "3" => TodoPriority.Medium,
                    "4" => TodoPriority.Low,
                    _ => todo.Priority
                };

                todo.Title = Regex.Replace(title, @"!\d\s*", "").Trim();
            }
        }

        private void ProcessDeadlineMacro(Todo todo)
        {
            string title = todo.Title;
            var deadlineMatch = Regex.Match(title, @"!before\s+(\d{2}[-\.]\d{2}[-\.]\d{4})");

            if (deadlineMatch.Success)
            {
                string dateStr = deadlineMatch.Groups[1].Value;
                dateStr = dateStr.Replace('-', '.');

                if (DateTime.TryParseExact(dateStr, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None,
                        out DateTime deadline))
                {
                    if (!todo.Deadline.HasValue)
                    {
                        todo.Deadline = deadline;
                    }

                    todo.Title = Regex.Replace(title, @"!before\s+\d{2}[-\.]\d{2}[-\.]\d{4}\s*", "").Trim();
                }
            }
        }
    }
}