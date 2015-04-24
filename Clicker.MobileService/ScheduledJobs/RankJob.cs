using Clicker.MobileService.DataObjects;
using Clicker.MobileService.Models;
using Microsoft.ServiceBus.Notifications;
using Microsoft.WindowsAzure.Mobile.Service;
using Microsoft.WindowsAzure.Mobile.Service.ScheduledJobs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace Clicker.MobileService.ScheduledJobs
{
    public class RankJob : ScheduledJob
    {
        private MobileServiceContext context;

        protected override void Initialize(ScheduledJobDescriptor scheduledJobDescriptor, CancellationToken cancellationToken)
        {
            base.Initialize(scheduledJobDescriptor, cancellationToken);

            // Create a new context with the supplied schema name.
            context = new MobileServiceContext();
        }

        public async override Task ExecuteAsync()
        {
            var players = context.Players.OrderByDescending(player => player.Clicks).ToList();

            for (int i = 0; i < players.Count; i++)
            {
                var player = context.Entry<Player>(players[i]);
                player.Entity.Rank = i + 1;
                player.Entity.OldClicks = player.Entity.Clicks;
            }

            await context.SaveChangesAsync();

            players = context.Players.OrderByDescending(player => player.Clicks).ToList();
            Player winner = null;
            for (int i = 0; i < players.Count; i++)
            {
                var player = context.Entry<Player>(players[i]).Entity;
                if (i == 0)
                {
                    winner = player;
                    WindowsPushMessage message = new WindowsPushMessage();

                    // Define the XML paylod for a WNS native toast notification 
                    // that contains the text of the inserted item.
                    message.XmlPayload = @"<?xml version=""1.0"" encoding=""utf-8""?>" +
                                         @"<toast><visual><binding template=""ToastText01"">" +
                                         @"<text id=""1"">" + "Congratulations " + player.Name + "!! You are todays first player with " + player.Clicks + " clicks. " + @"</text>" +
                                         @"</binding></visual></toast>";

                    // Use a tag to only send the notification to the logged-in user.
                    var result = await Services.Push.SendAsync(message, player.UserId);
                }
                else
                {
                    WindowsPushMessage message = new WindowsPushMessage();

                    // Define the XML paylod for a WNS native toast notification 
                    // that contains the text of the inserted item.
                    message.XmlPayload = @"<?xml version=""1.0"" encoding=""utf-8""?>" +
                                         @"<toast><visual><binding template=""ToastText01"">" +
                                         @"<text id=""1"">" + "Sorry " + player.Name + ". You lost to " + winner.Name + ". Better luck next time." + @"</text>" +
                                         @"</binding></visual></toast>";

                    // Use a tag to only send the notification to the logged-in user.
                    var result = await Services.Push.SendAsync(message, player.UserId);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                context.Dispose();
            }
        }
    }
}