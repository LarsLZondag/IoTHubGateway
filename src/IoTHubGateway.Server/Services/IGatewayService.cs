using System;
using System.Threading.Tasks;

namespace IoTHubGateway.Server.Services
{
    /// <summary>
    /// Gateway to IoT Hub service
    /// </summary>
    public interface IGatewayService
    {
        /// <summary>
        /// Sends device to cloud message using device token
        /// </summary>
        /// <param name="deviceId"></param>
        /// <param name="payload"></param>
        /// <param name="sasToken"></param>
        /// <param name="dateTime"></param>
        /// <returns></returns>
        Task SendDeviceToCloudMessageByToken(string deviceId, string payload, string sasToken, DateTime dateTime);

        /// <summary>
        /// Sends device to cloud message using shared access token
        /// </summary>
        /// <param name="deviceId"></param>
        /// <param name="payload"></param>
        /// <returns></returns>
        Task SendDeviceToCloudMessageBySharedAccess(string deviceId, string payload);

        /// <summary>
        /// Update reported twin properties for device using device token
        /// </summary>
        /// <param name="deviceId"></param>
        /// <param name="payload"></param>
        /// <param name="sasToken"></param>
        /// <param name="dateTime"></param>
        /// <returns></returns>
        Task UpdateDeviceReportedPropertiesByToken(string deviceId, string payload, string sasToken, DateTime dateTime);

        /// <summary>
        /// Update reported twin properties for device using shared access token
        /// </summary>
        /// <param name="deviceId"></param>
        /// <param name="payload"></param>
        /// <returns></returns>
        Task UpdateDeviceReportedPropertiesBySharedAccess(string deviceId, string payload);

        /// <summary>
        /// Get device twin for device using device token
        /// </summary>
        /// <param name="deviceId"></param>
        /// <param name="sasToken"></param>
        /// <param name="dateTime"></param>
        /// <returns>twin</returns>
        Task<string> GetDeviceTwinByToken(string deviceId, string sasToken, DateTime dateTime);

        /// <summary>
        /// Get device twin for device using shared access token
        /// </summary>
        /// <param name="deviceId"></param>
        /// <returns>twin</returns>
        Task<string> GetDeviceTwinBySharedAccess(string deviceId);
    }
}
