using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using IoTHubGateway.Server.Services;

namespace IoTHubGateway.Server.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class GatewayController : ControllerBase
    {
        private readonly ServerOptions options;
        private readonly IGatewayService gatewayService;
        private static readonly DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public GatewayController(IGatewayService gatewayService, ServerOptions options)
        {
            this.options = options;
            this.gatewayService = gatewayService;
        }

        /// <summary>
        /// Sends a message for the given device
        /// </summary>
        /// <param name="deviceId">Device identifier</param>
        /// <param name="payload">Payload (JSON format)</param>
        /// <returns></returns>
        [HttpPost("{deviceId}")]
        public async Task<IActionResult> Send(string deviceId, dynamic payload)
        {
            if (string.IsNullOrEmpty(deviceId))
                return BadRequest(new { error = "Missing deviceId" });

            //if (payload == null)
            //    return BadRequest(new { error = "Missing payload" });

            var sasToken = this.ControllerContext.HttpContext.Request.Headers[Constants.SasTokenHeaderName].ToString();
            if (!string.IsNullOrEmpty(sasToken))
            {
                var tokenExpirationDate = ResolveTokenExpiration(sasToken);
                if (!tokenExpirationDate.HasValue)
                    tokenExpirationDate = DateTime.UtcNow.AddMinutes(20);

                await gatewayService.SendDeviceToCloudMessageByToken(deviceId, payload.ToString(), sasToken, tokenExpirationDate.Value);
            }
            else
            {
                if (!this.options.SharedAccessPolicyKeyEnabled)
                    return BadRequest(new { error = "Shared access is not enabled" });
                await gatewayService.SendDeviceToCloudMessageBySharedAccess(deviceId, payload.ToString());
            }

            return Ok();
        }

        /// <summary>
        /// Sends a message for the given device
        /// </summary>
        /// <param name="deviceId">Device identifier</param>
        /// <param name="payload">Payload (JSON format)</param>
        /// <returns></returns>
        [HttpPost("{deviceId}/properties")]
        public async Task<IActionResult> SendProperties(string deviceId, dynamic payload)
        {
            if (string.IsNullOrEmpty(deviceId))
                return BadRequest(new { error = "Missing deviceId" });

            //if (payload == null)
            //    return BadRequest(new { error = "Missing payload" });

            var sasToken = this.ControllerContext.HttpContext.Request.Headers[Constants.SasTokenHeaderName].ToString();
            if (!string.IsNullOrEmpty(sasToken))
            {
                var tokenExpirationDate = ResolveTokenExpiration(sasToken);
                if (!tokenExpirationDate.HasValue)
                    tokenExpirationDate = DateTime.UtcNow.AddMinutes(20);

                await gatewayService.SendDeviceReportedPropertiesByToken(deviceId, payload.ToString(), sasToken, tokenExpirationDate.Value);
            }
            else
            {
                if (!this.options.SharedAccessPolicyKeyEnabled)
                    return BadRequest(new { error = "Shared access is not enabled" });
                await gatewayService.SendDeviceReportedPropertiesBySharedAccess(deviceId, payload.ToString());
            }

            return Ok();
        }

        /// <summary>
        /// Get the device twin of the given device
        /// </summary>
        /// <param name="deviceId">Device identifier</param>
        /// <returns>Device twin (JSON format)</returns>
        [HttpGet("{deviceId}/twin")]
        public async Task<IActionResult> GetTwin(string deviceId)
        {
            if (string.IsNullOrEmpty(deviceId))
                return BadRequest(new { error = "Missing deviceId" });

            string twin = "";

            var sasToken = this.ControllerContext.HttpContext.Request.Headers[Constants.SasTokenHeaderName].ToString();
            if (!string.IsNullOrEmpty(sasToken))
            {
                var tokenExpirationDate = ResolveTokenExpiration(sasToken);
                if (!tokenExpirationDate.HasValue)
                    tokenExpirationDate = DateTime.UtcNow.AddMinutes(20);

                twin = await gatewayService.GetDeviceTwinByToken(deviceId, sasToken, tokenExpirationDate.Value);
            }
            else
            {
                if (!this.options.SharedAccessPolicyKeyEnabled)
                    return BadRequest(new { error = "Shared access is not enabled" });
                twin = await gatewayService.GetDeviceTwinBySharedAccess(deviceId);
            }

            return Ok(twin);
        }

        /// <summary>
        /// Expirations is available as parameter "se" as a unix time in our sample application
        /// </summary>
        /// <param name="sasToken"></param>
        /// <returns>Expiration time</returns>
        private DateTime? ResolveTokenExpiration(string sasToken)
        {
            // TODO: Implement in more reliable way (regex or another built-in class)
            const string field = "se=";
            var index = sasToken.LastIndexOf(field);
            if (index >= 0)
            {
                var unixTime = sasToken.Substring(index + field.Length);
                if (int.TryParse(unixTime, out var unixTimeInt))
                {
                    return epoch.AddSeconds(unixTimeInt);
                }
            }

            return null;
        }
    }
}
