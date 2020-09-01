using Microsoft.Azure.Devices.Client;
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace CSharpClient
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("<PRESS ENTER TO CONTINUE>");
            Console.ReadLine();
            var hostName = "<enter-iothub-name>";
            var deviceId = "<enter-device-id>";

            var sasToken = new SharedAccessSignatureBuilder()
            {
                Key = "",
                Target = $"{hostName}.azure-devices.net/devices/{deviceId}",
                TimeToLive = TimeSpan.FromMinutes(20)
            }
            .ToSignature();

            int port = 5000;
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("sas_token", sasToken);
                while (true)
                {
                    var response = await client.PostAsync($"http://localhost:{port}/api/{deviceId}", new StringContent("{ content: 'from_rest_call' }", Encoding.UTF8, "application/json"));
                    Console.WriteLine($"Response: {response.StatusCode.ToString()}");

                    response = await client.GetAsync($"http://localhost:{port}/api/{deviceId}/twin");
                    Console.WriteLine($"Response: {response.StatusCode.ToString()}");
                    var content = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Twin: {content}");

                    response = await client.PostAsync($"http://localhost:{port}/api/{deviceId}/properties", new StringContent("{ content: 'from_rest_call' }", Encoding.UTF8, "application/json"));
                    Console.WriteLine($"Response: {response.StatusCode.ToString()}");

                    await Task.Delay(200);
                }
            }
        }
    }
}
