using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication2.Models;
using WebApplication2.Services;
using WebApplication2.Data;
using WebApplication2.Controllers;
using Moq;


namespace WebApplication2.Tests.Controllers
{
    public class TodoControllerBoundaryTests : IDisposable
    {
        private readonly TodoContext _context;
        private readonly Mock<ITodoService> _mockTodoService;
        private readonly TodoController _controller;

        private readonly DateTime _currentDate = new DateTime(2025, 5, 11, 10, 59, 15);

        public TodoControllerBoundaryTests()
        {
            var options = new DbContextOptionsBuilder<TodoContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new TodoContext(options);
            _mockTodoService = new Mock<ITodoService>();
            _controller = new TodoController(_context, _mockTodoService.Object);
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }


        [Theory]
        [InlineData(-10, TodoStatus.Overdue)]
        [InlineData(10, TodoStatus.Active)]
        public async Task PostTodoItem_DeadlineEquivalenceClasses_SetsCorrectStatus(int daysFromNow,
            TodoStatus expectedStatus)
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

            Assert.Equal(expectedStatus, returnValue.Status);
        }


        [Theory]
        [InlineData(0, false)]
        [InlineData(-1, false)]
        [InlineData(1, true)]
        [InlineData(999, false)]
        public async Task GetTodo_IdBoundaryValues_ReturnsExpectedResults(int id, bool shouldExist)
        {
            if (shouldExist && id == 1)
            {
                _context.Todos.Add(new Todo
                {
                    Id = 1,
                    Title = "Test Todo",
                    Status = TodoStatus.Active,
                    Priority = TodoPriority.Medium,
                    CreatedAt = _currentDate
                });
                await _context.SaveChangesAsync();
            }

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
        [InlineData("priority", TodoPriority.High, TodoPriority.Low)]
        [InlineData("deadline", "2025-05-15", "2025-05-20")]
        [InlineData("created", "2025-05-10", "2025-05-01")]
        [InlineData("invalid", null, null)]
        public async Task GetTodosSorted_SortingCriteriaBoundaries_SortsCorrectly(
            string criteria,
            object firstItemValue,
            object lastItemValue)
        {
            _context.Todos.AddRange(
                new Todo
                {
                    Id = 1,
                    Title = "First Todo",
                    Priority = TodoPriority.High,
                    CreatedAt = new DateTime(2025, 5, 10),
                    Deadline = null
                },
                new Todo
                {
                    Id = 2,
                    Title = "Second Todo",
                    Priority = TodoPriority.Medium,
                    CreatedAt = new DateTime(2025, 5, 5),
                    Deadline = new DateTime(2025, 5, 15)
                },
                new Todo
                {
                    Id = 3,
                    Title = "Third Todo",
                    Priority = TodoPriority.Low,
                    CreatedAt = new DateTime(2025, 5, 1),
                    Deadline = new DateTime(2025, 5, 20)
                }
            );
            await _context.SaveChangesAsync();


            var result = await _controller.GetTodosSorted(criteria);


            var actionResult = Assert.IsType<ActionResult<IEnumerable<Todo>>>(result);
            var todos = Assert.IsAssignableFrom<IEnumerable<Todo>>(actionResult.Value).ToList();

            Assert.Equal(3, todos.Count);

            if (criteria == "priority")
            {
                Assert.Equal(firstItemValue, todos.First().Priority);
                Assert.Equal(lastItemValue, todos.Last().Priority);
            }
            else if (criteria == "deadline")
            {
                Assert.Equal(DateTime.Parse((string)firstItemValue), todos.First().Deadline);


                Assert.Null(todos.Last().Deadline);
            }
            else if (criteria == "created")
            {
                Assert.Equal(DateTime.Parse((string)firstItemValue), todos.First().CreatedAt);
                Assert.Equal(DateTime.Parse((string)lastItemValue), todos.Last().CreatedAt);
            }
        }

        [Theory]
        [InlineData(TodoStatus.Active, TodoStatus.Completed)]
        [InlineData(TodoStatus.Overdue, TodoStatus.Late)]
        [InlineData(TodoStatus.Completed, TodoStatus.Completed)]
        public async Task MarkAsCompleted_StatusBoundaries_TransitionsCorrectly(
            TodoStatus initialStatus, TodoStatus expectedStatus)
        {
            var todo = new Todo
            {
                Id = 1,
                Title = "Status Transition Test",
                Status = initialStatus,
                Priority = TodoPriority.Medium
            };

            _context.Todos.Add(todo);
            await _context.SaveChangesAsync();

            _mockTodoService.Setup(s => s.MarkAsCompleted(It.IsAny<Todo>()))
                .Callback<Todo>(t =>
                {
                    if (t.Status == TodoStatus.Active)
                        t.Status = TodoStatus.Completed;
                    else if (t.Status == TodoStatus.Overdue)
                        t.Status = TodoStatus.Late;
                });

            await _controller.MarkAsCompleted(1);

            var updatedTodo = await _context.Todos.FindAsync(1);
            Assert.Equal(expectedStatus, updatedTodo.Status);
        }
    }
}