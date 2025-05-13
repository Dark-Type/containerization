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
    public class TodoBoundaryValueTests : IDisposable
    {
        private readonly TodoContext _context;
        private readonly TodoService _todoService;
        private readonly TodoController _controller;
        private readonly DateTime _currentDate = new DateTime(2025, 5, 13);

        public TodoBoundaryValueTests()
        {
            var options = new DbContextOptionsBuilder<TodoContext>()
                .UseInMemoryDatabase(databaseName: $"TodoBVATests_{Guid.NewGuid()}")
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
                    Title = "Regular task",
                    Description = "Task with normal values",
                    Status = TodoStatus.Active,
                    Priority = TodoPriority.Medium,
                    CreatedAt = _currentDate.AddDays(-5),
                    Deadline = _currentDate.AddDays(5)
                }
            );

            _context.SaveChanges();
        }

        #region ID Boundary Tests

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(int.MinValue)]
        public async Task GetTodo_InvalidIdBoundaries_ReturnsNotFound(int id)
        {
            var result = await _controller.GetTodo(id);


            var actionResult = Assert.IsType<ActionResult<Todo>>(result);
            Assert.IsType<NotFoundResult>(actionResult.Result);
        }

        [Theory]
        [InlineData(int.MaxValue)]
        public async Task GetTodo_MaximumIdBoundary_ReturnsNotFound(int id)
        {
            var result = await _controller.GetTodo(id);

            var actionResult = Assert.IsType<ActionResult<Todo>>(result);
            Assert.IsType<NotFoundResult>(actionResult.Result);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(int.MinValue)]
        public async Task DeleteTodoItem_InvalidIdBoundaries_ReturnsNotFound(int id)
        {
            var result = await _controller.DeleteTodoItem(id);

            Assert.IsType<NotFoundResult>(result);
        }

        #endregion

        #region Title Length Boundary Tests

        [Theory]
        [InlineData("", false, "Empty title")]
        [InlineData("abc", false, "Title too short (3 chars)")]
        [InlineData("abcd", true, "Title at minimum length (4 chars)")]
        [InlineData("Regular Title", true, "Normal title")]
        [InlineData("MaximumLength", true, "Title at maximum length (200 chars)", 200)]
        [InlineData("TooLong", false, "Title exceeding maximum length (201 chars)", 201)]
        public async Task PostTodoItem_TitleLengthBoundaries_ValidatesCorrectly(
            string titleBase, bool shouldSucceed, string testCase, int? titleLength = null)
        {
            string title = titleBase;
            if (titleLength.HasValue)
            {
                title = new string('A', titleLength.Value);
            }

            var todo = new Todo
            {
                Title = title,
                Description = $"Testing title boundary: {testCase}",
                Priority = TodoPriority.Medium,
                Status = TodoStatus.Active
            };


            var result = await _controller.PostTodoItem(todo);


            var actionResult = Assert.IsType<ActionResult<Todo>>(result);

            if (shouldSucceed)
            {
                Assert.IsType<CreatedAtActionResult>(actionResult.Result);
                var createdAtResult = (CreatedAtActionResult)actionResult.Result;
                var createdTodo = Assert.IsType<Todo>(createdAtResult.Value);
                Assert.Equal(title, createdTodo.Title);
            }
            else
            {
                Assert.IsType<BadRequestObjectResult>(actionResult.Result);
            }
        }

        [Theory]
        [InlineData("", false, "Empty title")]
        [InlineData("abc", false, "Title too short (3 chars)")]
        [InlineData("abcd", true, "Title at minimum length (4 chars)")]
        [InlineData("Regular Update", true, "Normal title")]
        [InlineData("MaximumLength", true, "Title at maximum length (200 chars)", 200)]
        [InlineData("TooLong", false, "Title exceeding maximum length (201 chars)", 201)]
        public async Task PutTodoItem_TitleLengthBoundaries_ValidatesCorrectly(
            string titleBase, bool shouldSucceed, string testCase, int? titleLength = null)
        {
            var todo = await _context.Todos.FindAsync(1);
            Assert.NotNull(todo);

            string title = titleBase;
            if (titleLength.HasValue)
            {
                title = new string('A', titleLength.Value);
            }

            todo.Title = title;

            var result = await _controller.PutTodoItem(1, todo);

            if (shouldSucceed)
            {
                Assert.IsType<NoContentResult>(result);

                var updatedTodo = await _context.Todos.FindAsync(1);
                Assert.Equal(title, updatedTodo.Title);
            }
            else
            {
                Assert.IsType<BadRequestObjectResult>(result);
            }
        }

        #endregion

        #region Description Length Boundary Tests

        [Fact]
        public async Task PostTodoItem_DescriptionAtMaximumBoundary_Succeeds()
        {
            var descriptionMaxLength = new string('A', 500);
            var todo = new Todo
            {
                Title = "Description Max Test",
                Description = descriptionMaxLength,
                Priority = TodoPriority.Medium,
                Status = TodoStatus.Active
            };

            var result = await _controller.PostTodoItem(todo);

            var actionResult = Assert.IsType<ActionResult<Todo>>(result);
            Assert.IsType<CreatedAtActionResult>(actionResult.Result);
        }

        #endregion

        #region Deadline Boundary Tests

        [Theory]
        [InlineData(-1, TodoStatus.Overdue, "Yesterday")]
        [InlineData(0, TodoStatus.Overdue, "Today")]
        [InlineData(1, TodoStatus.Active, "Tomorrow")]
        [InlineData("1900-01-01", TodoStatus.Overdue, "Extreme past")]
        [InlineData("9999-12-31", TodoStatus.Active, "Extreme future")]
        public async Task PostTodoItem_DeadlineBoundaries_SetsAppropriateStatus(object deadlineInput,
            TodoStatus expectedStatus, string testCase)
        {
            DateTime deadline;
            if (deadlineInput is int daysFromNow)
            {
                deadline = _currentDate.AddDays(daysFromNow).Date;
            }
            else if (deadlineInput is string dateString)
            {
                deadline = DateTime.Parse(dateString);
            }
            else
            {
                throw new ArgumentException("Invalid deadline input type");
            }

            var todo = new Todo
            {
                Title = $"Deadline boundary test: {testCase}",
                Description = "Testing deadline boundaries",
                Priority = TodoPriority.Medium,
                Status = TodoStatus.Active,
                Deadline = deadline
            };

            var result = await _controller.PostTodoItem(todo);
            var actionResult = Assert.IsType<ActionResult<Todo>>(result);
            var createdAtActionResult = Assert.IsType<CreatedAtActionResult>(actionResult.Result);
            var createdTodo = Assert.IsType<Todo>(createdAtActionResult.Value);
            Assert.Equal(expectedStatus, createdTodo.Status);
            Assert.Equal(deadline.Date, createdTodo.Deadline?.Date);
        }

        #endregion

        #region ID Mismatch Boundary Tests for PUT

        [Fact]
        public async Task PutTodoItem_IdMismatch_ReturnsBadRequest()
        {
            var todo = new Todo
            {
                Id = 2,
                Title = "Updated title",
                Description = "Testing ID mismatch",
                Priority = TodoPriority.Medium,
                Status = TodoStatus.Active
            };

            var result = await _controller.PutTodoItem(1, todo);

            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("ID mismatch", badRequestResult.Value);
        }

        #endregion

        #region Status Change Boundary Tests

        [Theory]
        [InlineData(TodoStatus.Completed)]
        [InlineData(TodoStatus.Late)]
        public async Task PostTodoItem_WithCompletedOrLateStatus_PreservesStatus(TodoStatus status)
        {
            var todo = new Todo
            {
                Title = $"Test with initial {status} status",
                Description = "Testing initial status preservation",
                Priority = TodoPriority.Medium,
                Status = status
            };
            var result = await _controller.PostTodoItem(todo);
            var actionResult = Assert.IsType<ActionResult<Todo>>(result);
            var createdAtActionResult = Assert.IsType<CreatedAtActionResult>(actionResult.Result);
            var createdTodo = Assert.IsType<Todo>(createdAtActionResult.Value);
            Assert.Equal(status, createdTodo.Status);
        }

        #endregion
    }
}