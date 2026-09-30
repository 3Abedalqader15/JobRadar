using Microsoft.AspNetCore.SignalR;

namespace JobRadar.Api.Hubs;

public class JobHub : Hub
{
    public const string HubUrl = "/hubs/jobs";

    public async Task BroadcastNewJob(object job)
    {
        await Clients.All.SendAsync("ReceiveNewJob", job);
    }
}
