using System.Linq;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Controllers;
using System.Web.Http.OData;
using Microsoft.WindowsAzure.Mobile.Service;
using Clicker.MobileService.DataObjects;
using Clicker.MobileService.Models;
using Microsoft.WindowsAzure.Mobile.Service.Security;
using Newtonsoft.Json;

namespace Clicker.MobileService.Controllers
{
    [AuthorizeLevel(AuthorizationLevel.User)]
    public class PlayerController : TableController<Player>
    {
        MobileServiceContext context;

        protected override void Initialize(HttpControllerContext controllerContext)
        {
            base.Initialize(controllerContext);
            context = new MobileServiceContext();
            DomainManager = new EntityDomainManager<Player>(context, Request, Services);
        }

        // GET tables/Player
        public IQueryable<Player> GetAllPlayer()
        {
            var currentUser = User as ServiceUser;

            return Query().Where(x => x.UserId == currentUser.Id);
        }

        [Route("api/players")]
        public IQueryable<Player> GetAllPlayers()
        {
            return Query().OrderByDescending(x => x.Rank);
        }

        // GET tables/Player/48D68C86-6EA6-4C25-AA33-223FC9A27959
        public SingleResult<Player> GetPlayer(string id)
        {
            return Lookup(id);
        }

        // PATCH tables/Player/48D68C86-6EA6-4C25-AA33-223FC9A27959
        public async Task<Player> PatchPlayer(string id, Delta<Player> patch)
        {
            var currentUser = User as ServiceUser;
            var item = patch.GetEntity();

            var result = await UpdateAsync(id, patch);

            var players = context.Players.OrderByDescending(player => player.Clicks).ToList();

            for (int i = 0; i < players.Count; i++)
            {
                var player = context.Entry<Player>(players[i]);
                player.Entity.Rank = i + 1;
                player.Entity.OldClicks = player.Entity.Clicks;
            }

            await context.SaveChangesAsync();

            var message = new WindowsPushMessage();
            message.Headers.Add("X-WNS-Type", "wns/raw");
            message.XmlPayload = JsonConvert.SerializeObject(context.Players.ToList());

            await Services.Push.SendAsync(message);

            return result;
        }

        // POST tables/Player
        public async Task<IHttpActionResult> PostPlayer(Player item)
        {
            var currentUser = User as ServiceUser;
            item.UserId = currentUser.Id;

            Player current = await InsertAsync(item);
            return CreatedAtRoute("Tables", new { id = current.Id }, current);
        }

        // DELETE tables/Player/48D68C86-6EA6-4C25-AA33-223FC9A27959
        public Task DeletePlayer(string id)
        {
            return DeleteAsync(id);
        }

    }
}