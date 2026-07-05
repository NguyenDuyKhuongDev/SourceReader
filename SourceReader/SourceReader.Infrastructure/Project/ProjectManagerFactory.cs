using Microsoft.Extensions.DependencyInjection;
using SourceReader.Core.Services.Project;
using SourceReader.Infrastructure.Analysis.AstAnalysis.CoreAst.CoreParser;
using System;
using System.Collections.Generic;
using System.Text;

namespace SourceReader.Infrastructure.Project
{
    // Why need this factory
    // Project Manager have 2 input is AstScanner and roootpath , i had inject astscanner in 
    //program.cs , so it will be auto create by consctructor , But when i want to inject ProjectManager , DI cant understand how to create it because it need rootpath(runtime input)
    // so this factory born for create ProjectManager with the combination of DI and runtime input.
    public class ProjectManagerFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public ProjectManagerFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public ProjectManager Create(string roothPath)
        {
            var scanner = _serviceProvider.GetRequiredService<AstScanner>();
            return new ProjectManager(scanner, roothPath);
        }
    }
}
