using SourceReader.Core.Services.Project;
using SourceReader.Infrastructure.Analysis.AstAnalysis.CoreAst.CoreParser;
using SourceReader.Infrastructure.Analysis.AstAnalysis.CoreAst.CoreQuery;
using SourceReader.Infrastructure.Factory;
using SourceReader.Infrastructure.WorkSpace;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<ProjectManagerFactory>();
builder.Services.AddSingleton<WorkSpaceManager>();
builder.Services.AddSingleton<AstScanner>();
//builder.Services.AddSingleton<ProjectManager>();
//FileParserDi
builder.Services.AddSingleton<FileParser>();
builder.Services.AddSingleton<ParserPool>();
builder.Services.AddSingleton<QueryRegistry>();
// Log
builder.Services.AddLogging();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
