using System;
using WebApplication2.Models;
using WebApplication2.Services;
using Xunit;
using System;
using WebApplication2.Models;
using WebApplication2.Services;
using Xunit;
using System.Globalization;

namespace WebApplication2.Tests.Services
{
    public class TodoServiceTests
    {
        private readonly TodoService _todoService;
        private readonly DateTime _currentDate = new DateTime(2025, 5, 12, 9, 29, 27);

        public TodoServiceTests()
        {
            _todoService = new TodoService();
        }

        #region Macro Processing Tests

        [Theory]
        [InlineData("Task !1 with critical priority", TodoPriority.Critical, "Task with critical priority")]
        [InlineData("Task !2 with high priority", TodoPriority.High, "Task with high priority")]
        [InlineData("Task !3 with medium priority", TodoPriority.Medium, "Task with medium priority")]
        [InlineData("Task !4 with low priority", TodoPriority.Low, "Task with low priority")]
        [InlineData("!1 At beginning", TodoPriority.Critical, "At beginning")]
        [InlineData("At end !1", TodoPriority.Critical, "At end")]
        [InlineData("No macro here", TodoPriority.Medium, "No macro here")]
        [InlineData("!5 Invalid priority", TodoPriority.Medium, "!5 Invalid priority")]
        public void ProcessPriorityMacro_AllCases_HandlesCorrectly(string title, TodoPriority expectedPriority,
            string expectedTitle)
        {
            var todo = new Todo { Title = title, Priority = TodoPriority.Medium };


            _todoService.ProcessTodoMacros(todo);


            Assert.Equal(expectedPriority, todo.Priority);
            Assert.Equal(expectedTitle, todo.Title);
        }

        [Theory]
        [InlineData("Task !before 15.06.2025", "15.06.2025", true, "Task")]
        [InlineData("!before 15.06.2025 At beginning", "15.06.2025", true, "At beginning")]
        [InlineData("At end !before 15.06.2025", "15.06.2025", true, "At end")]
        [InlineData("Task !before 30-11-2025", "30.11.2025", true, "Task")]
        [InlineData("Task !before 32.13.2025", null, false, "Task !before 32.13.2025")]
        [InlineData("Task with no deadline", null, false, "Task with no deadline")]
        public void ProcessDeadlineMacro_AllCases_HandlesCorrectly(string title, string expectedDateStr,
            bool shouldExtractDeadline, string expectedTitle)
        {
            var todo = new Todo { Title = title };
            DateTime? expectedDate = null;
            if (expectedDateStr != null)
            {
                expectedDate = DateTime.ParseExact(expectedDateStr, "dd.MM.yyyy", CultureInfo.InvariantCulture);
            }

            _todoService.ProcessTodoMacros(todo);


            if (shouldExtractDeadline)
            {
                Assert.NotNull(todo.Deadline);
                Assert.Equal(expectedDate?.Date, todo.Deadline?.Date);
                Assert.Equal(expectedTitle, todo.Title);
            }
            else
            {
                if (expectedDateStr == null)
                {
                    Assert.Null(todo.Deadline);
                }

                Assert.Equal(title, todo.Title);
            }
        }

        [Fact]
        public void ProcessTodoMacros_CombinedMacros_ExtractsBoth()
        {
            var todo = new Todo
            {
                Title = "!1 Critical task !before 15.06.2025",
                Priority = TodoPriority.Low,
                Status = TodoStatus.Active
            };


            _todoService.ProcessTodoMacros(todo);


            Assert.Equal("Critical task", todo.Title);
            Assert.Equal(TodoPriority.Critical, todo.Priority);
            Assert.NotNull(todo.Deadline);
            Assert.Equal(new DateTime(2025, 6, 15), todo.Deadline?.Date);
        }

        #endregion

        #region Status Transition Tests

        [Theory]
        [InlineData(-1, TodoStatus.Overdue)]
        [InlineData(0, TodoStatus.Overdue)]
        [InlineData(1, TodoStatus.Overdue)]
        public void UpdateTodoStatus_DeadlineVsToday_SetsCorrectStatus(int daysFromNow, TodoStatus expectedStatus)
        {
            var today = _currentDate.Date;
            var todo = new Todo
            {
                Title = "Task",
                Status = TodoStatus.Active,
                Deadline = today.AddDays(daysFromNow)
            };


            _todoService.UpdateTodoStatus(todo);


            Assert.Equal(expectedStatus, todo.Status);
        }

        [Theory]
        [InlineData(TodoStatus.Active, -1, TodoStatus.Late)]
        [InlineData(TodoStatus.Active, 1, TodoStatus.Completed)]
        [InlineData(TodoStatus.Overdue, -1, TodoStatus.Late)]
        public void MarkAsCompleted_DifferentInitialStatuses_TransitionsCorrectly(
            TodoStatus initialStatus, int deadlineDaysFromNow, TodoStatus expectedStatus)
        {
            var todo = new Todo
            {
                Title = "Task",
                Status = initialStatus,
                Deadline = _currentDate.AddDays(deadlineDaysFromNow)
            };


            _todoService.MarkAsCompleted(todo);


            Assert.Equal(expectedStatus, todo.Status);
            Assert.NotNull(todo.ModifiedAt);
        }

        [Theory]
        [InlineData(TodoStatus.Completed, 1, TodoStatus.Overdue)]
        [InlineData(TodoStatus.Late, -1, TodoStatus.Overdue)]
        [InlineData(TodoStatus.Completed, -1, TodoStatus.Overdue)]
        public void MarkAsIncomplete_DifferentInitialStatuses_TransitionsCorrectly(
            TodoStatus initialStatus, int deadlineDaysFromNow, TodoStatus expectedStatus)
        {
            var todo = new Todo
            {
                Title = "Task",
                Status = initialStatus,
                Deadline = _currentDate.AddDays(deadlineDaysFromNow)
            };


            _todoService.MarkAsIncomplete(todo);

            Assert.Equal(expectedStatus, todo.Status);
            Assert.NotNull(todo.ModifiedAt);
        }

        #endregion
    }
}