using System;
using WebApplication2.Models;
using WebApplication2.Services;
using Xunit;

namespace WebApplication2.Tests.Services
{
    public class TodoServiceTests
    {
        private readonly TodoService _todoService;

        public TodoServiceTests()
        {
            _todoService = new TodoService();
        }

        [Fact]
        public void ProcessPriorityMacro_WithMacro_SetsPriorityAndRemovesMacro()
        {
            var todo = new Todo { Title = "Task !1 with critical priority" };


            _todoService.ProcessTodoMacros(todo);


            Assert.Equal(TodoPriority.Critical, todo.Priority);
            Assert.Equal("Task with critical priority", todo.Title);
        }

        [Fact]
        public void ProcessDeadlineMacro_WithMacro_SetsDeadlineAndRemovesMacro()
        {
            var todo = new Todo { Title = "Complete task !before 15.06.2025" };
            var expectedDate = new DateTime(2025, 6, 15);


            _todoService.ProcessTodoMacros(todo);


            Assert.Equal(expectedDate.Date, todo.Deadline?.Date);
            Assert.Equal("Complete task", todo.Title);
        }

        [Fact]
        public void UpdateTodoStatus_WithOverdueDeadline_SetsOverdueStatus()
        {
            var todo = new Todo
            {
                Title = "Overdue task",
                Status = TodoStatus.Active,
                Deadline = DateTime.UtcNow.AddDays(-1)
            };


            _todoService.UpdateTodoStatus(todo);


            Assert.Equal(TodoStatus.Overdue, todo.Status);
        }

        [Fact]
        public void UpdateTodoStatus_WithFutureDeadline_SetsActiveStatus()
        {
            var todo = new Todo
            {
                Title = "Future task",
                Status = TodoStatus.Active,
                Deadline = DateTime.UtcNow.AddDays(5)
            };

            _todoService.UpdateTodoStatus(todo);

            Assert.Equal(TodoStatus.Active, todo.Status);
        }

        [Fact]
        public void MarkAsCompleted_WithOverdueTask_SetsLateStatus()
        {
            var todo = new Todo
            {
                Title = "Late task",
                Status = TodoStatus.Overdue,
                Deadline = DateTime.UtcNow.AddDays(-1)
            };

            _todoService.MarkAsCompleted(todo);

            Assert.Equal(TodoStatus.Late, todo.Status);
            Assert.NotNull(todo.ModifiedAt);
        }

        [Fact]
        public void MarkAsCompleted_WithActiveTask_SetsCompletedStatus()
        {
            var todo = new Todo
            {
                Title = "On-time task",
                Status = TodoStatus.Active,
                Deadline = DateTime.UtcNow.AddDays(1)
            };

            _todoService.MarkAsCompleted(todo);

            Assert.Equal(TodoStatus.Completed, todo.Status);
            Assert.NotNull(todo.ModifiedAt);
        }
    }
}