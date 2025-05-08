using Microsoft.AspNetCore.Mvc;
using ProtelWebReports.Services;
using System;
using System.Threading.Tasks;
using ProtelWebReports.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using static System.Collections.Specialized.BitVector32;

namespace ProtelWebReports.Controllers
{
    [Route("api/reports")]
    [ApiController]
    public class ApiController : ControllerBase
    {
        private readonly IReportService _reportService;
        private readonly ILogger<ApiController> _logger;

        internal static readonly ConcurrentDictionary<string, ReportModel> Store
            = new ConcurrentDictionary<string, ReportModel>();

        

        public ApiController(IReportService reportService, ILogger<ApiController> logger)
        {
            _reportService = reportService;
            _logger = logger;
        }

        [HttpGet]
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

        [HttpPost]
        public IActionResult Receive([FromBody] ReportModel report)
        {
            _logger.LogInformation("API Receive: incoming report with {ColCount} columns and {RowCount} rows",
 report.ColumnNames?.Count, report.Rows?.Count);
            var id = Guid.NewGuid().ToString();
            Store[id] = report;
            
            _logger.LogInformation("Stored report under id {ReportId}", id);

            var url = Url.Action(
                 action: "Show",
                 controller: "Report",
                 values: new { id },
                 protocol: Request.Scheme);
            
            _logger.LogInformation("Responding with URL {Url}", url);
            return Content(url, "text/plain"); // ← επιστρέφει απλώς το URL, χωρίς JSON
        }

    }
}