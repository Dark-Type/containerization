using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WebApplication2.Controllers;
using WebApplication2.Data;
using WebApplication2.Models;
using WebApplication2.Services;
using Xunit;

namespace WebApplication2.Tests.IntegrationTests
{
    public class TodoControllerIntegrationTests : IDisposable
    {
        private readonly TodoContext _context;
        private readonly TodoService _todoService;
        private readonly TodoController _controller;

        public TodoControllerIntegrationTests()
        {
            var options = new DbContextOptionsBuilder<TodoContext>()
                .UseInMemoryDatabase(databaseName: $"TodoTestDb_{Guid.NewGuid()}")
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
            DateTime currentDate = new DateTime(2025, 5, 11, 8, 41, 58);

            _context.Todos.AddRange(
                new Todo
                {
                    Id = 1,
                    Title = "Complete integration tests",
                    Description = "Create comprehensive tests for the API",
                    Status = TodoStatus.Active,
                    Priority = TodoPriority.High,
                    CreatedAt = currentDate.AddDays(-1),
                    Deadline = currentDate.AddDays(2)
                },
                new Todo
                {
                    Id = 2,
                    Title = "Past deadline task",
                    Description = "This task is overdue",
                    Status = TodoStatus.Overdue,
                    Priority = TodoPriority.Medium,
                    CreatedAt = currentDate.AddDays(-5),
                    Deadline = currentDate.AddDays(-1)
                },
                new Todo
                {
                    Id = 3,
                    Title = "Completed task",
                    Description = "This task is already done",
                    Status = TodoStatus.Completed,
                    Priority = TodoPriority.Low,
                    CreatedAt = currentDate.AddDays(-3),
                    Deadline = currentDate.AddDays(-2),
                    IsCompleted = true
                }
            );

            _context.SaveChanges();
        }

        [Fact]
        public async Task GetTodos_ReturnsAllTodos()
        {
            var result = await _controller.GetTodos();


            var actionResult = Assert.IsType<ActionResult<IEnumerable<Todo>>>(result);
            var todos = Assert.IsAssignableFrom<IEnumerable<Todo>>(actionResult.Value);
            Assert.Equal(3, todos.Count());
        }

        [Fact]
        public async Task GetTodosSorted_ByPriority_ReturnsSortedByPriority()
        {
            var result = await _controller.GetTodosSorted("priority");


            var actionResult = Assert.IsType<ActionResult<IEnumerable<Todo>>>(result);
            var todos = Assert.IsAssignableFrom<IEnumerable<Todo>>(actionResult.Value).ToList();

            Assert.Equal(3, todos.Count);
            Assert.Equal(TodoPriority.High, todos[0].Priority);
            Assert.Equal(TodoPriority.Medium, todos[1].Priority);
            Assert.Equal(TodoPriority.Low, todos[2].Priority);
        }

        [Fact]
        public async Task CreateTodo_WithMacros_ProcessesMacrosCorrectly()
        {
            var newTodo = new Todo
            {
                Title = "!1 Critical task !before 15.06.2025",
                Description = "Testing macros",
                Status = TodoStatus.Active
            };


            var result = await _controller.PostTodoItem(newTodo);


            var actionResult = Assert.IsType<ActionResult<Todo>>(result);
            var createdAtResult = Assert.IsType<CreatedAtActionResult>(actionResult.Result);
            var createdTodo = Assert.IsType<Todo>(createdAtResult.Value);


            Assert.Equal("Critical task", createdTodo.Title);
            Assert.Equal(TodoPriority.Critical, createdTodo.Priority);


            Assert.NotNull(createdTodo.Deadline);
            Assert.Equal(2025, createdTodo.Deadline.Value.Year);
            Assert.Equal(6, createdTodo.Deadline.Value.Month);
            Assert.Equal(15, createdTodo.Deadline.Value.Day);


            var todos = await _context.Todos.ToListAsync();
            Assert.Equal(4, todos.Count);
        }

        [Fact]
        public async Task MarkAsCompleted_UpdatesStatus()
        {
            var result = await _controller.MarkAsCompleted(1);


            Assert.IsType<NoContentResult>(result);

            var todo = await _context.Todos.FindAsync(1);
            Assert.Equal(TodoStatus.Completed, todo.Status);
        }

        [Fact]
        public async Task DeleteTodo_WithValidId_RemovesFromDatabase()
        {
            var result = await _controller.DeleteTodoItem(2);


            Assert.IsType<NoContentResult>(result);

            var todo = await _context.Todos.FindAsync(2);
            Assert.Null(todo);

            var todos = await _context.Todos.ToListAsync();
            Assert.Equal(2, todos.Count);
        }

        [Fact]
        public async Task UpdateTodo_WithValidId_UpdatesInDatabase()
        {
            var todo = await _context.Todos.FindAsync(1);
            todo.Title = "Updated title";
            todo.Description = "Updated description";


            var result = await _controller.PutTodoItem(1, todo);


            Assert.IsType<NoContentResult>(result);

            var updatedTodo = await _context.Todos.FindAsync(1);
            Assert.Equal("Updated title", updatedTodo.Title);
            Assert.Equal("Updated description", updatedTodo.Description);
        }
    }
}