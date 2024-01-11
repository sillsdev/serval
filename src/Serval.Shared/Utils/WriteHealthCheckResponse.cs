namespace Serval.Shared.Utils;

public class WriteHealthCheckResponse
{
    public static Task Generate(HttpContext context, HealthReport healthReport)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        var options = new JsonWriterOptions { Indented = true };

        using var memoryStream = new MemoryStream();
        using (var jsonWriter = new Utf8JsonWriter(memoryStream, options))
        {
            jsonWriter.WriteStartObject();
            jsonWriter.WriteString("status", healthReport.Status.ToString());
            jsonWriter.WriteStartObject("results");

            foreach (KeyValuePair<string, HealthReportEntry> healthReportEntry in healthReport.Entries)
            {
                jsonWriter.WriteStartObject(healthReportEntry.Key);
                jsonWriter.WriteString("status", healthReportEntry.Value.Status.ToString());
                if (healthReportEntry.Value.Description is not null || healthReportEntry.Value.Exception is not null)
                    jsonWriter.WriteString(
                        "description",
                        healthReportEntry.Value.Description ?? healthReportEntry.Value.Exception?.Message
                    );
                if (healthReportEntry.Value.Data.Count > 0)
                {
                    jsonWriter.WriteStartObject("data");
                    foreach (var item in healthReportEntry.Value.Data)
                    {
                        jsonWriter.WritePropertyName(item.Key);

                        JsonSerializer.Serialize(jsonWriter, item.Value, item.Value?.GetType() ?? typeof(object));
                    }
                    jsonWriter.WriteEndObject();
                }
                jsonWriter.WriteEndObject();
            }

            jsonWriter.WriteEndObject();
            jsonWriter.WriteEndObject();
        }

        return context.Response.WriteAsync(Encoding.UTF8.GetString(memoryStream.ToArray()));
    }
}
