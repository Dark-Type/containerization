using System;
using System.Globalization;
using System.Text.RegularExpressions;
using WebApplication2.Models;
using WebApplication2.Services;


namespace WebApplication2.Tests.Services
{
    public class TodoServiceAdvancedTests
    {
        private readonly TodoService _todoService;

        private readonly DateTime _currentDate = new DateTime(2025, 5, 11, 10, 47, 40);

        public TodoServiceAdvancedTests()
        {
            _todoService = new TodoService();
        }

        [Theory]
        [InlineData("!1 Critical Task", TodoPriority.Critical, "Critical Task")]
        [InlineData("!2 High Task", TodoPriority.High, "High Task")]
        [InlineData("!3 Medium Task", TodoPriority.Medium, "Medium Task")]
        [InlineData("!4 Low Task", TodoPriority.Low, "Low Task")]
        [InlineData("No Macro Task", TodoPriority.Medium, "No Macro Task")]
        [InlineData("!1!2 Multiple Macros", TodoPriority.Critical, "Multiple Macros")]
        [InlineData("!5 Invalid Macro", TodoPriority.Medium, "!5 Invalid Macro")]
        public void ProcessPriorityMacro_AllEquivalenceClasses_HandlesCorrectly(string title,
            TodoPriority expectedPriority, string expectedTitle)
        {
            var todo = new Todo
            {
                Title = title,
                Priority = TodoPriority.Medium 
            };


            _todoService.ProcessTodoMacros(todo);


            Assert.Equal(expectedPriority, todo.Priority);
            Assert.Equal(expectedTitle, todo.Title);
        }


        [Theory]
        [InlineData("Task !before 12.05.2025", "12.05.2025", true)]
        [InlineData("Task !before 11.05.2025", "11.05.2025", true)]
        [InlineData("Task !before 10.05.2025", "10.05.2025", true)]
        [InlineData("Task !before 01.01.2025", "01.01.2025", true)]
        [InlineData("Task !before 31.12.2025", "31.12.2025", true)]
        [InlineData("Task !before 30-11-2025", "30.11.2025", true)]
        [InlineData("Task !before 32.13.2025", null, false)]
        [InlineData("Task with no deadline", null, false)]
        public void ProcessDeadlineMacro_AllEquivalenceClasses_HandlesCorrectly(string title, string expectedDateStr,
            bool shouldExtractDeadline)
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
                Assert.Equal("Task", todo.Title);
            }
            else
            {
                if (expectedDate == null)
                {
                    Assert.Null(todo.Deadline);
                }

                Assert.Equal(title, todo.Title);
            }
        }

        [Theory]
        [InlineData(TodoStatus.Active, true, 1, TodoStatus.Completed)]
        [InlineData(TodoStatus.Active, true, -1, TodoStatus.Late)]
        [InlineData(TodoStatus.Active, false, 1, TodoStatus.Active)]
        [InlineData(TodoStatus.Active, false, -1, TodoStatus.Overdue)]
        [InlineData(TodoStatus.Completed, false, 1, TodoStatus.Active)]
        [InlineData(TodoStatus.Completed, false, -1, TodoStatus.Overdue)]
        [InlineData(TodoStatus.Completed, true, 1, TodoStatus.Completed)]
        [InlineData(TodoStatus.Overdue, true, -1, TodoStatus.Late)]
        [InlineData(TodoStatus.Overdue, false, -1, TodoStatus.Overdue)]
        [InlineData(TodoStatus.Late, false, -1, TodoStatus.Overdue)]
        [InlineData(TodoStatus.Late, true, -1, TodoStatus.Late)]
        public void TodoStatusTransitions_AllEquivalenceClasses_TransitionsCorrectly(
            TodoStatus initialStatus,
            bool markAsCompleted,
            int deadlineDaysFromNow,
            TodoStatus expectedStatus)
        {
            var todo = new Todo
            {
                Title = "Status Test Task",
                Status = initialStatus,
                Deadline = _currentDate.AddDays(deadlineDaysFromNow)
            };

            if (markAsCompleted)
            {
                _todoService.MarkAsCompleted(todo);
            }
            else
            {
                _todoService.MarkAsIncomplete(todo);
            }

            Assert.Equal(expectedStatus, todo.Status);
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
    }
}