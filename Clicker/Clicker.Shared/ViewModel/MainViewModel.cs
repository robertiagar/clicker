using GalaSoft.MvvmLight;
using Microsoft.WindowsAzure.MobileServices;
using System.Threading.Tasks;
using Windows.Security.Credentials;
using System.Linq;
using System.Linq.Expressions;
using System;
using Clicker.Model;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using System.Windows.Input;
using GalaSoft.MvvmLight.Command;
using Windows.UI.Xaml.Controls;
using System.Net.Http;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Windows.Networking.PushNotifications;
using Newtonsoft.Json;
using GalaSoft.MvvmLight.Threading;
using Windows.Foundation;

namespace Clicker.ViewModel
{
    public class MainViewModel : ViewModelBase
    {
        private MobileServiceClient _client;
        private MobileServiceUser _user;
        private bool _submitted;
        private DispatcherTimer _timer;

        public MainViewModel(MobileServiceClient client)
        {
            //Mobile service
            this._client = client;

            //flag for checking new values
            this._submitted = true;

            //timer
            this._timer = new DispatcherTimer();
            _timer.Interval = new TimeSpan(0, 0, 0, 0, 500);
            _timer.Tick += TimerTick;
            _timer.Start();

            this._players = new ObservableCollection<Player>();

            //Commands
            this.ClickCommand = new RelayCommand(() => Click());
            this.LogoutCommand = new RelayCommand(() => Logout());
            this.UpdateNameCommand = new RelayCommand(async () => await UpdateNameAsync());
            this.LoginCommand = new RelayCommand(async () => await LoginAsync());
            this.Working = false;
        }

        private async Task UpdateNameAsync()
        {
            await WorkingAsync(SaveAsync());
        }

        public ICommand ClickCommand { get; private set; }
        public ICommand LogoutCommand { get; private set; }
        public ICommand UpdateNameCommand { get; private set; }
        public ICommand LoginCommand { get; private set; }

        private void Click()
        {
            //increment clicks
            Player.Clicks++;
            //new values not submited
            _submitted = false;

            //restart timer
            if (_timer.IsEnabled)
            {
                _timer.Stop();
                _timer.Start();
            }
            else
            {
                _timer.Start();
            }
        }

        private async void TimerTick(object sender, object e)
        {
            //new values may be already submited
            if (!_submitted)
            {
                //stop timer
                _timer.Stop();
                await WorkingAsync(SaveAsync());
                _submitted = true; //no need send new values
            }
        }

        private async Task GetUserAsync()
        {
            var players = await WorkingAsync(_client.GetTable<Player>().ToCollectionAsync());
            var player = players.Where(pl => pl.ProviderId == _client.CurrentUser.UserId).SingleOrDefault();
            if (player != null)
            {
                Player = player;
            }
            else
            {
                Player = new Player()
                {
                    ProviderId = _client.CurrentUser.UserId,
                    Clicks = 0,
                };
                await WorkingAsync(_client.GetTable<Player>().InsertAsync(Player));
            }
        }

        private async Task GetUsersAsync()
        {
            var players = await WorkingAsync(_client.InvokeApiAsync<IEnumerable<Player>>("players", HttpMethod.Get, null));
            Players.Clear();
            foreach (var player in players.OrderBy(x => x.Rank))
            {
                Players.Add(player);
            }
        }

        public async Task LoginAsync()
        {
            string provider = MobileServiceAuthenticationProvider.Twitter.ToString();
            // Use the PasswordVault to securely store and access credentials.
            PasswordVault vault = new PasswordVault();
            PasswordCredential credential = null;

            while (credential == null)
            {
                try
                {
                    // Try to get an existing credential from the vault.
                    credential = vault.FindAllByResource(provider).FirstOrDefault();
                }
                catch (Exception)
                {
#if WINDOWS_PHONE_APP
                    return;
#endif
                }

                if (credential != null)
                {
                    // Create a user from the stored credentials.
                    _user = new MobileServiceUser(credential.UserName);
                    credential.RetrievePassword();
                    _user.MobileServiceAuthenticationToken = credential.Password;

                    // Set the user from the stored credentials.
                    _client.CurrentUser = _user;

                    try
                    {
                        // Try to return an item now to determine if the cached credential has expired.
                        await WorkingAsync(_client.GetTable<Player>().Take(1).ToListAsync());
                    }
                    catch (MobileServiceInvalidOperationException ex)
                    {
                        if (ex.Response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                        {
                            // Remove the credential with the expired token.
                            vault.Remove(credential);
                            credential = null;
                            continue;
                        }
                    }
                }
                else
                {
                    try
                    {
                        // Login with the identity provider.
                        _user = await WorkingAsync(_client.LoginAsync(provider, true));

                        // Create and store the user credentials.
                        credential = new PasswordCredential(provider,
                            _user.UserId, _user.MobileServiceAuthenticationToken);

                        vault.Add(credential);
                    }
                    catch (MobileServiceInvalidOperationException)
                    {
                        //message = "You must log in. Login Required";
                    }
                }
                //message = string.Format("You are now logged in - {0}", _user.UserId);
                //var dialog = new MessageDialog(message);
                //dialog.Commands.Add(new UICommand("OK"));
                //await dialog.ShowAsync();
                await WorkingAsync( GetUserAsync());
                await WorkingAsync( GetUsersAsync());
                await WorkingAsync( UploadChannel());
            }
        }

        public async Task UploadChannel()
        {
            var channel = await PushNotificationChannelManager.CreatePushNotificationChannelForApplicationAsync();
            try
            {
                await WorkingAsync(_client.GetPush().RegisterNativeAsync(channel.Uri));
                channel.PushNotificationReceived += PushNotificationReceived;
            }
            catch (Exception exception)
            {
                HandleRegisterException(exception);
            }
        }

        private async void PushNotificationReceived(PushNotificationChannel sender, PushNotificationReceivedEventArgs args)
        {
            if (args.NotificationType == PushNotificationType.Raw)
            {
                var players = JsonConvert.DeserializeObject<IEnumerable<Player>>(args.RawNotification.Content);
                await DispatcherHelper.RunAsync(() =>
                {
                    Players.Clear();
                    foreach (var player in players.OrderBy(pl => pl.Rank))
                    {
                        Players.Add(player);
                    }

                    Player = players.Where(pl => pl.UserId == Player.UserId).SingleOrDefault();
                });
            }
        }

        private static void HandleRegisterException(Exception exception)
        {
            //bad handling
        }

        public void Logout()
        {
            string provider = MobileServiceAuthenticationProvider.MicrosoftAccount.ToString();
            // Use the PasswordVault to securely store and access credentials.
            PasswordVault vault = new PasswordVault();
            PasswordCredential credential = null;

            try
            {
                // Try to get an existing credential from the vault.
                credential = vault.FindAllByResource(provider).FirstOrDefault();
            }
            catch
            {
                //nothing to do
            }

            if (credential != null)
            {
                vault.Remove(credential);
            }
        }

        public async Task SaveAsync()
        {
            if (_client.CurrentUser != null)
            {
                await WorkingAsync(_client.GetTable<Player>().UpdateAsync(Player));
                //await GetUsersAsync();
            }
        }

        #region Observables
        private Player _Player;
        public Player Player
        {
            get { return _Player; }
            set
            {
                Set<Player>(() => Player, ref _Player, value);
            }
        }

        private ObservableCollection<Player> _players;
        public ObservableCollection<Player> Players { get { return _players; } }
        #endregion

#if WINDOWS_APP
        private bool _Working;
        public bool Working
        {
            get { return _Working; }
            set
            {
                Set<bool>(() => Working, ref _Working, value);
            }
        }

        private async Task WorkingAsync(Task action)
        {
            Working = true;
            await action;
            Working = false;
        }

        private async Task<T> WorkingAsync<T>(Task<T> action) where T : class
        {
            Working = true;
            var result = await action;
            Working = false;
            return result;
        }

#else
        private async Task WorkingAsync(Task action)
        {
            var statusBar = Windows.UI.ViewManagement.StatusBar.GetForCurrentView();
            await statusBar.ProgressIndicator.ShowAsync();
            await action;
            await statusBar.ProgressIndicator.HideAsync();
        }

        private async Task<T> WorkingAsync<T>(Task<T> action) where T : class
        {
            var statusBar = Windows.UI.ViewManagement.StatusBar.GetForCurrentView();
            await statusBar.ProgressIndicator.ShowAsync();
            var result = await action;
            await statusBar.ProgressIndicator.HideAsync();
            return result;
        }
        public void ContinueWithAuthentication(Windows.ApplicationModel.Activation.WebAuthenticationBrokerContinuationEventArgs args)
        {
            _client.LoginComplete(args);
        }
#endif
    }
}