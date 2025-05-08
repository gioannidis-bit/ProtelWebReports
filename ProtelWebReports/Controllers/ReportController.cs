using Microsoft.AspNetCore.Mvc;
using ProtelWebReports.Models;
using ProtelWebReports.Services;
using System;
using System.Threading.Tasks;
using ProtelWebReports.Controllers;
using Microsoft.Extensions.Logging;

namespace ProtelWebReports.Controllers
{
    public class ReportController : Controller
    {
        private readonly IReportService _reportService;
        private readonly ILogger<ReportController> _logger;

        public ReportController(IReportService reportService, ILogger<ReportController> logger)
        {
            _reportService = reportService;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Show(string id)
        {
            _logger.LogInformation("Show called with id={ReportId}", id);

            if (String.IsNullOrEmpty(id) 
                || !ApiController.Store.TryGetValue(id, out var reportModel))
            {
                _logger.LogWarning("Report id={ReportId} not found", id);
                return NotFound($"Report με id={id} δεν βρέθηκε.");
            }

            _logger.LogInformation("Rendering report id={ReportId} with {RowCount} rows", id, reportModel.Rows.Count);

            // χρησιμοποιούμε το ίδιο View "Report" που ήδη έχεις
            return View("Report", reportModel);
        }


[HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Generate(string reportId, string fromDate, string toDate,
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

                return View("Report", reportModel);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error generating report: {ex.Message}");
            }
        }
    }
}