using Azure.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartFactoryWebApi.DTO;
using SmartFactoryWebApi.Services;

namespace SmartFactoryWebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class EntryController : ControllerBase
    {
        private readonly IEntryDetailService _entryDetailService;
        private readonly ILogger<EntryController> _logger;
        // 注入服务
        public EntryController(ILogger<EntryController> logger, IEntryDetailService entryDetailService)
        {
            _logger = logger;
            _entryDetailService = entryDetailService;
        }

        [HttpPost("allocate", Name = "entry-allocate")]
        public async Task<ActionResult<Result<IEnumerable<PalletBarRelation>>>> Allocate([FromBody] GetAllocateRequest request)
        {
            if (request == null)
            {
                return BadRequest("Request body is required.");
            }
            var result = await _entryDetailService.AllocateAsync(request.barCode, request.shelf, request.binNo);
            return Ok(result);
        }

        [HttpPost("commit", Name = "entry-commit")]
        public async Task<ActionResult<Result<IEnumerable<PalletBarRelation>>>> EnterWareHouse([FromBody] GetCommitBarDetails request)
        {
            if (request == null)
            {
                return BadRequest("Request body is required.");
            }
            var result = await _entryDetailService.CommitAsync(request.Items, request.WarehouseLocation);
            return Ok(result);
        }
    }
}
