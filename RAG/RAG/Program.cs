using Library.Extensions;
using Microsoft.EntityFrameworkCore;
using RAG.Extensions;
using System;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle

builder.Services.AddRegister(builder.Environment,builder.Configuration);

builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();
app.AddLibrary();
// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthorization();

app.MapControllers();

app.Run();
