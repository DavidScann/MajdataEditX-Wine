using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text;

namespace MajdataEdit.Utils;

public static class WebControl
{
    public static readonly HttpClient client = new();

    // For View
    public static string RequestPOST(string url, string data = "")
    {
        try
        {
            var webRequest = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(data, Encoding.UTF8)
            };

            var response = client.Send(webRequest);
            using var reader = new StreamReader(response.Content.ReadAsStream());

            return reader.ReadToEnd();
        }
        catch
        {
            return "ERROR";
        }
    }

    // For Update Check
    public static async Task<string> RequestGETAsync(string url)
    {
        try
        {
            var executingAssembly = Assembly.GetExecutingAssembly();

            var request = new HttpRequestMessage(HttpMethod.Get, url);

            request.Headers.Add("User-Agent", $"{executingAssembly.GetName().Name!} / {executingAssembly.GetName().Version!.ToString(3)}");

            var response = await client.SendAsync(request);
            return await response.Content.ReadAsStringAsync();
        }
        catch
        {
            return "ERROR";
        }
    }
}