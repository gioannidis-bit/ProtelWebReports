using ProtelWebReports.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ProtelWebReports.Services
{
    public class ReportService : IReportService
    {
        private readonly IProtelDbService _dbService;
        private readonly ILogger<ReportService> _logger;
        private readonly string _protelPath;

        public ReportService(
            IProtelDbService dbService,
            ILogger<ReportService> logger,
            Microsoft.Extensions.Configuration.IConfiguration config)
        {
            _dbService = dbService;
            _logger = logger;
            _protelPath = config["ProtelSettings:ProtelPath"];
        }

        public async Task<ReportModel> GenerateReportAsync(string reportId, DateTime fromDate, DateTime toDate,
                                                         string revenueType, string comType)
        {
            try
            {
                _logger.LogInformation($"Generating report {reportId} for period {fromDate:yyyy-MM-dd} to {toDate:yyyy-MM-dd}");

                // Create the report model
                var reportModel = new ReportModel
                {
                    ReportId = reportId,
                    Title = "Availability Period Report",
                    FromDate = fromDate,
                    ToDate = toDate,
                    RevenueType = revenueType == "0" ? "Gross" : "Net",
                    CommitmentType = comType == "1" ? "Detailed" : "Global"
                };

                // Build the SQL query for this report
                string sql = BuildAvailabilityPeriodQuery(fromDate, toDate, revenueType, comType);

                // Execute the query
                var result = await _dbService.ExecuteQueryAsync(sql);

                reportModel.ColumnNames = result.ColumnNames;
                reportModel.Rows = result.Rows;

                return reportModel;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error generating report {reportId}");

                // In case of error, return a model with the error message
                var errorModel = new ReportModel
                {
                    ReportId = reportId,
                    Title = "Error Report",
                    FromDate = fromDate,
                    ToDate = toDate,
                    ColumnNames = new List<string> { "Error" },
                    Rows = new List<List<string>> { new List<string> { ex.Message } }
                };

                return errorModel;
            }
        }

        private string BuildAvailabilityPeriodQuery(DateTime fromDate, DateTime toDate, string revenueType, string comType)
        {
            string fromDateStr = fromDate.ToString("yyyy-MM-dd");
            string toDateStr = toDate.ToString("yyyy-MM-dd");

            return @"
                SET NOCOUNT ON
                DECLARE @dtFrom DATETIME = '" + fromDateStr + @"', 
                        @dtTo DATETIME = '" + toDateStr + @"', 
                        @IsNet INT = " + revenueType + @",
                        @com INT = " + comType + @",
                        @mpehotel INT = 1,
                        @StationID INT = 1
                
                -- Temporary table to store results
                IF OBJECT_ID('tempdb..#PeriodAvail') IS NOT NULL
                    DROP TABLE #PeriodAvail

                CREATE TABLE #PeriodAvail (
                    ID INT IDENTITY(1,1), 
                    Grp INT, 
                    RType INT DEFAULT -1, 
                    Descr VARCHAR(100) DEFAULT '', 
                    Capacity VARCHAR(50) DEFAULT '',
                    Availabilty VARCHAR(50) DEFAULT '', 
                    Sold VARCHAR(50) DEFAULT '', 
                    OccPst VARCHAR(50) DEFAULT '', 
                    SoldPerc VARCHAR(50) DEFAULT '', 
                    ADR VARCHAR(50) DEFAULT '', 
                    RevPar VARCHAR(50) DEFAULT ''
                )
                
                -- Insert header row
                INSERT INTO #PeriodAvail (Grp, Descr, Capacity, Availabilty, Sold, OccPst, SoldPerc, ADR, RevPar)
                SELECT -10, 'Room Type','Capacity','Avail.','Sold','Occ %','Perc Sold%','ADR','RevPar'
                
                -- Calculate date range
                DECLARE @ndays INT = DATEDIFF(day, @dtFrom, @dtTo)
                
                -- Get room types and capacities
                DECLARE @Kats TABLE (katnr INT, katPos INT, KatTotal INT)
                INSERT INTO @Kats(katnr, katPos, KatTotal)
                SELECT k.katnr, k.katpos, COUNT(z.ziname)
                FROM kat AS k
                INNER JOIN zimmer AS z ON z.kat = k.katnr AND z.mpehotel = @mpehotel
                WHERE k.zimmer = 1
                GROUP BY k.katnr, k.katpos
                
                -- Get total rooms
                DECLARE @TotRooms INT
                SELECT @TotRooms = n.long
                FROM norooms AS n
                WHERE n.mpehotel = @mpehotel
                
                -- Insert room types
                INSERT INTO #PeriodAvail (Grp, RType, Descr)
                SELECT 1, k.katnr, k.kat + ' (' + CAST(COUNT(z.ziname) AS VARCHAR(10)) + ')'
                FROM kat AS k
                INNER JOIN zimmer AS z ON z.kat = k.katnr AND z.mpehotel = @mpehotel
                WHERE k.zimmer = 1
                GROUP BY k.katnr, k.kat, k.katpos
                ORDER BY k.katpos
                
                -- Insert totals row
                INSERT INTO #PeriodAvail (Grp, RType, Descr)
                VALUES (100, -1, 'Totals')
                
                -- Calculate occupancy and other metrics for each room type
                DECLARE @OccTotal INT = 0
                
                -- Get sold rooms for each type
                DECLARE @SoldRms TABLE (katnr INT, KatTotal INT)
                INSERT INTO @SoldRms(katnr, KatTotal)
                SELECT hsl.katnr, SUM(hsl.Occupancy) 
                FROM hitstatistic AS hsl
                INNER JOIN @Kats AS k ON k.katnr = hsl.katnr
                WHERE hsl.reschar IN (0,1) AND kattyp = 0 AND mpehotel = @mpehotel 
                    AND stationid = @StationID AND Date BETWEEN @dtFrom AND @dtTo 
                GROUP BY hsl.katnr
                
                -- Get out of order rooms
                DECLARE @OOO TABLE (katnr INT, KatTotal INT)
                INSERT INTO @OOO(katnr, KatTotal)
                SELECT hsl.katnr, count(hsl.Occupancy)
                FROM hitstatistic AS hsl
                INNER JOIN @Kats AS k ON k.katnr = hsl.katnr
                WHERE hsl.reschar IN (6) AND kattyp = 0 AND mpehotel = @mpehotel 
                    AND stationid = @StationID AND Date BETWEEN @dtFrom AND @dtTo 
                GROUP BY hsl.katnr
                
                -- Get revenues
                DECLARE @Revenues TABLE (katnr INT, Revenue DECIMAL(18,2))
                INSERT INTO @Revenues(katnr, Revenue)
                SELECT hsl.katnr, 
                       CASE WHEN @IsNet = 1 THEN SUM(hsl.logisnet) ELSE SUM(hsl.logis) END
                FROM hitstatistic AS hsl
                INNER JOIN @Kats AS k ON k.katnr = hsl.katnr
                WHERE hsl.reschar IN (0,1) AND kattyp = 0 AND mpehotel = @mpehotel 
                    AND stationid = @StationID AND Date BETWEEN @dtFrom AND @dtTo 
                GROUP BY hsl.katnr
                
                -- Update room type statistics
                UPDATE p
                SET p.Capacity = (k.KatTotal * (@ndays + 1)) - ISNULL(o.KatTotal, 0),
                    p.Sold = ISNULL(s.KatTotal, 0),
                    p.Availabilty = ((k.KatTotal * (@ndays + 1)) - ISNULL(o.KatTotal, 0)) - ISNULL(s.KatTotal, 0),
                    p.OccPst = CAST(
                        CASE 
                            WHEN ((k.KatTotal * (@ndays + 1)) - ISNULL(o.KatTotal, 0)) = 0 THEN 0
                            ELSE ROUND((CAST(ISNULL(s.KatTotal, 0) AS FLOAT) * 100) / 
                                      CAST(((k.KatTotal * (@ndays + 1)) - ISNULL(o.KatTotal, 0)) AS FLOAT), 2)
                        END AS VARCHAR) + ' %',
                    p.SoldPerc = '100 %', -- Will be updated later
                    p.ADR = CASE 
                              WHEN ISNULL(s.KatTotal, 0) = 0 THEN '0'
                              ELSE CAST(ROUND(CAST(ISNULL(r.Revenue, 0) AS FLOAT) / CAST(s.KatTotal AS FLOAT), 2) AS VARCHAR)
                           END,
                    p.RevPar = CASE 
                                 WHEN ((k.KatTotal * (@ndays + 1)) - ISNULL(o.KatTotal, 0)) = 0 THEN '0'
                                 ELSE CAST(ROUND(CAST(ISNULL(r.Revenue, 0) AS FLOAT) / 
                                           CAST(((k.KatTotal * (@ndays + 1)) - ISNULL(o.KatTotal, 0)) AS FLOAT), 2) AS VARCHAR)
                              END
                FROM #PeriodAvail p
                INNER JOIN @Kats k ON k.katnr = p.RType
                LEFT JOIN @OOO o ON o.katnr = p.RType
                LEFT JOIN @SoldRms s ON s.katnr = p.RType
                LEFT JOIN @Revenues r ON r.katnr = p.RType
                WHERE p.Grp = 1
                
                -- Calculate total values
                DECLARE @TotalCapacity INT, @TotalAvail INT, @TotalSold INT, @TotalRevenue DECIMAL(18,2)
                
                SELECT @TotalCapacity = SUM(CAST(Capacity AS INT)),
                       @TotalAvail = SUM(CAST(Availabilty AS INT)),
                       @TotalSold = SUM(CAST(Sold AS INT))
                FROM #PeriodAvail
                WHERE Grp = 1
                
                SELECT @TotalRevenue = SUM(Revenue)
                FROM @Revenues
                
                -- Update totals row
                UPDATE #PeriodAvail
                SET Capacity = CAST(@TotalCapacity AS VARCHAR),
                    Availabilty = CAST(@TotalAvail AS VARCHAR),
                    Sold = CAST(@TotalSold AS VARCHAR),
                    OccPst = CASE 
                               WHEN @TotalCapacity = 0 THEN '0 %'
                               ELSE CAST(ROUND((CAST(@TotalSold AS FLOAT) * 100) / CAST(@TotalCapacity AS FLOAT), 2) AS VARCHAR) + ' %'
                            END,
                    ADR = CASE 
                            WHEN @TotalSold = 0 THEN '0'
                            ELSE CAST(ROUND(CAST(@TotalRevenue AS FLOAT) / CAST(@TotalSold AS FLOAT), 2) AS VARCHAR)
                         END,
                    RevPar = CASE 
                               WHEN @TotalCapacity = 0 THEN '0'
                               ELSE CAST(ROUND(CAST(@TotalRevenue AS FLOAT) / CAST(@TotalCapacity AS FLOAT), 2) AS VARCHAR)
                            END
                WHERE Grp = 100
                
                -- Update SoldPerc for each room type
                UPDATE p
                SET p.SoldPerc = CASE 
                                   WHEN @TotalSold = 0 THEN '0 %'
                                   ELSE CAST(ROUND((CAST(ISNULL(p.Sold, '0') AS FLOAT) * 100) / 
                                         CAST(@TotalSold AS FLOAT), 2) AS VARCHAR) + ' %'
                                END
                FROM #PeriodAvail p
                WHERE p.Grp = 1
                
                -- Return results
                SELECT Descr, Capacity, Availabilty, Sold, OccPst, SoldPerc, ADR, RevPar
                FROM #PeriodAvail
                ORDER BY Grp, ID
                
                SET NOCOUNT OFF
            ";
        }
    }
}