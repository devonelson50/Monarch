using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Monarch.Hubs; // so we can reference DashboardHub

namespace Monarch.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TestUpdateController : ControllerBase
    {
        private readonly IHubContext<DashboardHub> _hub;

        public TestUpdateController(IHubContext<DashboardHub> hub)
        {
            _hub = hub;
        }

        // POST /api/testupdate
        [HttpPost]
        public async Task<IActionResult> TriggerUpdate()
        {
            // This simulates "the database changed"
            await _hub.Clients.All.SendAsync("ApplicationsUpdated");

            return Ok("Update broadcast sent to dashboards!");
        }
    }
}
