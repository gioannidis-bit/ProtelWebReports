using ProtelWebReports.Models;
using System;
using System.Threading.Tasks;

namespace ProtelWebReports.Services
{
    public interface IReportService
    {
        Task<ReportModel> GenerateReportAsync(string reportId, DateTime fromDate, DateTime toDate,
                                             string revenueType, string comType);
    }
}