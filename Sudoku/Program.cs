using Sudoku.Components;
using Sudoku.Application.Interfaces;
using Sudoku.Application.Services;
using Sudoku.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Sudoku DI registrations (Clean Architecture style)
builder.Services.AddSingleton<ISudokuValidator, SudokuValidator>();
builder.Services.AddSingleton<ISudokuSolver, SudokuSolver>();
builder.Services.AddSingleton<ISudokuHintProvider, SudokuHintProvider>();
builder.Services.AddSingleton<ISudokuGenerator, SudokuGenerator>();
builder.Services.AddScoped<SudokuService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
