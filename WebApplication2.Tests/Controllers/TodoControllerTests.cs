using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using WebApplication2.Controllers;
using WebApplication2.Data;
using WebApplication2.Models;
using WebApplication2.Services;


namespace WebApplication2.Tests.Controllers
{
    public class TodoControllerTests : IDisposable
    {
        private readonly Mock<ITodoService> _mockTodoService;
        private readonly TodoContext _context;
        private readonly TodoController _controller;

        public TodoControllerTests()
        {
            var options = new DbContextOptionsBuilder<TodoContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new TodoContext(options);
            _mockTodoService = new Mock<ITodoService>();
            _controller = new TodoController(_context, _mockTodoService.Object);


            _context.Todos.Add(new Todo
            {
                Id = 1,
                Title = "Test Task 1",
                Status = TodoStatus.Active,
                Priority = TodoPriority.High,
                CreatedAt = DateTime.UtcNow.AddDays(-2),
                Deadline = DateTime.UtcNow.AddDays(2)
            });

            _context.Todos.Add(new Todo
            {
                Id = 2,
                Title = "Test Task 2",
                Status = TodoStatus.Completed,
                Priority = TodoPriority.Low,
                CreatedAt = DateTime.UtcNow.AddDays(-5),
                Deadline = null
            });

            _context.SaveChanges();
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        [Fact]
        public async Task GetTodos_ReturnsAllTodos()
        {
            var result = await _controller.GetTodos();

            var actionResult = Assert.IsType<ActionResult<IEnumerable<Todo>>>(result);
            var todos = Assert.IsAssignableFrom<IEnumerable<Todo>>(actionResult.Value);
            Assert.Equal(2, todos.Count());
        }

        [Fact]
        public async Task GetTodosSorted_ByPriority_ReturnsSortedByPriorityDesc()
        {
            var result = await _controller.GetTodosSorted("priority");

            var actionResult = Assert.IsType<ActionResult<IEnumerable<Todo>>>(result);
            var todos = Assert.IsAssignableFrom<IEnumerable<Todo>>(actionResult.Value).ToList();
            Assert.Equal(2, todos.Count);
            Assert.Equal(TodoPriority.High, todos[0].Priority);
            Assert.Equal(TodoPriority.Low, todos[1].Priority);
        }

        [Fact]
        public async Task PostTodoItem_ValidTodo_ReturnsCreatedAtAction()
        {
            var newTodo = new Todo
            {
                Title = "New Test Task",
                Priority = TodoPriority.Medium,
                Status = TodoStatus.Active
            };

            _mockTodoService.Setup(s => s.ProcessTodoMacros(It.IsAny<Todo>())).Returns(newTodo);

            var result = await _controller.PostTodoItem(newTodo);

            var actionResult = Assert.IsType<ActionResult<Todo>>(result);
            var createdAtActionResult = Assert.IsType<CreatedAtActionResult>(actionResult.Result);
            var returnValue = Assert.IsType<Todo>(createdAtActionResult.Value);
            Assert.Equal("New Test Task", returnValue.Title);
            Assert.True(returnValue.Id > 0);
        }

        [Fact]
        public async Task DeleteTodoItem_ExistingId_ReturnsNoContent()
        {
            var result = await _controller.DeleteTodoItem(1);


            Assert.IsType<NoContentResult>(result);
            Assert.Null(await _context.Todos.FindAsync(1));
        }

        [Fact]
        public async Task DeleteTodoItem_NonExistingId_ReturnsNotFound()
        {
            var result = await _controller.DeleteTodoItem(999);

            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task MarkAsCompleted_WithValidId_CompletesTodo()
        {
            var options = new DbContextOptionsBuilder<TodoContext>()
                .UseInMemoryDatabase($"CompletionTest_{Guid.NewGuid()}")
                .Options;

            int todoId;

            await using (var setupContext = new TodoContext(options))
            {
                var todo = new Todo
                {
                    Title = "Test Todo",
                    Status = TodoStatus.Active,
                    Priority = TodoPriority.Medium,
                    CreatedAt = DateTime.UtcNow
                };

                setupContext.Todos.Add(todo);
                await setupContext.SaveChangesAsync();
                todoId = todo.Id;
            }

            IActionResult result;
            await using (var actContext = new TodoContext(options))
            {
                var todoService = new TodoService();
                var controller = new TodoController(actContext, todoService);

                result = await controller.MarkAsCompleted(todoId);
            }


            Assert.IsType<NoContentResult>(result);

            await using (var verifyContext = new TodoContext(options))
            {
                var completedTodo = await verifyContext.Todos.FindAsync(todoId);
                Assert.NotNull(completedTodo);
                Assert.Equal(TodoStatus.Completed, completedTodo.Status);
            }
        }

        [Fact]
        public async Task MarkAsCompleted_WithOverdueTodo_MarksAsLate()
        {
            var options = new DbContextOptionsBuilder<TodoContext>()
                .UseInMemoryDatabase($"LateTest_{Guid.NewGuid()}")
                .Options;

            int todoId;


            await using (var setupContext = new TodoContext(options))
            {
                var todo = new Todo
                {
                    Title = "Overdue Todo",
                    Status = TodoStatus.Overdue,
                    Priority = TodoPriority.Medium,
                    CreatedAt = DateTime.Parse("2024-01-01"),
                    Deadline = DateTime.Parse("2024-05-01")
                };

                setupContext.Todos.Add(todo);
                await setupContext.SaveChangesAsync();
                todoId = todo.Id;
            }


            await using (var actContext = new TodoContext(options))
            {
                var todoService = new TodoService();
                var controller = new TodoController(actContext, todoService);

                await controller.MarkAsCompleted(todoId);
            }


            await using (var verifyContext = new TodoContext(options))
            {
                var lateTodo = await verifyContext.Todos.FindAsync(todoId);
                Assert.NotNull(lateTodo);
                Assert.Equal(TodoStatus.Late, lateTodo.Status);
                Assert.NotNull(lateTodo.ModifiedAt);
            }
        }

        [Fact]
        public async Task MarkAsCompleted_WithInvalidId_ReturnsNotFound()
        {
            var options = new DbContextOptionsBuilder<TodoContext>()
                .UseInMemoryDatabase($"NotFoundTest_{Guid.NewGuid()}")
                .Options;


            IActionResult result;
            await using (var context = new TodoContext(options))
            {
                var todoService = new TodoService();
                var controller = new TodoController(context, todoService);

                result = await controller.MarkAsCompleted(999);
            }

            Assert.IsType<NotFoundResult>(result);
        }
    }
}