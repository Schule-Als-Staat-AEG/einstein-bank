using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;
using System.Collections.Generic;
using Max35050.Auth;

namespace Max35050.API
{
    public class LogAPI : Hub
    {
        private static readonly HashSet<string> ConnectedClients = new();

        public override async Task OnConnectedAsync()
        {
            try
            {
                var httpContext = Context.GetHttpContext();
                var token = httpContext!.Request.Query["access_token"].ToString();

                if (string.IsNullOrEmpty(token) || !await AuthManager.IsTokenSuperuser(token))
                {
                    Context.Abort();
                    Debug.LogError("Unauthorized Signal-R connection attempt");
                    return;
                }

                ConnectedClients.Add(Context.ConnectionId);

                // Send previous logs to the newly connected client
                foreach (var log in Debug.logs)
                {
                    await Clients.Caller.SendAsync("log", log);
                }

                Debug.Log($"Authorized Signal-R connection: {Context.ConnectionId}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error during Signal-R connect: {ex.Message}");
            }

            await base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            ConnectedClients.Remove(Context.ConnectionId);
            return base.OnDisconnectedAsync(exception);
        }

        public static async Task BroadcastLog(string ansiLog, IHubContext<LogAPI> context)
        {
            await context.Clients.All.SendAsync("log", ansiLog);
        }
    }
}