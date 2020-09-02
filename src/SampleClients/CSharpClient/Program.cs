using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Client;

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

            int port = 5001;
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("sas_token", sasToken);
                while (true)
                {
                    var response = await client.PostAsync($"https://localhost:{port}/gateway/{deviceId}", new StringContent("{ \"content\": \"from_rest_call\" }", Encoding.UTF8, "application/json"));
                    Console.WriteLine($"Response: {response.StatusCode.ToString()}");

                    response = await client.GetAsync($"https://localhost:{port}/gateway/{deviceId}/twin");
                    Console.WriteLine($"Response: {response.StatusCode.ToString()}");
                    var responseContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Twin: {responseContent}");

                    response = await client.PostAsync($"https://localhost:{port}/gateway/{deviceId}/properties", new StringContent("{ \"content\": \"from_rest_call\" }", Encoding.UTF8, "application/json"));
                    Console.WriteLine($"Response: {response.StatusCode.ToString()}");

                    await Task.Delay(200);
                }
            }
        }
    }
}
