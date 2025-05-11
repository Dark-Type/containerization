using WebApplication2.Models;

namespace WebApplication2.Services
{
    public interface ITodoService
    {
        Todo ProcessTodoMacros(Todo todo);
        void UpdateTodoStatus(Todo todo);
        void MarkAsCompleted(Todo todo);
        void MarkAsIncomplete(Todo todo);
    }
}