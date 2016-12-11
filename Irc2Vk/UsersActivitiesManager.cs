using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace Irc2Vk
{
    class UsersActivitiesManager
    {
        private Timer _clearTimer;
        private TimeSpan _maxInactiveInterval;

        public event Action<long> UserRemoved;
        public event Action<long> UserAdded;

        private void OnUserRemoved(long uid)
        {
            UserRemoved?.Invoke(uid);
        }

        private void OnUserAdded(long uid)
        {
            UserAdded?.Invoke(uid);
        }

        public Dictionary<long, DateTime> UserActivities { get; private set; }
        public UsersActivitiesManager(Dictionary<long, DateTime> userUidActivities, TimeSpan maxInactiveInterval)
        {
            UserActivities = userUidActivities;
            _clearTimer = new System.Timers.Timer(5000.0);
            _clearTimer.Elapsed += Clear;
            _maxInactiveInterval = maxInactiveInterval;
            _clearTimer.Start();
        }

        private void Clear(object sender, ElapsedEventArgs e)
        {
            var now = DateTime.Now;
            var inactive = UserActivities.Where(kv => now.Subtract(kv.Value) > _maxInactiveInterval);
            foreach(var val in inactive)
            {
                UserActivities.Remove(val.Key);
                OnUserRemoved(val.Key);
            }
        }

        public void RefreshUserActivity(long uid)
        {
            UserActivities[uid] = DateTime.Now;
            OnUserAdded(uid);
        }

        public void RemoveUserActivity(long uid)
        {
            UserActivities.Remove(uid);
            OnUserRemoved(uid);
        }
    }
}
