using NoMercyBot.Globals.Information;

namespace NoMercyBot.Globals.Extensions;

public static class Url
{
    public static Uri ToHttps(this Uri url)
    {
        UriBuilder uriBuilder = new(url)
        {
            Scheme = Uri.UriSchemeHttps,
            Port = -1, // default port for scheme
        };

        return uriBuilder.Uri;
    }

    public static string FileName(this Uri url)
    {
        return Path.GetFileName(url.LocalPath);
    }

    public static string BasePath(this Uri url)
    {
        return url.ToString().Replace("/" + url.FileName(), "");
    }

    public static bool HasSuccessStatus(this Uri url, string? contentType = null)
    {
        try
        {
            System.Net.Http.HttpClient httpClient = new();
            httpClient.DefaultRequestHeaders.Add("User-Agent", Config.UserAgent);

            if (contentType is not null)
                httpClient.DefaultRequestHeaders.Add("Accept", contentType);

            HttpResponseMessage res = httpClient.SendAsync(new(HttpMethod.Head, url)).Result;
            return res.IsSuccessStatusCode;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
