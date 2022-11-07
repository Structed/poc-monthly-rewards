using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PlayFab;
using PlayFab.ServerModels;

namespace MonthlyReward
{
    public class Function1
    {
        private readonly ILogger _logger;

        public Function1(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<Function1>();
        }

        [Function("Function1")]
        public async Task Run([QueueTrigger("monthly-reward", Connection = "QueueStorage")] string myQueueItem)
        {
            var payload = JsonConvert.DeserializeObject<dynamic>(myQueueItem);
            var playerId = (string)payload.PlayerProfile.PlayerId;
            var titleAuthContext = payload.TitleAuthenticationContext;

            var apiSettings = new PlayFabApiSettings
            {
                TitleId = Environment.GetEnvironmentVariable("PlayFab.TitleId", EnvironmentVariableTarget.Process),
                DeveloperSecretKey = Environment.GetEnvironmentVariable("PLAYFAB_DEV_SECRET_KEY", EnvironmentVariableTarget.Process),
            };


            var playfab = new PlayFabServerInstanceAPI(apiSettings);
            const string key = "monthly_logins";
            PlayFab.ServerModels.GetUserDataRequest getUserDataRequest = new PlayFab.ServerModels.GetUserDataRequest
            {
                PlayFabId = playerId,
                Keys = new List<string>
                {
                    key
                }
            };
            var userDataResponse = await playfab.GetUserDataAsync(getUserDataRequest);
            if (userDataResponse.Error != null)
            {
                throw new Exception(userDataResponse.Error.GenerateErrorReport());
            }

            var jsonData = userDataResponse.Result.Data[key].Value;
            var logins = JsonConvert.DeserializeObject<dynamic>(jsonData);
            var now = DateTime.UtcNow;
            string monthKey = $"{now.Year}-{now.Month}";
            var loggedInThisMonth = false;
            string responseJson = "";
            try
            {
                loggedInThisMonth = (bool)logins[monthKey];
                logins[monthKey] = true;
                responseJson = JsonConvert.SerializeObject(logins);
            } catch (Exception e) {
                _logger.LogError(e.Message);
            }

            if (!loggedInThisMonth)
            {
                // write to PF
                var setDataRequest = new UpdateUserDataRequest {
                    PlayFabId = playerId,
                    Data = new Dictionary<string, string>{
                        { key, responseJson }
                    }
                };
                var response = await playfab.UpdateUserDataAsync(setDataRequest);
                if (response.Error != null)
                {
                    throw new Exception(response.Error.GenerateErrorReport());
                }

            }


            _logger.LogInformation($"C# Queue trigger function processed: {myQueueItem}");
        }
    }
}
