using System.CommandLine;
using System.CommandLine.Parsing;
using System.IO.Abstractions;
using EntriesAndTheirContentsServices;
using IOOperationUtilityServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<IFileSystem, FileSystem>();

// 1. 註冊基礎實作
builder.Services.AddTransient<IFileUtilityService, FileUtilityService>();
builder.Services.AddTransient<IDirectoryUtilityService, DirectoryUtilityService>();

// 2. 註冊如何解析 Lazy<IFileUtilityService>
builder.Services.AddTransient<Lazy<IFileUtilityService>>(provider => 
    new Lazy<IFileUtilityService>(() => provider.GetRequiredService<IFileUtilityService>()));

// 3. 註冊如何解析 Lazy<IDirectoryUtilityService>
builder.Services.AddTransient<Lazy<IDirectoryUtilityService>>(provider => 
    new Lazy<IDirectoryUtilityService>(() => provider.GetRequiredService<IDirectoryUtilityService>()));

// 4. 註冊高階服務
builder.Services.AddTransient<IEntriesAndTheirContentsService, EntriesAndTheirContentsService>();

using IHost host = builder.Build();

Option<string> pathOption = new("--path")
{
    Description = "To list all entries and its contents (if it is a file) into a log file"
};

Option<string> logFileOption = new("--log-to")
{
    Description = "To list all entries and its contents (if it is a file) into a log file"
};

RootCommand rootCommand = new("Sample app for listing all entries and its contents (if it is a file) into a log file");
rootCommand.Options.Add(pathOption);
rootCommand.Options.Add(logFileOption);

ParseResult parseResult = rootCommand.Parse(args);
if (parseResult.Errors.Count == 0)
{
    var directory = parseResult.GetValue(pathOption);
    var logFileName = parseResult.GetValue(logFileOption);
    
    var entriesAndTheirContentsService = host.Services.GetRequiredService<IEntriesAndTheirContentsService>();
    entriesAndTheirContentsService.LogEntriesOfDirectoryAndTheirContentsToFile(directory,"*",logFileName);
}
else
{
    foreach (ParseError parseError in parseResult.Errors)
    {
        Console.Error.WriteLine(parseError.Message);
    }
}
