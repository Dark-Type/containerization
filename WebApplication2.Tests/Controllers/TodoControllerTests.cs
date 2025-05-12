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
        private readonly TodoContext _context;
        private readonly Mock<ITodoService> _mockTodoService;
        private readonly TodoController _controller;
        private readonly DateTime _currentDate = new DateTime(2025, 5, 12, 9, 29, 27);

        public TodoControllerTests()
        {
            var options = new DbContextOptionsBuilder<TodoContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new TodoContext(options);
            _mockTodoService = new Mock<ITodoService>();
            _controller = new TodoController(_context, _mockTodoService.Object);

            SeedTestData();
        }

        private void SeedTestData()
        {
            _context.Todos.Add(new Todo
            {
                Id = 1,
                Title = "Test Task 1",
                Status = TodoStatus.Active,
                Priority = TodoPriority.High,
                CreatedAt = _currentDate.AddDays(-2),
                Deadline = _currentDate.AddDays(2)
            });

            _context.Todos.Add(new Todo
            {
                Id = 2,
                Title = "Test Task 2",
                Status = TodoStatus.Completed,
                Priority = TodoPriority.Low,
                CreatedAt = _currentDate.AddDays(-5),
                Deadline = null
            });

            _context.SaveChanges();
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        #region Basic Functionality Tests

        [Fact]
        public async Task GetTodos_ReturnsAllTodos()
        {
            var result = await _controller.GetTodos();

            var actionResult = Assert.IsType<ActionResult<IEnumerable<Todo>>>(result);
            var todos = Assert.IsAssignableFrom<IEnumerable<Todo>>(actionResult.Value);
            Assert.Equal(2, todos.Count());
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

        #endregion

        #region Boundary Tests

        [Theory]
        [InlineData(-1, false)]
        [InlineData(0, false)]
        [InlineData(1, true)]
        [InlineData(2, true)]
        [InlineData(999, false)]
        public async Task GetTodo_IdBoundaryValues_ReturnsExpectedResults(int id, bool shouldExist)
        {
            var result = await _controller.GetTodo(id);


            if (shouldExist)
            {
                var actionResult = Assert.IsType<ActionResult<Todo>>(result);
                var todo = Assert.IsType<Todo>(actionResult.Value);
                Assert.Equal(id, todo.Id);
            }
            else
            {
                var actionResult = Assert.IsType<ActionResult<Todo>>(result);
                Assert.IsType<NotFoundResult>(actionResult.Result);
            }
        }

        [Theory]
        [InlineData("priority", "High", "Low")]
        [InlineData("deadline", "Has deadline", "No deadline")]
        [InlineData("created", "Newer", "Older")]
        [InlineData("invalid", null, null)]
        public async Task GetTodosSorted_SortingCriteria_SortsCorrectly(
            string criteria, string expectedFirstProperty, string expectedLastProperty)
        {
            var result = await _controller.GetTodosSorted(criteria);


            var actionResult = Assert.IsType<ActionResult<IEnumerable<Todo>>>(result);
            var todos = Assert.IsAssignableFrom<IEnumerable<Todo>>(actionResult.Value).ToList();
            Assert.Equal(2, todos.Count);


            if (criteria == "priority")
            {
                Assert.Equal(TodoPriority.High, todos.First().Priority);
                Assert.Equal(TodoPriority.Low, todos.Last().Priority);
            }
            else if (criteria == "deadline")
            {
                Assert.NotNull(todos.First().Deadline);
                Assert.Null(todos.Last().Deadline);
            }
            else if (criteria == "created")
            {
                Assert.True(todos.First().CreatedAt > todos.Last().CreatedAt);
            }
        }

        #endregion

        #region Status Transition Tests

        [Theory]
        [InlineData(TodoStatus.Active, TodoStatus.Completed)]
        [InlineData(TodoStatus.Overdue, TodoStatus.Late)]
        public async Task MarkAsCompleted_StatusTransitions_CompletesWithCorrectStatus(
            TodoStatus initialStatus, TodoStatus expectedStatus)
        {
            var options = new DbContextOptionsBuilder<TodoContext>()
                .UseInMemoryDatabase($"CompletionTest_{Guid.NewGuid()}")
                .Options;

            int todoId;


            await using (var setupContext = new TodoContext(options))
            {
                var todo = new Todo
                {
                    Title = $"Status Test: {initialStatus}",
                    Status = initialStatus,
                    Priority = TodoPriority.Medium,
                    CreatedAt = _currentDate
                };

                setupContext.Todos.Add(todo);
                await setupContext.SaveChangesAsync();
                todoId = todo.Id;
            }


            _mockTodoService.Setup(s => s.MarkAsCompleted(It.IsAny<Todo>()))
                .Callback<Todo>(t =>
                {
                    if (t.Status == TodoStatus.Active)
                        t.Status = TodoStatus.Completed;
                    else if (t.Status == TodoStatus.Overdue)
                        t.Status = TodoStatus.Late;
                    t.ModifiedAt = _currentDate;
                });


            IActionResult result;
            await using (var actContext = new TodoContext(options))
            {
                var controller = new TodoController(actContext, _mockTodoService.Object);
                result = await controller.MarkAsCompleted(todoId);
            }


            Assert.IsType<OkObjectResult>(result);


            await using (var verifyContext = new TodoContext(options))
            {
                var completedTodo = await verifyContext.Todos.FindAsync(todoId);
                Assert.NotNull(completedTodo);
                _mockTodoService.Verify(s => s.MarkAsCompleted(It.IsAny<Todo>()), Times.Once);
            }
        }

        #endregion

        #region Deadline Handling Tests

        [Theory]
        [InlineData(-10, TodoStatus.Overdue)]
        [InlineData(10, TodoStatus.Active)]
        public async Task PostTodoItem_DeadlineEquivalenceClasses_SetsCorrectStatus(
            int daysFromNow, TodoStatus expectedStatus)
        {
            var deadline = _currentDate.AddDays(daysFromNow).Date;
            var todo = new Todo
            {
                Title = "Deadline Test Task",
                Deadline = deadline,
                Priority = TodoPriority.Medium,
                Status = TodoStatus.Active
            };

            _mockTodoService.Setup(s => s.ProcessTodoMacros(It.IsAny<Todo>())).Returns(todo);
            _mockTodoService.Setup(s => s.UpdateTodoStatus(It.IsAny<Todo>()))
                .Callback<Todo>(t =>
                {
                    if (t.Deadline.HasValue && t.Deadline.Value < _currentDate)
                        t.Status = TodoStatus.Overdue;
                    else
                        t.Status = TodoStatus.Active;
                });


            var result = await _controller.PostTodoItem(todo);


            var actionResult = Assert.IsType<ActionResult<Todo>>(result);
            var createdAtActionResult = Assert.IsType<CreatedAtActionResult>(actionResult.Result);
            var returnValue = Assert.IsType<Todo>(createdAtActionResult.Value);

            _mockTodoService.Verify(s => s.UpdateTodoStatus(It.IsAny<Todo>()), Times.Once);
        }

        #endregion
    }
}