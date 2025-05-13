using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication2.Controllers;
using WebApplication2.Data;
using WebApplication2.Models;
using WebApplication2.Services;
using Xunit;

namespace WebApplication2.Tests.ApiTests
{
    public class TodoEquivalencePartitioningTests : IDisposable
    {
        private readonly TodoContext _context;
        private readonly TodoService _todoService;
        private readonly TodoController _controller;
        private readonly DateTime _currentDate = new DateTime(2025, 5, 13);

        public TodoEquivalencePartitioningTests()
        {
            var options = new DbContextOptionsBuilder<TodoContext>()
                .UseInMemoryDatabase(databaseName: $"TodoEPTests_{Guid.NewGuid()}")
                .Options;

            _context = new TodoContext(options);
            _todoService = new TodoService();
            _controller = new TodoController(_context, _todoService);
            SeedDatabase();
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        private void SeedDatabase()
        {
            _context.Todos.AddRange(
                new Todo
                {
                    Id = 1,
                    Title = "Active task with future deadline",
                    Description = "This task is active with future deadline",
                    Status = TodoStatus.Active,
                    Priority = TodoPriority.High,
                    CreatedAt = _currentDate.AddDays(-5),
                    Deadline = _currentDate.AddDays(5)
                },
                new Todo
                {
                    Id = 2,
                    Title = "Overdue task",
                    Description = "This task has a past deadline",
                    Status = TodoStatus.Overdue,
                    Priority = TodoPriority.Medium,
                    CreatedAt = _currentDate.AddDays(-10),
                    Deadline = _currentDate.AddDays(-2)
                },
                new Todo
                {
                    Id = 3,
                    Title = "Completed task with past deadline",
                    Description = "This task was completed after deadline",
                    Status = TodoStatus.Late,
                    Priority = TodoPriority.Low,
                    CreatedAt = _currentDate.AddDays(-15),
                    Deadline = _currentDate.AddDays(-5),
                    IsCompleted = true
                },
                new Todo
                {
                    Id = 4,
                    Title = "Completed task on time",
                    Description = "This task was completed before deadline",
                    Status = TodoStatus.Completed,
                    Priority = TodoPriority.Critical,
                    CreatedAt = _currentDate.AddDays(-8),
                    Deadline = _currentDate.AddDays(3),
                    IsCompleted = true
                },
                new Todo
                {
                    Id = 5,
                    Title = "Active task with no deadline",
                    Description = "This task has no deadline",
                    Status = TodoStatus.Active,
                    Priority = TodoPriority.Medium,
                    CreatedAt = _currentDate.AddDays(-3),
                    Deadline = null
                }
            );

            _context.SaveChanges();
        }

        #region Get Tests - Equivalence Partitioning

        [Theory]
        [InlineData("deadline", 5)]
        [InlineData("priority", 5)]
        [InlineData("created", 5)]
        [InlineData("title", 5)]
        [InlineData("status", 5)]
        public async Task GetTodosSorted_ValidCriteria_ReturnsSortedTodos(string criteria, int expectedCount)
        {
            var result = await _controller.GetTodosSorted(criteria);


            var actionResult = Assert.IsType<ActionResult<IEnumerable<Todo>>>(result);
            var todos = Assert.IsAssignableFrom<IEnumerable<Todo>>(actionResult.Value).ToList();
            Assert.Equal(expectedCount, todos.Count);

            switch (criteria)
            {
                case "priority":

                    Assert.Equal(TodoPriority.Critical, todos[0].Priority);
                    Assert.Equal(TodoPriority.Low, todos[todos.Count - 1].Priority);
                    break;

                case "deadline":
                    if (todos[0].Deadline.HasValue && todos[todos.Count - 1].Deadline.HasValue)
                    {
                        Assert.True(todos[0].Deadline <= todos[todos.Count - 1].Deadline);
                    }
                    else
                    {
                        Assert.Null(todos[todos.Count - 1].Deadline);
                    }

                    break;

                case "title":
                    Assert.True(string.Compare(todos[0].Title, todos[todos.Count - 1].Title) <= 0);
                    break;
            }
        }

        [Theory]
        [InlineData("invalid", 5)]
        public async Task GetTodosSorted_InvalidCriteria_ReturnsAllTodos(string criteria, int expectedCount)
        {
            var result = await _controller.GetTodosSorted(criteria);


            var actionResult = Assert.IsType<ActionResult<IEnumerable<Todo>>>(result);
            var todos = Assert.IsAssignableFrom<IEnumerable<Todo>>(actionResult.Value).ToList();
            Assert.Equal(expectedCount, todos.Count);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        public async Task GetTodo_ValidId_ReturnsTodo(int id)
        {
            var result = await _controller.GetTodo(id);


            var actionResult = Assert.IsType<ActionResult<Todo>>(result);
            var todo = Assert.IsType<Todo>(actionResult.Value);
            Assert.Equal(id, todo.Id);
        }

        [Theory]
        [InlineData(999)]
        public async Task GetTodo_NonExistentId_ReturnsNotFound(int id)
        {
            var result = await _controller.GetTodo(id);


            var actionResult = Assert.IsType<ActionResult<Todo>>(result);
            Assert.IsType<NotFoundResult>(actionResult.Result);
        }

        #endregion

        #region Post Tests - Equivalence Partitioning

        [Theory]
        [InlineData("Regular task", TodoPriority.Low, null)]
        [InlineData("Task with deadline", TodoPriority.Medium, 5)]
        [InlineData("Task with past deadline", TodoPriority.High, -5)]
        [InlineData("Critical task", TodoPriority.Critical, 0)]
        public async Task PostTodoItem_ValidEquivalenceClasses_CreatesSuccessfully(
            string title, TodoPriority priority, int? deadlineDaysOffset)
        {
            var todo = new Todo
            {
                Title = title,
                Description = $"Description for {title}",
                Priority = priority,
                Status = TodoStatus.Active,
                Deadline = deadlineDaysOffset.HasValue
                    ? _currentDate.AddDays(deadlineDaysOffset.Value)
                    : null
            };

            var result = await _controller.PostTodoItem(todo);

            var actionResult = Assert.IsType<ActionResult<Todo>>(result);
            var createdAtActionResult = Assert.IsType<CreatedAtActionResult>(actionResult.Result);
            var createdTodo = Assert.IsType<Todo>(createdAtActionResult.Value);

            Assert.Equal(title, createdTodo.Title);
            Assert.Equal(priority, createdTodo.Priority);

            if (deadlineDaysOffset.HasValue && deadlineDaysOffset.Value <= 0)
            {
                Assert.Equal(TodoStatus.Overdue, createdTodo.Status);
            }
            else
            {
                Assert.Equal(TodoStatus.Active, createdTodo.Status);
            }

            var savedTodo = await _context.Todos.FindAsync(createdTodo.Id);
            Assert.NotNull(savedTodo);
        }

        [Theory]
        [InlineData("!1 Critical via macro", "Critical via macro", TodoPriority.Critical)]
        [InlineData("!2 High via macro", "High via macro", TodoPriority.High)]
        [InlineData("!3 Medium via macro", "Medium via macro", TodoPriority.Medium)]
        [InlineData("!4 Low via macro", "Low via macro", TodoPriority.Low)]
        [InlineData("!5 Invalid priority macro", "!5 Invalid priority macro", TodoPriority.Medium)]
        [InlineData("!0 Invalid priority macro", "!0 Invalid priority macro", TodoPriority.Medium)]
        [InlineData("! Invalid priority macro", "! Invalid priority macro", TodoPriority.Medium)]
        [InlineData("!A Invalid priority macro", "!A Invalid priority macro", TodoPriority.Medium)]
        public async Task PostTodoItem_WithPriorityMacros_ProcessesMacrosCorrectly(
            string titleWithMacro, string expectedTitle, TodoPriority expectedPriority)
        {
            var todo = new Todo
            {
                Title = titleWithMacro,
                Description = "Testing priority macros",
                Status = TodoStatus.Active,
                Priority = TodoPriority.Medium
            };


            var result = await _controller.PostTodoItem(todo);


            var actionResult = Assert.IsType<ActionResult<Todo>>(result);
            var createdAtActionResult = Assert.IsType<CreatedAtActionResult>(actionResult.Result);
            var createdTodo = Assert.IsType<Todo>(createdAtActionResult.Value);

            Assert.Equal(expectedTitle, createdTodo.Title);
            Assert.Equal(expectedPriority, createdTodo.Priority);
        }

        [Theory]
        [InlineData("Task !before 20.05.2025", "Task", "20.05.2025", true)]
        [InlineData("!before 01.01.2026 Task", "Task", "01.01.2026", true)]
        [InlineData("Task with !before 30-11-2025 deadline", "Task with deadline", "30.11.2025", true)]
        [InlineData("Task !before 01.01.2000", "Task", "01.01.2000", true)]
        [InlineData("Task !before 31.12.9999", "Task", "31.12.9999", true)]
        [InlineData("Task !before 32.13.2025", "Task !before 32.13.2025", null, false)]
        [InlineData("Task !before 00.00.2025", "Task !before 00.00.2025", null, false)]
        [InlineData("Task !before abc", "Task !before abc", null, false)]
        [InlineData("Task !before", "Task !before", null, false)]
        [InlineData("Task !before  ", "Task !before  ", null, false)]
        public async Task PostTodoItem_WithDeadlineMacros_ProcessesMacrosCorrectly(
            string titleWithMacro, string expectedTitle, string expectedDateStr, bool shouldHaveDeadline)
        {
            var todo = new Todo
            {
                Title = titleWithMacro,
                Description = "Testing deadline macros",
                Status = TodoStatus.Active,
                Priority = TodoPriority.Medium
            };

            DateTime? expectedDate = null;
            if (shouldHaveDeadline && expectedDateStr != null)
            {
                expectedDate = DateTime.ParseExact(
                    expectedDateStr, "dd.MM.yyyy", System.Globalization.CultureInfo.InvariantCulture);
            }


            var result = await _controller.PostTodoItem(todo);


            var actionResult = Assert.IsType<ActionResult<Todo>>(result);
            var createdAtActionResult = Assert.IsType<CreatedAtActionResult>(actionResult.Result);
            var createdTodo = Assert.IsType<Todo>(createdAtActionResult.Value);

            Assert.Equal(expectedTitle, createdTodo.Title);

            if (shouldHaveDeadline)
            {
                Assert.NotNull(createdTodo.Deadline);
                Assert.Equal(expectedDate?.Date, createdTodo.Deadline?.Date);
            }
        }

        [Fact]
        public async Task PostTodoItem_WithMixedValidAndInvalidMacros_ProcessesOnlyValidMacros()
        {
            var todo = new Todo
            {
                Title = "!1 Critical task !before 32.13.2025 with invalid date",
                Description = "Testing mixed valid and invalid macros",
                Status = TodoStatus.Active,
                Priority = TodoPriority.Low
            };


            var result = await _controller.PostTodoItem(todo);


            var actionResult = Assert.IsType<ActionResult<Todo>>(result);
            var createdAtActionResult = Assert.IsType<CreatedAtActionResult>(actionResult.Result);
            var createdTodo = Assert.IsType<Todo>(createdAtActionResult.Value);
            Assert.Equal(TodoPriority.Critical, createdTodo.Priority);

            Assert.Equal("Critical task !before 32.13.2025 with invalid date", createdTodo.Title);

            Assert.Null(createdTodo.Deadline);
        }

        #endregion

        #region Status Change Tests - Equivalence Partitioning

        [Theory]
        [InlineData(1, TodoStatus.Completed)]
        [InlineData(2, TodoStatus.Late)]
        public async Task MarkAsCompleted_DifferentInitialStatuses_CompletesCorrectly(int id, TodoStatus expectedStatus)
        {
            var result = await _controller.MarkAsCompleted(id);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var completedTodo = Assert.IsType<Todo>(okResult.Value);

            Assert.Equal(expectedStatus, completedTodo.Status);
            Assert.NotNull(completedTodo.ModifiedAt);

            var todoInDb = await _context.Todos.FindAsync(id);
            Assert.Equal(expectedStatus, todoInDb.Status);
        }

        [Theory]
        [InlineData(3)]
        [InlineData(4)]
        public async Task MarkAsCompleted_AlreadyCompletedTodos_ReturnsBadRequest(int id)
        {
            var result = await _controller.MarkAsCompleted(id);


            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Contains("Cannot mark as completed", badRequestResult.Value.ToString());
        }

        [Theory]
        [InlineData(4, TodoStatus.Active)]
        [InlineData(3, TodoStatus.Overdue)]
        public async Task MarkAsIncomplete_DifferentInitialStatuses_UpdatesStatusCorrectly(int id,
            TodoStatus expectedStatus)
        {
            var result = await _controller.MarkAsIncomplete(id);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var incompleteTodo = Assert.IsType<Todo>(okResult.Value);

            Assert.Equal(expectedStatus, incompleteTodo.Status);
            Assert.NotNull(incompleteTodo.ModifiedAt);

            var todoInDb = await _context.Todos.FindAsync(id);
            Assert.Equal(expectedStatus, todoInDb.Status);
        }

        #endregion
    }
}