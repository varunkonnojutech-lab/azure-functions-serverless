using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using OrderFunctions.Services;

namespace OrderFunctions.Functions;

/// <summary>
/// Runs every morning at 9:00 AM and logs an order summary — a stand-in for
/// emailing a daily operations digest to stakeholders.
/// </summary>
public class DailyReport
{
    private readonly ILogger<DailyReport> _logger;
    private readonly IOrderStore _store;

    public DailyReport(ILogger<DailyReport> logger, IOrderStore store)
    {
        _logger = logger;
        _store = store;
    }

    // NCRONTAB: {second} {minute} {hour} {day} {month} {day-of-week}
    [Function("DailyReport")]
    public void Run(
        [TimerTrigger("0 0 9 * * *")] TimerInfo timer,
        FunctionContext context)
    {
        _logger.LogInformation(
            "Daily report: {Count} orders in the store. Next run at {Next}.",
            _store.Count,
            timer.ScheduleStatus?.Next);
    }
}
