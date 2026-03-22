using System.Collections;

namespace OrganizeMedia.Framework;

public record ErrorInfo(
    string Type,
    string Message,
    string StackTrace = null,
    string Source = null,
    string HelpLink = null,
    Dictionary<string, string> Data = null,
    ErrorInfo InnerError = null)
{
    public static ErrorInfo FromException(Exception exception)
    {
        if (exception is null)
            return null;

        Dictionary<string, string> data = null;

        if (exception.Data.Count > 0)
        {
            data = new Dictionary<string, string>();

            foreach (DictionaryEntry entry in exception.Data)
            {
                string key = entry.Key?.ToString();
                if (key is null)
                    continue;

                data[key] = entry.Value?.ToString();
            }
        }

        return new ErrorInfo(
            Type: exception.GetType().FullName,
            Message: exception.Message,
            StackTrace: exception.StackTrace,
            Source: exception.Source,
            HelpLink: exception.HelpLink,
            Data: data,
            InnerError: FromException(exception.InnerException));
    }
}