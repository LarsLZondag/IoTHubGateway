using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using IoTHubGateway.Server.Services;

namespace IoTHubGateway.Server.Controllers
{
    [Route("api")]
    public class GatewayController : Controller
    {
        private readonly IGatewayService gatewayService;
        private readonly ServerOptions options;
        private static readonly DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public GatewayController(IGatewayService gatewayService, ServerOptions options)
        {
            this.gatewayService = gatewayService;
            this.options = options;
        }

        /// <summary>
        /// Sends a message for the given device
        /// </summary>
        /// <param name="deviceId">Device identifier</param>
        /// <param name="payload">Message payload (JSON format)</param>
        /// <returns></returns>
        [HttpPost("{deviceId}")]
        public async Task<IActionResult> Send(string deviceId, [FromBody] dynamic payload)
        {
            if (string.IsNullOrEmpty(deviceId))
                return BadRequest(new { error = "Missing deviceId" });

            if (payload == null)
                return BadRequest(new { error = "Missing payload" });
            
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
        /// Update reported twin properties for the given device
        /// </summary>
        /// <param name="deviceId">Device identifier</param>
        /// <param name="payload">Device twin (JSON format)</param>
        /// <returns></returns>
        [HttpPost("{deviceId}/properties")]
        public async Task<IActionResult> SendProperties(string deviceId, [FromBody] dynamic payload)
        {
            if (string.IsNullOrEmpty(deviceId))
                return BadRequest(new { error = "Missing deviceId" });

            if (payload == null)
                return BadRequest(new { error = "Missing payload" });
            
            var sasToken = this.ControllerContext.HttpContext.Request.Headers[Constants.SasTokenHeaderName].ToString();
            if (!string.IsNullOrEmpty(sasToken))
            {
                var tokenExpirationDate = ResolveTokenExpiration(sasToken);
                if (!tokenExpirationDate.HasValue)
                    tokenExpirationDate = DateTime.UtcNow.AddMinutes(20);

                await gatewayService.UpdateDeviceReportedPropertiesByToken(deviceId, payload.ToString(), sasToken, tokenExpirationDate.Value);
            }
            else
            {
                if (!this.options.SharedAccessPolicyKeyEnabled)
                    return BadRequest(new { error = "Shared access is not enabled" });
                await gatewayService.UpdateDeviceReportedPropertiesBySharedAccess(deviceId, payload.ToString());
            }

            return Ok();
        }

        /// <summary>
        /// Get device twin for the given device
        /// </summary>
        /// <param name="deviceId">Device identifier</param>
        /// <returns></returns>
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
        /// <returns></returns>
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
