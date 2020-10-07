using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace IoTHubGateway.Server.Services
{
    /// <summary>
    /// IoT Hub Device client multiplexer based on AMQP
    /// </summary>
    public class GatewayService : IGatewayService
    {
        private readonly ServerOptions serverOptions;
        private readonly IMemoryCache cache;
        private readonly ILogger<GatewayService> logger;
        RegisteredDevices registeredDevices;

        /// <summary>
        /// Sliding expiration for each device client connection
        /// Default: 30 minutes
        /// </summary>
        public TimeSpan DeviceConnectionCacheSlidingExpiration { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        public GatewayService(ServerOptions serverOptions, IMemoryCache cache, ILogger<GatewayService> logger, RegisteredDevices registeredDevices)
        {
            this.serverOptions = serverOptions;
            this.cache = cache;
            this.logger = logger;
            this.registeredDevices = registeredDevices;
            this.DeviceConnectionCacheSlidingExpiration = TimeSpan.FromMinutes(serverOptions.DefaultDeviceCacheInMinutes);
        }

        public async Task SendDeviceToCloudMessageByToken(string deviceId, string payload, string sasToken, DateTime tokenExpiration)
        {
            var deviceClient = await ResolveDeviceClient(deviceId, sasToken, tokenExpiration);
            if (deviceClient == null)
                throw new DeviceConnectionException($"Failed to connect to device {deviceId}");

            try
            {
                await deviceClient.SendEventAsync(new Message(Encoding.UTF8.GetBytes(payload))
                {
                    ContentEncoding = "utf-8",
                    ContentType = "application/json"
                });

                this.logger.LogInformation($"Event sent to device {deviceId} using device token. Payload: {payload}");
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, $"Could not send device message to IoT Hub (device: {deviceId})");
                throw;
            }
        }

        public async Task SendDeviceToCloudMessageBySharedAccess(string deviceId, string payload)
        {
            var deviceClient = await ResolveDeviceClient(deviceId);

            try
            {
                await deviceClient.SendEventAsync(new Message(Encoding.UTF8.GetBytes(payload))
                {
                    ContentEncoding = "utf-8",
                    ContentType = "application/json"
                });

                this.logger.LogInformation($"Event sent to device {deviceId} using shared access. Payload: {payload}");
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, $"Could not send device message to IoT Hub (device: {deviceId})");
                throw;
            }
        }

        public async Task SendDeviceReportedPropertiesByToken(string deviceId, string payload, string sasToken, DateTime tokenExpiration)
        {
            var deviceClient = await ResolveDeviceClient(deviceId, sasToken, tokenExpiration);
            if (deviceClient == null)
                throw new DeviceConnectionException($"Failed to connect to device {deviceId}");

            try
            {
                await deviceClient.UpdateReportedPropertiesAsync(new TwinCollection(payload));

                this.logger.LogInformation($"Properties sent to device {deviceId} using device token. Payload: {payload}");
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, $"Could not send device properties to IoT Hub (device: {deviceId})");
                throw;
            }
        }

        public async Task SendDeviceReportedPropertiesBySharedAccess(string deviceId, string payload)
        {
            var deviceClient = await ResolveDeviceClient(deviceId);

            try
            {
                await deviceClient.UpdateReportedPropertiesAsync(new TwinCollection(payload));

                this.logger.LogInformation($"Properties sent to device {deviceId} using shared access. Payload: {payload}");
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, $"Could not send device properties to IoT Hub (device: {deviceId})");
                throw;
            }
        }

        public async Task<string> GetDeviceTwinByToken(string deviceId, string sasToken, DateTime tokenExpiration)
        {
            var deviceClient = await ResolveDeviceClient(deviceId, sasToken, tokenExpiration);
            if (deviceClient == null)
                throw new DeviceConnectionException($"Failed to connect to device {deviceId}");

            try
            {
                Twin twin = await deviceClient.GetTwinAsync().ConfigureAwait(false);

                this.logger.LogInformation($"Twin retrieved from device {deviceId} using device token.");

                return twin.ToJson();
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, $"Could not retrieve device twin (device: {deviceId})");
                throw;
            }
        }

        public async Task<string> GetDeviceTwinBySharedAccess(string deviceId)
        {
            var deviceClient = await ResolveDeviceClient(deviceId);

            try
            {
                Twin twin = await deviceClient.GetTwinAsync().ConfigureAwait(false);

                this.logger.LogInformation($"Twin retrieved from device{deviceId} using shared access.");

                return twin.ToJson();
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, $"Could not retrieve device twin (device: {deviceId})");
                throw;
            }
        }

        private async Task<DeviceClient> ResolveDeviceClient(string deviceId, string sasToken = null, DateTime? tokenExpiration = null)
        {
            try
            {
                var deviceClient = await cache.GetOrCreateAsync<DeviceClient>(deviceId, async (cacheEntry) =>
                {
                    IAuthenticationMethod auth = null;
                    if (string.IsNullOrEmpty(sasToken))
                    {
                        auth = new DeviceAuthenticationWithSharedAccessPolicyKey(deviceId, this.serverOptions.AccessPolicyName, this.serverOptions.AccessPolicyKey);
                    }
                    else
                    {
                        auth = new DeviceAuthenticationWithToken(deviceId, sasToken);
                    }

                    var newDeviceClient = DeviceClient.Create(
                       this.serverOptions.IoTHubHostName,
                        auth,
                        new ITransportSettings[]
                        {
                            new AmqpTransportSettings(TransportType.Amqp_Tcp_Only)
                            {
                                AmqpConnectionPoolSettings = new AmqpConnectionPoolSettings()
                                {
                                    Pooling = true,
                                    MaxPoolSize = (uint)this.serverOptions.MaxPoolSize,
                                }
                            }
                        }
                    );

                    newDeviceClient.OperationTimeoutInMilliseconds = (uint)this.serverOptions.DeviceOperationTimeout;

                    await newDeviceClient.OpenAsync();

                    if (this.serverOptions.DirectMethodEnabled)
                       await newDeviceClient.SetMethodDefaultHandlerAsync(this.serverOptions.DirectMethodCallback, deviceId);

                    if (!tokenExpiration.HasValue)
                        tokenExpiration = DateTime.UtcNow.AddMinutes(this.serverOptions.DefaultDeviceCacheInMinutes);
                    cacheEntry.SetAbsoluteExpiration(tokenExpiration.Value);
                    cacheEntry.RegisterPostEvictionCallback(this.CacheEntryRemoved, deviceId);

                    this.logger.LogInformation($"Connection to device {deviceId} has been established, valid until {tokenExpiration.Value.ToString()}");

                    registeredDevices.AddDevice(deviceId);

                    return newDeviceClient;
                });

                return deviceClient;
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, $"Could not connect device {deviceId}");
            }

            return null;
        }

        private void CacheEntryRemoved(object key, object value, EvictionReason reason, object state)
        {
            this.registeredDevices.RemoveDevice(key);
        }
    }
}
