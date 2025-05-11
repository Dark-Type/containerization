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

            _todoService.ProcessTodoMacros(todo);
            todo.ModifiedAt = DateTime.UtcNow.Date;
            _todoService.UpdateTodoStatus(todo);

            _context.Entry(todo).State = EntityState.Modified;
            _context.Entry(todo).Property(x => x.CreatedAt).IsModified = false;

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

            _todoService.MarkAsCompleted(todo);
            await _context.SaveChangesAsync();
            return NoContent();
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
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadTodoList(List<Todo> todos)
        {
            foreach (var todo in todos)
            {
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