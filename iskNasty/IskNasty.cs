using System;
using System.Linq;
using TeleSharp.TL;
using TeleSharp.TL.Channels;
using TeleSharp.TL.Messages;
using TLSharp.Core;
using System.Collections.Generic;
using System.Timers;
using System.Threading;
using System.Data;


namespace iskNasty
{
    public class IskNasty
    {
        public TelegramClient _isk;
        public event EventHandler<string> OnInfo, OnDebug, OnError, OnFatal, OnStop, OnUpdate;
        
        private System.Timers.Timer sendTimer;
        private List<MessageToSend> _messagesToSend;

        private List<AdUser> _userCache;
        private System.Timers.Timer _channelsCheckTimer;

        private System.Timers.Timer _cacheUpdateTimer;
        private MySqlWorker _authsqlw;

        private bool _testmode;

        #region Base

        public IskNasty(int AppID,
            string ApiHash,
            string authdb_host,
            string authdb_name,
            string authdb_user,
            string authdb_password,
            bool testmode)
        {
            _testmode = testmode;

            _authsqlw = new MySqlWorker(authdb_host,  authdb_name,  authdb_user,  authdb_password);

            _messagesToSend = new List<MessageToSend>();
            sendTimer = new System.Timers.Timer();
            sendTimer.Interval = 1000 * 5;
            sendTimer.Elapsed += SendTimer_Elapsed;
            sendTimer.Enabled = true;

            _userCache = new List<AdUser>();

            _cacheUpdateTimer = new System.Timers.Timer(1000 * 60 * 1); //обновление кеша известных пользователей раз в 10 минут
            _cacheUpdateTimer.Elapsed += _cacheUpdateTimer_Elapsed;

            _channelsCheckTimer = new System.Timers.Timer(1000* 15);
            _channelsCheckTimer.Elapsed += _channelsCheckTimer_Elapsed;

            _isk = new TelegramClient(AppID, ApiHash);
        }

        public void StartChannelCheck()
        {
            _channelsCheckTimer.Enabled = true;
        }
        public void StartUserCacheUpdate()
        {
            _cacheUpdateTimer.Enabled = true;
        }

        private void _channelsCheckTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            _channelsCheckTimer.Enabled = false;
            try
            {
                ProcessCheckChannels();
                ProcessCheckChats();
            }
            catch(Exception ex)
            {
                OnError?.Invoke(this, $"Cant process check channels: {ex.Message}");
            }
            _channelsCheckTimer.Enabled = true;
        }

        private void ProcessCheckChannels()
        {
            OnDebug?.Invoke(this, "Check channels timer elapsed");

            //защита от кика всех подряд
            if (_userCache.Count <= 0)
            {
                OnError?.Invoke(this, "User cache empty! I can't kick all, sorry. Check db");
                return;
            }

            var channels = GetAllChannels();
            foreach (var channel in channels)
            {
                var users = GetChannelUsers(channel);
                foreach (var user in users)
                {
                    var aduser = GetAdUser(user.Id.ToString());
                    if (aduser == null)
                    {
                        KickFromChannel(channel, user, _testmode);
                        OnInfo?.Invoke(this, $"User {user.FirstName}, {user.LastName}, {user.Username} kicked from channel {channel.Title}");
                    }
                }
            }
            OnDebug?.Invoke(this, "Check channels timer done");
        }

        public void ProcessCheckChats()
        {
            OnDebug?.Invoke(this, "Check chats timer elapsed");

            //защита от кика всех подряд
            if (_userCache.Count <= 0)
            {
                OnError?.Invoke(this, "User cache empty! I can't kick all, sorry. Check db");
                return;
            }

            var chats = GetAllChats();
            foreach (var chat in chats)
            {
                var users = GetChatUsers(chat);
                foreach (var user in users)
                {
                    var aduser = GetAdUser(user.Id.ToString());
                    if (aduser == null)
                    {
                        KickFromChat(chat, user, _testmode);
                        OnInfo?.Invoke(this, $"User {user.FirstName}, {user.LastName}, {user.Username} kicked from chat {chat.Title}");
                    }
                }
            }
            OnDebug?.Invoke(this, "Check chats timer done");
        }

        private void _cacheUpdateTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            OnDebug?.Invoke(this, "Cache update timer elapsed");

            try
            {
                UpdateUserCache();
            }
            catch (Exception ex)
            {
                OnError?.Invoke(this, $"UserCache not updated. Reason: [{ex.Message.ToString()}]");
            }
            OnDebug?.Invoke(this, "Cache update timer done");
        }

        public void UpdateUserCache()
        {
            OnDebug?.Invoke(this, "Start update user cache");
            string select = $"select * from ng_users u where status = 1";

            try
            {
                var dt = _authsqlw.Exec(select);

                _userCache.Clear();

                foreach (DataRow dr in dt.Rows)
                {
                    AdUser au = new AdUser();
                    au.Email = dr["email"].ToString();
                    au.status = dr["status"].ToString();
                    au.userkey = dr["userkey"].ToString();
                    au.userdata = dr["userdata"].ToString();
                    au.roleGroup = dr["roleGroup"].ToString();
                    au.TelegramID = dr["telegramid"].ToString();

                    au.UserName = au.Email.Split('@')[0];

                    _userCache.Add(au);

                }
                OnDebug?.Invoke(this, $"Updated {dt.Rows.Count} users");
            }
            catch (Exception ex)
            {
                OnError?.Invoke(this, ex.Message);
            }            
        }

        public AdUser GetAdUser(string telegramID)
        {
            foreach (var u in _userCache)
            {
                if (u.TelegramID == telegramID)
                {
                    return u;
                }
            }
            return null;
        }

        public bool Connect()
        {
            try
            {
                _isk.ConnectAsync().Wait();
            }
            catch (Exception ex)
            {
                OnFatal?.Invoke(this, ex.Message);
                return false;
            }
            return true;
        }

        public bool isAuthorized()
        {
            return _isk.IsUserAuthorized();
        }

        public string StartAuthorisation(string Phone)
        {
            return _isk.SendCodeRequestAsync(Phone).Result;
        }

        public string ProcessAuthorization(string Phone, string Hash, string Code)
        {
            try
            {
                _isk.MakeAuthAsync(Phone, Hash, Code).Wait();
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
            return "";
        }

        #endregion Base

        #region Messages

        public void SendMessage(Object Target, string Text)
        {
            _messagesToSend.Add(new MessageToSend(Target, Text));
        }

        private void SendTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            new Thread(() =>
            {
                sendTimer.Enabled = false;
                Thread.CurrentThread.IsBackground = true;

                List<MessageToSend> tmpList = new List<MessageToSend>();
                tmpList.AddRange(_messagesToSend);

                foreach (MessageToSend mts in tmpList)
                {
                    if (_SendMessage(mts.target, mts.text))
                    {
                        _messagesToSend.Remove(mts);
                    }
                    Thread.Sleep(2000);
                }

                sendTimer.Enabled = true;
            }).Start();            
        }

        private bool _SendMessage(object trgt, string Text)
        {
            TLAbsInputPeer peer = null;

            if (trgt.GetType() == typeof(TLUser))
            {
                peer = new TLInputPeerUser() { AccessHash = ((TLUser)trgt).AccessHash.Value, UserId = ((TLUser)trgt).Id };
            }
            if (trgt.GetType() == typeof(TLChannel))
            {
                peer = new TLInputPeerChannel() { AccessHash = ((TLChannel)trgt).AccessHash.Value, ChannelId = ((TLChannel)trgt).Id };
            }
            if (trgt.GetType() == typeof(TLChat))
            {
                peer = new TLInputPeerChat() { ChatId = ((TLChat)trgt).Id };
            }
            try
            {
                _isk.SendMessageAsync(peer, Text);
                return true;
            }
            catch
            {
                return false;
            }
        }

        #endregion Messages

        #region Channels

        public TLChannel GetChannelByName(string ChannelName)
        {
            var dialogs = (TLDialogs)_isk.GetUserDialogsAsync().Result;

            var channel = dialogs.Chats
                .Where(c => c.GetType() == typeof(TLChannel))
                .Cast<TLChannel>()
                .FirstOrDefault(c => c.Title == ChannelName);

            return channel;
        }

        public List<TLChannel> GetAllChannels()
        {
            var res = new List<TLChannel>();
            var dialogs = (TLDialogs)_isk.GetUserDialogsAsync().Result;
            var channels = dialogs.Chats
                .Where(c => c.GetType() == typeof(TLChannel))
                .Cast<TLChannel>();
            res.AddRange(channels);
            return res;
        }

        public List<TLChat> GetAllChats()
        {
            var res = new List<TLChat>();
            var dialogs = (TLDialogs)_isk.GetUserDialogsAsync().Result;
            var chats = dialogs.Chats
                .Where(c => c.GetType() == typeof(TLChat))
                .Cast<TLChat>();
            res.AddRange(chats);
            return res;
        }

        public List<TLUser> GetChatUsers(TLChat chat)
        {
            var res = new List<TLUser>();

            var request = new TLRequestGetFullChat() { ChatId = chat.Id };
            var fullChat = _isk.SendRequestAsync<TeleSharp.TL.Messages.TLChatFull>(request).Result;
            //var participants = (fullChat.FullChat as TeleSharp.TL.TLChatFull).Participants as TLChatParticipants;
            foreach (var user in fullChat.Users)
            {
                res.Add(user as TLUser);
            }
            return res;
        }

        public List<TLUser> GetChannelUsers(TLChannel channel)
        {
            var res = new List<TLUser>();

            var request = new TLRequestGetParticipants
            {
                Channel = new TLInputChannel
                {
                    AccessHash = channel.AccessHash.Value,
                    ChannelId = channel.Id
                },
                Filter = new TLChannelParticipantsRecent()
            };

            TLChannelParticipants found = _isk.SendRequestAsync<TLChannelParticipants>(request).Result;
            var users = found.Users
                .Cast<TLUser>();
            res.AddRange(users);
            return res;
        }

        public void KickFromChannel(TLChannel Channel, TLUser User, bool IS_TEST = false)
        {
            var absChannel = new TLInputChannel() { AccessHash = Channel.AccessHash.Value, ChannelId = Channel.Id };
            var absUser = new TLInputUser() { AccessHash = User.AccessHash.Value, UserId = User.Id };

            var r = new TLRequestKickFromChannel()
            {
                Channel = absChannel,
                Kicked = !IS_TEST,
                UserId = absUser,
                 
            };

            _isk.SendRequestAsync<TLAbsUpdates>(r).Wait();
        }

        private void KickFromChat(TLChat chat, TLUser user, bool IS_TEST)
        {
            var r = new TLRequestDeleteChatUser()
            {
                ChatId = chat.Id,
                UserId = new TLInputUser()
                {
                    UserId = user.Id
                }
            };
            if (!IS_TEST)
            {
                _isk.SendRequestAsync<TLUpdates>(r).Wait();
            }

        }

        #endregion Channels       
    }
}
