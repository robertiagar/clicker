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

namespace Clicker.ViewModel
{
    public class MainViewModel : ViewModelBase
    {
        private MobileServiceClient _client;
        private MobileServiceUser _user;
        private bool _loggedIn;
        private bool _submitted;
        private DispatcherTimer _timer;

        public MainViewModel(MobileServiceClient client)
        {
            //Mobile service
            this._client = client;

            //Checking for loging
            this._loggedIn = false;
            this._submitted = true;

            //timer
            this._timer = new DispatcherTimer();
            _timer.Interval = new TimeSpan(0, 0, 2);
            _timer.Tick += TimerTick;
            _timer.Start();

            //Commands
            this.ClickCommand = new RelayCommand(() => Click());
            this.LogoutCommand = new RelayCommand(() => Logout());
        }

        public ICommand ClickCommand { get; private set; }
        public ICommand LogoutCommand { get; private set; }

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
                await _client.GetTable<Player>().UpdateAsync(Player);
                _submitted = true; //no need send new values
            }
        }

        private async Task GetUserAsync()
        {
            var players = await _client.GetTable<Player>().ToCollectionAsync();
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
                await _client.GetTable<Player>().InsertAsync(Player);
            }
        }

        private async Task GetUsersAsync()
        {
            var players = await _client.InvokeApiAsync<IEnumerable<Player>>("players", HttpMethod.Get, null);
        }

        public async Task LoginAsync()
        {
            string message;
            string provider = MobileServiceAuthenticationProvider.MicrosoftAccount.ToString();
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
                    // When there is no matching resource an error occurs, which we ignore.
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
                        await _client.GetTable<Player>().Take(1).ToListAsync();
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
                        _user = await _client.LoginAsync(provider, true);

                        // Create and store the user credentials.
                        credential = new PasswordCredential(provider,
                            _user.UserId, _user.MobileServiceAuthenticationToken);

                        vault.Add(credential);
                    }
                    catch (MobileServiceInvalidOperationException ex)
                    {
                        message = "You must log in. Login Required";
                    }
                }
                //message = string.Format("You are now logged in - {0}", _user.UserId);
                //var dialog = new MessageDialog(message);
                //dialog.Commands.Add(new UICommand("OK"));
                //await dialog.ShowAsync();
                _loggedIn = true;
                await GetUserAsync();
                await GetUsersAsync();
            }
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

        private MobileServiceCollection<Player, Player> _Players;
        public MobileServiceCollection<Player, Player> Players
        {
            get { return _Players; }
            set
            {
                Set<MobileServiceCollection<Player, Player>>(() => Players, ref _Players, value);
            }
        }
        #endregion
    }
}