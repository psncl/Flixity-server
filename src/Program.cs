global using server.Common.Api;
using Scalar.AspNetCore;
using server;

var builder = WebApplication.CreateBuilder(args);

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Check validation attributes on endpoint parameters, and describe errors as ProblemDetails.
builder.Services.AddValidation();
builder.Services.AddProblemDetails();
// Registered after the built-in writer, so it's only used when that one refuses.
builder.Services.AddSingleton<IProblemDetailsWriter, FallbackProblemDetailsWriter>();

// By default this is true in Development, where a missing parameter throws an exception
// instead of returning 400. Use the production behaviour everywhere.
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = false);

builder.AddServices();

var app = builder.Build();

// Unhandled exceptions. Development keeps the detailed developer exception page.
if (!app.Environment.IsDevelopment()) app.UseExceptionHandler();
app.UseStatusCodePages(); // Error responses without a body

app.MapEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapScalarApiReference();
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.Run();