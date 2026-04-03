using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NoMercyBot.Database;
using NoMercyBot.Database.Models;
using NoMercyBot.Services.Twitch;

namespace NoMercyBot.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/commands")]
public class CommandController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly TwitchCommandService _commandService;

    public CommandController(AppDbContext dbContext, TwitchCommandService commandService)
    {
        _dbContext = dbContext;
        _commandService = commandService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        List<Command> commands = await _dbContext.Commands.ToListAsync();
        return Ok(commands);
    }

    [HttpGet("{name}")]
    public async Task<IActionResult> Get(string name)
    {
        Command? command = await _dbContext.Commands.FirstOrDefaultAsync(c => c.Name == name);
        if (command == null)
            return NotFound();
        return Ok(command);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Command command)
    {
        if (await _dbContext.Commands.AnyAsync(c => c.Name == command.Name))
            return Conflict("Command with this name already exists.");
        _dbContext.Commands.Add(command);
        await _dbContext.SaveChangesAsync();
        await _commandService.AddOrUpdateUserCommandAsync(
            command.Name,
            command.Response,
            command.Permission,
            command.Type,
            command.IsEnabled,
            command.Description
        );
        return CreatedAtAction(nameof(Get), new { name = command.Name }, command);
    }

    [HttpPut("{name}")]
    public async Task<IActionResult> Update(string name, [FromBody] Command command)
    {
        Command? dbCommand = await _dbContext.Commands.FirstOrDefaultAsync(c => c.Name == name);
        if (dbCommand == null)
            return NotFound();
        if (
            dbCommand.Name != command.Name
            && await _dbContext.Commands.AnyAsync(c => c.Name == command.Name)
        )
            return Conflict("Command with this name already exists.");
        dbCommand.Name = command.Name;
        dbCommand.Response = command.Response;
        dbCommand.Permission = command.Permission;
        dbCommand.Type = command.Type;
        dbCommand.IsEnabled = command.IsEnabled;
        dbCommand.Description = command.Description;
        await _dbContext.SaveChangesAsync();
        await _commandService.AddOrUpdateUserCommandAsync(
            dbCommand.Name,
            dbCommand.Response,
            dbCommand.Permission,
            dbCommand.Type,
            dbCommand.IsEnabled,
            dbCommand.Description
        );
        return Ok(dbCommand);
    }

    [HttpDelete("{name}")]
    public async Task<IActionResult> Delete(string name)
    {
        Command? dbCommand = await _dbContext.Commands.FirstOrDefaultAsync(c => c.Name == name);
        if (dbCommand == null)
            return NotFound();
        _dbContext.Commands.Remove(dbCommand);
        await _dbContext.SaveChangesAsync();
        await _commandService.RemoveUserCommandAsync(name);
        return NoContent();
    }
}
