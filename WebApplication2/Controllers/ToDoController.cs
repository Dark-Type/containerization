using WebApplication2.Data;
using WebApplication2.Models;
using WebApplication2.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;


namespace WebApplication2.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TodoController : ControllerBase
    {
        private readonly TodoContext _context;
        private readonly ITodoService _todoService;

        public TodoController(TodoContext context, ITodoService todoService)
        {
            _context = context;
            _todoService = todoService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Todo>>> GetTodos()
        {
            return await _context.Todos.ToListAsync();
        }

        [HttpGet("sort/{criteria}")]
        public async Task<ActionResult<IEnumerable<Todo>>> GetTodosSorted(string criteria)
        {
            return criteria.ToLower() switch
            {
                "deadline" => await _context.Todos.OrderBy(t => t.Deadline ?? DateTime.MaxValue).ToListAsync(),
                "priority" => await _context.Todos.OrderByDescending(t => t.Priority).ToListAsync(),
                "created" => await _context.Todos.OrderByDescending(t => t.CreatedAt).ToListAsync(),
                "title" => await _context.Todos.OrderBy(t => t.Title).ToListAsync(),
                "status" => await _context.Todos.OrderBy(t => t.Status).ToListAsync(),
                _ => await _context.Todos.ToListAsync()
            };
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Todo>> GetTodo(int id)
        {
            var todo = await _context.Todos.FindAsync(id);

            if (todo == null)
            {
                return NotFound();
            }

            _todoService.UpdateTodoStatus(todo);

            if (todo.Status != TodoStatus.Active && todo.Status != TodoStatus.Completed)
            {
                await _context.SaveChangesAsync();
            }

            return todo;
        }

        [HttpPost]
        public async Task<ActionResult<Todo>> PostTodoItem(Todo todo)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (string.IsNullOrEmpty(todo.Title) || todo.Title.Length < 4)
            {
                ModelState.AddModelError("Title", "Title must be at least 4 characters long");
                return BadRequest(ModelState);
            }

            if (todo.Title.Length > 200)
            {
                ModelState.AddModelError("Title", "Title cannot exceed 200 characters");
                return BadRequest(ModelState);
            }

            if (todo.Description != null && todo.Description.Length > 500)
            {
                ModelState.AddModelError("Description", "Description cannot exceed 500 characters");
                return BadRequest(ModelState);
            }

            _todoService.ProcessTodoMacros(todo);

            todo.CreatedAt = DateTime.UtcNow.Date;
            todo.ModifiedAt = null;

            _todoService.UpdateTodoStatus(todo);

            Console.WriteLine(
                $"Attempting to save todo: Title={todo.Title}, Status={todo.Status}, Priority={todo.Priority}");

            _context.Todos.Add(todo);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                string errorMessage = $"Error saving todo: {ex.Message}";
                if (ex.InnerException != null)
                {
                    errorMessage += $"\nInner exception: {ex.InnerException.Message}";
                }

                Console.WriteLine(errorMessage);

                return StatusCode(500, errorMessage);
            }

            return CreatedAtAction(nameof(GetTodo), new { id = todo.Id }, todo);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTodoItem(int id)
        {
            var todo = await _context.Todos.FindAsync(id);
            if (todo == null)
            {
                return NotFound();
            }

            _context.Todos.Remove(todo);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutTodoItem(int id, Todo todo)
        {
            if (id != todo.Id)
            {
                return BadRequest("ID mismatch");
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (string.IsNullOrEmpty(todo.Title) || todo.Title.Length < 4)
            {
                ModelState.AddModelError("Title", "Title must be at least 4 characters long");
                return BadRequest(ModelState);
            }

            if (todo.Title.Length > 200)
            {
                ModelState.AddModelError("Title", "Title cannot exceed 200 characters");
                return BadRequest(ModelState);
            }

            if (todo.Description != null && todo.Description.Length > 500)
            {
                ModelState.AddModelError("Description", "Description cannot exceed 500 characters");
                return BadRequest(ModelState);
            }

            var currentTodo = await _context.Todos.FindAsync(id);
            if (currentTodo == null)
            {
                return NotFound();
            }

            var previousStatus = currentTodo.Status;
            var previousDeadline = currentTodo.Deadline;

            _todoService.ProcessTodoMacros(todo);
            todo.ModifiedAt = DateTime.UtcNow;


            _todoService.UpdateTodoStatus(todo, previousStatus, previousDeadline);

            _context.Entry(currentTodo).CurrentValues.SetValues(todo);
            _context.Entry(currentTodo).Property(x => x.CreatedAt).IsModified = false;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Todos.Any(e => e.Id == id))
                {
                    return NotFound();
                }

                throw;
            }

            return NoContent();
        }

        [HttpPut("complete/{id}")]
        public async Task<IActionResult> MarkAsCompleted(int id)
        {
            var todo = await _context.Todos.FindAsync(id);
            if (todo == null)
            {
                return NotFound();
            }

            if (todo.Status == TodoStatus.Completed || todo.Status == TodoStatus.Late)
            {
                return BadRequest(new { error = "Cannot mark as completed. The todo is already completed or late." });
            }

            _todoService.MarkAsCompleted(todo);

            _context.Entry(todo).Property(x => x.Status).IsModified = true;
            _context.Entry(todo).Property(x => x.ModifiedAt).IsModified = true;

            await _context.SaveChangesAsync();

            return Ok(todo);
        }

        [HttpPut("incomplete/{id}")]
        public async Task<IActionResult> MarkAsIncomplete(int id)
        {
            var todo = await _context.Todos.FindAsync(id);
            if (todo == null)
            {
                return NotFound();
            }

            _todoService.MarkAsIncomplete(todo);

            _context.Entry(todo).Property(x => x.Status).IsModified = true;
            _context.Entry(todo).Property(x => x.ModifiedAt).IsModified = true;

            await _context.SaveChangesAsync();

            return Ok(todo);
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadTodoList(List<Todo> todos)
        {
            foreach (var todo in todos)
            {
                if (string.IsNullOrEmpty(todo.Title) || todo.Title.Length < 4)
                {
                    return BadRequest(new { error = "Title must be at least 4 characters long" });
                }

                if (todo.Title.Length > 200)
                {
                    return BadRequest(new { error = "Title cannot exceed 200 characters" });
                }

                if (todo.Description != null && todo.Description.Length > 500)
                {
                    return BadRequest(new { error = "Description cannot exceed 500 characters" });
                }

                _todoService.ProcessTodoMacros(todo);
                _todoService.UpdateTodoStatus(todo);

                if (todo.CreatedAt == default)
                {
                    todo.CreatedAt = DateTime.UtcNow;
                }
            }

            _context.Todos.RemoveRange(_context.Todos);
            _context.Todos.AddRange(todos);
            await _context.SaveChangesAsync();
            return Ok();
        }
    }
}