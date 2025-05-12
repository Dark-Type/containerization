using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication2.Controllers;
using WebApplication2.Data;
using WebApplication2.Models;
using WebApplication2.Services;
using Xunit;

namespace WebApplication2.Tests.IntegrationTests
{
    public class TodoIntegrationEdgeCaseTests : IDisposable
    {
        private readonly TodoContext _context;
        private readonly TodoService _todoService;
        private readonly TodoController _controller;

        private readonly DateTime _currentDate = new DateTime(2025, 5, 11, 10, 47, 40);

        public TodoIntegrationEdgeCaseTests()
        {
            var options = new DbContextOptionsBuilder<TodoContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new TodoContext(options);
            _todoService = new TodoService();
            _controller = new TodoController(_context, _todoService);
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        [Fact]
        public async Task UpdateTodo_DeadlineChanged_UpdatesStatusCorrectly()
        {
            var todo = new Todo
            {
                Title = "Deadline Change Test",
                Status = TodoStatus.Active,
                Priority = TodoPriority.Medium,
                Deadline = _currentDate.AddDays(5),
                CreatedAt = _currentDate.AddDays(-1)
            };


            _context.Todos.Add(todo);
            await _context.SaveChangesAsync();
            int todoId = todo.Id;


            todo.Deadline = _currentDate.AddDays(-1);


            await _controller.PutTodoItem(todoId, todo);

            var updatedTodo = await _context.Todos.FindAsync(todoId);
            Assert.Equal(TodoStatus.Overdue, updatedTodo.Status);
        }

        [Fact]
        public async Task CreateTodo_WithComplexMacrosCombination_ProcessesCorrectly()
        {
            var todo = new Todo
            {
                Title = "!2 Task with !1 multiple priority !before 20.05.2025 macros and !before 15.05.2025 deadlines"
            };


            var result = await _controller.PostTodoItem(todo);


            var todos = await _context.Todos.ToListAsync();
            Assert.Single(todos);

            var savedTodo = todos[0];
            Assert.Equal("Task with multiple priority macros and deadlines", savedTodo.Title);

            Assert.Equal(TodoPriority.High, savedTodo.Priority);
            Assert.Equal(new DateTime(2025, 5, 20), savedTodo.Deadline?.Date);
        }

        [Theory]
        [InlineData("abc", false)]
        [InlineData("abcd", true)]
        [InlineData("abcde", true)]
        public async Task PutTodoItem_TitleLengthBoundary_ValidatesCorrectly(string newTitle, bool shouldBeValid)
        {
            var options = new DbContextOptionsBuilder<TodoContext>()
                .UseInMemoryDatabase($"TitleValidationTest_{Guid.NewGuid()}")
                .Options;

            int todoId;

            await using (var setupContext = new TodoContext(options))
            {
                var originalTodo = new Todo
                {
                    Title = "Original Valid Title",
                    Status = TodoStatus.Active,
                    Priority = TodoPriority.Medium,
                    CreatedAt = DateTime.UtcNow
                };

                setupContext.Todos.Add(originalTodo);
                await setupContext.SaveChangesAsync();
                todoId = originalTodo.Id;
            }

            IActionResult result;
            await using (var updateContext = new TodoContext(options))
            {
                var updateRequest = new Todo
                {
                    Id = todoId,
                    Title = newTitle,
                    Status = TodoStatus.Active,
                    Priority = TodoPriority.Medium,
                    CreatedAt = DateTime.UtcNow
                };

                var todoService = new TodoService();
                var controller = new TodoController(updateContext, todoService);

                result = await controller.PutTodoItem(todoId, updateRequest);
            }

            await using (var verifyContext = new TodoContext(options))
            {
                if (shouldBeValid)
                {
                    Assert.IsType<NoContentResult>(result);
                    var updatedTodo = await verifyContext.Todos.FindAsync(todoId);
                    Assert.Equal(newTitle, updatedTodo.Title);
                }
                else
                {
                    Assert.IsType<BadRequestObjectResult>(result);
                    var unchangedTodo = await verifyContext.Todos.FindAsync(todoId);
                    Assert.Equal("Original Valid Title", unchangedTodo.Title);
                }
            }
        }

        // [Fact]
        // public async Task CompleteTodoExactlyOnDeadlineDay()
        // {
        //     DateTime currentDate = new DateTime(2025, 5, 11, 0, 0, 0);
        //
        //     var todo = new Todo
        //     {
        //         Title = "Due Today Task",
        //         Status = TodoStatus.Active,
        //         Priority = TodoPriority.High,
        //         Deadline = currentDate.Date,
        //         CreatedAt = currentDate.AddDays(-5)
        //     };
        //
        //     _context.Todos.Add(todo);
        //     await _context.SaveChangesAsync();
        //
        //     var realTodoService = new TodoService();
        //     var controller = new TodoController(_context, realTodoService);
        //
        //     await controller.MarkAsCompleted(todo.Id);
        //
        //
        //     var completedTodo = await _context.Todos.FindAsync(todo.Id);
        //     Assert.Equal(TodoStatus.Completed, completedTodo.Status);
        // }
    }
}