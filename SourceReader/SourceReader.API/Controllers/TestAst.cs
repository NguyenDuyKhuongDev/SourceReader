using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SourceReader.Core.Services.Project;
using SourceReader.Infrastructure.DataModel;
using SourceReader.Infrastructure.WorkSpace;

namespace SourceReader.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TestAst : ControllerBase
    {
        private readonly WorkSpaceManager _workSpaceManager;
        public TestAst(WorkSpaceManager workSpaceManager)
        {
            _workSpaceManager = workSpaceManager;
        }

        [HttpPost("{rootPath}")]
        public async Task<IActionResult> RunScan(string rootPath, CancellationToken ct)
        {
            try
            {
                var projectManager = await _workSpaceManager.GetOrCreateAsync(rootPath);
                await projectManager.CachedProject(ct);
                projectManager.StartScanAsync(ct);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
            return Ok();
        }

        [HttpGet("{rootPath}")]
        public async Task<ProjectIndex> GetAst(string rootPath, CancellationToken ct)
        {
            var projectManager = await _workSpaceManager.GetOrCreateAsync(rootPath);

            return projectManager._index;
        }



    }
}
