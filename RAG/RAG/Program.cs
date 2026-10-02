using Library.Extensions;
using Microsoft.EntityFrameworkCore;
using RAG.Extensions;
using RAG.Spaces;
using System;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

builder.Services.AddRegister(builder.Environment,builder.Configuration);

builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();
// Configure the HTTP request pipeline.
app.UseLibraryExceptionHandling();
app.UseLibraryCors();
app.UseLibraryOpenApi();

app.UseAuthentication();
app.UseAuthorization();
app.UseSpaceContext();

app.MapControllers();

app.Run();

/// <summary>
/// 供整合測試 (WebApplicationFactory) 使用
/// </summary>
public partial class Program { }
