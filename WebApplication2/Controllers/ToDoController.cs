using WebApplication2.Data;
using WebApplication2.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace WebApplication2.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TodoController(TodoContext context) : ControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Todo>>> GetTodos()
        {
            return await context.Todos.ToListAsync();
        }

        [HttpPost]
        public async Task<ActionResult<Todo>> PostTodoItem(Todo todo)
        {
            context.Todos.Add(todo);
            await context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetTodos), new { id = todo.Id }, todo);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTodoItem(int id)
        {
            var todo = await context.Todos.FindAsync(id);
            if (todo == null)
            {
                return NotFound();
            }

            context.Todos.Remove(todo);
            await context.SaveChangesAsync();
            return NoContent();
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutTodoItem(int id, Todo todo)
        {
            if (id != todo.Id)
            {
                return BadRequest();
            }

            context.Entry(todo).State = EntityState.Modified;
            try
            {
                await context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!context.Todos.Any(e => e.Id == id))
                {
                    return NotFound();
                }
            }

            return NoContent();
        }

        [HttpPut("complete/{id}")]
        public async Task<IActionResult> MarkAsCompleted(int id)
        {
            var todo = await context.Todos.FindAsync(id);
            if (todo == null)
            {
                return NotFound();
            }

            todo.IsCompleted = true;
            await context.SaveChangesAsync();
            return NoContent();
        }

        [HttpPut("incomplete/{id}")]
        public async Task<IActionResult> MarkAsIncomplete(int id)
        {
            var todo = await context.Todos.FindAsync(id);
            if (todo == null)
            {
                return NotFound();
            }

            todo.IsCompleted = false;
            await context.SaveChangesAsync();
            return NoContent();
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadTodoList(List<Todo> todos)
        {
            context.Todos.RemoveRange(context.Todos);
            context.Todos.AddRange(todos);
            await context.SaveChangesAsync();
            return Ok();
        }
    }
}