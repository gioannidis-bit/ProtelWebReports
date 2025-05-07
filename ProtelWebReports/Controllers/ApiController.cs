using Microsoft.AspNetCore.Mvc;
using ProtelWebReports.Services;
using System;
using System.Threading.Tasks;

namespace ProtelWebReports.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ApiController : ControllerBase
    {
        private readonly IReportService _reportService;

        public ApiController(IReportService reportService)
        {
            _reportService = reportService;
        }

        [HttpGet("report")]
        public async Task<IActionResult> GetReport(string reportId, string fromDate, string toDate,
                                             string revenueType = "0", string comType = "1")
        {
            try
            {
                if (string.IsNullOrEmpty(reportId))
                {
                    return BadRequest("Report ID is required");
                }

                DateTime fromDateTime = DateTime.Parse(fromDate);
                DateTime toDateTime = DateTime.Parse(toDate);

                var reportModel = await _reportService.GenerateReportAsync(
                    reportId, fromDateTime, toDateTime, revenueType, comType);

                return Ok(reportModel);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error generating report: {ex.Message}");
            }
        }
    }
}