using System.ComponentModel.DataAnnotations;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddProblemDetails();
var app = builder.Build();
var store = new UserStore();

app.Use(async (context, next) =>
{
    var timer = Stopwatch.StartNew();
    await next(context);
    app.Logger.LogInformation("{Method} {Path} returned {StatusCode} in {ElapsedMs} ms",
        context.Request.Method, context.Request.Path, context.Response.StatusCode,
        timer.ElapsedMilliseconds);
});
app.UseExceptionHandler();
app.UseStatusCodePages();

var users = app.MapGroup("/api/users");
users.MapGet("", () => Results.Ok(store.List()));
users.MapGet("/{id:guid}", (Guid id) =>
    store.Find(id) is { } user ? Results.Ok(user) : UserNotFound());
users.MapPost("", (UserInput input) => SaveUser(input));
users.MapPut("/{id:guid}", (Guid id, UserInput input) => SaveUser(input, id));
users.MapDelete("/{id:guid}", (Guid id) =>
    store.Delete(id) ? Results.NoContent() : UserNotFound());

app.Run();

IResult SaveUser(UserInput input, Guid? id = null)
{
    string name = input.Name?.Trim() ?? "";
    string email = input.Email?.Trim() ?? "";
    var errors = new Dictionary<string, string[]>();

    if (name.Length is < 1 or > 100)
        errors["name"] = ["Name must contain 1 to 100 characters."];
    if (email.Length is < 1 or > 254 || email.Any(char.IsWhiteSpace) ||
        !new EmailAddressAttribute().IsValid(email))
        errors["email"] = ["Enter a valid email address of at most 254 characters."];

    if (errors.Count > 0)
        return Results.ValidationProblem(errors);

    var (status, user) = store.Save(id, name, email);
    return status switch
    {
        SaveStatus.Missing => UserNotFound(),
        SaveStatus.DuplicateEmail => Results.Problem(
            statusCode: 409, title: "That email address is already in use."),
        _ when id is null => Results.Created($"/api/users/{user!.Id}", user),
        _ => Results.Ok(user)
    };
}

static IResult UserNotFound() =>
    Results.Problem(statusCode: 404, title: "User not found.");

record UserInput(string? Name, string? Email);
record User(Guid Id, string Name, string Email);
enum SaveStatus { Saved, Missing, DuplicateEmail }

sealed class UserStore
{
    private readonly object gate = new();
    private readonly Dictionary<Guid, User> users = new();

    public User[] List()
    {
        lock (gate)
            return users.Values.OrderBy(user => user.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(user => user.Id).ToArray();
    }

    public User? Find(Guid id)
    {
        lock (gate)
            return users.GetValueOrDefault(id);
    }

    public bool Delete(Guid id)
    {
        lock (gate)
            return users.Remove(id);
    }

    public (SaveStatus Status, User? User) Save(Guid? id, string name, string email)
    {
        lock (gate)
        {
            if (id is Guid existingId && !users.ContainsKey(existingId))
                return (SaveStatus.Missing, null);

            if (users.Values.Any(user => user.Id != id &&
                string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase)))
                return (SaveStatus.DuplicateEmail, null);

            var user = new User(id ?? Guid.NewGuid(), name, email);
            users[user.Id] = user;
            return (SaveStatus.Saved, user);
        }
    }
}
