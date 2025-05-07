using Microsoft.AspNetCore.Mvc;
using ProtelWebReports.Models;
using ProtelWebReports.Services;
using System;
using System.Threading.Tasks;

namespace ProtelWebReports.Controllers
{
    public class ReportController : Controller
    {
        private readonly IReportService _reportService;

        public ReportController(IReportService reportService)
        {
            _reportService = reportService;
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