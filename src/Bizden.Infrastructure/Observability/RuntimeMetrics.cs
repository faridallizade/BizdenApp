using System.Text;

namespace Bizden.Infrastructure.Observability;

public sealed class RuntimeMetrics
{
    private long requestCount;
    private long requestErrorCount;
    private long uploadCompletionCount;
    private long uploadFailureCount;
    private long r2FailureCount;
    private long reservationExpiryCount;

    public void RecordRequest(int statusCode)
    {
        Interlocked.Increment(ref requestCount);
        if (statusCode >= 500) Interlocked.Increment(ref requestErrorCount);
    }

    public void RecordUploadCompleted() => Interlocked.Increment(ref uploadCompletionCount);
    public void RecordUploadFailed() => Interlocked.Increment(ref uploadFailureCount);
    public void RecordR2Failure() => Interlocked.Increment(ref r2FailureCount);
    public void RecordReservationExpiries(int count) => Interlocked.Add(ref reservationExpiryCount, count);

    public string ToPrometheusText()
    {
        var output = new StringBuilder();
        AppendCounter(output, "bizden_http_requests_total", "HTTP requests served by the API.", Interlocked.Read(ref requestCount));
        AppendCounter(output, "bizden_http_requests_error_total", "HTTP requests that returned a 5xx status.", Interlocked.Read(ref requestErrorCount));
        AppendCounter(output, "bizden_uploads_completed_total", "Uploads completed and verified in object storage.", Interlocked.Read(ref uploadCompletionCount));
        AppendCounter(output, "bizden_uploads_failed_total", "Upload verification or object-storage preparation failures.", Interlocked.Read(ref uploadFailureCount));
        AppendCounter(output, "bizden_r2_failures_total", "Object storage operation failures.", Interlocked.Read(ref r2FailureCount));
        AppendCounter(output, "bizden_reservation_expiries_total", "Upload reservations that expired before completion.", Interlocked.Read(ref reservationExpiryCount));
        return output.ToString();
    }

    private static void AppendCounter(StringBuilder output, string name, string help, long value)
    {
        output.Append("# HELP ").Append(name).Append(' ').AppendLine(help);
        output.Append("# TYPE ").Append(name).AppendLine(" counter");
        output.Append(name).Append(' ').AppendLine(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }
}
